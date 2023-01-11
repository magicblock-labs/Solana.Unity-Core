using NUnit.Framework;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Errors;
using Solana.Unity.Dex.Orca.Core.Accounts;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class IncreaseLiquidityTests
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
        [Description("increase liquidity of a position in one tick array")]
        public static async Task IncreaseLiquidityInOneTickArray()
        {
            int currentTick = 500;
            int lowerTickIndex = 7168;
            int upperTickIndex = 8960;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currentTick),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = lowerTickIndex, TickUpperIndex = upperTickIndex, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var positionInitInfo = testInfo.Positions[0];
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            TokenAmounts tokenAmounts = TokenAmounts.FromValues(1_000_000, 0);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currentTick,
                lowerTickIndex,
                upperTickIndex,
                tokenAmounts
            );

            //call to increase liquidity
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo.InitPoolParams.WhirlpoolPda,
                    testInfo.InitPoolParams,
                    positionInitInfo,
                    liquidityAmount,
                    testInfo.TokenAccountA,
                    testInfo.TokenAccountB,
                    (ulong)tokenAmounts.TokenA,
                    (ulong)tokenAmounts.TokenB
                )
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));


            //token balances 
            string tokenBalanceA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey
                , _defaultCommitment
            )).Result.Value.Amount;

            Assert.That(tokenBalanceA, Is.EqualTo(tokenAmounts.TokenA.ToString()));
            Assert.That(tokenBalanceB, Is.EqualTo(tokenAmounts.TokenB.ToString()));

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                positionInitInfo.PublicKey.ToString()
            )).ParsedResult;

            Assert.NotNull(position);
            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));

            //ticks
            TickArray tickArray = (await _context.WhirlpoolClient.GetTickArrayAsync(
                positionInitInfo.TickArrayLower
            )).ParsedResult;

            AssertUtils.AssertTick(tickArray.Ticks[56], true, liquidityAmount, liquidityAmount);
            AssertUtils.AssertTick(tickArray.Ticks[70], true, liquidityAmount, -liquidityAmount);

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey,
                _defaultCommitment
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(BigInteger.Zero));
        }

        [Test]   
        [Description("increase liquidity of a position spanning two tick arrays")]
        public static async Task IncreaseLiquiditySpanningTwoTickArrays()
        {
            int currentTick = 0;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currentTick),
                positions: new FundedPositionParams[]
                {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = BigInteger.Zero
                    },
                }
            );

            var testInfo = fixture.GetTestInfo();
            var positionInitInfo = testInfo.Positions[0];
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            TokenAmounts tokenAmounts = TokenAmounts.FromValues(167_000, 167_000);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currentTick,
                lowerTickIndex,
                upperTickIndex,
                tokenAmounts
            );

            //call to increase liquidity
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo.InitPoolParams.WhirlpoolPda,
                    testInfo.InitPoolParams,
                    positionInitInfo,
                    liquidityAmount,
                    testInfo.TokenAccountA,
                    testInfo.TokenAccountB,
                    (ulong)tokenAmounts.TokenA,
                    (ulong)tokenAmounts.TokenB
                )
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            //token balances 
            string tokenBalanceA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            Assert.That(tokenBalanceA, Is.EqualTo(tokenAmounts.TokenA.ToString()));
            Assert.That(tokenBalanceB, Is.EqualTo(tokenAmounts.TokenB.ToString()));

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                positionInitInfo.PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            Assert.NotNull(position);
            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));

            //ticks
            TickArray tickArrayLower = (await _context.WhirlpoolClient.GetTickArrayAsync(
                positionInitInfo.TickArrayLower,
                _defaultCommitment
            )).ParsedResult;
            TickArray tickArrayUpper = (await _context.WhirlpoolClient.GetTickArrayAsync(
                positionInitInfo.TickArrayUpper,
                _defaultCommitment
            )).ParsedResult;

            AssertUtils.AssertTick(tickArrayLower.Ticks[78], true, liquidityAmount, liquidityAmount);
            AssertUtils.AssertTick(tickArrayUpper.Ticks[10], true, liquidityAmount, -liquidityAmount);

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(liquidityAmount));
        }

        [Test]  
        [Description("initialize and increase liquidity of a position in a single transaction")]
        public static async Task InitializeIncreaseLiquiditySingleTransaction()
        {
            //TODO: (MID) all this repeated code could be in a test fixture or utility 
            int currentTick = 500;
            int lowerTickIndex = 7168;
            int upperTickIndex = 8960;
            ushort tickSpacing = TickSpacing.Standard;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                tickSpacing: tickSpacing,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currentTick)
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //get whirlpool 
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
            )).ParsedResult;

            TokenAmounts tokenAmounts = TokenAmounts.FromValues(1_000_000, 0);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currentTick,
                lowerTickIndex,
                upperTickIndex,
                tokenAmounts
            );

            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                whirlpoolPda,
                lowerTickIndex,
                upperTickIndex,
                _context.WalletPubKey
            );

            Pda tickArrayLower = PdaUtils.GetTickArray(
                _context.ProgramId,
                whirlpoolPda,
                TickUtils.GetStartTickIndex(lowerTickIndex, tickSpacing)
            );

            Pda tickArrayUpper = PdaUtils.GetTickArray(
                _context.ProgramId,
                whirlpoolPda,
                TickUtils.GetStartTickIndex(upperTickIndex, tickSpacing)
            );

            InitializeTickArrayParams initTickArrayParams = TickArrayTestUtils.GenerateParams(
                _context,
                whirlpool: whirlpoolPda,
                startTick: TickUtils.GetStartTickIndex(lowerTickIndex, tickSpacing)
            );

            //initialize tick array, open position, and increase liquidity in one transaction 
            var blockHash = await _context.RpcClient.GetRecentBlockHashAsync(_context.WhirlpoolClient.DefaultCommitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(openPositionParams.Accounts.Funder)
                .AddInstruction(WhirlpoolProgram.InitializeTickArray(
                    programId: _context.ProgramId,
                    startTickIndex: initTickArrayParams.StartTick,
                    accounts: initTickArrayParams.Accounts))
                .AddInstruction(WhirlpoolProgram.OpenPosition(
                    programId: _context.ProgramId,
                    accounts: openPositionParams.Accounts,
                    tickLowerIndex: openPositionParams.TickLowerIndex,
                    tickUpperIndex: openPositionParams.TickUpperIndex,
                    bumps: openPositionParams.Bumps))
                .AddInstruction(WhirlpoolProgram.IncreaseLiquidity(
                    programId: _context.ProgramId,
                    accounts: new IncreaseLiquidityAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = openPositionParams.PositionPda,
                        PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair,
                        TickArrayLower = tickArrayLower.PublicKey,
                        TickArrayUpper = tickArrayUpper.PublicKey,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    liquidityAmount: liquidityAmount,
                    tokenMaxA: (ulong)tokenAmounts.TokenA,
                    tokenMaxB: (ulong)tokenAmounts.TokenB
                ))
                .Build(new List<Account> {
                    _context.WalletAccount,
                    openPositionParams.PositionMintKeypair
                });

            var txResult = await _context.RpcClient.SendTransactionAsync(
                tx, 
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(txResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(txResult.Result, _defaultCommitment));

            //token balances 
            string tokenBalanceA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                _defaultCommitment
            )).Result.Value.Amount;

            Assert.That(tokenBalanceA, Is.EqualTo(tokenAmounts.TokenA.ToString()));
            Assert.That(tokenBalanceB, Is.EqualTo(tokenAmounts.TokenB.ToString()));

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                openPositionParams.PositionPda.PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            Assert.NotNull(position);
            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));

            //ticks
            TickArray tickArray = (await _context.WhirlpoolClient.GetTickArrayAsync(
                tickArrayLower.PublicKey,
                _defaultCommitment
            )).ParsedResult;

            AssertUtils.AssertTick(tickArray.Ticks[56], true, liquidityAmount, liquidityAmount);
            AssertUtils.AssertTick(tickArray.Ticks[70], true, liquidityAmount, -liquidityAmount);

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
                , _defaultCommitment
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(BigInteger.Zero));
        }

        [Test] 
        [Description("increase liquidity of a position with an approved position authority delegate")]
        public static async Task IncreaseLiquidityApprovedDelegate()
        {
            int currTick = 1300;
            int tickLowerIndex = -1280;
            int tickUpperIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currTick),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var positionInitInfo = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
            )).ParsedResult;
            TokenAmounts tokenAmounts = TokenAmounts.FromValues(0, 167_000);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currTick, tickLowerIndex, tickUpperIndex, tokenAmounts
            );
            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.Positions[0].TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParams = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                testInfo.InitPoolParams.WhirlpoolPda,
                testInfo.InitPoolParams,
                positionInitInfo,
                liquidityAmount,
                testInfo.TokenAccountA,
                testInfo.TokenAccountB,
                (ulong)tokenAmounts.TokenA,
                (ulong)tokenAmounts.TokenB
            );

            increaseParams.Accounts.PositionAuthority = delegateAccount;

            //fails with Signature verification failed
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams,
                feePayer: delegateAccount
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            //get the position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey
            )).ParsedResult;
            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(BigInteger.Zero));

            //tick arrays 
            TickArray tickArrayLower = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayLower,
                _defaultCommitment
            )).ParsedResult;
            TickArray tickArrayUpper = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayUpper,
                _defaultCommitment
            )).ParsedResult;

            AssertUtils.AssertTick(tickArrayLower.Ticks[78], true, liquidityAmount, liquidityAmount);
            AssertUtils.AssertTick(tickArrayUpper.Ticks[10], true, liquidityAmount, -liquidityAmount);
        }

        [Test]  
        [Description("add maximum amount of liquidity near minimum price")]
        public static async Task AddMaxLiquidityNearMinPrice()
        {
            int currTick = -443621;
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(
                ctx: _context,
                tickSpacing: TickSpacing.Stable,
                initSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currTick)
            );

            PublicKey tokenMintA = poolInitResult.InitPoolParams.Accounts.TokenMintA;
            PublicKey tokenMintB = poolInitResult.InitPoolParams.Accounts.TokenMintB;
            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            PublicKey tokenAccountA = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context, 
                _context.WalletAccount,
                tokenMintA, ArithmeticUtils.MaxU64,
                commitment: Commitment.Confirmed);
            PublicKey tokenAccountB = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount, 
                tokenMintB, ArithmeticUtils.MaxU64,
                commitment: Commitment.Confirmed);

            //initialize tick array 
            InitializeTickArrayParams initTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                whirlpoolPda,
                -444224
            );
            var tickArrayResult = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initTickParams);
            Assert.IsTrue(tickArrayResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult.Result, _defaultCommitment));

            int tickLowerIndex = -443632;
            int tickUpperIndex = -443624;

            //open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                whirlpoolPda,
                tickLowerIndex,
                tickUpperIndex
            );
            var positionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            Assert.IsTrue(positionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(positionResult.Result, _defaultCommitment));

            TokenAmounts tokenAmounts = TokenAmounts.FromValues(BigInteger.Zero, ArithmeticUtils.MaxU64);

            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currTick, tickLowerIndex, tickUpperIndex, tokenAmounts
            );

            IncreaseLiquidityParams increaseParams = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                whirlpoolPda,
                poolInitResult.InitPoolParams,
                openPositionParams,
                initTickParams,
                liquidityAmount,
                tokenAccountA,
                tokenAccountB,
                (ulong)tokenAmounts.TokenA,
                (ulong)tokenAmounts.TokenB
            );

            //call to increase liquidity
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                openPositionParams.PositionPda.PublicKey.ToString()
            )).ParsedResult;

            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));
        }

        [Test]   
        [Description("add maximum amount of liquidity near maximum price")]
        public static async Task AddMaxLiquidityNearMaxPrice()
        {
            int currTick = 443635;
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(
                ctx: _context,
                tickSpacing: TickSpacing.Stable,
                initSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currTick)
            );

            PublicKey tokenMintA = poolInitResult.InitPoolParams.Accounts.TokenMintA;
            PublicKey tokenMintB = poolInitResult.InitPoolParams.Accounts.TokenMintB;
            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            PublicKey tokenAccountA = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount, 
                tokenMintA, ArithmeticUtils.MaxU64,
                commitment: Commitment.Confirmed);
            PublicKey tokenAccountB = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount, 
                tokenMintB, ArithmeticUtils.MaxU64,
                commitment: Commitment.Confirmed);

            //initialize tick array 
            InitializeTickArrayParams initTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                whirlpoolPda,
                436480
            );
            var tickArrayResult = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initTickParams);
            Assert.IsTrue(tickArrayResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult.Result, _defaultCommitment));

            int tickLowerIndex = 436488;
            int tickUpperIndex = 436496;

            //open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                whirlpoolPda,
                tickLowerIndex,
                tickUpperIndex
            );
            var positionResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            Assert.IsTrue(positionResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(positionResult.Result, _defaultCommitment));

            TokenAmounts tokenAmounts = TokenAmounts.FromValues(BigInteger.Zero, ArithmeticUtils.MaxU64);

            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currTick, tickLowerIndex, tickUpperIndex, tokenAmounts
            );

            IncreaseLiquidityParams increaseParams = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                whirlpoolPda,
                poolInitResult.InitPoolParams,
                openPositionParams,
                initTickParams,
                liquidityAmount,
                tokenAccountA,
                tokenAccountB,
                (ulong)tokenAmounts.TokenA,
                (ulong)tokenAmounts.TokenB
            );

            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            Position position = _context.WhirlpoolClient.GetPositionAsync(
                openPositionParams.PositionPda.PublicKey.ToString()
            ).Result.ParsedResult;

            Assert.That(position.Liquidity, Is.EqualTo(liquidityAmount));
        }

        [Test]  
        [Description("fails with zero liquidity amount")]
        public static async Task FailsZeroLiquidity()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();

            //fails with LiquidityZero /0x177c/
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo.InitPoolParams.WhirlpoolPda,
                    testInfo.InitPoolParams,
                    testInfo.Positions[0],
                    BigInteger.Zero,
                    testInfo.TokenAccountA,
                    testInfo.TokenAccountB,
                    0,
                    1_000_000
                )
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.LiquidityZero);
        }


        [Test]  
        [Description("fails when token max A exceeded")]
        public static async Task FailsTokenMaxAExceeded()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            //fails with TokenMaxExceeded /0x1781/
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount,
                    0,
                    999_999_999
                )
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TokenMaxExceeded);
        }

        [Test]  
        [Description("fails when token max B exceeded")]
        public static async Task FailsTokenMaxBExceeded()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            //fails with TokenMaxExceeded /0x1781/
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount,
                    999_999_999,
                    0
                )
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TokenMaxExceeded);
        }

        [Test] 
        [Description("fails when position account does not have exactly 1 token")]
        public static async Task FailsPositionAccountNotOne()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            PublicKey newPositionTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Positions[0].MintKeyPair.PublicKey,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParams1 = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                testInfo,
                liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 1_000_000
            );
            increaseParams1.Accounts.PositionTokenAccount = newPositionTokenAccount;

            //fails with ConstraintRaw /0x7d3/
            var result1 = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams1
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);

            // Send position token to other position token account
            var transferResult = await TokenUtils.TransferTokensAsync(
                _context, testInfo.Positions[0].TokenAccount, newPositionTokenAccount, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            );

            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));

            IncreaseLiquidityParams increaseParams2 = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                testInfo,
                liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 1_000_000
            );

            //fails with ConstraintRaw /0x7d3/
            var result2 = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams2
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position token account mint does not match position mint")]
        public static async Task FailsTokenMintMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;
            
            PublicKey invalidPositionTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintA,
                1,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParams = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                testInfo,
                liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 1_000_000
            );
            increaseParams.Accounts.PositionTokenAccount = invalidPositionTokenAccount;

            //fails with A raw constraint was violated /0x7d3/
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position does not match whirlpool")]
        public static async Task FailsPositionMismatch()
        {
            int tickLowerIndex = 7168;
            int tickUpperIndex = 8960;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                tickSpacing: TickSpacing.Standard,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            //initialize a test whirlpool
            PoolInitResult poolInitResult2 = await PoolTestUtils.BuildPool(_context);

            //open a position and get its position token account 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                whirlpoolAddr: poolInitResult2.InitPoolParams.WhirlpoolPda,
                tickLowerIndex: tickLowerIndex,
                tickUpperIndex: tickUpperIndex
            );
            var positionResult = await PositionTestUtils.OpenPositionAsync(
                _context, openPositionParams
            );
            Assert.IsTrue(positionResult.WasSuccessful);
            Pda positionPda = openPositionParams.PositionPda;
            PublicKey positionTokenAddress = openPositionParams.Accounts.PositionTokenAccount;

            //initialize tick array 
            InitializeTickArrayParams initTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                whirlpool: poolInitResult2.InitPoolParams.WhirlpoolPda,
                startTick: 0
            );
            var initTickResult = await TickArrayTestUtils.InitializeTickArrayAsync(
                _context, initTickParams
            );
            Assert.IsTrue(initTickResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initTickResult.Result, _defaultCommitment));

            Pda tickArrayPda = initTickParams.TickArrayPda;

            //fails with A has_one constraint was violated
            var result = await _context.WhirlpoolClient.SendIncreaseLiquidityAsync(
                programId: _context.ProgramId,
                liquidityAmount: liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 1_000_000,
                feePayer: _context.WalletPubKey,
                accounts: new IncreaseLiquidityAccounts
                {
                    Whirlpool = testInfo.InitPoolParams.WhirlpoolPda,
                    PositionAuthority = _context.WalletAccount,
                    Position = positionPda,
                    PositionTokenAccount = positionTokenAddress,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    TickArrayLower = tickArrayPda,
                    TickArrayUpper = tickArrayPda,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signingCallback: (byte[] msg, PublicKey pub) => _context.Sign(msg)
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }

        [Test] 
        [Description("fails when token vaults do not match whirlpool vaults")]
        public static async Task FailsTokenVaultsMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );
            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            //mint token accounts 
            PublicKey fakeVaultA = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintA,
                1000,
                commitment: Commitment.Confirmed
            );
            PublicKey fakeVaultB = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintB,
                1000,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParamsA =
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount,
                    0,
                    1_000_000
                );
            increaseParamsA.Accounts.TokenVaultA = fakeVaultA;

            //fails with ConstraintRaw /0x7d3/
            var resultA = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParamsA
            );

            AssertUtils.AssertFailedWithStandardError(resultA, StandardErrorType.RawConstraint);

            //fails with ConstraintRaw
            IncreaseLiquidityParams increaseParamsB =
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount,
                    0,
                    1_000_000
                );
            increaseParamsB.Accounts.TokenVaultB = fakeVaultB;

            //fails with ConstraintRaw /0x7d3/
            var resultB = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParamsB
            );

            AssertUtils.AssertFailedWithStandardError(resultB, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when owner token account mint does not match whirlpool token mint")]
        public static async Task FailsOwnerTokenMintMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 0
                    }
                }
            );
            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 6_500_000;

            //create invalid mint
            PublicKey invalidMint = await TokenUtils.CreateMintAsync(
                _context,
                authority: _context.WalletAccount, 
                commitment: Commitment.Confirmed
            );

            //mint invalid token account
            PublicKey invalidTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                invalidMint,
                1_000_000,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParams =
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount,
                    0,
                    1_000_000
                );
            increaseParams.Accounts.TokenOwnerAccountA = invalidTokenAccount;

            //fails with ConstraintRaw /0x7d3/
            var result = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                increaseParams
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position authority is not approved delegate for position token account")]
        public static async Task FailsPositionAuthNotApprovedDelegate()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );
            Account delegateAccount = new Account();
            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 
                1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(_context, testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 
                1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //fails with MissingOrInvalidDelegate
            var increaseResult = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMaxA: 0,
                    tokenMaxB: 167_000,
                    positionAuthority: delegateAccount
                ),
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(increaseResult, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test] 
        [Description("fails when position authority is not authorized for exactly 1 token")]
        public static async Task FailsPositionAuthUnauthorized()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );
            Account delegateAccount = new Account();
            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.Positions[0].TokenAccount, 
                delegateAccount.PublicKey, 0,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //fails with InvalidPositionTokenAmount
            var increaseResult = await LiquidityTestUtils.IncreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateIncreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMaxA: 0,
                    tokenMaxB: 167_000,
                    positionAuthority: delegateAccount
                ),
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(increaseResult, WhirlpoolErrorType.InvalidPositionTokenAmount);
        }

        [Test] 
        [Description("fails when position authority was not a signer")]
        public static async Task FailsPositionAuthNotSigner()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );
            Account delegateAccount = new();
            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.Positions[0].TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            IncreaseLiquidityParams increaseParams = LiquidityTestUtils.GenerateIncreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 167_000,
                 positionAuthority: delegateAccount
            );

            //don't change this to call LiquidityTestUtils.IncreaseLiquidityAsync, or the test will fail 
            //  (because it will then be properly signed)
            //fails with Signature verification failed
            var increaseResult = await _context.WhirlpoolClient.SendIncreaseLiquidityAsync(
                increaseParams.Accounts,
                increaseParams.LiquidityAmount,
                increaseParams.TokenMaxA,
                increaseParams.TokenMaxB,
                feePayer: _context.WalletAccount,
                signingCallback: (byte[] msg, PublicKey key) => _context.Sign(msg),
                programId: _context.ProgramId
            );

            AssertUtils.AssertSignatureError(increaseResult);
        }

        [Test]  
        [Description("fails when position authority is not approved for token owner accounts")]
        public static async Task FailsPositionAuthNotApproved()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.Positions[0].TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //fails with // owner does not match 0x4
            var result = await _context.WhirlpoolClient.SendIncreaseLiquidityAsync(
                programId: _context.ProgramId,
                liquidityAmount: liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 167_000,
                feePayer: delegateAccount.PublicKey,
                accounts: new IncreaseLiquidityAccounts
                {
                    Whirlpool = testInfo.InitPoolParams.WhirlpoolPda,
                    PositionAuthority = delegateAccount,
                    Position = testInfo.Positions[0].PublicKey,
                    PositionTokenAccount = testInfo.Positions[0].TokenAccount,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    TickArrayLower = testInfo.Positions[0].TickArrayLower,
                    TickArrayUpper = testInfo.Positions[0].TickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signingCallback: (byte[] msg, PublicKey pub) => delegateAccount.Sign(msg)
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.OwnerMismatch);
        }

        [Test] 
        [Description("fails when tick arrays do not match the position")]
        public static async Task FailsTickArraysMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            //init tick arrays
            InitializeTickArrayParams initLowerTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                testInfo.InitPoolParams.WhirlpoolPda,
                11264
            );
            InitializeTickArrayParams initUpperTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                testInfo.InitPoolParams.WhirlpoolPda,
                22528
            );
            var initLowerRes = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initLowerTickParams);
            var initUpperRes = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initUpperTickParams);
            
            Assert.IsTrue(initLowerRes.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initLowerRes.Result));
            Assert.IsTrue(initUpperRes.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initUpperRes.Result));

            //fails with TicKNotFound
            var result = await _context.WhirlpoolClient.SendIncreaseLiquidityAsync(
                programId: _context.ProgramId,
                liquidityAmount: liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 167_000,
                feePayer: _context.WalletPubKey,
                accounts: new IncreaseLiquidityAccounts
                {
                    Whirlpool = testInfo.InitPoolParams.WhirlpoolPda,
                    PositionAuthority = _context.WalletAccount,
                    Position = testInfo.Positions[0].PublicKey,
                    PositionTokenAccount = testInfo.Positions[0].TokenAccount,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    TickArrayLower = initLowerTickParams.TickArrayPda,
                    TickArrayUpper = initUpperTickParams.TickArrayPda,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signingCallback: (byte[] msg, PublicKey pub) => _context.Sign(msg)
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TickNotFound);
        }

        [Test] 
        [Description("fails when the tick arrays are for a different whirlpool")]
        public static async Task FailsTickArraysWrongWhirlpool()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 0
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            BigInteger liquidityAmount = 1_250_000;

            //initialize a test whirlpool
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);

            //init tick arrays
            InitializeTickArrayParams initLowerTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                poolInitResult.InitPoolParams.WhirlpoolPda,
                -11264
            );
            InitializeTickArrayParams initUpperTickParams = TickArrayTestUtils.GenerateParams(
                _context,
                poolInitResult.InitPoolParams.WhirlpoolPda,
                0
            );
            var initLowerRes = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initLowerTickParams);
            var initUpperRes = await TickArrayTestUtils.InitializeTickArrayAsync(_context, initUpperTickParams);
            
            Assert.IsTrue(initLowerRes.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initLowerRes.Result));
            Assert.IsTrue(initUpperRes.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initUpperRes.Result));

            //fails with TicKNotFound
            var result = await _context.WhirlpoolClient.SendIncreaseLiquidityAsync(
                programId: _context.ProgramId,
                liquidityAmount: liquidityAmount,
                tokenMaxA: 0,
                tokenMaxB: 167_000,
                feePayer: _context.WalletPubKey,
                accounts: new IncreaseLiquidityAccounts
                {
                    Whirlpool = testInfo.InitPoolParams.WhirlpoolPda,
                    PositionAuthority = _context.WalletAccount,
                    Position = testInfo.Positions[0].PublicKey,
                    PositionTokenAccount = testInfo.Positions[0].TokenAccount,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    TickArrayLower = initLowerTickParams.TickArrayPda,
                    TickArrayUpper = initUpperTickParams.TickArrayPda,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signingCallback: (msg, pub) => _context.Sign(msg)
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }
    }
}