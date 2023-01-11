using System;
using System.Numerics;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Positions;

namespace Solana.Unity.Dex.Orca.Quotes
{
    public static class IncreaseLiquidityQuoteUtils
    {
        public static IncreaseLiquidityQuote GenerateIncreaseQuote(
            PublicKey inputTokenMint,
            BigDecimal inputTokenAmount,
            int tickLower,
            int tickUpper, 
            Percentage slippageTolerance,
            Whirlpool whirlpool
        )
        {
            PublicKey inputMint = (inputTokenMint == whirlpool.TokenMintA) ? 
                whirlpool.TokenMintA : 
                whirlpool.TokenMintB; 
                
            return GenerateIncreaseQuoteWithParams(
               new IncreaseLiquidityQuoteParams{
                   InputTokenMint = inputMint,
                   //TODO: (HIGH) see below 
                   //InputTokenAmount = DecimalUtil.toU64(inputTokenAmount, inputTokenInfo.decimals), 
                   TickCurrentIndex = whirlpool.TickCurrentIndex,
                   TickLowerIndex = TickUtils.GetInitializableTickIndex(tickLower, whirlpool.TickSpacing),
                   TickUpperIndex = TickUtils.GetInitializableTickIndex(tickUpper, whirlpool.TickSpacing),
                   SlippageTolerance = slippageTolerance, 
                   TokenMintA = whirlpool.TokenMintA, 
                   TokenMintB = whirlpool.TokenMintB,
                   SqrtPrice = whirlpool.SqrtPrice
               } 
            ); 
        }

        public static IncreaseLiquidityQuote GenerateIncreaseQuoteWithParams(
            IncreaseLiquidityQuoteParams increaseParams
        )
        {
            //check veracity of tick indices 
            if (!TickUtils.CheckTickInBounds(increaseParams.TickLowerIndex)){
                throw new IndexOutOfRangeException($"TickLowerIndex is out of bounds: {increaseParams.TickLowerIndex}");
            }
            if (!TickUtils.CheckTickInBounds(increaseParams.TickUpperIndex)){
                throw new IndexOutOfRangeException($"TickUpperIndex is out of bounds: {increaseParams.TickUpperIndex}");
            }
            if (!TickUtils.CheckTickInBounds(increaseParams.TickCurrentIndex)){
                throw new IndexOutOfRangeException($"TickCurrentIndex is out of bounds: {increaseParams.TickCurrentIndex}");
            }
            
            //check veracity of input token mint 
            if (increaseParams.InputTokenMint != increaseParams.TokenMintA && 
                increaseParams.InputTokenMint != increaseParams.TokenMintB) {
                throw new ArgumentException($"Input token mint {increaseParams.InputTokenMint} does not match any tokens in the provided pool.");
            }
            
            PositionStatus positionStatus = PositionUtils.GetPositionStatus(
                increaseParams.TickCurrentIndex, increaseParams.TickLowerIndex, increaseParams.TickUpperIndex
            ); 
            
            switch(positionStatus)
            {
                case PositionStatus.BelowRange:
                    return QuotePositionBelowRange(increaseParams);
                case PositionStatus.InRange:
                    return QuotePositionInRange(increaseParams);
                case PositionStatus.AboveRange:
                    return QuotePositionAboveRange(increaseParams);
            }
            
            throw new Exception($"Unhandled position status: {positionStatus}");
        }

        private static IncreaseLiquidityQuote QuotePositionBelowRange(IncreaseLiquidityQuoteParams increaseParams)
        {
            if (increaseParams.TokenMintA != increaseParams.InputTokenMint) {
                return new IncreaseLiquidityQuote();
            }

            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickUpperIndex);
            
            BigInteger liquidityAmount = PositionUtils.GetLiquidityFromTokenA(
                increaseParams.InputTokenAmount,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64,
                false
            ); 
            
            BigInteger tokenEstA = PositionUtils.GetTokenAFromLiquidity(
                liquidityAmount,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64,
                true
            );
            
            BigInteger tokenMaxA = TokenMath.AdjustForSlippage(
                tokenEstA, increaseParams.SlippageTolerance, true
            );
            
            return new IncreaseLiquidityQuote
            {
                TokenMaxA = tokenMaxA, 
                TokenMaxB = BigInteger.Zero, 
                TokenEstA = tokenEstA, 
                TokenEstB = BigInteger.Zero, 
                LiquidityAmount = liquidityAmount
            };
        }

        private static IncreaseLiquidityQuote QuotePositionInRange(IncreaseLiquidityQuoteParams increaseParams)
        {
            BigInteger sqrtPriceX64 = increaseParams.SqrtPrice;
            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickUpperIndex);
            
            BigInteger tokenEstA = 0; 
            BigInteger tokenEstB = 0; 
            BigInteger liquidityAmount = 0; 
            
            bool tokenAisInput = (increaseParams.TokenMintA == increaseParams.InputTokenMint);
            if (tokenAisInput) 
                tokenEstA = increaseParams.InputTokenAmount; 
            else 
                tokenEstB = increaseParams.InputTokenAmount;
                
            if (tokenAisInput)
            {
                liquidityAmount = PositionUtils.GetLiquidityFromTokenA(tokenEstA, sqrtPriceX64, sqrtPriceUpperX64, false);
                tokenEstA = PositionUtils.GetTokenAFromLiquidity(liquidityAmount, sqrtPriceX64, sqrtPriceUpperX64, true);
                tokenEstB = PositionUtils.GetTokenBFromLiquidity(liquidityAmount, sqrtPriceLowerX64, sqrtPriceX64, true);
            }
            else
            {
                liquidityAmount = PositionUtils.GetLiquidityFromTokenB(tokenEstB, sqrtPriceLowerX64, sqrtPriceX64, false);
                tokenEstA = PositionUtils.GetTokenAFromLiquidity(liquidityAmount, sqrtPriceX64, sqrtPriceUpperX64, true);
                tokenEstB = PositionUtils.GetTokenBFromLiquidity(liquidityAmount, sqrtPriceLowerX64, sqrtPriceX64, true);
            }

            BigInteger tokenMaxA = TokenMath.AdjustForSlippage(tokenEstA, increaseParams.SlippageTolerance, true);
            BigInteger tokenMaxB = TokenMath.AdjustForSlippage(tokenEstB, increaseParams.SlippageTolerance, true);
            
            return new IncreaseLiquidityQuote
            {
                TokenEstA = tokenEstA, 
                TokenEstB = tokenEstB, 
                TokenMaxA = tokenMaxA, 
                TokenMaxB = tokenMaxB, 
                LiquidityAmount = liquidityAmount
            }; 
        }

        private static IncreaseLiquidityQuote QuotePositionAboveRange(IncreaseLiquidityQuoteParams increaseParams)
        {
            if (increaseParams.TokenMintB != increaseParams.InputTokenMint) {
                return new IncreaseLiquidityQuote();
            }

            BigInteger sqrtPriceLowerX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickLowerIndex);
            BigInteger sqrtPriceUpperX64 = PriceMath.TickIndexToSqrtPriceX64(increaseParams.TickUpperIndex);

            BigInteger liquidityAmount = PositionUtils.GetLiquidityFromTokenB(
                increaseParams.InputTokenAmount,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64,
                false
            );

            BigInteger tokenEstB = PositionUtils.GetTokenBFromLiquidity(
                liquidityAmount,
                sqrtPriceLowerX64,
                sqrtPriceUpperX64,
                true
            );

            BigInteger tokenMaxB = TokenMath.AdjustForSlippage(
                tokenEstB, increaseParams.SlippageTolerance, true
            );

            return new IncreaseLiquidityQuote
            {
                TokenMaxA = BigInteger.Zero,
                TokenMaxB = tokenMaxB,
                TokenEstA = BigInteger.Zero,
                TokenEstB = tokenEstB,
                LiquidityAmount = liquidityAmount
            };
        }
    }

    public class IncreaseLiquidityQuoteParams
    {
        public BigInteger InputTokenAmount { get; set; }
        public PublicKey InputTokenMint { get; set; }
        public PublicKey TokenMintA { get; set; }
        public PublicKey TokenMintB { get; set; }
        public BigInteger SqrtPrice { get; set; }
        public int TickCurrentIndex { get; set; }
        public int TickLowerIndex { get; set; }
        public int TickUpperIndex { get; set; }
        public Percentage SlippageTolerance { get; set; }
    }

    public class IncreaseLiquidityQuote
    {
        public BigInteger LiquidityAmount { get; set; }
        public BigInteger TokenMaxA { get; set; }
        public BigInteger TokenMaxB { get; set; }
        public BigInteger TokenEstA { get; set; }
        public BigInteger TokenEstB { get; set; }
    }
}