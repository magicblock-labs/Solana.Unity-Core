using System.Numerics;

namespace Solana.Unity.Dex.Orca.Math
{
    /// <summary>
    /// Calculations used specifically for calculating swap amounts and estimates. 
    /// </summary>
    public static class SwapMath
    {
        public static readonly BigInteger PROTOCOL_FEE_RATE_MUL_VALUE = 10_000;
        public static readonly BigInteger FEE_RATE_MUL_VALUE = 1_000_000;

        public class SwapStep
        {
            public BigInteger AmountIn { get; set; }
            public BigInteger AmountOut { get; set; }
            public BigInteger NextPrice { get; set; }
            public BigInteger FeeAmount { get; set; }
        }

        public static SwapStep ComputeSwapStep(
            BigInteger amountRemaining,
            long feeRate,
            BigInteger currentLiquidity,
            BigInteger currentSqrtPrice,
            BigInteger targetSqrtPrice,
            bool amountSpecifiedIsInput,
            bool aToB
        )
        {
            BigInteger amountFixedDelta = GetAmountFixedDelta(
                currentSqrtPrice,
                targetSqrtPrice,
                currentLiquidity,
                amountSpecifiedIsInput,
                aToB
            );

            BigInteger amountCalc = amountRemaining;
            if (amountSpecifiedIsInput)
            {
                BigInteger result = BitMath.MulDiv(
                    amountRemaining,
                    SwapMath.FEE_RATE_MUL_VALUE - feeRate,
                    SwapMath.FEE_RATE_MUL_VALUE,
                    128
                );
                amountCalc = result;
            }

            BigInteger nextSqrtPrice = amountCalc >= amountFixedDelta
                ? targetSqrtPrice
                : TokenMath.GetNextSqrtPrice(currentSqrtPrice, currentLiquidity, amountCalc, amountSpecifiedIsInput, aToB);

            bool isMaxSwap = nextSqrtPrice == targetSqrtPrice;

            BigInteger amountUnfixedDelta = GetAmountUnfixedDelta(
                currentSqrtPrice,
                nextSqrtPrice,
                currentLiquidity,
                amountSpecifiedIsInput,
                aToB
            );

            if (!isMaxSwap)
            {
                amountFixedDelta = GetAmountFixedDelta(
                  currentSqrtPrice,
                  nextSqrtPrice,
                  currentLiquidity,
                  amountSpecifiedIsInput,
                  aToB
                );
            }

            BigInteger amountIn = amountSpecifiedIsInput ? amountFixedDelta : amountUnfixedDelta;
            BigInteger amountOut = amountSpecifiedIsInput ? amountUnfixedDelta : amountFixedDelta;

            if (!amountSpecifiedIsInput && amountOut > amountRemaining)
            {
                amountOut = amountRemaining;
            }

            BigInteger feeAmount;
            if (amountSpecifiedIsInput && !isMaxSwap)
            {
                feeAmount = amountRemaining - amountIn;
            }
            else
            {
                BigInteger feeRateBN = feeRate;
                feeAmount = BitMath.MulDivRoundUp(
                    amountIn, 
                    feeRateBN, 
                    SwapMath.FEE_RATE_MUL_VALUE - feeRateBN, 
                    128
                );
            }

            return new SwapStep {
                AmountIn = amountIn,
                AmountOut = amountOut,
                NextPrice = nextSqrtPrice,
                FeeAmount = feeAmount
            };
        }

        public static BigInteger GetAmountFixedDelta(
            BigInteger currentSqrtPrice,
            BigInteger targetSqrtPrice,
            BigInteger currentLiquidity,
            bool amountSpecifiedIsInput,
            bool aToB
        )
        {
            return (aToB == amountSpecifiedIsInput)
                ? TokenMath.GetAmountDeltaA(currentSqrtPrice, targetSqrtPrice, currentLiquidity, amountSpecifiedIsInput)
                : TokenMath.GetAmountDeltaB(currentSqrtPrice, targetSqrtPrice, currentLiquidity, amountSpecifiedIsInput);
        }

        public static BigInteger GetAmountUnfixedDelta(
            BigInteger currentSqrtPrice,
            BigInteger targetSqrtPrice,
            BigInteger currentLiquidity,
            bool amountSpecifiedIsInput,
            bool aToB
        )
        {
            return (aToB == amountSpecifiedIsInput)
                ? TokenMath.GetAmountDeltaB(currentSqrtPrice, targetSqrtPrice, currentLiquidity, amountSpecifiedIsInput)
                : TokenMath.GetAmountDeltaA(currentSqrtPrice, targetSqrtPrice, currentLiquidity, amountSpecifiedIsInput);
        }
    }
}