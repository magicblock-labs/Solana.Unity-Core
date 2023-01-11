using System;
using System.Numerics;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Positions;

namespace Solana.Unity.Dex.Orca.Quotes
{
    public static class DecreaseLiquidityQuoteUtils
    {
        public static DecreaseLiquidityQuote GenerateDecreaseQuote(
            BigInteger liquidity,
            Percentage slippageTolerance,
            Position position, 
            Whirlpool whirlpool
        )
        {
             if (liquidity > position.Liquidity) {
                 throw new ArgumentException("Quote liquidity should not be greater than position liquidity");
             }
             
             return GenerateDecreaseQuoteWithParams(
                 new DecreaseLiquidityQuoteParams
                 {
                     Liquidity = liquidity, 
                     SqrtPrice = whirlpool.SqrtPrice, 
                     TickCurrentIndex = whirlpool.TickCurrentIndex, 
                     TickUpperIndex = position.TickUpperIndex, 
                     TickLowerIndex = position.TickLowerIndex, 
                     SlippageTolerance = slippageTolerance
                 }
             ); 
        }
        
        public static DecreaseLiquidityQuote GenerateDecreaseQuoteWithParams(
            DecreaseLiquidityQuoteParams decreaseParams
        )
        {
            //check that indexes are in bounds
            if (!TickUtils.CheckTickInBounds(decreaseParams.TickLowerIndex)){
                throw new IndexOutOfRangeException($"TickLowerIndex is out of bounds: {decreaseParams.TickLowerIndex}");
            }
            if (!TickUtils.CheckTickInBounds(decreaseParams.TickUpperIndex)){
                throw new IndexOutOfRangeException($"TickUpperIndex is out of bounds: {decreaseParams.TickUpperIndex}");
            }
            if (!TickUtils.CheckTickInBounds(decreaseParams.TickCurrentIndex)){
                throw new IndexOutOfRangeException($"TickCurrentIndex is out of bounds: {decreaseParams.TickCurrentIndex}");
            }
            
            PositionStatus status = PositionUtils.GetPositionStatus(
                decreaseParams.TickCurrentIndex,
                decreaseParams.TickLowerIndex,
                decreaseParams.TickUpperIndex
            );
            
            switch(status)
            {
                case PositionStatus.BelowRange: 
                    return QuotePositionBelowRange(decreaseParams);
                case PositionStatus.InRange:
                    return QuotePositionInRange(decreaseParams);
                case PositionStatus.AboveRange:
                    return QuotePositionAboveRange(decreaseParams);
            }
            
            return new DecreaseLiquidityQuote();
        }

        private static DecreaseLiquidityQuote QuotePositionBelowRange(DecreaseLiquidityQuoteParams decreaseParams)
        {
            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickUpperIndex);
            
            BigInteger tokenEstA = PositionUtils.GetTokenAFromLiquidity(
                decreaseParams.Liquidity,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64, 
                false
            ); 
            BigInteger tokenMinA = TokenMath.AdjustForSlippage( 
                tokenEstA, 
                decreaseParams.SlippageTolerance,
                false
            ); 
            
            return new DecreaseLiquidityQuote{
                TokenMinA = tokenMinA, 
                TokenEstA = tokenEstA, 
                TokenMinB = BigInteger.Zero,
                TokenEstB = BigInteger.Zero,
                LiquidityAmount = decreaseParams.Liquidity
            }; 
        }

        private static DecreaseLiquidityQuote QuotePositionInRange(DecreaseLiquidityQuoteParams decreaseParams)
        {
            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickUpperIndex);

            BigInteger tokenEstA = PositionUtils.GetTokenAFromLiquidity(
                decreaseParams.Liquidity,
                decreaseParams.SqrtPrice,
                sqrtPriceUpperX64,
                false
            );
            BigInteger tokenMinA = TokenMath.AdjustForSlippage( 
                tokenEstA,
                decreaseParams.SlippageTolerance,
                false
            );
            BigInteger tokenEstB = PositionUtils.GetTokenBFromLiquidity(
                decreaseParams.Liquidity,
                sqrtPriceLowerX64,
                decreaseParams.SqrtPrice,
                false
            );
            BigInteger tokenMinB = TokenMath.AdjustForSlippage( 
                tokenEstB,
                decreaseParams.SlippageTolerance,
                false
            );

            return new DecreaseLiquidityQuote
            {
                TokenMinA = tokenMinA,
                TokenEstA = tokenEstA,
                TokenMinB = tokenMinB,
                TokenEstB = tokenEstB,
                LiquidityAmount = decreaseParams.Liquidity
            };
        }

        private static DecreaseLiquidityQuote QuotePositionAboveRange(DecreaseLiquidityQuoteParams decreaseParams)
        {
            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(decreaseParams.TickUpperIndex);

            BigInteger tokenEstB = PositionUtils.GetTokenAFromLiquidity(
                decreaseParams.Liquidity,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64,
                false
            );
            BigInteger tokenMinB = TokenMath.AdjustForSlippage(
                tokenEstB,
                decreaseParams.SlippageTolerance,
                false
            );

            return new DecreaseLiquidityQuote
            {
                TokenMinA = BigInteger.Zero,
                TokenEstA = BigInteger.Zero,
                TokenMinB = tokenMinB,
                TokenEstB = tokenEstB,
                LiquidityAmount = decreaseParams.Liquidity
            };
        }
    }

    public class DecreaseLiquidityQuoteParams 
    {
        public BigInteger Liquidity { get; set; }
        public BigInteger SqrtPrice { get; set; }
        public int TickCurrentIndex { get; set; }
        public int TickLowerIndex { get; set; }
        public int TickUpperIndex { get; set; }
        public Percentage SlippageTolerance { get; set; }
    }

    public class DecreaseLiquidityQuote
    {
        public BigInteger LiquidityAmount { get; set; }
        public BigInteger TokenMinA { get; set; }
        public BigInteger TokenMinB { get; set; }
        public BigInteger TokenEstA { get; set; }
        public BigInteger TokenEstB { get; set; }
    }
}