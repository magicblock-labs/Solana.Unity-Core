using NUnit.Framework;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Errors;
using Solana.Unity.Dex.Ticks;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class CollectFeesTests
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
        [Description("successfully collect fees")]
        public static async Task SuccessfullyCollectFees()
        {
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;
            ushort tickSpacing = (ushort)TickSpacing.Standard;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                //initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new()
                    { // In range position
                        TickLowerIndex = tickLowerIndex,
                        TickUpperIndex = tickUpperIndex,
                        LiquidityAmount = 10_000_000
                    },
                    new()
                    { // Out of range position
                        TickLowerIndex = 0,
                        TickUpperIndex = 128,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            Account tokenVaultAKeypair = testInfo.InitPoolParams.TokenVaultAKeyPair;
            Account tokenVaultBKeypair = testInfo.InitPoolParams.TokenVaultBKeyPair;
            PublicKey tokenMintA = testInfo.InitPoolParams.Accounts.TokenMintA;
            PublicKey tokenMintB = testInfo.InitPoolParams.Accounts.TokenMintB;
            FundedPositionInfo[] positions = testInfo.Positions;
            PublicKey tokenAccountA = testInfo.TokenAccountA;
            PublicKey tokenAccountB = testInfo.TokenAccountB;

            var tickArrayPda = PdaUtils.GetTickArray(_context.ProgramId, whirlpoolPda, 22528);
            var positionBeforeSwap = (await _context.WhirlpoolClient.GetPositionAsync(
                positions[0].PublicKey,
                _defaultCommitment
            )).ParsedResult;

            Assert.That(positionBeforeSwap.FeeOwedA, Is.EqualTo(0));
            Assert.That(positionBeforeSwap.FeeOwedB, Is.EqualTo(0));

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);

            //generate swap params to accrue fees in token A
            SwapParams swapParamsA = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult: testInfo.PoolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrayPda, tickArrayPda, tickArrayPda
                },
                oracleAddress: oraclePda,
                amount: 200_000,
                amountSpecifiedIsInput: true,
                aToB: true,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4)
            );
            swapParamsA.Accounts.TokenAuthority = _context.WalletAccount;
            swapParamsA.Accounts.TokenOwnerAccountA = tokenAccountA;
            swapParamsA.Accounts.TokenOwnerAccountB = tokenAccountB;
            swapParamsA.Accounts.TokenVaultA = tokenVaultAKeypair;
            swapParamsA.Accounts.TokenVaultB = tokenVaultBKeypair;

            // Accrue fees in token A
            var swapResultA = await SwapTestUtils.SwapAsync(_context, swapParamsA);

            Assert.IsTrue(swapResultA.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResultA.Result, _defaultCommitment));


            //generate swap params to accrue fees in token B
            SwapParams swapParamsB = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult: testInfo.PoolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrayPda, tickArrayPda, tickArrayPda
                },
                oracleAddress: oraclePda,
                amount: 200_000,
                sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(5),
                aToB: false,
                amountSpecifiedIsInput: true    
            );
            swapParamsB.Accounts.TokenAuthority = _context.WalletAccount;
            swapParamsA.Accounts.TokenOwnerAccountA = tokenAccountA;
            swapParamsA.Accounts.TokenOwnerAccountB = tokenAccountB;
            swapParamsA.Accounts.TokenVaultA = tokenVaultAKeypair;
            swapParamsA.Accounts.TokenVaultB = tokenVaultBKeypair;

            // Accrue fees in token B
            var swapResultB = await SwapTestUtils.SwapAsync(_context, swapParamsB);

            Assert.IsTrue(swapResultB.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResultB.Result, _defaultCommitment));

            //update fees and rewards 
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = positions[0].PublicKey,
                    TickArrayLower = tickArrayPda,
                    TickArrayUpper = tickArrayPda
                }
            );
            
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));

            //get position before
            Position positionBefore = (
                await _context.WhirlpoolClient.GetPositionAsync(positions[0].PublicKey)
            ).ParsedResult;

            //assert that fees owed are correct 
            Assert.That(positionBefore.FeeOwedA, Is.EqualTo(581));
            Assert.That(positionBefore.FeeOwedB, Is.EqualTo(581));

            //create new token accounts 
            PublicKey feeAccountA = await TokenUtils.CreateTokenAccountAsync(
                _context, tokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey feeAccountB = await TokenUtils.CreateTokenAccountAsync(
                _context, tokenMintB,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            // Generate collect fees expectation
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString())
            ).ParsedResult;
            TickArray tickArrayData = (await _context.WhirlpoolClient.GetTickArrayAsync(
                tickArrayPda.PublicKey.ToString())
            ).ParsedResult;

            Tick lowerTick = TickArrayUtils.GetTickFromArray(tickArrayData, tickLowerIndex, tickSpacing);
            Tick upperTick = TickArrayUtils.GetTickFromArray(tickArrayData, tickUpperIndex, tickSpacing);

            CollectFeesQuote expectation = new CollectFeesQuote(whirlpool, positionBefore, lowerTick, upperTick);

            //call CollectFees 
            var collectFeesResult = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams 
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = positions[0].PublicKey,
                        PositionTokenAccount = positions[0].TokenAccount,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenOwnerAccountA = feeAccountA,
                        TokenOwnerAccountB = feeAccountB,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }, 
                    PositionAuthorityKeypair = _context.WalletAccount
                }
            );
            
            Assert.IsTrue(collectFeesResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectFeesResult.Result, _defaultCommitment));

            //get position after
            Position positionAfter = (
                await _context.WhirlpoolClient.GetPositionAsync(positions[0].PublicKey)
            ).ParsedResult;

            BigInteger feeBalanceA = (await _context.RpcClient.GetTokenAccountBalanceAsync(feeAccountA, _defaultCommitment)).Result.Value.AmountUlong;
            BigInteger feeBalanceB = (await _context.RpcClient.GetTokenAccountBalanceAsync(feeAccountB, _defaultCommitment)).Result.Value.AmountUlong;

            Assert.That(feeBalanceA, Is.EqualTo(expectation.FeeOwedA));
            Assert.That(feeBalanceB, Is.EqualTo(expectation.FeeOwedB));
            Assert.That(positionAfter.FeeOwedA, Is.EqualTo(0));
            Assert.That(positionAfter.FeeOwedB, Is.EqualTo(0));

            // Assert out of range position values
            await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = positions[1].PublicKey,
                    TickArrayLower = positions[1].TickArrayLower,
                    TickArrayUpper = positions[1].TickArrayUpper
                }
            );

            Position outOfRangePosition = (
                await _context.WhirlpoolClient.GetPositionAsync(positions[1].PublicKey)
            ).ParsedResult;

            Assert.That(outOfRangePosition.FeeOwedA, Is.EqualTo(0));
            Assert.That(outOfRangePosition.FeeOwedB, Is.EqualTo(0));
        }

        [Test] 
        [Description("successfully collect fees with approved delegate")]
        public static async Task SuccessfullyCollectFeesWithDelegate()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  { // In range position
                        TickLowerIndex = 0,
                        TickUpperIndex = 128,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount); 
            
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //call CollectFees 
            var collectResult = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams 
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = delegateAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }, 
                    PositionAuthorityKeypair = delegateAccount
                },
                feePayer: delegateAccount
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("successfully collect fees with owner even if there is approved delegate")]
        public static async Task SuccessfullyCollectFeesWithOwner()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  { // In range position
                        TickLowerIndex = 0,
                        TickUpperIndex = 128,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);
                
            Assert.IsTrue(approveResult.WasSuccessful); 
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //call CollectFees 
            var collectResult = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = delegateAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    }, 
                    PositionAuthorityKeypair = delegateAccount
                }
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("successfully collect fees with transferred position token")]
        public static async Task CollectFeesWithTransferredPositionToken()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  { // In range position
                        TickLowerIndex = 0,
                        TickUpperIndex = 128,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //create token account for new owner 
            Account newOwner = new Account();
            await SolUtils.FundTestAccountAsync(_context, newOwner); 
            PublicKey newOwnerTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair, newOwner,
                commitment: Commitment.Confirmed
            );

            //transfer new owner account a token 
            var approveResult = await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, newOwnerTokenAccount, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            );

            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //call CollectFees 
            var collectResult = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = newOwner,
                        Position = position.PublicKey,
                        PositionTokenAccount = newOwnerTokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = newOwner, 
                }, 
                newOwner
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("fails when position does not match whirlpool")]
        public static async Task FailsPositionWhirlpoolMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  { // In range position
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo[] positions = testInfo.Positions;

            //create a test whirlpool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);

            //call CollectFees: fails with ConstraintHasOne
            var result = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = poolInitResult.InitPoolParams.WhirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = positions[0].PublicKey,
                        PositionTokenAccount = positions[0].TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );
            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }

        [Test] 
        [Description("fails when position token account does not contain exactly one token")]
        public static async Task FailsTokenAccountAmountWrong()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //create a second token account 
            PublicKey tokenAccount2 = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair.PublicKey,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //call CollectFees: fails with ConstraintRaw
            var collectResult1 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = tokenAccount2,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            ); 

            AssertUtils.AssertFailedWithStandardError(collectResult1, StandardErrorType.RawConstraint);

            //transfer into second token account 
            var transferResult = await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, tokenAccount2, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed);
            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));

            //call CollectFees: fails with ConstraintRaw
            var collectResult2 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.OpenPositionParams.Accounts.PositionTokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            AssertUtils.AssertFailedWithStandardError(collectResult2, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position authority is not approved delegate for position token account")]
        public static async Task FailsPositionAuthNotApprovedDelegate()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //call CollectFees: fails with MissingOrInvalidDelegate
            var result = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = delegateAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = delegateAccount,
                },
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test] 
        [Description("fails when position authority is not authorized to transfer exactly one token")]
        public static async Task FailsPositionAuthNotAuthorizedForAmount()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);
            await TokenUtils.ApproveTokenAsync(
                _context, 
                position.TokenAccount, 
                delegateAccount.PublicKey, 2,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //call CollectFees: fails with InvalidPositionTokenAmount
            var result = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = delegateAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = delegateAccount,
                },
                feePayer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.InvalidPositionTokenAmount);
        }

        [Test] 
        [Description("fails when position authority is not a signer")]
        public static async Task FailsPositionAuthNotSigner()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, 
                position.TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed);
                
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //call CollectFees: fails with Signature verification failed

            SigningCallback signer = new(_context.WalletAccount);

            var result = await _context.WhirlpoolClient.SendCollectFeesAsync(
                new CollectFeesAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = delegateAccount,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.PublicKey,
                    TokenOwnerAccountA = testInfo.TokenAccountA,
                    TokenOwnerAccountB = testInfo.TokenAccountB,
                    TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                    TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                _context.WalletAccount,
                (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                programId: _context.ProgramId
            );

            AssertUtils.AssertSignatureError(result);
        }

        [Test] 
        [Description("fails when position token account mint does not equal position mint")]
        public static async Task FailsPositionTokenMintMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //create unassociated token account 
            PublicKey fakeTokenAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //call CollectFees: fails with ConstraintRaw 0x7d3
            var result = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = fakeTokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when token vault does not match whirlpool token vault")]
        public static async Task FailsFeeVaultWhirlpoolMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //create unassociated token accounts 
            PublicKey fakeVaultA = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey fakeVaultB = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintB,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //call CollectFees: fails with ConstraintAddress
            var result1 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = fakeVaultA,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            //call CollectFees: fails with ConstraintAddress
            var result2 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = fakeVaultB,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.AddressConstraint);
            AssertUtils.AssertFailedWithStandardError(result2, StandardErrorType.AddressConstraint);
        }

        [Test] 
        [Description("fails when owner token account mint does not match whirlpool token mint")]
        public static async Task FailsMintWhirlpoolMismatch()
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = 29440,
                        TickUpperIndex = 33536,
                        LiquidityAmount = 10_000_000
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //create unassociated token accounts 
            PublicKey invalidOwnerAccountA = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintB,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey invalidOwnerAccountB = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //call CollectFees: fails with ConstraintRaw 0x7d3
            var result1 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = invalidOwnerAccountA,
                        TokenOwnerAccountB = testInfo.TokenAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            //call CollectFees: fails with ConstraintRaw 0x7d3
            var result2 = await FeesAndRewardsTestUtils.CollectFeesAsync(
                _context,
                new CollectFeesParams
                {
                    Accounts = new CollectFeesAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletAccount,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        TokenOwnerAccountA = testInfo.TokenAccountA,
                        TokenOwnerAccountB = invalidOwnerAccountB,
                        TokenVaultA = testInfo.PoolInitResult.InitPoolParams.TokenVaultAKeyPair,
                        TokenVaultB = testInfo.PoolInitResult.InitPoolParams.TokenVaultBKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    PositionAuthorityKeypair = _context.WalletAccount,
                }
            );

            AssertUtils.AssertFailedWithStandardError(result1, StandardErrorType.RawConstraint);
            AssertUtils.AssertFailedWithStandardError(result2, StandardErrorType.RawConstraint);
        }
    }
}
