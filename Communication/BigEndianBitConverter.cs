using System;

namespace Dprint.Plugins.Roslyn.Communication
{
    // the cli uses uints and big endian

    public static class BigEndianBitConverter
    {
        public static uint GetUInt(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes);
        }

        public static byte[] GetBytes(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }
    }
}
