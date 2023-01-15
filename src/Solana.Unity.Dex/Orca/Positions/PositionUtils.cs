using System;
using System.Numerics;

using Solana.Unity.Dex.Orca.Math;

namespace Solana.Unity.Dex.Orca.Positions
{
    public static class PositionUtils
    {
        public static PositionStatus GetPositionStatus(int tickCurrentIndex, int tickLowerIndex, int tickUpperIndex) 
        {
            return (tickCurrentIndex < tickLowerIndex) ? PositionStatus.BelowRange : 
                (tickCurrentIndex < tickUpperIndex) ? PositionStatus.InRange : 
                    PositionStatus.AboveRange; 
        }
        
        public static BigInteger GetTokenAFromLiquidity(
            BigInteger liquidity, 
            BigInteger sqrtPrice0X64, 
            BigInteger sqrtPrice1X64, 
            bool roundUp
        )
        {
            Tuple<BigInteger, BigInteger> ordered = OrderSqrtPrice(sqrtPrice0X64, sqrtPrice1X64);

            BigInteger numerator = (liquidity * (ordered.Item2 - ordered.Item1)) << 64;
            BigInteger denominator = ordered.Item2 * ordered.Item1; 
            
            return roundUp ? 
                ArithmeticUtils.DivRoundUp(numerator, denominator) : 
                numerator / denominator;
        }

        public static BigInteger GetTokenBFromLiquidity(
            BigInteger liquidity,
            BigInteger sqrtPrice0X64,
            BigInteger sqrtPrice1X64,
            bool roundUp
        )
        {
            Tuple<BigInteger, BigInteger> ordered = OrderSqrtPrice(sqrtPrice0X64, sqrtPrice1X64);

            BigInteger result = (liquidity * (ordered.Item2 - ordered.Item1));

            return roundUp ?
                ArithmeticUtils.ShiftRightRoundUp(result) :
                result >> 64;
        }
        
        public static BigInteger GetLiquidityFromTokenA(
            BigInteger amount, 
            BigInteger sqrtPriceLowerX64, 
            BigInteger sqrtPriceUpperX64, 
            bool roundUp
        )
        {
            BigInteger result = amount * sqrtPriceLowerX64 * sqrtPriceUpperX64 / (sqrtPriceUpperX64 - sqrtPriceLowerX64);
            return roundUp ?
                ArithmeticUtils.ShiftRightRoundUp(result) :
                result >> 64;
        }
        
        public static BigInteger GetLiquidityFromTokenB(
            BigInteger amount,
            BigInteger sqrtPriceLowerX64,
            BigInteger sqrtPriceUpperX64,
            bool roundUp
        )
        {
            BigInteger numerator = amount << 64;
            BigInteger denominator = sqrtPriceUpperX64 - sqrtPriceLowerX64;
            
            return roundUp ?
                ArithmeticUtils.DivRoundUp(numerator, denominator) :
                numerator / denominator;
        }
        
        private static Tuple<BigInteger, BigInteger> OrderSqrtPrice(BigInteger sqrtPrice0X64, BigInteger sqrtPrice1X64)
        {
            return  (sqrtPrice0X64 < sqrtPrice1X64) ? 
                Tuple.Create<BigInteger, BigInteger>(sqrtPrice0X64, sqrtPrice1X64) :
                Tuple.Create<BigInteger, BigInteger>(sqrtPrice1X64, sqrtPrice0X64);
        }
    }
}