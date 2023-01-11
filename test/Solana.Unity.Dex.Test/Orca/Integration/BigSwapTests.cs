using NUnit.Framework;

using System; 
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Test.Orca.Utils;

//TODO: (MID) handling for 'funder', 'feePayer', 'signer' is inconsistent. make it consistent

namespace Solana.Unity.Dex.Test.Orca.Integration
{
     
    [TestFixture]
    public class BigSwapTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }


        private static async Task DisplayTokenVaultBalances(PoolInitResult poolInitResult) 
        {
            Console.WriteLine((await _context.RpcClient.GetTokenAccountBalanceAsync(poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey.ToString(), Commitment.Processed)).Result.Value.Amount);
            Console.WriteLine((await _context.RpcClient.GetTokenAccountBalanceAsync(poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey.ToString(), Commitment.Processed)).Result.Value.Amount);
        }
        
        private static async Task DisplayOneTickArray(Pda tickArrayPda)
        {
            var tickArray = (await _context.WhirlpoolClient.GetTickArrayAsync(tickArrayPda.PublicKey.ToString(), Commitment.Processed)).ParsedResult;
            for (int n = 0; n < tickArray.Ticks.Count(); n++)
            {
                Tick tick = tickArray.Ticks[n];
                if (!tick.Initialized)
                    continue;

                Console.WriteLine($"{tickArray.StartTickIndex + n * TickSpacing.Stable}: {tick.FeeGrowthOutsideA.ToString()}, {tick.FeeGrowthOutsideB.ToString()}");
            }
        }
        
        private static async Task DisplayTickArrays(IList<Pda> tickArrays) 
        {
            await Task.WhenAll(
                tickArrays.Select(e => DisplayOneTickArray(e))
            );
        }
        
        private static async Task WithdrawOnePosition(
            FundedPositionInfo positionInfo,
            PublicKey tokenOwnerAccountA,
            PublicKey tokenOwnerAccountB
        ) 
        {
            Whirlpool pool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                positionInfo.OpenPositionParams.Accounts.Whirlpool, 
                Commitment.Processed
            )).ParsedResult;
            
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                positionInfo.OpenPositionParams.PositionPda.PublicKey.ToString(),
                Commitment.Processed
            )).ParsedResult;
            
            Assert.IsNotNull(pool);
            Assert.IsNotNull(position);

            BigInteger priceLower = PriceMath.TickIndexToSqrtPriceX64(position.TickLowerIndex);
            BigInteger priceUpper = PriceMath.TickIndexToSqrtPriceX64(position.TickUpperIndex);

            TokenAmounts tokenAmounts = PoolUtils.GetTokenAmountsFromLiquidity(
                position.Liquidity,
                pool.SqrtPrice,
                priceLower, 
                priceUpper,
                roundUp: false
            );
            
            int numTicksInTickArray = pool.TickSpacing * TickConstants.TICK_ARRAY_SIZE;
            int lowerStartTick =
                position.TickLowerIndex - (position.TickLowerIndex % numTicksInTickArray);
            Pda tickArrayLowerPda = PdaUtils.GetTickArray(
                _context.ProgramId,
                positionInfo.OpenPositionParams.Accounts.Whirlpool,
                lowerStartTick
            );
            int upperStartTick = position.TickUpperIndex - (position.TickUpperIndex % numTicksInTickArray);
            Pda tickArrayUpperPda = PdaUtils.GetTickArray(
                _context.ProgramId,
                positionInfo.OpenPositionParams.Accounts.Whirlpool,
                upperStartTick
            );
            
            var decreaseResult = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                new DecreaseLiquidityParams 
                {
                    Accounts = new DecreaseLiquidityAccounts 
                    {
                        Whirlpool = positionInfo.OpenPositionParams.Accounts.Whirlpool,
                        PositionAuthority = _context.WalletAccount,
                        Position = positionInfo.OpenPositionParams.PositionPda,
                        PositionTokenAccount = positionInfo.OpenPositionParams.Accounts.PositionTokenAccount,
                        TokenOwnerAccountA = tokenOwnerAccountA, 
                        TokenOwnerAccountB = tokenOwnerAccountB, 
                        TokenVaultA = pool.TokenVaultA, 
                        TokenVaultB = pool.TokenVaultB, 
                        TickArrayLower = tickArrayLowerPda, 
                        TickArrayUpper = tickArrayUpperPda, 
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    LiquidityAmount = position.Liquidity,
                    TokenMinA = (ulong)tokenAmounts.TokenA, 
                    TokenMinB = (ulong)tokenAmounts.TokenB, 
                    PositionAuthorityKeypair = _context.WalletAccount
                }
            ); 
            
            Assert.IsTrue(decreaseResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(decreaseResult.Result, _defaultCommitment));
            
            var collectResult = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = positionInfo.OpenPositionParams.Accounts.Whirlpool,
                        PositionAuthority = _context.WalletAccount,
                        Position = positionInfo.OpenPositionParams.PositionPda,
                        PositionTokenAccount = positionInfo.OpenPositionParams.Accounts.PositionTokenAccount,
                        TokenOwnerAccountA = tokenOwnerAccountA,
                        TokenOwnerAccountB = tokenOwnerAccountB,
                        TokenVaultA = pool.TokenVaultA,
                        TokenVaultB = pool.TokenVaultB,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }, 
                    PositionAuthorityKeypair = _context.WalletAccount 
                }
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }
        
        private static async Task WithdrawPositions(
            IList<FundedPositionInfo> positionInfos, 
            PublicKey tokenOwnerAccountA,
            PublicKey tokenOwnerAccountB
        ) 
        {
            await Task.WhenAll(
                positionInfos.Select(p => WithdrawOnePosition(p, tokenOwnerAccountA, tokenOwnerAccountB))
            );
        }
        
        [Test] 
        [Description("swaps across ten tick arrays")]
        public static async Task SwapsAcrossTenTickArrays()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Stable,
                initSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(27500)
            );
            
            Assert.IsTrue(poolInitResult.WasSuccessful);

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            // tick array range: 27658 to 29386
            // tick arrays: (27456, 28152), (28160, 28856), (28864, 29,560)
            // current tick: 27727
            // initialized ticks:
            //   27712, 27736, 27840, 28288, 28296, 28304, 28416, 28576, 28736, 29112, 29120, 29240, 29360
            IList<Pda> tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                27456, // to 30528
                3,
                TickSpacing.Stable,
                aToB: false
            );

            FundedPositionParams[] fundParams = new FundedPositionParams[] {
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 27712,
                    TickUpperIndex = 29360
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 27736,
                    TickUpperIndex = 29240
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 27840,
                    TickUpperIndex = 29120
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 28288,
                    TickUpperIndex = 29112
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 28416,
                    TickUpperIndex = 29112
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 28288,
                    TickUpperIndex = 28304
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 28296,
                    TickUpperIndex = 29112
                },
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 28576,
                    TickUpperIndex = 28736
                }
            };

            var positionInfos = await PositionTestUtils.FundPositionsAsync(
                _context, poolInitResult, fundParams
            );

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays); 

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda.PublicKey);

            // ----- SWAP 1 ------------------------------------------------------------------------------------------
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[0],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[2],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 829996,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(29240),
                    AmountSpecifiedIsInput = false,
                    AtoB = false
                }
            );

            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            // ----- SWAP 2 ------------------------------------------------------------------------------------------
            swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[2],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[0],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 14538074,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(27712),
                    AmountSpecifiedIsInput = false,
                    AtoB = true
                }
            );
            
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            // ----- SWAP 3 ------------------------------------------------------------------------------------------
            swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[0],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[2],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 829996,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(29240),
                    AmountSpecifiedIsInput = false,
                    AtoB = false
                }
            );

            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            // ----- SWAP 4 ------------------------------------------------------------------------------------------
            swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[2],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[0],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 14538074,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(27712),
                    AmountSpecifiedIsInput = false,
                    AtoB = true
                }
            );

            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            // ----- SWAP 5 ------------------------------------------------------------------------------------------
            swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[0],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[2],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 829996,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(29240),
                    AmountSpecifiedIsInput = false,
                    AtoB = false
                }
            );

            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            // ----- SWAP 6 ------------------------------------------------------------------------------------------
            swapResult = await SwapTestUtils.SwapAsync(
                _context,
                new SwapParams
                {
                    Accounts = new SwapAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        TokenAuthority = _context.WalletPubKey,
                        TokenOwnerAccountA = poolInitResult.TokenAccountA,
                        TokenVaultA = poolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenOwnerAccountB = poolInitResult.TokenAccountB,
                        TokenVaultB = poolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TickArray0 = tickArrays[2],
                        TickArray1 = tickArrays[1],
                        TickArray2 = tickArrays[0],
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    Amount = 14538074,
                    OtherThresholdAmount = ArithmeticUtils.MaxU64,
                    SqrtPriceLimit = PriceMath.TickIndexToSqrtPriceX64(27712),
                    AmountSpecifiedIsInput = false,
                    AtoB = true
                }
            );
            
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            await WithdrawPositions(positionInfos, poolInitResult.TokenAccountA, poolInitResult.TokenAccountB);

            await DisplayTokenVaultBalances(poolInitResult);
            await DisplayTickArrays(tickArrays);

            //collect protocol fees 
            //TODO: (MID) implement collect protocol fees 

            await DisplayTokenVaultBalances(poolInitResult);
        }
    }
}