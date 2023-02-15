using NUnit.Framework;
using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class UpdateFeesAndRewardsTxApiTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<WhirlpoolsTestFixture> InitializeTestPool()
        {
            // In same tick array - start index 22528
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 1_000_000
                    }
                },
                rewards: new RewardParams[] {
                    new()
                    {
                        EmissionsPerSecondX64 = ArithmeticUtils.DecimalToX64BigInt(new BigDecimal(2)),
                        VaultAmount = 1_000_000
                    }
                }
            );
            
            return testFixture;
        }

        [Test]
        [Description("all things for updates fees and rewards in one transaction")]
        public static async Task UpdateFeesAndRewardsSingleTransaction()
        {
            //initialize pool, positions, and liquidity 
            WhirlpoolsTestFixture testFixture = await InitializeTestPool();
            OrcaDex dex = new (_context);
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testInfo.Positions[0];

            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //pool before liquidity decrease 
            Whirlpool poolBefore = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, Commitment.Processed)
            ).ParsedResult;

            //get position before
            Position positionBefore = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey)
            ).ParsedResult;

            Assert.That(positionBefore.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(positionBefore.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(positionBefore.RewardInfos[0].AmountOwed, Is.EqualTo(0));
            Assert.That(positionBefore.RewardInfos[0].GrowthInsideCheckpoint, Is.EqualTo(BigInteger.Zero));

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda.PublicKey);

            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, whirlpoolPda, 22528
            );

            //swap
            var swapResult = await SwapTestUtils.SwapAsync(
                _context,
                SwapTestUtils.GenerateParams(
                    _context,
                    testInfo.PoolInitResult,
                    whirlpoolAddress: whirlpoolPda,
                    tickArrays: new PublicKey[]{
                        tickArrayPda.PublicKey, tickArrayPda.PublicKey, tickArrayPda.PublicKey
                    },
                    oracleAddress: oraclePda,
                    amount: 100_000,
                    sqrtPriceLimit: ArithmeticUtils.DecimalToX64BigInt(4.95)
                )
            );

            Assert.IsTrue(swapResult.WasSuccessful);

            //generate the transaction to update fees and rewards 
            Transaction tx = await dex.UpdateFeesAndRewards(
                position.PublicKey,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var updateResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));
        }
    }
}