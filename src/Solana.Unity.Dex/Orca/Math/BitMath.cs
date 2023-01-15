using System.Numerics;

using Solana.Unity.Dex.Orca.Exceptions;

namespace Solana.Unity.Dex.Orca.Math
{
    /// <summary>
    /// Utilities for arithmetic with BigIntegers, most of which involve extra steps and/or bitwise
    /// operations.
    /// </summary>
    public static class BitMath
    {
        public static BigInteger Mul(BigInteger n0, BigInteger n1, int limit)
        {
            BigInteger result = n0 * n1;
            if (IsOverLimit(result, limit))
            {
                throw new WhirlpoolsException($"Mul result higher than u${limit}",
                    MathErrorCode.MultiplicationOverflow);
            }
            
            return result;
        }
        
        public static BigInteger MulDiv(BigInteger n0, BigInteger n1, BigInteger d, int limit)
        {
            return MulDivRoundUp(n0, n1, d, limit, false);
        }
        
        public static BigInteger MulDivRoundUp(BigInteger n0, BigInteger n1, BigInteger d, int limit, bool roundUp = true)
        {
            if (d == 0) 
                throw new WhirlpoolsException("mulDiv denominator is zero", MathErrorCode.DivideByZero);
            
            BigInteger p = Mul(n0, n1, limit);
            BigInteger n = p / d;
            
            return roundUp && (p % d > 0) ? n+1 : n;
        }
        
        public static BigInteger CheckedMulShiftRight(BigInteger n0, BigInteger n1, int limit, bool roundUp = false)
        {
            if (n0 == 0 || n1 == 0)
                return 0;

            BigInteger p = Mul(n0, n1, limit);
            if (IsOverLimit(p, limit))
            {
                throw new WhirlpoolsException(
                    $"MulShiftRight overflowed u${limit}.",
                    MathErrorCode.MultiplicationShiftRightOverflow
                );
            } 
            
            BigInteger result = ArithmeticUtils.BigIntFromX64(p);
            bool shouldRound = false; //TODO: (MID) is this bitwise AND for BigInteger? bool shouldRound = roundUp && (result & (ArithmeticUtils.MaxU64) > 0);
            if (shouldRound && result == ArithmeticUtils.MaxU64)
            {
                throw new WhirlpoolsException(
                    $"MulShiftRight overflowed u${limit}.",
                    MathErrorCode.MultiplicationOverflow
                );
            }
            
            return shouldRound ? result+1 : result;
        }
        
        public static BigInteger DivRoundUp(BigInteger n, BigInteger d, bool roundUp = true)
        {
            if (d == 0)
            {
                throw new WhirlpoolsException("DivRoundUpIf - divide by zero", MathErrorCode.DivideByZero);
            }

            BigInteger q = n / d;
            return roundUp && ((n % d) > 0) ? q +1 : q;
        }
        
        public static bool IsOverLimit(BigInteger n, int limit)
        {
            BigInteger limitBigInt = BigInteger.Pow(2, limit) - 1;
            return n > limitBigInt;
        }
    }
}