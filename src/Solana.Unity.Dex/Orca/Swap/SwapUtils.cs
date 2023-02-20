using Solana.Unity.Dex.Math;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Programs.Models;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Swap;

namespace Solana.Unity.Dex.Orca.Swap
{
    public static class SwapUtils
    {
        /// <summary>
        /// Get the default values for the sqrtPriceLimit parameter in a swap.
        /// </summary>
        /// <param name="aToB">The direction of a swap</param>
        /// <returns>The default values for the sqrtPriceLimit parameter in a swap.</returns>
        public static BigInteger GetDefaultSqrtPriceLimit(bool aToB)
        {
            return aToB ?
                BigInteger.Parse(MathConstants.MIN_SQRT_PRICE) :
                BigInteger.Parse(MathConstants.MAX_SQRT_PRICE);
        }

        /// <summary>
        /// Get the default values for the otherAmountThreshold parameter in a swap.
        /// </summary>
        /// <param name="amountSpecifiedIsInput">The direction of a swap</param>
        /// <returns>The default values for the otherAmountThreshold parameter in a swap.</returns>
        public static BigInteger GetDefaultOtherAmountThreshold(bool amountSpecifiedIsInput)
        {
            return amountSpecifiedIsInput ? ArithmeticUtils.ZERO : ArithmeticUtils.MaxU64;
        }

        /// <summary>
        /// Given the intended token mint to swap, return the swap direction of a swap for a Whirlpool.
        /// </summary>
        /// <param name="pool">The Whirlpool to evaluate the mint against</param>
        /// <param name="swapTokenMint">The token mint PublicKey the user bases their swap against</param>
        /// <param name="swapTokenIsInput">Whether the swap token is the input token. (similar to amountSpecifiedIsInput from swap Ix)</param>
        /// <returns>The direction of the swap given the swapTokenMint. undefined if the token mint is not part of the trade pair of the pool.</returns>
        public static SwapDirection GetSwapDirection(
            Whirlpool pool,
            PublicKey swapTokenMint,
            bool swapTokenIsInput
        )
        {
            TokenType tokenType = PoolUtils.GetTokenType(pool, swapTokenMint);
            if (tokenType == TokenType.None)
                return SwapDirection.None;

            return ((tokenType == TokenType.TokenA) == swapTokenIsInput) ?
                SwapDirection.AtoB : SwapDirection.BtoA;
        }

        /// <summary>
        /// Given the current tick-index, returns the dervied PDA and fetched data for the tick-arrays that
        /// this swap may traverse across.
        /// </summary>
        /// <param name="tickCurrentIndex">The current tickIndex for the Whirlpool to swap on.</param>
        /// <param name="tickSpacing">The tickSpacing for the Whirlpool.</param>
        /// <param name="aToB">The direction of the trade.</param>
        /// <param name="programId">The Whirlpool programId which the Whirlpool lives on.</param>
        /// <param name="whirlpoolAddress">PublicKey of the whirlpool to swap on.</param>
        /// <returns>An array of PublicKey[] for the tickArray accounts that this swap may traverse across.</returns>
        public static IList<PublicKey> GetTickArrayPublicKeys(
            int tickCurrentIndex,
            ushort tickSpacing,
            bool aToB,
            PublicKey programId,
            PublicKey whirlpoolAddress
        )
        {
            int shift = aToB ? 0 : tickSpacing;
            int offset = 0;
            List<PublicKey> tickArrayAddresses = new List<PublicKey>();
            for (int n = 0; n < TickConstants.MAX_SWAP_TICK_ARRAYS; n++)
            {
                int startIndex = 0;
                try
                {
                    startIndex = TickUtils.GetStartTickIndex(tickCurrentIndex + shift, tickSpacing, offset);
                }
                catch (Exception)
                {
                    return tickArrayAddresses;
                }

                Pda pda = PdaUtils.GetTickArray(programId, whirlpoolAddress, startIndex);
                tickArrayAddresses.Add(pda);
                offset = aToB ? offset - 1 : offset + 1;
            }

            return tickArrayAddresses;
        }

        /// <summary>
        /// Given the current tick-index, returns TickArray objects that this swap may traverse across.
        /// </summary>
        /// <param name="tickCurrentIndex">The current tickIndex for the Whirlpool to swap on.</param>
        /// <param name="tickSpacing">The tickSpacing for the Whirlpool.</param>
        /// <param name="aToB">The direction of the trade.</param>
        /// <param name="programId">The Whirlpool programId which the Whirlpool lives on.</param>
        /// <param name="whirlpoolAddress">PublicKey of the whirlpool to swap on.</param>
        /// <param name="fetcher">AccountFetcher object to fetch solana accounts</param>
        /// <param name="refresh">If true, fetcher would default to fetching the latest accounts</param>
        /// <returns>An array of PublicKey[] for the tickArray accounts that this swap may traverse across.</returns>
        public static async Task<IList<TickArrayContainer>> GetTickArrays(
            IWhirlpoolContext ctx,
            int tickCurrentIndex,
            ushort tickSpacing,
            bool aToB,
            PublicKey programId,
            PublicKey whirlpoolAddress
        )
        {
            //get addresses for which to return tickarrays 
            var addresses = SwapUtils.GetTickArrayPublicKeys(
                tickCurrentIndex, tickSpacing, aToB, programId, whirlpoolAddress
            ); 
            
            //get a tick array for each addresss
            var promises = new List<Tuple<PublicKey, Task<AccountResultWrapper<TickArray>>>>(); 
            foreach(PublicKey addr in addresses) 
            {
                promises.Add(Tuple.Create<PublicKey, Task<AccountResultWrapper<TickArray>>>(
                    addr, ctx.WhirlpoolClient.GetTickArrayAsync(addr.ToString())
                ));
            }
            
            //let all execute async
            await Task.WhenAll(promises.Select(t => t.Item2)); 
            
            //return each result with address 
            return promises.Select(t => new TickArrayContainer{
                Address = t.Item1,
                Data = t.Item2.Result.ParsedResult
            }).ToList();
        }

        /// <summary>
        /// Calculate the SwapInput parameters `amount` & `otherAmountThreshold` based on the amountIn & amountOut
        /// estimates from a quote.
        /// </summary>
        /// <param name="amount">The amount of tokens the user wanted to swap from.</param>
        /// <param name="estAmountIn">The estimated amount of input tokens expected in a `SwapQuote`</param>
        /// <param name="estAmountOut">The estimated amount of output tokens expected from a `SwapQuote`</param>
        /// <param name="slippageTolerance">The amount of slippage to adjust for.</param>
        /// <param name="amountSpecifiedIsInput">Specifies the token the parameter `amount`represents in the swap quote.
        /// If true, the amount represents the input token of the swap.</param>
        /// <returns></returns>
        public static Tuple<BigInteger, BigInteger> CalculateSwapAmountsFromQuote(
            BigInteger amount,
            BigInteger estAmountIn,
            BigInteger estAmountOut,
            Percentage slippageTolerance,
            bool amountSpecifiedIsInput
        )
        {
            if (amountSpecifiedIsInput)
            {
                return Tuple.Create(
                    amount,
                    TokenMath.AdjustForSlippage(estAmountOut, slippageTolerance, false)
                );
            }
            else
            {
                return Tuple.Create(
                    amount,
                    TokenMath.AdjustForSlippage(estAmountIn, slippageTolerance, true)
                );
            }
        }
    }
}
