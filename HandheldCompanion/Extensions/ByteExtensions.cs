using System;

namespace HandheldCompanion.Extensions
{
    public static class ByteExtensions
    {
        public static bool GetBit(this byte b, int index)
        {
            if (index < 0 || index > 7)
            {
                throw new ArgumentOutOfRangeException();
            }

            return (b & (1 << index)) != 0;
        }

        public static byte SetBit(this byte b, int index, bool value)
        {
            if (index < 0 || index > 7)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value)
            {
                b = (byte)(b | (1 << index));
                return b;
            }

            b = (byte)(b & ~(1 << index));
            return b;
        }
    }
}
