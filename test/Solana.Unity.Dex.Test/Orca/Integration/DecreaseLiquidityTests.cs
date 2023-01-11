using NUnit.Framework;
using Solana.Unity.Dex.Math;
using System.Threading.Tasks;
using System.Numerics;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Errors;
using Solana.Unity.Dex.Quotes;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class DecreaseLiquidityTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        public DecreaseLiquidityTests()
        {
        }

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        [Test]  
        [Description("decrease liquidity of a position in one tick array")]
        public static async Task DecreaseLiquidityInOneTickArray()
        {
            BigInteger liquidityAmount = 1_250_000;
            int lowerTickIndex = 7168;
            int upperTickIndex = 8960;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = lowerTickIndex, TickUpperIndex = upperTickIndex, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey,
                Commitment.Processed
            )).ParsedResult;

            DecreaseLiquidityQuote removalQuote = DecreaseLiquidityQuoteUtils.GenerateDecreaseQuoteWithParams(
                new DecreaseLiquidityQuoteParams
                {
                    Liquidity = 1_000_000,
                    SqrtPrice = poolBefore.SqrtPrice,
                    SlippageTolerance = Percentage.FromFraction(1, 100),
                    TickCurrentIndex = poolBefore.TickCurrentIndex,
                    TickLowerIndex = lowerTickIndex,
                    TickUpperIndex = upperTickIndex
                }
            ); 

            //call to decrease liquidity
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    removalQuote.LiquidityAmount,
                    (ulong)removalQuote.TokenMinA,
                    (ulong)removalQuote.TokenMinB
                )
            );
            
            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result));

            
            BigInteger remainingLiquidity = liquidityAmount - removalQuote.LiquidityAmount;

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey,
                _defaultCommitment
            )).ParsedResult;

            Assert.NotNull(position);
            Assert.That(position.Liquidity, Is.EqualTo(remainingLiquidity));

            //ticks
            TickArray tickArray = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayLower,
                _defaultCommitment
            )).ParsedResult;

            AssertUtils.AssertTick(tickArray.Ticks[56], true, remainingLiquidity, remainingLiquidity);
            AssertUtils.AssertTick(tickArray.Ticks[70], true, remainingLiquidity, -remainingLiquidity);

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey,
                Commitment.Finalized
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(remainingLiquidity));
        }

        [Test]  
        [Description("successfully decrease liquidity from position in two tick arrays")]
        public static async Task DecreaseLiquiditySpanningTwoTickArrays()
        {
            BigInteger liquidityAmount = 1_250_000; 
            int tickLowerIndex = -1280; 
            int tickUpperIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.BigIntToX64(1),
                positions: new FundedPositionParams[]
                {
                    new()
                    {
                        TickLowerIndex = tickLowerIndex,
                        TickUpperIndex = tickUpperIndex,
                        LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey
            )).ParsedResult;
            
            var removalQuote = DecreaseLiquidityQuoteUtils.GenerateDecreaseQuoteWithParams(
                new DecreaseLiquidityQuoteParams
                {
                    Liquidity = 1_000_000, 
                    SqrtPrice = poolBefore.SqrtPrice, 
                    SlippageTolerance = Percentage.FromFraction(1, 100), 
                    TickCurrentIndex = poolBefore.TickCurrentIndex, 
                    TickLowerIndex = tickLowerIndex, 
                    TickUpperIndex = tickUpperIndex
                }
            );

            //call to decrease liquidity
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    removalQuote.LiquidityAmount, 
                    (ulong)removalQuote.TokenMinA,
                    (ulong)removalQuote.TokenMinB
                )
            );
            
            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));
            
            BigInteger remainingLiquidity = liquidityAmount - removalQuote.LiquidityAmount;

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(remainingLiquidity));

            //position 
            Position positionAfter = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey.ToString()
            )).ParsedResult;

            Assert.NotNull(positionAfter);
            Assert.That(positionAfter.Liquidity, Is.EqualTo(remainingLiquidity));

            //ticks
            TickArray tickArrayLower = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayLower
            )).ParsedResult;

            TickArray tickArrayUpper = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayUpper
            )).ParsedResult;

            AssertUtils.AssertTick(tickArrayLower.Ticks[78], true, remainingLiquidity, remainingLiquidity);
            AssertUtils.AssertTick(tickArrayUpper.Ticks[10], true, remainingLiquidity, -remainingLiquidity);
        }

        [Test]  
        [Description("successfully decrease liquidity with approved delegate")]
        public static async Task DecreaseLiquidityApprovedDelegate()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 1_250_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account delegateAccount = new Account();
            
            await SolUtils.FundTestAccountAsync(_context, delegateAccount); 

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
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
            
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: 1_000_000,
                    tokenMinA: 0,
                    tokenMinB: 0,
                    positionAuthority: delegateAccount
                ),
                feePayer: delegateAccount
            );
            
            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));
        }

        [Test] 
        [Description("successfully decrease liquidity with owner even if there is approved delegate")]
        public static async Task DecreaseLiquidityOwnerApprovedDelegate()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 1_250_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account delegateAccount = new Account();

            //approve tokens 
            await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
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

            BigInteger removeAmount = 1_000_000;

            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: 1_000_000,
                    tokenMinA: 0,
                    tokenMinB: 0,
                    positionAuthority: delegateAccount
                ),
                feePayer: _context.WalletAccount
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));
        }

        [Test] 
        [Description("successfully decrease liquidity with transferred position token")]
        public static async Task DecreaseLiquidityTransferredPositionToken()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = 1_250_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account newOwner = new Account();
            await SolUtils.FundTestAccountAsync(_context, newOwner); 

            //approve tokens 
            PublicKey newOwnerPositionTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair.PublicKey, newOwner,
                commitment: Commitment.Confirmed
            );
            
            //transfer one 
            var transferResult = await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, newOwnerPositionTokenAccount, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            );
            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));

            BigInteger removeAmount = 1_000_000;

            RequestResult<string> result = await _context.WhirlpoolClient.SendDecreaseLiquidityAsync(
                programId: _context.ProgramId,
                liquidityAmount: removeAmount,
                tokenMinA: 0,
                tokenMinB: 0,
                feePayer: newOwner.PublicKey,
                accounts: new DecreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = newOwner,
                    Position = position.PublicKey,
                    PositionTokenAccount = newOwnerPositionTokenAccount,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                    TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signingCallback: (byte[] msg, PublicKey pub) => newOwner.Sign(msg)
            );

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));
        }

        [Test]  
        [Description("fails when liquidity amount is zero")]
        public static async Task FailsZeroLiquidity()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = 1_250_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //fails with LiquidityZero
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: 0,
                    tokenMinA: 0,
                    tokenMinB: 0
                )
            );
            
            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.LiquidityZero);
        }

        [Test]  
        [Description("fails when position has insufficient liquidity for the withdraw amount")]
        public static async Task FailsInsufficientLiquidity()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = BigInteger.Zero
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //fails with LiquidityUnderflow
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: 1_000,
                    tokenMinA: 0,
                    tokenMinB: 0
                )
            );
            
            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.LiquidityUnderflow); 
        }

        [Test]  
        [Description("fails when token min a subceeded")]
        public static async Task FailsTokenMinASubceeded()
        {
            BigInteger liquidityAmount = 1_250_000; 
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(0.005),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = liquidityAmount
                    }
                }
            );
            
            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //fails with TokenMinSubceeded
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMinA: 1_000_000,
                    tokenMinB: 0
                )
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TokenMinSubceeded);
        }

        [Test]  
        [Description("fails when token min b subceeded")]
        public static async Task FailsTokenMinBSubceeded()
        {
            BigInteger liquidityAmount = 1_250_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(5),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = -1280, TickUpperIndex = 1280, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //fails with TokenMinSubceeded
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMinA: 0,
                    tokenMinB: 1_000_000
                )
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TokenMinSubceeded);
        }

        [Test]  
        [Description("fails when position account does not have exactly 1 token")]
        public static async Task FailsPositionAccountNotOne()
        {
            BigInteger liquidityAmount = 1_250_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            PublicKey newPositionTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair.PublicKey,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            DecreaseLiquidityParams params1 = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            params1.Accounts.PositionTokenAccount = newPositionTokenAccount;

            //fails with ConstraintRaw
            var result1 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                params1
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);

            // Send position token to other position token account
            await TokenUtils.TransferTokensAsync(
                _context, 
                source: position.TokenAccount, 
                destination: newPositionTokenAccount, 
                amount: 1,
                feePayer: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            var result2 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMinA: 0,
                    tokenMinB: 0
                )
            );

            AssertUtils.AssertFailedWithStandardError(result2, StandardErrorType.RawConstraint);
        }

        [Test]  
        [Description("fails when position token account mint does not match position mint")]
        public static async Task FailsTokenMintMismatch()
        {
            int tickLowerIndex = 7168;
            int tickUpperIndex = 8960;
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            PublicKey invalidPositionTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintA,
                1,
                commitment: Commitment.Confirmed
            );

            var decreaseParams = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams.Accounts.PositionTokenAccount = invalidPositionTokenAccount;

            //fails with A raw constraint was violated
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position does not match whirlpool")]
        public static async Task FailsPositionMismatch()
        {
            BigInteger liquidityAmount = 6_500_000;
            int tickLowerIndex = 7168;
            int tickUpperIndex = 8960; 
            
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //create test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);

            //open position 
            OpenPositionParams openPositionParams = PositionTestUtils.GenerateOpenParams(
                _context,
                poolInitResult.InitPoolParams.WhirlpoolPda,
                tickLowerIndex,
                tickUpperIndex
            );
            var openPosResult = await PositionTestUtils.OpenPositionAsync(
                _context,
                openPositionParams
            );
            Assert.IsTrue(openPosResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openPosResult.Result, _defaultCommitment));


            var decreaseParams = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams.Accounts.Position = openPositionParams.PositionPda;

            //fails with 0x7d1 - a has_one constraint was violated
            var result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }

        [Test]  
        [Description("fails when token vaults do not match whirlpool vaults")]
        public static async Task FailsTokenVaultsMismatch()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //create fake vaults 
            PublicKey fakeVaultA = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context, 
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintA, 1000,
                commitment: Commitment.Confirmed
            );
            PublicKey fakeVaultB = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context, 
                _context.WalletAccount,
                testInfo.InitPoolParams.Accounts.TokenMintB, 1000,
                commitment: Commitment.Confirmed
            );

            var decreaseParams1 = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams1.Accounts.TokenVaultA = fakeVaultA;

            //fails with A raw constraint was violated
            var result1 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams1
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);

            var decreaseParams2 = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams2.Accounts.TokenVaultB = fakeVaultB;

            //fails with A raw constraint was violated
            var result2 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams2
            );

            AssertUtils.AssertFailedWithStandardError(result2, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when owner token account mint does not match whirlpool token mint")]
        public static async Task FailsOwnerTokenMintMismatch()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            PublicKey invalidMint = await TokenUtils.CreateMintAsync(
                _context, 
                authority: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey invalidTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount, 
                invalidMint, 1_000_000, commitment: Commitment.Confirmed
            );

            var decreaseParams1 = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams1.Accounts.TokenOwnerAccountA = invalidTokenAccount;

            //fails with A raw constraint was violated
            var result1 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams1
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);

            //fails with ConstraintRaw
            var decreaseParams2 = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams2.Accounts.TokenOwnerAccountB = invalidTokenAccount;

            //fails with A raw constraint was violated
            var result2 = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams2
            );

            AssertUtils.AssertFailedWithStandardError(result2, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position authority is not approved delegate for position token account")]
        public static async Task FailsPositionAuthNotApprovedDelegate()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account delegateAccount = new Account();

            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

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

            //fails with MissingOrInvalidDelegate
            RequestResult<string> result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMinA: 0,
                    tokenMinB: 0,
                    positionAuthority: delegateAccount
                ),
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test]  
        [Description("fails when position authority is not authorized for exactly 1 token")]
        public static async Task FailsPositionAuthUnauthorized()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account delegateAccount = new();

            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount.PublicKey, 0,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);
            await TokenUtils.ApproveTokenAsync(
                _context, testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);

            //fails with InvalidPositionTokenAmount
            RequestResult<string> result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                LiquidityTestUtils.GenerateDecreaseParams(
                    _context,
                    testInfo,
                    liquidityAmount: liquidityAmount,
                    tokenMinA: 0,
                    tokenMinB: 0,
                    positionAuthority: delegateAccount
                ),
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.InvalidPositionTokenAmount);
        }

        [Test] 
        [Description("fails when position authority was not a signer")]
        public static async Task FailsPositionAuthNotSigner()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Account delegateAccount = new Account();

            await TokenUtils.ApproveTokenAsync(_context, 
                position.TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(_context, 
                testInfo.TokenAccountA, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            await TokenUtils.ApproveTokenAsync(_context, 
                testInfo.TokenAccountB, 
                delegateAccount.PublicKey, 1_000_000,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            DecreaseLiquidityParams decreaseParams = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 167_000,
                positionAuthority: delegateAccount
            );
            
            //don't change this to call LiquidityTestUtils.DecreaseLiquidityAsync, or the test will fail 
            //  (because it will then be properly signed)
            //fails with Signature verification failed
            var result = await _context.WhirlpoolClient.SendDecreaseLiquidityAsync(
                decreaseParams.Accounts,
                decreaseParams.LiquidityAmount,
                decreaseParams.TokenMinA,
                decreaseParams.TokenMinB,
                feePayer: _context.WalletAccount,
                signingCallback: (byte[] msg, PublicKey key) => _context.Sign(msg),
                programId: _context.ProgramId
            );

            AssertUtils.AssertSignatureError(result);
        }

        [Test] 
        [Description("fails when tick arrays do not match the position")]
        public static async Task FailsTickArraysMismatch()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            InitializeTickArrayParams tickArrayLowerParams = TickArrayTestUtils.GenerateParams(
                _context, testInfo.InitPoolParams.WhirlpoolPda, 11264
            );
            InitializeTickArrayParams tickArrayUpperParams = TickArrayTestUtils.GenerateParams(
                _context, testInfo.InitPoolParams.WhirlpoolPda, 22528
            );

            var tickArrayResult1 = await TickArrayTestUtils.InitializeTickArrayAsync(
                _context, tickArrayLowerParams
            );
            var tickArrayResult2 = await TickArrayTestUtils.InitializeTickArrayAsync(
                _context, tickArrayUpperParams
            );
            Assert.IsTrue(tickArrayResult1.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult1.Result));
            Assert.IsTrue(tickArrayResult2.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult2.Result));

            DecreaseLiquidityParams decreaseParams = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams.Accounts.TickArrayLower = tickArrayLowerParams.TickArrayPda;
            decreaseParams.Accounts.TickArrayUpper = tickArrayUpperParams.TickArrayPda;

            //fails with TicKNotFound
            RequestResult<string> result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.TickNotFound);
        }

        [Test]  
        [Description("fails when the tick arrays are for a different whirlpool")]
        public static async Task FailsTickArraysWrongWhirlpool()
        {
            BigInteger liquidityAmount = 6_500_000;
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(2.2),
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = 7168, TickUpperIndex = 8960, LiquidityAmount = liquidityAmount
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            var position = testInfo.Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            //create different test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);

            InitializeTickArrayParams tickArrayLowerParams = TickArrayTestUtils.GenerateParams(
                _context, poolInitResult.InitPoolParams.WhirlpoolPda, -11264
            );
            InitializeTickArrayParams tickArrayUpperParams = TickArrayTestUtils.GenerateParams(
                _context, poolInitResult.InitPoolParams.WhirlpoolPda, 0
            );
            var tickArrayResult1 = await TickArrayTestUtils.InitializeTickArrayAsync(
                _context, tickArrayLowerParams
            );
            var tickArrayResult2 = await TickArrayTestUtils.InitializeTickArrayAsync(
                _context, tickArrayUpperParams
            );
            
            Assert.IsTrue(tickArrayResult1.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult1.Result));
            Assert.IsTrue(tickArrayResult2.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(tickArrayResult2.Result));

            DecreaseLiquidityParams decreaseParams = LiquidityTestUtils.GenerateDecreaseParams(
                _context,
                testInfo,
                liquidityAmount: liquidityAmount,
                tokenMinA: 0,
                tokenMinB: 0
            );
            decreaseParams.Accounts.TickArrayLower = tickArrayLowerParams.TickArrayPda;
            decreaseParams.Accounts.TickArrayUpper = tickArrayUpperParams.TickArrayPda;

            //fails with 0x7d1 - has_one constraint violated
            RequestResult<string> result = await LiquidityTestUtils.DecreaseLiquidityAsync(
                _context,
                decreaseParams
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }
    }
}