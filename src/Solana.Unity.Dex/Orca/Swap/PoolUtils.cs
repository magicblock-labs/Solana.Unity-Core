using Solana.Unity.Dex.Math;
using System;
using System.Numerics;
using System.Collections.Generic;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Swap;
using BigDecimal = Solana.Unity.Dex.Orca.Math.BigDecimal;

namespace Solana.Unity.Dex.Orca.Swap
{
    public static class PoolUtils
    {
        public static bool IsRewardInitialized(WhirlpoolRewardInfo rewardInfo)
        {
            return 
                !AddressConstants.DEFAULT_PUBLIC_KEY.Equals(rewardInfo.Mint) && 
                !AddressConstants.DEFAULT_PUBLIC_KEY.Equals(rewardInfo.Vault);
        }

        /// <summary>
        /// These are the token mints that will be prioritized as the second token in the pair (quote).
        /// The number that the mint maps to determines the priority that it will be used as the quote currency.
        /// </summary>
        public static readonly Dictionary<string, int> QuoteTokens = new()
        {
            { "Es9vMFrzaCERmJfrF4H2FYD4KCoNkY11McCe8BenwNYB", 100},
            { "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", 90},
            { "USDH1SM1ojwWUga67PGrgFWUHibbjqMvuMaDkRJTgkX", 80},
            { AddressConstants.NATIVE_MINT, 70},
            { "mSoLzYCxHdYgdzU16g5QSh3i5K3z3KZK7ytfqcJm7So", 60},
            { "7dHbWXmci3dT8UFYWYZweBLXgycu7Y3iL6trKn1Y7ARj", 50},
        };

        public const int DefaultQuotePriority = 0;
        
        public static TokenType GetTokenType(Whirlpool pool, PublicKey mint)
        {
            if (pool.TokenMintA.Equals(mint))
            {
                return TokenType.TokenA;
            }
            else if (pool.TokenMintB.Equals(mint))
            {
                return TokenType.TokenB;
            }

            return TokenType.None;
        }

        public static Percentage GetFeeRate(long feeRate)
        {
            /*
             * Smart Contract comment: https://github.com/orca-so/whirlpool/blob/main/programs/whirlpool/src/state/whirlpool.rs#L9-L11
             * // Stored as hundredths of a basis point
             * // u16::MAX corresponds to ~6.5%
             * pub fee_rate: u16,
             */
            return Percentage.FromFraction(feeRate, (long)1e6); 
        }
        
        public static Percentage GetProtocolFeeRate(long protocolFeeRate)
        {
            /*
             * Smart Contract comment: https://github.com/orca-so/whirlpool/blob/main/programs/whirlpool/src/state/whirlpool.rs#L13-L14
             * // Stored as a basis point
             * pub protocol_fee_rate: u16,
             */
            return Percentage.FromFraction(protocolFeeRate, (long)1e4); 
        }
        
        public static BigInteger EstimateLiquidityForTokenA(
            BigInteger sqrtPrice1,
            BigInteger sqrtPrice2,
            BigInteger tokenAmount
        )
        {
            BigInteger lowerSqrtPriceX64 = BigInteger.Min(sqrtPrice1, sqrtPrice2);
            BigInteger upperSqrtPriceX64 = BigInteger.Max(sqrtPrice1, sqrtPrice2);
            
            BigInteger num = ArithmeticUtils.BigIntFromX64(tokenAmount * upperSqrtPriceX64 * lowerSqrtPriceX64);
            BigInteger dem = upperSqrtPriceX64 - lowerSqrtPriceX64;

            return num / dem;
        }

        public static BigInteger EstimateLiquidityForTokenB(
            BigInteger sqrtPrice1,
            BigInteger sqrtPrice2,
            BigInteger tokenAmount
        )
        {
            BigInteger lowerSqrtPriceX64 = BigInteger.Min(sqrtPrice1, sqrtPrice2);
            BigInteger upperSqrtPriceX64 = BigInteger.Max(sqrtPrice1, sqrtPrice2);

            BigInteger delta = upperSqrtPriceX64 - lowerSqrtPriceX64;

            return (tokenAmount << 64) / delta;
        }

        public static BigInteger EstimateLiquidityFromTokenAmounts(
            int currentTick,
            int lowerTick,
            int upperTick, 
            TokenAmounts tokenAmounts
        ) 
        {
            if (upperTick < lowerTick)
                throw new ArgumentException("Upper tick shouldn't be lower than lower tick");

            BigInteger currSqrtPrice = PriceMath.TickIndexToSqrtPriceX64(currentTick);
            BigInteger lowerSqrtPrice = PriceMath.TickIndexToSqrtPriceX64(lowerTick);
            BigInteger upperSqrtPrice = PriceMath.TickIndexToSqrtPriceX64(upperTick);
            
            if (currentTick >= upperTick) 
            {
                return EstimateLiquidityForTokenB(upperSqrtPrice, lowerSqrtPrice, tokenAmounts.TokenB);
            }
            else if (currentTick < lowerTick)
            {
                return EstimateLiquidityForTokenA(lowerSqrtPrice, upperSqrtPrice, tokenAmounts.TokenA);
            }
            else
            {
                BigInteger estLiquidityAmountA = 
                    EstimateLiquidityForTokenA(currSqrtPrice, upperSqrtPrice, tokenAmounts.TokenA);
                BigInteger estLiquidityAmountB = 
                    EstimateLiquidityForTokenB(currSqrtPrice, lowerSqrtPrice, tokenAmounts.TokenB);
                return BigInteger.Min(estLiquidityAmountA, estLiquidityAmountB);
            }
        }
        
        /// <summary>
        /// Given an arbitrary pair of token mints, this function returns an ordering of the token mints
        /// in the format [base, quote]. USD based stable coins are prioritized as the quote currency
        /// followed by variants of SOL.
        /// </summary>
        /// <param name="tokenMintAKey">The mint of token A in the token pair.</param>
        /// <param name="tokenMintBKey">The mint of token B in the token pair.</param>
        /// <returns>A two-element array with the tokens sorted in the order of [baseToken, quoteToken].</returns>
        public static System.Tuple<PublicKey, PublicKey> ToBaseQuoteOrder(
            PublicKey tokenMintAKey,
            PublicKey tokenMintBKey
        )
        {
            return (GetQuoteTokenPriority(tokenMintAKey.ToString()) > 
                    GetQuoteTokenPriority(tokenMintBKey.ToString())) 
                ? Tuple.Create(tokenMintBKey, tokenMintAKey)
                : Tuple.Create(tokenMintAKey, tokenMintBKey);
        }

        public static int GetQuoteTokenPriority(string mint)
        {
            return (QuoteTokens.ContainsKey(mint)) ? QuoteTokens[mint] : DefaultQuotePriority;
        }
    
        public static TokenAmounts GetTokenAmountsFromLiquidity(
            BigInteger liquidity,
            BigInteger currentSqrtPrice,
            BigInteger lowerSqrtPrice,
            BigInteger upperSqrtPrice,
            bool roundUp
        )
        {
            BigDecimal dLiquidity = new BigDecimal(liquidity);
            BigDecimal dCurrentSqrtPrice = new BigDecimal(currentSqrtPrice);
            BigDecimal dLowerSqrtPrice = new BigDecimal(lowerSqrtPrice);
            BigDecimal dUpperSqrtPrice = new BigDecimal(upperSqrtPrice);

            BigDecimal tokenA = BigDecimal.Zero;
            BigDecimal tokenB = BigDecimal.Zero;
            if (currentSqrtPrice < lowerSqrtPrice)
            {
                tokenA = ArithmeticUtils.DecimalToX64(dLiquidity) *
                         (dUpperSqrtPrice - dLowerSqrtPrice) /
                         (dLowerSqrtPrice * dUpperSqrtPrice);
                tokenB = BigDecimal.Zero;
            }
            else if (currentSqrtPrice < upperSqrtPrice)
            {
                // x = L * (pb - p) / (p * pb)
                // y = L * (p - pa)
                tokenA = ArithmeticUtils.DecimalToX64(dLiquidity) *
                         (dUpperSqrtPrice - dCurrentSqrtPrice) /
                         (dCurrentSqrtPrice * dUpperSqrtPrice);
                tokenB = ArithmeticUtils.DecimalFromX64(dLiquidity * (dCurrentSqrtPrice - dLowerSqrtPrice));
            }
            else
            {
                // y = L * (pb - pa)
                tokenA = BigDecimal.Zero;
                tokenB = ArithmeticUtils.DecimalFromX64(dLiquidity * (dUpperSqrtPrice - dLowerSqrtPrice));
            }

            return roundUp
                ? new TokenAmounts
                {
                    TokenA = BigInteger.Parse(tokenA.Ceiling().ToString()),
                    TokenB = BigInteger.Parse(tokenB.Ceiling().ToString())
                }
                : new TokenAmounts
                {
                    TokenA = BigInteger.Parse(tokenA.Floor().ToString()),
                    TokenB = BigInteger.Parse(tokenB.Floor().ToString())
                };
        }
    }
}
