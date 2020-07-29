using System;
using System.IO;
using System.Text;

namespace Dprint.Plugins.Roslyn.Communication
{
    public class StdInOutReaderWriter : IDisposable
    {
        private readonly Stream _stdin;
        private readonly Stream _stdout;
        private readonly int _bufferSize = 1024;

        public StdInOutReaderWriter()
        {
            _stdin = Console.OpenStandardInput();
            _stdout = Console.OpenStandardOutput();
        }

        public void Dispose()
        {
            _stdin.Dispose();
            _stdout.Dispose();
        }

        public int ReadMessageKind()
        {
            return ReadInt();
        }

        private int ReadInt()
        {
            byte[] buffer = new byte[4];
            _stdin.Read(buffer, 0, buffer.Length);
            return (int)BigEndianBitConverter.GetUInt(buffer);
        }

        public string ReadMessagePartAsString()
        {
            return Encoding.UTF8.GetString(ReadMessagePart());
        }

        public byte[] ReadMessagePart()
        {
            var size = ReadInt();
            var messageData = new byte[size];

            if (size > 0)
            {
                // read the first part of the message part
                _stdin.Read(messageData, 0, Math.Min(_bufferSize, size));

                var index = _bufferSize;
                while (index < size) {
                    // send "ready" to the client
                    WriteInt(0);
                    _stdout.Flush();

                    // read from buffer
                    _stdin.Read(messageData, index, Math.Min(size - index, _bufferSize));
                    index += _bufferSize;
                }
            }

            return messageData;
        }

        public void SendMessageKind(int messageKind)
        {
            WriteInt(messageKind);
            _stdout.Flush();
        }

        public void SendMessagePart(byte[] data)
        {
            WriteInt(data.Length);
            _stdout.Write(data, 0, Math.Min(data.Length, _bufferSize));
            _stdout.Flush();

            var index = _bufferSize;
            while (index < data.Length) {
                // wait for "ready" from the server
                ReadInt();

                _stdout.Write(data, index, Math.Min(data.Length - index, _bufferSize));
                _stdout.Flush();

                index += _bufferSize;
            }
        }

        private void WriteInt(int value)
        {
            var bytes = BigEndianBitConverter.GetBytes((uint)value);
            _stdout.Write(bytes, 0, bytes.Length);
        }
    }
}
