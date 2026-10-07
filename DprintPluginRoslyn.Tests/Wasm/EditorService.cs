using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Dprint.Plugins.Roslyn.Wasm;

/// <summary>
/// Communicates with a <c>dprint editor-service</c> process, which is what editor extensions
/// use to format. This allows providing a range and override configuration.
/// </summary>
public sealed class EditorService : IDisposable
{
    private const uint ErrorKind = 1;
    private const uint CloseKind = 2;
    private const uint CanFormatKind = 4;
    private const uint CanFormatResponseKind = 5;
    private const uint FormatKind = 6;
    private const uint FormatResponseKind = 7;

    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private readonly StringBuilder _stderr = new();
    private uint _lastMessageId;

    public EditorService(ProcessStartInfo startInfo)
    {
        _process = Process.Start(startInfo) ?? throw new Exception("Could not start dprint.");
        _stdin = _process.StandardInput.BaseStream;
        _stdout = _process.StandardOutput.BaseStream;
        _process.ErrorDataReceived += (_, e) =>
        {
            lock (_stderr)
                _stderr.AppendLine(e.Data);
        };
        _process.BeginErrorReadLine();
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                SendMessage(CloseKind, []);
                _stdin.Close();
                if (!_process.WaitForExit(TimeSpan.FromSeconds(10)))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // ignore, the process may have already exited
        }
        _process.Dispose();
    }

    public bool CanFormat(string filePath)
    {
        var messageId = SendMessage(CanFormatKind, WriteSizedBytes(Encoding.UTF8.GetBytes(filePath)));
        var body = ReadResponse(messageId, CanFormatResponseKind);
        return BinaryPrimitives.ReadUInt32BigEndian(body) == 1;
    }

    /// <summary>
    /// Formats the text returning null when there was no change.
    /// </summary>
    public string? FormatText(string filePath, string text, (int Start, int End)? byteRange = null, string? overrideConfigJson = null)
    {
        var result = Format(filePath, Encoding.UTF8.GetBytes(text), byteRange, overrideConfigJson);
        return result is null ? null : Encoding.UTF8.GetString(result);
    }

    /// <summary>
    /// Formats the file returning null when there was no change.
    /// </summary>
    public byte[]? Format(string filePath, byte[] fileBytes, (int Start, int End)? byteRange = null, string? overrideConfigJson = null)
    {
        var range = byteRange ?? (0, fileBytes.Length);
        var messageId = SendMessage(FormatKind, [
            .. WriteSizedBytes(Encoding.UTF8.GetBytes(filePath)),
            .. WriteUInt32((uint)range.Start),
            .. WriteUInt32((uint)range.End),
            .. WriteSizedBytes(Encoding.UTF8.GetBytes(overrideConfigJson ?? "")),
            .. WriteSizedBytes(fileBytes),
        ]);
        var body = ReadResponse(messageId, FormatResponseKind);
        var hasChange = BinaryPrimitives.ReadUInt32BigEndian(body) == 1;
        return hasChange ? ReadSizedBytes(body[4..]) : null;
    }

    private uint SendMessage(uint kind, byte[] body)
    {
        var messageId = ++_lastMessageId;
        _stdin.Write([.. WriteUInt32(messageId), .. WriteUInt32(kind), .. WriteSizedBytes(body), 255, 255, 255, 255]);
        _stdin.Flush();
        return messageId;
    }

    /// <summary>
    /// Reads the body of the response to the provided message excluding the message id.
    /// </summary>
    private byte[] ReadResponse(uint messageId, uint expectedKind)
    {
        ReadUInt32(); // the response's message id
        var kind = ReadUInt32();
        var body = ReadBytes((int)ReadUInt32());
        if (!ReadBytes(4).AsSpan().SequenceEqual<byte>([255, 255, 255, 255]))
            throw new Exception("Did not find the bytes that end a message.");

        var responseToMessageId = BinaryPrimitives.ReadUInt32BigEndian(body);
        if (responseToMessageId != messageId)
            throw new Exception($"Expected a response to message {messageId}, but it was for {responseToMessageId}.");
        if (kind == ErrorKind)
            throw new Exception(Encoding.UTF8.GetString(ReadSizedBytes(body[4..])));
        if (kind != expectedKind)
            throw new Exception($"Expected message kind {expectedKind}, but was {kind}.");
        return body[4..];
    }

    private uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4));

    private byte[] ReadBytes(int length)
    {
        var bytes = new byte[length];
        try
        {
            _stdout.ReadExactly(bytes);
        }
        catch (EndOfStreamException)
        {
            _process.WaitForExit(TimeSpan.FromSeconds(10));
            lock (_stderr)
                throw new Exception($"The editor service exited.\n{_stderr}");
        }
        return bytes;
    }

    private static byte[] ReadSizedBytes(byte[] bytes)
    {
        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return bytes[4..(4 + length)];
    }

    private static byte[] WriteSizedBytes(byte[] bytes) => [.. WriteUInt32((uint)bytes.Length), .. bytes];

    private static byte[] WriteUInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
