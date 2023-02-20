using System;
using System.Linq; 
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Ticks;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public class WhirlpoolsTestFixture
    {
        private TestWhirlpoolContext _context;
        
        private InitializePoolParams _initPoolParams = new()
        {
            Accounts = new InitializePoolAccounts
            {
                WhirlpoolsConfig = AddressConstants.DEFAULT_PUBLIC_KEY,
                TokenMintA = AddressConstants.DEFAULT_PUBLIC_KEY,
                TokenMintB = AddressConstants.DEFAULT_PUBLIC_KEY,
                FeeTier = AddressConstants.DEFAULT_PUBLIC_KEY,
                Funder = new Account()
            },
            InitSqrtPrice = BigInteger.Zero,
            WhirlpoolPda = new Pda(AddressConstants.DEFAULT_PUBLIC_KEY, 0),
            TokenVaultAKeyPair = new Account(),
            TokenVaultBKeyPair = new Account(),
            TickSpacing = TickSpacing.HundredTwentyEight,
        };
        private InitializeConfigParams _initConfigParams = new InitializeConfigParams
        {
            Accounts = new InitializeConfigAccounts
            {
                Config = new Account(),
                Funder = new Account(),
            },
            DefaultProtocolFeeRate = 0,
            FeeAuthorityKeypair = new Account(),
            CollectProtocolFeesAuthority = AddressConstants.DEFAULT_PUBLIC_KEY,
            RewardEmissionsSuperAuthority = AddressConstants.DEFAULT_PUBLIC_KEY
        };
        private WhirlpoolsConfigKeyPairs _configKeyPairs;
        private FundedPositionInfo[] _positions = Array.Empty<FundedPositionInfo>(); 
        private InitializedRewardInfo[] _rewards = Array.Empty<InitializedRewardInfo>();
        private PublicKey _tokenAccountA = AddressConstants.DEFAULT_PUBLIC_KEY;
        private PublicKey _tokenAccountB = AddressConstants.DEFAULT_PUBLIC_KEY;
        private PoolInitResult _poolInitResult; 
        private bool _initialized; 

        private WhirlpoolsTestFixture(TestWhirlpoolContext ctx) 
        {
            _context = ctx;
        }
        
        public static async Task<WhirlpoolsTestFixture> CreateInstance(
            TestWhirlpoolContext ctx,
            ushort tickSpacing = TickSpacing.HundredTwentyEight, 
            BigInteger? initialSqrtPrice = null,
            FundedPositionParams[] positions = null, 
            RewardParams[] rewards = null,
            bool tokenAIsNative = false
        )
        {
            WhirlpoolsTestFixture output = new(ctx);
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(output._context); 

            PoolInitResult result = await PoolTestUtils.BuildPoolWithTokens(
                output._context, 
                initConfigParams,
                tickSpacing,
                InitializeFeeTierParams.DefaultDefaultFeeRate,
                initialSqrtPrice,
                tokenAIsNative: tokenAIsNative
            );
            
            output._initPoolParams = result.InitPoolParams;
            output._initConfigParams = result.InitConfigParams;
            output._tokenAccountA = result.TokenAccountA;
            output._tokenAccountB = result.TokenAccountB;
            output._configKeyPairs = result.InitConfigParams.ConfigKeyPairs;
            output._poolInitResult = result;
            
            //if funded positions params were passed 
            if (positions != null && positions.Length > 0) 
            {
                await InitializeTickArrays(output._context, output._initPoolParams, positions);

                output._positions = (await PositionTestUtils.FundPositionsAsync(
                    ctx,
                    output._initPoolParams,
                    positions
                )).ToArray();
            }

            //if rewards params were passed 
            if (rewards is { Length: > 0 })
            {
                List<InitializedRewardInfo> initRewards = new();
                for (int i = 0; i < rewards.Length; i++)
                {
                    initRewards.Add(
                        (await FeesAndRewardsTestUtils.InitializeRewardsSetEmissions(
                            ctx, 
                            output._configKeyPairs.RewardEmissionsSuperAuthorityKeyPair,
                            output._initPoolParams.WhirlpoolPda,
                            (byte)i, 
                            rewards[i].VaultAmount,
                            rewards[i].EmissionsPerSecondX64
                        )).Item1
                    );
                }
                output._rewards = initRewards.ToArray();
            }
            
            output._initialized = true;
            return output;
        }

        public TestFixtureInfo GetTestInfo() 
        {
            //must be initialized 
            if (!_initialized) 
                throw new Exception("Test fixture is not initialized");
                
            return new TestFixtureInfo
            {
                PoolInitResult = _poolInitResult,
                InitPoolParams = _initPoolParams,
                InitConfigParams = _initConfigParams,
                ConfigKeyPairs = _configKeyPairs,
                TokenAccountA = _tokenAccountA,
                TokenAccountB = _tokenAccountB,
                Positions = _positions,
                Rewards = _rewards
            };
        }


        private static async Task InitializeTickArrays(
          TestWhirlpoolContext ctx,
          InitializePoolParams initPoolParams,
          FundedPositionParams[]  positions
        )
        {
            List<int> startTickSet = new();
            foreach(FundedPositionParams p in positions)
            {
                int startLower = TickUtils.GetStartTickIndex(p.TickLowerIndex, initPoolParams.TickSpacing);
                int startUpper = TickUtils.GetStartTickIndex(p.TickUpperIndex, initPoolParams.TickSpacing); 
                if (!startTickSet.Contains(startLower))
                    startTickSet.Add(startLower);
                if (!startTickSet.Contains(startUpper))
                    startTickSet.Add(startUpper);
            }

            await Task.WhenAll(
                startTickSet.Select(t => TickArrayTestUtils.InitializeTickArrayAsync(
                    ctx,
                    initPoolParams.WhirlpoolPda.PublicKey,
                    t
                ))
            );
        }
        
        public class TestFixtureInfo
        {
            public PoolInitResult PoolInitResult { get; set; }
            public InitializePoolParams InitPoolParams { get; set; }
            public InitializeConfigParams InitConfigParams { get; set; }
            public WhirlpoolsConfigKeyPairs ConfigKeyPairs { get; set; }
            public PublicKey TokenAccountA { get; set; }
            public PublicKey TokenAccountB { get; set; }
            public FundedPositionInfo[] Positions { get; set; }
            public InitializedRewardInfo[] Rewards { get; set; }
        }
    }
}