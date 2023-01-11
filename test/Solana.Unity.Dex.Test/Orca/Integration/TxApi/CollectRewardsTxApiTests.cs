using NUnit.Framework;
using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class CollectRewardsTxApiTests
    {
        private static TestWhirlpoolContext _context;
        private static readonly BigInteger vaultStartBalance = 1_000_000;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<WhirlpoolsTestFixture> InitializeTestPool()
        {
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
            
            return fixture;
        }

        private static async Task UpdateFeesAndRewards(
            OrcaDex dex,
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper
        )
        {
            //get transaction 
            Transaction tx = await dex.UpdateFeesAndRewards(
                positionAddress,
                tickArrayLower,
                tickArrayUpper,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and send tx 
            tx.Sign(_context.WalletAccount);
            var updateResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));
        }
        
        private static async Task TestCollectRewards(
            OrcaDex dex,
            WhirlpoolsTestFixture.TestFixtureInfo testInfo,
            BigInteger vaultStartBalance,
            byte rewardIndex
        )
        {
            FundedPositionInfo position = testInfo.Positions[0]; 
            
            //generate a transaction 
            Transaction tx = await dex.CollectRewards(
                testInfo.Positions[0].PublicKey,
                testInfo.Rewards[rewardIndex].RewardMint,
                testInfo.Rewards[rewardIndex].RewardVaultKeyPair.PublicKey, 
                rewardIndex,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var collectResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );
            
            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));

            //derive reward owner token account 
            PublicKey rewardOwnerAccount = TokenUtils.GetAssociatedTokenAddress(
                testInfo.Rewards[rewardIndex].RewardMint, 
                _context.WalletPubKey
            );

            BigInteger collectedBalance = BigInteger.Parse(
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                rewardOwnerAccount.ToString(),
                _defaultCommitment
            )).Result.Value.Amount);

            BigInteger vaultBalance = BigInteger.Parse(
                (await _context.RpcClient.GetTokenAccountBalanceAsync(
                    testInfo.Rewards[rewardIndex].RewardVaultKeyPair.PublicKey,
                    _defaultCommitment
            )).Result.Value.Amount);

            Assert.That((vaultStartBalance - collectedBalance), Is.EqualTo(vaultStartBalance));

            //position after
            Position positionAfter = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey, _defaultCommitment)
            ).ParsedResult;

            Assert.That(positionAfter.RewardInfos[rewardIndex].AmountOwed, Is.EqualTo(0));
            Assert.That(positionAfter.RewardInfos[rewardIndex].GrowthInsideCheckpoint, Is.EqualTo(BigInteger.Zero));
        }

        [Test]
        [Description("all things for collect rewards in one transaction")]
        public static async Task CollectRewards_Single_Transaction()
        {
            //initialize pool, positions, and liquidity 
            WhirlpoolsTestFixture testFixture = await InitializeTestPool();
            OrcaDex dex = new(_context);

            //get data from test pool fixture 
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testInfo.Positions[0];

            //update fees/Rewards
            await UpdateFeesAndRewards(
                dex, position.PublicKey, position.TickArrayLower, position.TickArrayUpper
            );

            //collect rewards
            await TestCollectRewards(dex, testInfo, vaultStartBalance, 0);
            await TestCollectRewards(dex, testInfo, vaultStartBalance, 1);
            await TestCollectRewards(dex, testInfo, vaultStartBalance, 2);
        }
    }
}