using System;
using System.Numerics;

using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Accounts;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class TestDataUtils
    {
        public static Whirlpool TestWhirlpool => new Whirlpool
        {
            WhirlpoolsConfig = new Account().PublicKey,
            FeeRate = 300,
            ProtocolFeeRate = 1800,
            Liquidity = BigInteger.Parse("32523523532"),
            SqrtPrice = BigInteger.Parse("32523523532"),
            TickCurrentIndex = PriceMath.SqrtPriceX64ToTickIndex(BigInteger.Parse("32523523532")),
            ProtocolFeeOwedA = 2314532532,
            ProtocolFeeOwedB = 2314532532,
            TokenMintA = new Account().PublicKey,
            TokenVaultA = new Account().PublicKey,
            TokenMintB = new Account().PublicKey,
            TokenVaultB = new Account().PublicKey,
            FeeGrowthGlobalA = 32532523523523523,
            FeeGrowthGlobalB = 32532523523523523,
            RewardLastUpdatedTimestamp = 3253252312412235235,
            TickSpacing = 64
        };

        public static Tick TestInitializedTickData => new Tick
        {
            FeeGrowthOutsideA = BigInteger.Zero,
            FeeGrowthOutsideB = BigInteger.Zero,
            LiquidityGross = BigInteger.Zero,
            LiquidityNet = BigInteger.Zero,
            Initialized = true,
            RewardGrowthsOutside = new BigInteger[] { BigInteger.Zero, BigInteger.Zero }
        };

        public static Tick TestUninitializedTickData => new Tick
        {
            FeeGrowthOutsideA = BigInteger.Zero,
            FeeGrowthOutsideB = BigInteger.Zero,
            LiquidityGross = BigInteger.Zero,
            LiquidityNet = BigInteger.Zero,
            Initialized = false,
            RewardGrowthsOutside = new BigInteger[] { BigInteger.Zero, BigInteger.Zero }
        };

        public static TickArrayContainer TestEmptyTickArray => new TickArrayContainer
        {
            Address = AddressConstants.DEFAULT_PUBLIC_KEY,
            Data = null
        };

        public static TickArrayContainer BuildTickArrayData(int startTick, int[] initializedOffsets)
        {
            TickArray data = new TickArray()
            {
                StartTickIndex = startTick,
                Whirlpool = AddressConstants.DEFAULT_PUBLIC_KEY,
                Ticks = new Tick[TickConstants.TICK_ARRAY_SIZE]
            };

            for (int n = 0; n < data.Ticks.Length; n++)
            {
                data.Ticks[n] = TestDataUtils.TestUninitializedTickData;
            }

            foreach (int offset in initializedOffsets)
            {
                if (offset >= TickConstants.TICK_ARRAY_SIZE)
                {
                    throw new Exception($"Cannot build tick-array with initialized offset - ${offset}");
                }
                data.Ticks[offset] = TestDataUtils.TestInitializedTickData;
            }

            PublicKey randomAddr = new Account().PublicKey;

            return new TickArrayContainer
            {
                Address = randomAddr,
                Data = data
            };
        }
    }
}
