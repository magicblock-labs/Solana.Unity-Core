using Solana.Unity.Dex.Math;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Exceptions;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Swap;

namespace Solana.Unity.Dex.Orca.Quotes.Swap
{
    /// <summary>
    /// Utility class for SwapQuote.
    /// </summary>
    public static class SwapQuoteUtils
    {
        /// <summary>
        /// Checks if all the tick arrays have been initialized.
        /// </summary>
        /// <param name="tickArrays"></param>
        /// <param name="throwException"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static bool CheckIfAllTickArraysInitialized(IList<TickArrayContainer> tickArrays, bool throwException = true)
        {
            // Check if all the tick arrays have been initialized.
            var uninitializedIndices = TickArrayUtils.GetUninitializedArrays(tickArrays.Select(e => e.Data).ToArray());
            if (uninitializedIndices.Length > 0)
            {
                if (throwException)
                {
                    //throw exception 
                    string uninitializedArrays = String.Join(",",
                        uninitializedIndices.Select((i) => tickArrays[i].Address.ToString()).ToList()
                    );
                    throw new Exception($"TickArray addresses - [{uninitializedArrays}] need to be initialized.");
                }

                //or just return false
                return false;
            }

            return true;
        }

        public static SwapQuote SwapQuoteWithParams(
            SwapQuoteParam swapQuoteParam,
            Percentage slippageTolerance
        )
        {
            //CheckIfAllTickArraysInitialized(swapQuoteParam.TickArrays);
            SwapQuote quote = SimulateSwap(swapQuoteParam);
            var (amount, otherAmountThreshold) = SwapUtils.CalculateSwapAmountsFromQuote(
                swapQuoteParam.TokenAmount,
                quote.EstimatedAmountIn,
                quote.EstimatedAmountOut, 
                slippageTolerance,
                swapQuoteParam.AmountSpecifiedIsInput
            );
            quote.Amount = amount;
            quote.OtherAmountThreshold = otherAmountThreshold;
            
            return quote;
        }
        
        /// <summary>
        /// Returns a SwapQuote using the input token mint.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="whirlpool"></param>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="inputTokenMint"></param>
        /// <param name="tokenAmount"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="programId"></param>
        /// <returns></returns>
        public static async Task<SwapQuote> SwapQuoteByInputToken(
            IWhirlpoolContext ctx,
            Whirlpool whirlpool,
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMint,
            BigInteger tokenAmount,
            Percentage slippageTolerance,
            PublicKey programId
        )
        {
            SwapQuoteParam swapQuoteParam = await SwapQuoteByToken(
                ctx,
                whirlpool,
                whirlpoolAddress,
                inputTokenMint,
                tokenAmount,
                amountSpecifiedIsInput: true,
                programId
            );

            return SwapQuoteWithParams(
                swapQuoteParam,
                slippageTolerance
            );
        }

        /// <summary>
        /// Returns a SwapQuote using the output token mint.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="whirlpool"></param>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="outputTokenMint"></param>
        /// <param name="tokenAmount"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="programId"></param>
        /// <returns></returns>
        public static async Task<SwapQuote> SwapQuoteByOutputToken(
            IWhirlpoolContext ctx,
            Whirlpool whirlpool,
            PublicKey whirlpoolAddress,
            PublicKey outputTokenMint,
            BigInteger tokenAmount,
            Percentage slippageTolerance,
            PublicKey programId
        )
        {
            SwapQuoteParam swapQuoteParam = await SwapQuoteByToken(
                    ctx,
                    whirlpool,
                    whirlpoolAddress,
                    outputTokenMint,
                    tokenAmount,
                    amountSpecifiedIsInput: false,
                    programId
                );
                
            return SwapQuoteWithParams(
                swapQuoteParam, 
                slippageTolerance
            );
        }

        /// <summary>
        /// Returns a SwapQuoteParam object that can be used to calculate a swap quote.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="whirlpool"></param>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="inputTokenMint"></param>
        /// <param name="tokenAmount"></param>
        /// <param name="amountSpecifiedIsInput"></param>
        /// <param name="programId"></param>
        /// <returns></returns>
        /// <exception cref="WhirlpoolsException"></exception>
        public static async Task<SwapQuoteParam> SwapQuoteByToken(
            IWhirlpoolContext ctx,
            Whirlpool whirlpool,
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMint,
            BigInteger tokenAmount,
            bool amountSpecifiedIsInput,
            PublicKey programId
        )
        {
            TokenType swapTokenType = PoolUtils.GetTokenType(whirlpool, inputTokenMint);
            if(swapTokenType == TokenType.None)
                throw new WhirlpoolsException(
                    "swapTokenMint does not match any tokens on this pool",
                    SwapErrorCode.SqrtPriceOutOfBounds            
                );
            bool aToB = inputTokenMint == whirlpool.TokenMintA;

            IList<TickArrayContainer> tickArrays = await SwapUtils.GetTickArrays(
                ctx,
                whirlpool.TickCurrentIndex,
                whirlpool.TickSpacing,
                aToB,
                programId,
                whirlpoolAddress
            );

            return new SwapQuoteParam
            {
                Whirlpool = whirlpool,
                WhirlpoolAddress = whirlpoolAddress,
                TokenAmount = tokenAmount,
                AtoB = aToB,
                AmountSpecifiedIsInput = amountSpecifiedIsInput,
                SqrtPriceLimit = SwapUtils.GetDefaultSqrtPriceLimit(aToB),
                OtherAmountThreshold = SwapUtils.GetDefaultOtherAmountThreshold(amountSpecifiedIsInput),
                TickArrays = tickArrays
            };
        }
        
        public static SwapQuote SimulateSwap(SwapQuoteParam quoteParam) 
        {
            if (quoteParam.SqrtPriceLimit > BigInteger.Parse(MathConstants.MAX_SQRT_PRICE) ||
                quoteParam.SqrtPriceLimit < BigInteger.Parse(MathConstants.MIN_SQRT_PRICE))
            {
                throw new WhirlpoolsException(
                    "Provided SqrtPriceLimit is out of bounds.",
                    SwapErrorCode.SqrtPriceOutOfBounds            
                );
            }

            if ((quoteParam.AtoB && quoteParam.SqrtPriceLimit > quoteParam.Whirlpool.SqrtPrice) ||
                (!quoteParam.AtoB && quoteParam.SqrtPriceLimit < quoteParam.Whirlpool.SqrtPrice))
            {
                throw new WhirlpoolsException(
                    "Provided SqrtPriceLimit is in the opposite direction of the trade.",
                    SwapErrorCode.InvalidSqrtPriceLimitDirection
                );
            }
            
            if (quoteParam.TokenAmount  == 0)
            {
                throw new WhirlpoolsException(
                    "Provided tokenAmount is zero.", SwapErrorCode.ZeroTradableAmount
                );
            }
            
            TickArraySequence tickSequence = new(
                quoteParam.TickArrays,
                quoteParam.Whirlpool.TickSpacing,
                quoteParam.AtoB
            );

            if (!tickSequence.IsValidTickArray0(quoteParam.Whirlpool.TickCurrentIndex))
            {
                throw new WhirlpoolsException(
                    "TickArray at index 0 does not contain the Whirlpool current tick index.",
                    SwapErrorCode.TickArraySequenceInvalid
                ); 
            }

            SwapResults swapResults = SwapManager.ComputeSwap(
                quoteParam.Whirlpool,
                tickSequence,
                quoteParam.TokenAmount,
                quoteParam.SqrtPriceLimit,
                quoteParam.AmountSpecifiedIsInput,
                quoteParam.AtoB
            ); 
            
            if (quoteParam.AmountSpecifiedIsInput) {
                if ((quoteParam.AtoB && quoteParam.OtherAmountThreshold > swapResults.AmountB) ||
                    (!quoteParam.AtoB && quoteParam.OtherAmountThreshold > swapResults.AmountA))
                {
                    throw new WhirlpoolsException(
                        "Quoted amount for the other token is below the otherAmountThreshold.",
                        SwapErrorCode.AmountOutBelowMinimum
                    );
                }
            } 
            else
            {
                if ((quoteParam.AtoB && quoteParam.OtherAmountThreshold < swapResults.AmountA) ||
                    (!quoteParam.AtoB && quoteParam.OtherAmountThreshold < swapResults.AmountB))
                {
                    throw new WhirlpoolsException(
                        "Quoted amount for the other token is above the otherAmountThreshold.",
                        SwapErrorCode.AmountInAboveMaximum
                    );
                }
            }
            
            BigInteger estimatedAmountIn = quoteParam.AtoB ? swapResults.AmountA : swapResults.AmountB;
            BigInteger estimatedAmountOut = quoteParam.AtoB ? swapResults.AmountB : swapResults.AmountA;

            var touchedArrays = tickSequence.GetTouchedArrays(MathConstants.MAX_SWAP_TICK_ARRAYS);
            int numOfTickCrossings = touchedArrays.Count;
            if (numOfTickCrossings > MathConstants.MAX_SWAP_TICK_ARRAYS)
            {
                throw new WhirlpoolsException(
                    $"Input amount causes the quote to traverse more than the allowable amount of tick-arrays {numOfTickCrossings}",
                    SwapErrorCode.TickArrayCrossingAboveMax
                ); 
            }
            
            return new SwapQuote
            {
                EstimatedAmountIn = estimatedAmountIn,
                EstimatedAmountOut = estimatedAmountOut,
                EstimatedEndTickIndex = swapResults.NextTickIndex,
                EstimatedEndSqrtPrice = swapResults.NextSqrtPrice,
                EstimatedFeeAmount = swapResults.TotalFeeAmount,
                Amount = quoteParam.TokenAmount, 
                AmountSpecifiedIsInput = quoteParam.AmountSpecifiedIsInput,
                AtoB = quoteParam.AtoB, 
                OtherAmountThreshold = quoteParam.OtherAmountThreshold,
                SqrtPriceLimit = quoteParam.SqrtPriceLimit,
                TickArray0 = touchedArrays[0],
                TickArray1 = touchedArrays[1],
                TickArray2 = touchedArrays[2],
            }; 
        }
    }

    public class SwapQuoteParam 
    {
        public Whirlpool Whirlpool { get; set; }
        public PublicKey WhirlpoolAddress { get; set; }
        public BigInteger TokenAmount { get; set; }
        public BigInteger SqrtPriceLimit { get; set; }
        public BigInteger OtherAmountThreshold { get; set; }
        public bool AtoB { get; set; }
        public bool AmountSpecifiedIsInput { get; set; }
        public IList<TickArrayContainer> TickArrays { get; set; }
    }
}
