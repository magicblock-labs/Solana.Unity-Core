using System.Numerics;
using System.Collections.Generic;

namespace Solana.Unity.Dex.Orca.Math
{
    //TODO: (MID) need unit tests for this 
    /// <summary>
    /// Math utility functions, involving BigDecimal, BigInteger, and Percentage.
    /// </summary>
    public static class ArithmeticUtils
    {
        public static readonly BigInteger ZERO = BigInteger.Zero;
        public static readonly BigInteger ONE = new BigInteger(1);
        public static readonly BigInteger TWO = new BigInteger(2);
        public static readonly BigInteger U128 = new BigInteger(System.Math.Pow(2, 128));


        /// <summary>
        /// Converts a BigInteger to a bigger BigInteger (the original * 2^64)
        /// </summary>
        /// <param name="num">The original value.</param>
        /// <returns>The original value * 2*64</returns>
        public static BigInteger BigIntToX64(BigInteger num)
        {
            return num * new BigInteger(System.Math.Pow(2, 64));
        }

        /// <summary>
        /// Returns the given Decimal value * 2^64, as Decimal. 
        /// </summary>
        /// <param name="d">The original value.</param>
        /// <returns>The given value * 2^64 as Decimal</returns>
        public static BigDecimal DecimalToX64(BigDecimal d)
        {
            return d * (System.Math.Pow(2, 64));
        }

        /// <summary>
        /// Returns the given Decimal value * 2^64 as BigInteger. 
        /// </summary>
        /// <param name="d">The original value.</param>
        /// <returns>The given value * 2^64, as BigInteger, possibly rounded.</returns>
        public static BigInteger DecimalToX64BigInt(BigDecimal d)
        {
            return BigInteger.Parse((d * System.Math.Pow(2, 64)).Floor().ToString());
        }

        /// <summary>
        /// Returns the given BigInteger value * 2^-64 as Decimal. 
        /// </summary>
        /// <param name="d">The original value.</param>
        /// <returns>The given value * 2^-64, as Decimal.</returns>
        public static BigDecimal DecimalFromX64BigInt(BigInteger num)
        {
            return new BigDecimal(num) * new BigDecimal(2).Pow(-64) * 2;
        }

        /// <summary>
        /// Returns the given Decimal value * 2^-64 as Decimal. 
        /// </summary>
        /// <param name="d">The original value.</param>
        /// <returns>The given value * 2^-64, as Decimal.</returns>
        public static BigDecimal DecimalFromX64(BigDecimal d)
        {
            return d * new BigDecimal(2).Pow(-64) * 2;
        }

        /// <summary>
        /// Returns the given BigInteger value * 2^64 as BigInteger. 
        /// </summary>
        /// <param name="d">The original value.</param>
        /// <returns>The given value * 2^64, as BigInteger.</returns>
        public static BigInteger BigIntFromX64(BigInteger num)
        {
            return num / new BigInteger(System.Math.Pow(2, 64));
        }

        /// <summary>
        /// Shift right 64 bits, then round up. 
        /// </summary>
        /// <param name="num">The value to shift and round.</param>
        /// <returns>A modified BigInteger value.</returns>
        public static BigInteger ShiftRightRoundUp(BigInteger num)
        {
            BigInteger result = num >> 64;
            if (result % MaxU64 > ZERO)
                result += 1;

            return result;
        }

        /// <summary>
        /// Divide n0 by n1, then round the result up.
        /// </summary>
        /// <param name="n0">Dividend in the operation.</param>
        /// <param name="n1">Divisor in the operation.</param>
        /// <returns>The result of the division rounded up as BigInteger.</returns>
        public static BigInteger DivRoundUp(BigInteger n0, BigInteger n1)
        {
            return (n0 % n1 != ZERO) ? (n0 / n1) + ONE : (n0 / n1);
        }

        public static BigInteger SubUnderflowU128(BigInteger n0, BigInteger n1)
        {
            return (n0 + U128 - n1) % U128;
        }

        /// <summary>
        /// Converts a BigInteger instance to an array of bytes.
        /// </summary>
        /// <param name="n">A BigInteger value.</param>
        /// <param name="endianness">If you want the return value to be represented in big-endian or little-endian.</param>
        /// <param name="desiredLength">Optional desired length of the returned byte array (if under, will be padded with zeros
        /// at either the front or back, depending on endianness)</param>
        /// <returns>A byte array representing the given BigInteger.</returns>
        public static byte[] BigIntToArray(BigInteger n, Endianness endianness = Endianness.LittleEndian, int desiredLength = 0)
        {
            //get bytes
            List<byte> bytes = new List<byte>(n.ToByteArray());

            if (desiredLength > 0)
            {
                //pad if too small 
                while (bytes.Count < desiredLength)
                {
                    bytes.Add(0);
                }

                //trim if too big
                if (bytes.Count > desiredLength)
                    bytes = bytes.GetRange(0, desiredLength);
            }

            //obey endianness
            if (endianness == Endianness.BigEndian && bytes.Count > 1)
                bytes.Reverse();

            return bytes.ToArray();
        }

        public static BigInteger MaxU64 => new BigInteger(System.Math.Pow(2, 64)) - (BigInteger.One);
        
        public static int BigIntBitLength(BigInteger n) 
        {
            return n.ToByteArray().Length * 8; //TODO: (MID) not sure if this is what was meant 
        }

        /// <summary>
        /// Gets the absolute value of the given BigInteger.
        /// </summary>
        /// <param name="n">A BigInteger value.</param>
        /// <returns>The absolute value of given value.</returns>
        public static BigInteger Abs(BigInteger n)
        {
            return (n < 0) ? n * -1 : n;
        }
    }

    public enum Endianness
    {
        BigEndian,
        LittleEndian
    }
}
