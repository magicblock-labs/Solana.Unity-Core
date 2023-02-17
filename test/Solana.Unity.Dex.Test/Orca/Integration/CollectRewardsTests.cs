using NUnit.Framework;

using System.Numerics;
using System.Threading.Tasks; 

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Core.Errors;
using Solana.Unity.Dex.Ticks;
using BigDecimal = Solana.Unity.Dex.Orca.Math.BigDecimal;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class CollectRewardsTests
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
        [Description("successfully collect rewards")]
        public static async Task SuccessfullyCollectRewards()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;
            ushort tickSpacing = TickSpacing.HundredTwentyEight;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[] {
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(10),
                        VaultAmount = vaultStartBalance
                    },
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(10),
                        VaultAmount = vaultStartBalance
                    },
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(10),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //update rewards and fees
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper
                }
            );
            
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));

            //pool 
            Whirlpool whirlpool = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, _defaultCommitment)
            ).ParsedResult;
            
            //position
            Position positionBefore = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey, _defaultCommitment)
            ).ParsedResult;
            
            //tick arrays 
            TickArray tickArrayLower = (
                await _context.WhirlpoolClient.GetTickArrayAsync(position.TickArrayLower, _defaultCommitment)
            ).ParsedResult;
            TickArray tickArrayUpper = (
                await _context.WhirlpoolClient.GetTickArrayAsync(position.TickArrayUpper, _defaultCommitment)
            ).ParsedResult;

            var tickLower = TickArrayUtils.GetTickFromArray(tickArrayLower, lowerTickIndex, tickSpacing);
            var tickUpper = TickArrayUtils.GetTickFromArray(tickArrayUpper, upperTickIndex, tickSpacing);


            CollectRewardsQuote expectation = new CollectRewardsQuote(
                whirlpool, positionBefore, tickLower, tickUpper
            );

            // Perform collect rewards tx
            for (byte i = 0; i < FeesAndRewardsTestUtils.NUM_REWARDS; i++)
            {
                PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                    _context, testInfo.Rewards[i].RewardMint,
                    ownerAccount: _context.WalletAccount,
                    commitment: Commitment.Confirmed
                );

                await FeesAndRewardsTestUtils.CollectRewardsAsync(
                    _context,
                    new CollectRewardAccounts
                    {
                        Whirlpool = whirlpoolPda,
                        PositionAuthority = _context.WalletPubKey,
                        Position = position.PublicKey,
                        PositionTokenAccount = position.TokenAccount,
                        RewardOwnerAccount = rewardOwnerAccount,
                        RewardVault = testInfo.Rewards[0].RewardVaultKeyPair,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                    },
                    rewardIndex: i
                );

                BigInteger collectedBalance = BigInteger.Parse(
                    (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    rewardOwnerAccount.ToString(),
                    _defaultCommitment
                )).Result.Value.Amount);
                
                //TODO: (HIGH) the collected balance and expectation are both 0 - is it correct though? 
                Assert.That(collectedBalance, Is.EqualTo(expectation.RewardOwedA));

                BigInteger vaultBalance =  BigInteger.Parse(
                    (await _context.RpcClient.GetTokenAccountBalanceAsync(
                        testInfo.Rewards[i].RewardVaultKeyPair.PublicKey
                )).Result.Value.Amount);
                
                Assert.That((vaultStartBalance - collectedBalance), Is.EqualTo(vaultStartBalance));

                //position after
                Position positionAfter = (
                    await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey)
                ).ParsedResult;
                
                Assert.That(positionAfter.RewardInfos[i].AmountOwed, Is.EqualTo(0));
                Assert.That(positionAfter.RewardInfos[i].GrowthInsideCheckpoint, Is.EqualTo(BigInteger.Zero));
            }
        }

        [Test] 
        [Description("successfully collect reward with a position authority delegate")]
        public static async Task SuccessfullyCollectRewardWithDelegate()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[] {
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0]; 
            
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context, 
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda, 
                    Position = position.PublicKey, 
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper
                }
            );
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));
            
            //create and approve token 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint, _context.WalletAccount,
                commitment: Commitment.Confirmed
            ); 
            
            Account delegateAccount = new Account(); 
            await SolUtils.FundTestAccountAsync(_context, delegateAccount); 
            
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));
            
            //collect reward
            var collectResult = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context, 
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda, 
                    PositionAuthority = delegateAccount.PublicKey, 
                    Position = position.PublicKey, 
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                signer: delegateAccount,
                rewardIndex: 0
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("successfully collect reward with transferred position token")]
        public static async Task CollectRewardWithTransferredPosToken()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[] {
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];
            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount); 
            
            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            //account for delegate 
            PublicKey delegatePositionAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair, delegateAccount,
                commitment: Commitment.Confirmed
            );

            var transferResult = await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, delegatePositionAccount, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            );
            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));

            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper
                }
            );
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));

            //collect reward
            var collectResult = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = delegateAccount.PublicKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = delegatePositionAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair
                },
                signer: delegateAccount,
                rewardIndex: 0
            );
            
            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("fails when reward index references an uninitialized reward")]
        public static async Task FailsUninitializedReward()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey fakeRewardMint = await TokenUtils.CreateMintAsync(
                _context, 
                authority: _context.WalletAccount,
                commitment: _defaultCommitment);
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, fakeRewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: _defaultCommitment
            );

            //collect reward: fails with AccountNotInitialized
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = AddressConstants.DEFAULT_PUBLIC_KEY,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                rewardIndex: 0
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.AccountOwnedByWrongProgram);
        }

        [Test]  
        [Description("successfully collect reward with owner even when there is a delegate")]
        public static async Task CollectRewardWithOwnerWhenDelegate()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[] {
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];
            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount); 

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            //account for delegate 
            PublicKey delegatePositionAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair, delegateAccount,
                commitment: Commitment.Confirmed
            );

            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper
                }
            );
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));

            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //collect reward
            var collectResult = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair.PublicKey
                },
                rewardIndex: 0
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
        }

        [Test] 
        [Description("fails when position does not match whirlpool")]
        public static async Task FailsPositionWhirlpoolMismatch()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                }, 
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2), 
                        VaultAmount = vaultStartBalance
                    }
                }
            );
            
            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //build test pool
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //collect reward: fails with ConstraintHasOne
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = poolInitResult.InitPoolParams.WhirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair
                },
                rewardIndex: 0
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.HasOneConstraint);
        }

        [Test]  
        [Description("fails when position token account does not have exactly one token")]
        public static async Task FailsTokenAccountAmountWrong()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new RewardParams{
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            PublicKey otherPositionAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, position.MintKeyPair.PublicKey,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //transfer one to another position 
            var transferResult = await TokenUtils.TransferTokensAsync(
                _context, position.TokenAccount, otherPositionAccount, 1,
                feePayer: _context.WalletAccount, commitment: Commitment.Confirmed
            );
            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));

            //collect reward: fails with ConstraintRaw 0x7d3
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair.PublicKey
                },
                rewardIndex: 0
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when position token account mint does not match position mint")]
        public static async Task FailsTokenPositionMintMismatch()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new RewardParams{
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            
            PublicKey fakePositionTokenAccount = await TokenUtils.CreateAndMintToTokenAccountAsync(
                _context,
                _context.WalletAccount, 
                testInfo.InitPoolParams.Accounts.TokenMintA, 1,
                commitment: Commitment.Confirmed
            );

            //collect reward: fails with ConstraintRaw
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = fakePositionTokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair.PublicKey
                },
                rewardIndex: 0
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test]  
        [Description("fails when position authority is not approved delegate for position token account")]
        public static async Task FailsPositionAuthNotApprovedDelegate()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //collect reward: fails with MissingOrInvalidDelegate
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = delegateAccount.PublicKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair.PublicKey
                },
                rewardIndex: 0,
                signer: delegateAccount
            );
            
            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.MissingOrInvalidDelegate);
        }

        [Test] 
        [Description("fails when position authority is not authorized for exactly one token")]
        public static async Task FailsPositionAuthNotAuthorizedForAmount()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new()
                    {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();
            await SolUtils.FundTestAccountAsync(_context, delegateAccount);

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            
            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount.PublicKey, 2,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));


            //collect reward: fails with InvalidPositionTokenAmount
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = delegateAccount.PublicKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair
                },
                rewardIndex: 0,
                signer: delegateAccount
            );

            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.InvalidPositionTokenAmount);
        }
        
        [Test] 
        [Description("fails when position authority was not a signer")]
        public static async Task FailsPositionAuthNotSigner()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            Account delegateAccount = new Account();

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            var approveResult = await TokenUtils.ApproveTokenAsync(
                _context, position.TokenAccount, 
                delegateAccount.PublicKey, 1,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );
            Assert.IsTrue(approveResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(approveResult.Result, _defaultCommitment));

            //collect reward: fails with Signature verification failed
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = delegateAccount,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair
                },
                rewardIndex: 0,
                signer: _context.WalletAccount
            );

            AssertUtils.AssertSignatureError(result);
        }

        [Test] 
        [Description("fails when reward vault does not match whirlpool reward vault")]
        public static async Task FailsRewardVaultWhirlpoolMismatch()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.Rewards[0].RewardMint,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //collect reward: fails with ConstraintAddress
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = rewardOwnerAccount
                },
                rewardIndex: 0
            );
            
            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.AddressConstraint);
        }

        [Test] 
        [Description("fails when reward owner account mint does not match whirlpool reward mint")]
        public static async Task FailsRewardOwnerMintMismatch()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccountKey = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //collect reward: fails with Signature verification failed
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                FeesAndRewardsTestUtils.GenerateCollectRewardsParams(
                    _context,
                    whirlpoolPda,
                    position,
                    rewardOwnerAccountKey,
                    rewardVaultKey: testInfo.Rewards[0].RewardVaultKeyPair.PublicKey
                )
            );

            AssertUtils.AssertFailedWithStandardError(result, StandardErrorType.RawConstraint);
        }

        [Test] 
        [Description("fails when reward index is out of bounds")]
        public static async Task FailsRewardIndexOutOfBounds()
        {
            BigInteger vaultStartBalance = 1_000_000;
            int lowerTickIndex = -1280;
            int upperTickIndex = 1280;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1),
                positions: new FundedPositionParams[]  {
                    new FundedPositionParams  {
                        TickLowerIndex = lowerTickIndex,
                        TickUpperIndex = upperTickIndex,
                        LiquidityAmount = vaultStartBalance
                    }
                },
                rewards: new RewardParams[]{
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(2),
                        VaultAmount = vaultStartBalance
                    }
                }
            );

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];

            //account for reward 
            PublicKey rewardOwnerAccount = await TokenUtils.CreateTokenAccountAsync(
                _context, testInfo.InitPoolParams.Accounts.TokenMintA,
                ownerAccount: _context.WalletAccount,
                commitment: Commitment.Confirmed
            );

            //collect reward: fails with index out of bounds: "program failed to complete" 0x0
            var result = await FeesAndRewardsTestUtils.CollectRewardsAsync(
                _context,
                new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = _context.WalletPubKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccount,
                    RewardVault = testInfo.Rewards[0].RewardVaultKeyPair
                },
                rewardIndex: 4
            );
            
            Assert.IsFalse(result.WasSuccessful); 
            Assert.IsTrue(result.Reason.EndsWith("Program failed to complete"));
        }
    }
}
