using System;
using System.IO;
using System.Text;

namespace Dprint.Plugins.Roslyn.Communication
{
  public class StdIoReaderWriter : IDisposable
  {
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private readonly int _bufferSize = 1024;

    public StdIoReaderWriter()
    {
      _stdin = Console.OpenStandardInput();
      _stdout = Console.OpenStandardOutput();
    }

    public void Dispose()
    {
      _stdin.Dispose();
      _stdout.Dispose();
    }

    public int ReadInt()
    {
      byte[] buffer = new byte[4];
      ReadStdIn(buffer, 0, buffer.Length);
      return (int)BigEndianBitConverter.GetUInt(buffer);
    }

    public void ReadSuccessBytes()
    {
      byte[] buffer = new byte[4];
      ReadStdIn(buffer, 0, buffer.Length);
      for (var i = 0; i < buffer.Length; i++)
      {
        if (buffer[i] != 255)
        {
          Console.Error.Write($"Catastrophic error. Did not find success bytes. Instead found: [{string.Join(", ", buffer)}]");
          Environment.Exit(1);
        }
      }
    }

    public byte[] ReadVariableData()
    {
      var size = ReadInt();
      var variableData = new byte[size];

      if (size > 0)
      {
        // read the first part of the message part
        ReadStdIn(variableData, 0, Math.Min(_bufferSize, size));

        var index = _bufferSize;
        while (index < size)
        {
          // send "ready" to the client
          SendInt(0);

          // read from buffer
          ReadStdIn(variableData, index, Math.Min(size - index, _bufferSize));
          index += _bufferSize;
        }
      }

      return variableData;
    }

    public void SendInt(int messageKind)
    {
      RawSendInt(messageKind);
      _stdout.Flush();
    }

    public void SendVariableWidth(byte[] data)
    {
      RawSendInt(data.Length);
      WriteStdOut(data, 0, Math.Min(data.Length, _bufferSize));
      _stdout.Flush();

      var index = _bufferSize;
      while (index < data.Length)
      {
        // wait for "ready" from the server
        ReadInt();

        WriteStdOut(data, index, Math.Min(data.Length - index, _bufferSize));
        _stdout.Flush();

        index += _bufferSize;
      }
    }

    public void SendSuccessBytes()
    {
      var successBytes = new byte[] { 255, 255, 255, 255 };
      WriteStdOut(successBytes, 0, 4);
    }

    private void RawSendInt(int value)
    {
      var bytes = BigEndianBitConverter.GetBytes((uint)value);
      WriteStdOut(bytes, 0, bytes.Length);
    }

    private void ReadStdIn(byte[] buffer, int offset, int count)
    {
      var bytesReadCount = _stdin.Read(buffer, offset, count);

      // This most likely indicates the process writing to stdin doesn't exist anymore.
      if (bytesReadCount != count)
        throw new Exception($"The bytes read was {bytesReadCount}, but expected {count}.");
    }

    private void WriteStdOut(byte[] bytes, int offset, int count)
    {
      _stdout.Write(bytes, offset, count);
    }
  }
}
