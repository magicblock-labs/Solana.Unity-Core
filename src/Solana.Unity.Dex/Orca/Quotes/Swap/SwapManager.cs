using System;
using System.Linq;
using System.Numerics;

using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;

namespace Solana.Unity.Dex.Orca.Quotes.Swap
{
    internal static class SwapManager
    {
        public static SwapResults ComputeSwap(
            Whirlpool whirlpool,
            TickArraySequence tickSequence, 
            BigInteger tokenAmount, 
            BigInteger sqrtPriceLimit,
            bool amountSpecifiedIsInput, 
            bool aToB
        )
        {
            BigInteger amountRemaining = tokenAmount;
            BigInteger amountCalculated = BigInteger.Zero;
            BigInteger currSqrtPrice = whirlpool.SqrtPrice;
            BigInteger currLiquidity = whirlpool.Liquidity;
            BigInteger totalFeeAmount = BigInteger.Zero;
            int currTickIndex = whirlpool.TickCurrentIndex;
            ushort feeRate = whirlpool.FeeRate;
            ushort protocolFeeRate = whirlpool.ProtocolFeeRate;
            BigInteger currProtocolFee = BigInteger.Zero;
            BigInteger currFeeGrowthGlobalInput = aToB
                ? whirlpool.FeeGrowthGlobalA
                : whirlpool.FeeGrowthGlobalB;


            while (amountRemaining > BigInteger.Zero && sqrtPriceLimit != currSqrtPrice)
            {
                var (nextTickIndex, nextInitializedTick) = tickSequence.FindNextInitializedTickIndex(currTickIndex);
                NextSqrtPrice _a = GetNextSqrtPrices(nextTickIndex, sqrtPriceLimit, aToB);
                BigInteger nextTickPrice = _a.NextTickPrice;
                BigInteger targetSqrtPrice = _a.NextSqrtPriceLimit;
                
                SwapMath.SwapStep swapComputation = SwapMath.ComputeSwapStep(amountRemaining, feeRate, currLiquidity, currSqrtPrice, targetSqrtPrice, amountSpecifiedIsInput, aToB);
                totalFeeAmount = totalFeeAmount + swapComputation.FeeAmount;
                if (amountSpecifiedIsInput)
                {
                    amountRemaining = amountRemaining - swapComputation.AmountIn;
                    amountRemaining = amountRemaining - swapComputation.FeeAmount;
                    amountCalculated = amountCalculated + swapComputation.AmountOut;
                }
                else
                {
                    amountRemaining = amountRemaining - swapComputation.AmountOut;
                    amountCalculated = amountCalculated + swapComputation.AmountIn;
                    amountCalculated = amountCalculated + swapComputation.FeeAmount;
                }
                
                CalculatedFee _b = CalculateFees(swapComputation.FeeAmount, protocolFeeRate, currLiquidity, currProtocolFee, currFeeGrowthGlobalInput); 
                BigInteger nextProtocolFee = _b.NextProtocolFee;
                BigInteger nextFeeGrowthGlobalInput = _b.NextFeeGrowthGlobalInput;
                
                currProtocolFee = nextProtocolFee;
                currFeeGrowthGlobalInput = nextFeeGrowthGlobalInput;
                if (swapComputation.NextPrice == nextTickPrice)
                {
                    var nextTick = tickSequence.GetTick(nextTickIndex);
                    if (nextTick.Initialized)
                    {
                        currLiquidity = CalculateNextLiquidity(nextTick.LiquidityNet, currLiquidity, aToB);
                    }
                    currTickIndex = aToB ? nextTickIndex - 1 : nextTickIndex;
                }
                else
                {
                    currTickIndex = PriceMath.SqrtPriceX64ToTickIndex(swapComputation.NextPrice);
                }
                currSqrtPrice = swapComputation.NextPrice;
            }

            var (amountA, amountB) = CalculateEstTokens(tokenAmount, amountRemaining, amountCalculated, aToB, amountSpecifiedIsInput); 
            
            return new SwapResults 
            {
                AmountA = amountA, 
                AmountB = amountB,
                NextTickIndex = currTickIndex, 
                NextSqrtPrice = currSqrtPrice,
                TotalFeeAmount = totalFeeAmount
            };
        }

        private static NextSqrtPrice GetNextSqrtPrices(int nextTick, BigInteger sqrtPriceLimit, bool aToB)
        {
            BigInteger nextTickPrice = PriceMath.TickIndexToSqrtPriceX64(nextTick);
            BigInteger nextSqrtPriceLimit = aToB
                ? BigInteger.Max(sqrtPriceLimit, nextTickPrice)
                : BigInteger.Min(sqrtPriceLimit, nextTickPrice);
                
            return new NextSqrtPrice 
            {
                NextTickPrice = nextTickPrice, 
                NextSqrtPriceLimit = nextSqrtPriceLimit
            };
        }

        private static CalculatedFee CalculateFees(
            BigInteger feeAmount,
            ushort protocolFeeRate,
            BigInteger currLiquidity,
            BigInteger currProtocolFee,
            BigInteger currFeeGrowthGlobalInput
        )
        {
            BigInteger nextProtocolFee = currProtocolFee;
            BigInteger nextFeeGrowthGlobalInput = currFeeGrowthGlobalInput;
            BigInteger globalFee = feeAmount;
            
            if (protocolFeeRate > 0)
            {
                var delta = CalculateProtocolFee(globalFee, protocolFeeRate);
                globalFee = globalFee - delta;
                nextProtocolFee = nextProtocolFee + currProtocolFee;
            }
            if (currLiquidity > BigInteger.Zero)
            {
                BigInteger globalFeeIncrement = (globalFee << 64) / currLiquidity;
                nextFeeGrowthGlobalInput = nextFeeGrowthGlobalInput + globalFeeIncrement;
            }
            
            return new CalculatedFee {
                NextProtocolFee = nextProtocolFee,
                NextFeeGrowthGlobalInput = nextFeeGrowthGlobalInput
            };
        }

        private static BigInteger CalculateProtocolFee(BigInteger globalFee, BigInteger protocolFeeRate)
        {
            return globalFee * ((BigInteger)protocolFeeRate / SwapMath.PROTOCOL_FEE_RATE_MUL_VALUE);
        }
        
        private static Tuple<BigInteger, BigInteger> CalculateEstTokens(
            BigInteger amount,
            BigInteger amountRemaining,
            BigInteger amountCalculated, 
            bool aToB, 
            bool amountSpecifiedIsInput
        )
        {
            return aToB == amountSpecifiedIsInput
                ? Tuple.Create<BigInteger, BigInteger>(amount - amountRemaining, amountCalculated) 
                : Tuple.Create<BigInteger, BigInteger>(amountCalculated, amount - amountRemaining);
        }
        
        private static BigInteger CalculateNextLiquidity(BigInteger tickNetLiquidity, BigInteger currLiquidity, bool aToB)
        {
            return aToB ? 
                currLiquidity - tickNetLiquidity : 
                currLiquidity + tickNetLiquidity;
        }
        
        private struct NextSqrtPrice
        {
            public BigInteger NextTickPrice { get; set; }
            public BigInteger NextSqrtPriceLimit { get; set; }
        }
        
        private struct CalculatedFee
        {
            public BigInteger NextProtocolFee { get; set; }
            public BigInteger NextFeeGrowthGlobalInput { get; set; }
        }
    }
    
    public struct SwapResults 
    {
        public BigInteger AmountA { get; set; }
        public BigInteger AmountB { get; set; }
        public BigInteger NextSqrtPrice { get; set; }
        public BigInteger TotalFeeAmount { get; set; }
        public int NextTickIndex { get; set; }
    }
}
