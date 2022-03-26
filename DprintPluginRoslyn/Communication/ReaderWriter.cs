using System;
using System.IO;

namespace Dprint.Plugins.Roslyn.Communication;

public class MessageReader
{
    private readonly Stream _stream;

    public MessageReader(Stream stream)
    {
        _stream = stream;
    }

    public uint ReadUint()
    {
        byte[] buffer = new byte[4];
        ReadStdIn(buffer, buffer.Length);
        return BigEndianBitConverter.GetUInt(buffer);
    }

    public byte[] ReadVariableData()
    {
        var size = (int)ReadUint();
        var variableData = new byte[size];

        if (size > 0)
        {
            ReadStdIn(variableData, size);
        }

        return variableData;
    }

    public void ReadSuccessBytes()
    {
        byte[] buffer = new byte[4];
        ReadStdIn(buffer, buffer.Length);
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != 255)
            {
                Console.Error.Write($"Catastrophic error. Did not find success bytes. Instead found: [{string.Join(", ", buffer)}]");
                Environment.Exit(1);
            }
        }
    }

    private void ReadStdIn(byte[] buffer, int count)
    {
        var offset = 0;
        while (true)
        {
            var bytesReadCount = _stream.Read(buffer, offset, count - offset);
            offset += bytesReadCount;
            if (offset == count)
                break;
            else if (bytesReadCount == 0)
                throw new Exception($"Read zero bytes on stdin.");
        }
    }
}

public class MessageWriter
{
    private readonly Stream _stream;

    public MessageWriter(Stream stream)
    {
        _stream = stream;
    }

    public void WriteUint(uint value)
    {
        var bytes = BigEndianBitConverter.GetBytes(value);
        WriteStdOut(bytes, 0, bytes.Length);
    }

    public void WriteVariableWidth(byte[] data)
    {
        WriteUint((uint)data.Length);
        WriteStdOut(data, 0, data.Length);
    }

    public void WriteSuccessBytes()
    {
        var successBytes = new byte[] { 255, 255, 255, 255 };
        WriteStdOut(successBytes, 0, 4);
        Flush();
    }

    public void Flush()
    {
        _stream.Flush();
    }

    private void WriteStdOut(byte[] bytes, int offset, int count)
    {
        _stream.Write(bytes, offset, count);
    }
}
