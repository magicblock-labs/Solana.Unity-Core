using NUnit.Framework;
using Solana.Unity.Dex.Math;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Quotes.Swap;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Core.Errors; 
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Quotes;
using BigDecimal = Solana.Unity.Dex.Orca.Math.BigDecimal;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Types;


namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class SwapTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        [Test]
        [Description("swaps across one tick array")]
        public static async Task SwapsAcrossOneTickArray()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );
            
            Assert.IsTrue(poolInitResult.WasSuccessful);

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528, // to 33792
                arrayCount: 3,
                aToB: false
            );

            FundedPositionParams[] fundParams = {
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult: poolInitResult,
                fundParams: fundParams
            );

            TokenBalance tokenVaultABefore =
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    _defaultCommitment
                )).Result.Value;
            TokenBalance tokenVaultBBefore =
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    _defaultCommitment
                )).Result.Value;

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                poolInitResult.InitPoolParams.WhirlpoolPda.PublicKey.ToString()
            )).ParsedResult;

            //SwapQuoteByInputToken
            SwapQuote swapQuote = await SwapQuoteUtils.SwapQuoteByInputToken(
                _context,
                whirlpool, 
                whirlpoolAddress: whirlpoolPda,
                inputTokenMint: whirlpool.TokenMintB,
                tokenAmount: 100000,
                slippageTolerance: Percentage.FromFraction(1, 100),
                programId: _context.ProgramId
            ); 
            
            //do swap 
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
                        TickArray0 = swapQuote.TickArray0,
                        TickArray1 = swapQuote.TickArray1,
                        TickArray2 = swapQuote.TickArray2,
                        Oracle = oraclePda,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }, 
                    Amount = swapQuote.Amount,
                    OtherThresholdAmount = swapQuote.OtherAmountThreshold,
                    SqrtPriceLimit = swapQuote.SqrtPriceLimit,
                    AmountSpecifiedIsInput = swapQuote.AmountSpecifiedIsInput,
                    AtoB = swapQuote.AtoB
                }
            );
            
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            TokenBalance tokenVaultAAfter =
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    _defaultCommitment
                )).Result.Value;
            TokenBalance tokenVaultBAfter =
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    _defaultCommitment
                )).Result.Value;

            Assert.That(BigInteger.Parse(tokenVaultBAfter.Amount), Is.EqualTo(
                BigInteger.Parse(tokenVaultBBefore.Amount) + swapQuote.EstimatedAmountIn)
            );
            
            Assert.IsTrue(IsWithinRoundingRange(
                BigInteger.Parse(tokenVaultAAfter.Amount),
                BigInteger.Parse(tokenVaultABefore.Amount) - swapQuote.EstimatedAmountOut)
            );
        }

        [Test] 
        [Description("swaps across three tick arrays")]
        public static async Task SwapsAcrossThreeTickArrays()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Stable,
                initSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(27500)
            );
            
            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 27456,
                arrayCount: 5, 
                tickSpacing: TickSpacing.Stable
            );

            //generate fund params 
            FundedPositionParams[] fundParams = new FundedPositionParams[]{
                new()
                {
                    LiquidityAmount = 100_000_000,
                    TickLowerIndex = 27456,
                    TickUpperIndex = 27840
                },
                new()
                {
                    LiquidityAmount = 100_000_000,
                    TickLowerIndex = 28864,
                    TickUpperIndex = 28928
                },
                new()
                {
                    LiquidityAmount = 100_000_000,
                    TickLowerIndex = 27712,
                    TickUpperIndex = 28928,
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult.InitPoolParams,
                poolInitResult.TokenAccountA,
                poolInitResult.TokenAccountB,
                fundParams
            );

            //token balances before 
            string tokenBalanceBeforeA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceBeforeB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;
            
            Assert.That(tokenBalanceBeforeA, Is.EqualTo("1977429"));
            Assert.That(tokenBalanceBeforeB, Is.EqualTo("869058"));
            
            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[1].PublicKey, tickArrays[2].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 7051000,
                amountSpecifiedIsInput: true,
                aToB: false,
                sqrtPriceLimit: PriceMath.TickIndexToSqrtPriceX64(28500)
            );
            swapParams.Accounts.TokenAuthority = _context.WalletAccount;

            //fails with ConstraintAddress
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            //token balances after
            string tokenBalanceAfterA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceAfterB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            Assert.That(tokenBalanceAfterA, Is.EqualTo("1535201"));
            Assert.That(tokenBalanceAfterB, Is.EqualTo("7920058"));
        }
        
        [Test] 
        [Description("fail on token vault mint A does not match whirlpool token A")]
        public static async Task FailsTokenVaultMintAMismatch()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context, 
                ConfigTestUtils.GenerateParams( _context),
                TickSpacing.Standard
            );

            //initialize another test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );
            
            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;
            
            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context, 
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            ); 
            
            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );
            swapParams.Accounts.TokenVaultA = anotherPoolInitResult.InitPoolParams.TokenVaultAKeyPair;

            //fails with ConstraintAddress
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);

            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.AddressConstraint);
        }

        [Test] 
        [Description("fail on token vault mint B does not match whirlpool token B")]
        public static async Task FailsTokenVaultMintBMismatch() 
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );

            //initialize another test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );
            swapParams.Accounts.TokenVaultB = anotherPoolInitResult.InitPoolParams.TokenVaultBKeyPair;

            //fails with ConstraintAddress
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);

            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.AddressConstraint);
        }

        [Test] 
        [Description("fail on token owner account A does not match vault A mint")]
        public static async Task FailsTokenOwnerAMismatch()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );

            //initialize another test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Stable
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                        tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );

            swapParams.Accounts.TokenOwnerAccountA = anotherPoolInitResult.TokenAccountA;

            //fails with ConstraintAddress
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fail on token owner account B does not match vault B mint")]
        public static async Task FailsTokenOwnerBMismatch()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );

            //initialize another test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Stable
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );

            swapParams.Accounts.TokenOwnerAccountB = anotherPoolInitResult.TokenAccountB;

            //fails with ConstraintAddress
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.RawConstraint);
        }

        [Test]   
        [Description("fails to swap with incorrect token authority")]
        public static async Task FailsIncorrectTokenAuthority()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            Account otherTokenAuthority = new Account();
            await SolUtils.FundTestAccountAsync(_context, otherTokenAuthority);

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                amountSpecifiedIsInput: true,
                aToB: true,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );
            swapParams.Accounts.TokenAuthority = otherTokenAuthority.PublicKey;

            //fails with OwnerMismatch /0x4/ 
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                swapParams,
                feePayer: otherTokenAuthority
            );
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.OwnerMismatch);
        }

        [Test] 
        [Description("fails on passing in the wrong tick-array")]
        public static async Task FailsWrongTickArray()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard,
                initSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(new BigDecimal(0.0242).Sqrt())
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context, 
                poolInitResult, 
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda, 
                amount: 10, 
                sqrtPriceLimit: PriceMath.TickIndexToSqrtPriceX64(-50000)
            ); 
                
            //fails with InvalidTickArraySequence
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams); 
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.InvalidTickArraySequence);
        }

        [Test] 
        [Description("fails on passing in the wrong whirlpool")]
        public static async Task FailsWrongWhirlpool()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );
            
            //initialize test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );
            
            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: anotherPoolInitResult.InitPoolParams.WhirlpoolPda,
                tickArrays: new PublicKey[]{
                        tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );
            swapParams.Accounts.Whirlpool = anotherPoolInitResult.InitPoolParams.WhirlpoolPda;

            //fails with ConstraintRaw
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails on passing in the tick-arrays from another whirlpool")]
        public static async Task FailsTickArraysFromWrongWhirlpool()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            //initialize test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: anotherPoolInitResult.InitPoolParams.WhirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: anotherPoolInitResult.InitPoolParams.WhirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );
            swapParams.Accounts.Whirlpool = anotherPoolInitResult.InitPoolParams.WhirlpoolPda;

            //fails with ConstraintRaw
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails on passing in an account of another type for the oracle")]
        public static async Task FailsWrongOracleAccountType()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: tickArrays[0].PublicKey,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );

            //fails with ConstraintSeeds 0x7d6
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.SeedsConstraint);
        }

        [Test] 
        [Description("fails on passing in an incorrectly hashed oracle PDA")]
        public static async Task FailsIncorrectlyHashedPda()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            //initialize test pool 
            PoolInitResult anotherPoolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //get a different oracle address
            Pda anotherOraclePda = PdaUtils.GetOracle(
                _context.ProgramId, anotherPoolInitResult.InitPoolParams.WhirlpoolPda
            );

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: anotherOraclePda,
                amount: 10,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            );

            //fails with ConstraintSeeds 0x7d6
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.SeedsConstraint);
        }

        [Test] 
        [Description("fail on passing in zero tradable amount")]
        public static async Task FailsZeroTradeableAmount()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //generate swap params 
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context, 
                poolInitResult, 
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                },
                oracleAddress: oraclePda, 
                amount: 0, 
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
            ); 

            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);
            
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.ZeroTradableAmount);
        }

        [Test] 
        [Description("Error on passing in uninitialized tick-array")]
        public static async Task FailsUninitializedTickarray()
        {
            //initialize test pool 
            PoolInitWithLiquidityResult poolInitResult = await PoolTestUtils.BuildPoolWithLiquidity(
                _context
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;
            
            var uninitializedTickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, whirlpoolPda, 0
            );

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //fails with AccountOwnedByWrongProgram
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolAddress: whirlpoolPda,
                    tickArrays: new PublicKey[]{
                        poolInitResult.TickArrays[0],
                        uninitializedTickArrayPda,
                        poolInitResult.TickArrays[2],
                    },
                    oracleAddress: oraclePda,
                    amount: 10,
                    sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4294886578)
                )
            );
            
            AssertUtils.AssertFailedWithStandardError(swapResult, StandardErrorType.AccountOwnedByWrongProgram);
        }

        [Test] 
        [Description("Error if sqrt_price_limit exceeds max")]
        public static async Task FailsSqrtPriceExceedsMax()
        {
            //initialize test pool 
            PoolInitWithLiquidityResult poolInitResult = await PoolTestUtils.BuildPoolWithLiquidity(
                _context
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //fails with SqrtPriceOutOfBounds
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolAddress: whirlpoolPda,
                    tickArrays: poolInitResult.TickArrays.Select(t => t.PublicKey).ToArray(),
                    oracleAddress: oraclePda,
                    amount: 10,
                    sqrtPriceLimit: BigInteger.Parse(MathConstants.MAX_SQRT_PRICE) + BigInteger.One
                )
            );
            
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.SqrtPriceOutOfBounds);
        }

        [Test] 
        [Description("Error if sqrt_price_limit subceed min")]
        public static async Task FailsSqrtPriceSubceedsMin()
        {
            //initialize test pool 
            PoolInitWithLiquidityResult poolInitResult = await PoolTestUtils.BuildPoolWithLiquidity(
                _context
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //get oracle address
            Pda oraclePda = PdaUtils.GetOracle(
                _context.ProgramId, whirlpoolPda
            );

            //fails with SqrtPriceOutOfBounds
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolAddress: whirlpoolPda,
                    tickArrays: poolInitResult.TickArrays.Select(t => t.PublicKey).ToArray(),
                    oracleAddress: oraclePda,
                    amount: 10,
                    sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4294886578)
                )
            );

            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.SqrtPriceOutOfBounds);
        }

        [Test] 
        [Description("Error if a to b swap below minimum output")]
        public static async Task FailsBelowMinOutputAtoB()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3,
                aToB: false
            );

            //generate fund params 
            FundedPositionParams[] fundParams = new FundedPositionParams[]{
                new FundedPositionParams{
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context, 
                poolInitResult.InitPoolParams, 
                poolInitResult.TokenAccountA,
                poolInitResult.TokenAccountB,
                fundParams
            );
            
            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //do swap: error AmountOutBelowMinimum
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolPda,
                    tickArrays: new PublicKey[] { 
                        tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey 
                    },
                    oracleAddress: oraclePda,
                    amount: 10,
                    amountSpecifiedIsInput: true,
                    aToB: true,
                    sqrtPriceLimit: BigInteger.Parse(MathConstants.MIN_SQRT_PRICE),
                    otherThresholdAmount: ArithmeticUtils.MaxU64
                )
            );

            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.AmountOutBelowMinimum);
        }

        [Test] 
        [Description("Error if b to a swap below minimum output")]
        public static async Task FailsBelowMinOutputBtoA()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //fund params 
            FundedPositionParams[] fundParams = new FundedPositionParams[]{
                new FundedPositionParams{
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult.InitPoolParams,
                poolInitResult.TokenAccountA,
                poolInitResult.TokenAccountB,
                fundParams
            );

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //do swap: throws error AmountOutBelowMinimum
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolPda,
                    tickArrays.Select(t => t.PublicKey).ToArray(),
                    oraclePda,
                    amount: 10,
                    amountSpecifiedIsInput: true,
                    aToB: false,
                    sqrtPriceLimit: BigInteger.Parse(MathConstants.MAX_SQRT_PRICE),
                    otherThresholdAmount: Int64.MaxValue
                )
            );
                
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.AmountOutBelowMinimum);
        }
        
        [Test] 
        [Description("Error if a to b swap above maximum input")]
        public static async Task FailsAboveMaxInputAtoB()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3
            );

            //fund params 
            FundedPositionParams[] fundParams = new FundedPositionParams[]{
                new FundedPositionParams{
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult.InitPoolParams,
                poolInitResult.TokenAccountA,
                poolInitResult.TokenAccountB,
                fundParams
            );

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //do swap: throws error AmountInAboveMaximum
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolPda,
                    tickArrays.Select(t => t.PublicKey).ToArray(),
                    oraclePda,
                    amount: 10,
                    amountSpecifiedIsInput: false,
                    aToB: true,
                    sqrtPriceLimit: BigInteger.Parse(MathConstants.MIN_SQRT_PRICE)
                )
            );
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.AmountInAboveMaximum);
        }

        [Test] 
        [Description("Error if b to a swap above maximum input")]
        public static async Task FailsAboveMaxInputBtoA()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            var tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3,
                aToB: false
            );

            //fund params 
            FundedPositionParams[] fundParams = new FundedPositionParams[]{
                new FundedPositionParams{
                    LiquidityAmount = 100_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult.InitPoolParams,
                poolInitResult.TokenAccountA,
                poolInitResult.TokenAccountB,
                fundParams
            );

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //do swap: fails with AmountInAboveMaximum
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    poolInitResult,
                    whirlpoolPda,
                    tickArrays: new PublicKey[]{
                        tickArrays[0].PublicKey, tickArrays[0].PublicKey, tickArrays[0].PublicKey
                    },
                    oracleAddress: oraclePda,
                    amount: 10,
                    amountSpecifiedIsInput: false,
                    aToB: false,
                    sqrtPriceLimit: BigInteger.Parse(MathConstants.MAX_SQRT_PRICE)
                )
            );
            AssertUtils.AssertFailedWithCustomError(swapResult, WhirlpoolErrorType.AmountInAboveMaximum);
        }
        
        //TODO: (HIGH) this is necessary because of rounding differences (or possibly errors) in the math, 
        //that can create a discrepancy of +1 or -1 from expected value. (might be already fixed)
        private static bool IsWithinRoundingRange(BigInteger a, BigInteger b) 
        {
            return (a == b) || (a == (b-1)) || (a == (b+1)); 
        }
    }
}