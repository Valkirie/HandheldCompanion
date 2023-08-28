using System;

namespace HandheldCompanion.Utils
{
    public static class BitwiseUtils
    {
        /// <summary>
        /// Returns the value of the lowest set bit in the given integer.
        /// </summary>
        /// <param name="num">The integer to check.</param>
        public static int FirstSetBit(int num)
        {
            return num & (-num);
        }

        /// <summary>
        /// Returns true if the wanted bit is set in the given integer.
        /// </summary>
        /// <param name="num">The integer to check.</param>
        /// <param name="want">The bit(s) to check for.</param>
        public static bool WantCurrent(int num, int want)
        {
            if (want <= 0) return false;
            return (num & want) == want;
        }

        /// <summary>
        /// Returns true if the wanted bit is set in the first integer, and not in the second.
        /// </summary>
        /// <param name="num1">The first integer to check.</param>
        /// <param name="num2">The second integer to check.</param>
        /// <param name="want">The bit(s) to check for.</param>
        public static bool WantCurrentAndNotLast(int num1, int num2, int want)
        {
            if (want <= 0) return false;
            return ((num1 & want) == want) && ((num2 & want) != want);
        }

        /// <summary>
        /// Returns true if the wanted bit is not set in the first integer, but set in the second.
        /// </summary>
        /// <param name="num1">The first integer to check.</param>
        /// <param name="num2">The second integer to check.</param>
        /// <param name="want">The bit(s) to check for.</param>
        public static bool WantNotCurrentAndLast(int num1, int num2, int want)
        {
            if (want <= 0) return false;
            return ((num1 & want) != want) && ((num2 & want) == want);
        }

        // A function that takes an integer n and a byte position p as parameters
        // and returns true if the p-th byte of n is 1, false otherwise
        public static bool HasByteSet(int n, int p)
        {
            // Check if p is a valid byte position (between 0 and 31)
            if (p < 0 || p > 31)
            {
                // Throw an exception if p is invalid
                throw new ArgumentOutOfRangeException("p", "Byte position must be between 0 and 31");
            }

            // Create a mask with only the p-th bit set to 1
            // using the left shift operator <<
            int mask = 1 << p;

            // Perform a bitwise AND operation between n and mask
            // using the & operator
            int result = n & mask;

            // Check if the result is non-zero, meaning the p-th bit of n is 1
            // using the != operator
            bool isSet = result != 0;

            // Return the boolean value of isSet
            return isSet;
        }
    }
}
