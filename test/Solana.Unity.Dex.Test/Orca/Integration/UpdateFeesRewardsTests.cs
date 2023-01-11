using NUnit.Framework;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Errors;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Types;
using BigDecimal = Solana.Unity.Dex.Orca.Math.BigDecimal;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class UpdateFeesRewardsTests
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
        [Description("successfully updates fees and rewards")]
        public static async Task SuccessfulUpdateFeesRewards()
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
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testFixture.GetTestInfo().Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda; 

            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, whirlpoolPda, 22528
            );

            //get position before
            Position positionBefore = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey)
            ).ParsedResult;

            Assert.That(positionBefore.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(positionBefore.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(positionBefore.RewardInfos[0].AmountOwed, Is.EqualTo(0));
            Assert.That(positionBefore.RewardInfos[0].GrowthInsideCheckpoint, Is.EqualTo(BigInteger.Zero));

            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda.PublicKey);

            //do swap 
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
            
            //update fees & rewards 
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = tickArrayPda.PublicKey,
                    TickArrayUpper = tickArrayPda.PublicKey
                }
            ); 
            
            Assert.IsTrue(updateResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(updateResult.Result, _defaultCommitment));

            //get position after
            Position positionAfter = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey, _defaultCommitment)
            ).ParsedResult;
            
            Assert.That(positionAfter.FeeOwedA, Is.GreaterThan(positionBefore.FeeOwedA));
            Assert.That(positionAfter.FeeOwedB, Is.EqualTo(0));
            Assert.That(positionAfter.FeeGrowthCheckpointA, Is.GreaterThan(positionBefore.FeeGrowthCheckpointA));
            Assert.That(positionAfter.FeeGrowthCheckpointB, Is.EqualTo(positionBefore.FeeGrowthCheckpointB));
            Assert.That(positionAfter.RewardInfos[0].AmountOwed, Is.GreaterThan(positionBefore.RewardInfos[0].AmountOwed));
            Assert.That(positionAfter.RewardInfos[0].GrowthInsideCheckpoint, Is.GreaterThan(positionBefore.RewardInfos[0].GrowthInsideCheckpoint));
            Assert.That(positionAfter.Liquidity, Is.EqualTo(positionBefore.Liquidity));
        }

        [Test] 
        [Description("fails when position has zero liquidity")]
        public static async Task FailsWhenPositionZeroLiquidity()
        {
            // In same tick array - start index 22528
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 0
                    }
                }
            );
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testFixture.GetTestInfo().Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, whirlpoolPda, 22528
            );

            //update fees & rewards: fails with LiquidityZero
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = tickArrayPda.PublicKey,
                    TickArrayUpper = tickArrayPda.PublicKey
                }
            );

            AssertUtils.AssertFailedWithCustomError(updateResult, WhirlpoolErrorType.LiquidityZero);
        }

        [Test]  
        [Description("fails when position does not match whirlpool")]
        public static async Task FailsPositionWhirlpoolMismatch()
        {
            // In same tick array - start index 22528
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            ushort tickSpacing = TickSpacing.Standard;
            
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(
                _context, 
                tickSpacing: tickSpacing
            );
            Assert.IsTrue(poolInitResult.WasSuccessful);
            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, poolInitResult.InitPoolParams.WhirlpoolPda, 22528
            );

            WhirlpoolsTestFixture otherPoolTestFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 1_000_000
                    }
                }
            );
            var other = otherPoolTestFixture.GetTestInfo();

            //update fees & rewards: fails with AccountOwnedByWrongProgram
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = other.InitPoolParams.WhirlpoolPda,
                    Position = other.Positions[0].PublicKey,
                    TickArrayLower = tickArrayPda.PublicKey,
                    TickArrayUpper = tickArrayPda.PublicKey
                }
            );
            
            AssertUtils.AssertFailedWithStandardError(updateResult, StandardErrorType.AccountOwnedByWrongProgram); 
        }

        [Test]  
        [Description("fails when tick arrays do not match position")]
        public static async Task FailsTickArrayPositionMismatch()
        {
            // In same tick array - start index 22528
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 1_000_000
                    }
                }
            );
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testFixture.GetTestInfo().Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, whirlpoolPda, 0
            );

            //update fees & rewards: fails with AccountOwnedByWrongProgram
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = tickArrayPda.PublicKey,
                    TickArrayUpper = tickArrayPda.PublicKey
                }
            );

            AssertUtils.AssertFailedWithStandardError(updateResult, StandardErrorType.AccountOwnedByWrongProgram);
        }

        [Test]  
        [Description("fails when tick arrays do not match whirlpool")]
        public static async Task FailsTickArrayWhirlpoolMismatch()
        {
            // In same tick array - start index 22528
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            WhirlpoolsTestFixture testFixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new FundedPositionParams[]{
                    new FundedPositionParams{
                        TickLowerIndex = tickLowerIndex, TickUpperIndex = tickUpperIndex, LiquidityAmount = 0
                    }
                }
            );
            var testInfo = testFixture.GetTestInfo();
            FundedPositionInfo position = testFixture.GetTestInfo().Positions[0];
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            PoolInitResult otherPoolInitResult = await PoolTestUtils.BuildPool(
                _context
            );
            
            Assert.IsTrue(otherPoolInitResult.WasSuccessful);

            Pda tickArrayPda = PdaUtils.GetTickArray(
                _context.ProgramId, otherPoolInitResult.InitPoolParams.WhirlpoolPda, 22528
            );

            //update fees & rewards: fails with AccountOwnedByWrongProgram
            var updateResult = await FeesAndRewardsTestUtils.UpdateFeesAndRewardsAsync(
                _context,
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = whirlpoolPda,
                    Position = position.PublicKey,
                    TickArrayLower = tickArrayPda.PublicKey,
                    TickArrayUpper = tickArrayPda.PublicKey
                }
            );

            AssertUtils.AssertFailedWithStandardError(updateResult, StandardErrorType.AccountOwnedByWrongProgram);
        }
    }
}