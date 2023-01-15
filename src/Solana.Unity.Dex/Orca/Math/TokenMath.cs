using Solana.Unity.Dex.Math;
using System;
using System.Numerics;

using Solana.Unity.Dex.Orca.Exceptions;

namespace Solana.Unity.Dex.Orca.Math
{
    public static class TokenMath
    {
        public static BigInteger GetAmountDeltaA(
            BigInteger currentSqrtPrice,
            BigInteger targetSqrtPrice,
            BigInteger currentLiquidity,
            bool roundUp
        )
        {
            var (sqrtPriceLower, sqrtPriceUpper) = ToIncreasingPriceOrder(currentSqrtPrice, targetSqrtPrice);
            BigInteger sqrtPriceDiff = sqrtPriceUpper - sqrtPriceLower;

            BigInteger numerator = currentLiquidity * sqrtPriceDiff << 64;
            BigInteger denominator = sqrtPriceLower * sqrtPriceUpper;

            BigInteger quotient = numerator / (denominator);
            BigInteger remainder = numerator % (denominator);

            var result = roundUp && remainder != 0 ? quotient + BigInteger.One : quotient;

            if (result > ArithmeticUtils.MaxU64)
            {
                throw new WhirlpoolsException("Results larger than U64", TokenErrorCode.TokenMaxExceeded);
            }

            return result;
        }
        
        public static BigInteger GetAmountDeltaB(
            BigInteger currentSqrtPrice,
            BigInteger targetSqrtPrice,
            BigInteger currentLiquidity,
            bool roundUp
        )
        {
            var (sqrtPriceLower, sqrtPriceUpper) = ToIncreasingPriceOrder(currentSqrtPrice, targetSqrtPrice);
            BigInteger sqrtPriceDiff = sqrtPriceUpper - sqrtPriceLower;
            return BitMath.CheckedMulShiftRight(currentLiquidity, sqrtPriceDiff, 128, roundUp); 
        }

        public static BigInteger AdjustForSlippage(BigInteger num, Percentage pct, bool adjustUp)
        {
            return (adjustUp) ?
                num * (pct.Denominator + pct.Numerator) / pct.Denominator :
                num * pct.Denominator / (pct.Denominator + pct.Numerator);
        }

        public static Tuple<BigInteger, BigInteger> ToIncreasingPriceOrder(
            BigInteger sqrtPrice0,
            BigInteger sqrtPrice1
        )
        {
            return (sqrtPrice0 > sqrtPrice1) ? 
                Tuple.Create<BigInteger, BigInteger>(sqrtPrice1, sqrtPrice0) : 
                Tuple.Create<BigInteger, BigInteger>(sqrtPrice0, sqrtPrice1);
        }

        public static BigInteger GetNextSqrtPrice(
          BigInteger sqrtPrice,
          BigInteger currLiquidity,
          BigInteger amount,
          bool amountSpecifiedIsInput,
          bool aToB
        )
        {
            return (amountSpecifiedIsInput == aToB)
            ? GetNextSqrtPriceFromARoundUp(sqrtPrice, currLiquidity, amount, amountSpecifiedIsInput)
            : GetNextSqrtPriceFromBRoundDown(sqrtPrice, currLiquidity, amount, amountSpecifiedIsInput);
        }
        public static BigInteger GetNextSqrtPriceFromARoundUp(
            BigInteger sqrtPrice,
            BigInteger currLiquidity,
            BigInteger amount,
            bool amountSpecifiedIsInput
        )
        {
            if (amount == 0)
            {
                return sqrtPrice;
            }

            BigInteger p = BitMath.Mul(sqrtPrice, amount, 256);
            BigInteger numerator = BitMath.Mul(currLiquidity, sqrtPrice, 256) << 64;
            if (BitMath.IsOverLimit(numerator, 256))
            {
                throw new WhirlpoolsException(
                    "getNextSqrtPriceFromARoundUp - numerator overflow u256",
                    MathErrorCode.MultiplicationOverflow
                );
            }

            BigInteger currLiquidityShiftLeft = currLiquidity << 64;
            if (!amountSpecifiedIsInput && currLiquidityShiftLeft <= p)
            {
                throw new WhirlpoolsException(
                    "getNextSqrtPriceFromARoundUp - Unable to divide currLiquidityX64 by product",
                    MathErrorCode.DivideByZero
                );
            }

            BigInteger denominator = amountSpecifiedIsInput
                ? currLiquidityShiftLeft + p
                : currLiquidityShiftLeft + p;

            BigInteger price = BitMath.DivRoundUp(numerator, denominator);

            if (price < BigInteger.Parse(MathConstants.MIN_SQRT_PRICE))
            {
                throw new WhirlpoolsException(
                    "getNextSqrtPriceFromARoundUp - price less than min sqrt price",
                    TokenErrorCode.TokenMinSubceeded
                );
            }
            else if (price > BigInteger.Parse(MathConstants.MAX_SQRT_PRICE))
            {
                throw new WhirlpoolsException(
                    "getNextSqrtPriceFromARoundUp - price less than max sqrt price",
                    TokenErrorCode.TokenMaxExceeded
                );
            }

            return price;
        }

        public static BigInteger GetNextSqrtPriceFromBRoundDown(
            BigInteger sqrtPrice,
            BigInteger currLiquidity,
            BigInteger amount,
            bool amountSpecifiedIsInput
        )
        {
            BigInteger amountX64 = (amount << 64); 
            BigInteger delta = BitMath.DivRoundUp(amountX64, currLiquidity, !amountSpecifiedIsInput);
            
            return amountSpecifiedIsInput ? (sqrtPrice + delta) : (sqrtPrice - delta);
        }
    }
}
