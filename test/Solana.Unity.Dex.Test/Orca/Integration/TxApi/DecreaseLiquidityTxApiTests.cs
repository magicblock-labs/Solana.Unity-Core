using NUnit.Framework;
using Orca;
using Solana.Unity.Dex.Math;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Test.Orca.Utils;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class DecreaseLiquidityTxApiTests
    {
        private static readonly int lowerTickIndex = 7168;
        private static readonly int upperTickIndex = 8960;
        private static readonly BigInteger liquidityAmount = 1_250_000;

        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<WhirlpoolsTestFixture> InitializeTestPool(bool tokenAIsNative = false)
        {
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: ArithmeticUtils.DecimalToX64BigInt(1.48),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = lowerTickIndex, TickUpperIndex = upperTickIndex, LiquidityAmount = liquidityAmount
                    }
                },
                tokenAIsNative: tokenAIsNative
            );

            return fixture;
        }

        [Test]
        [Description("all things for decreasing liquidity of a position in one transaction")]
        public static async Task DecreaseLiquiditySingleTransaction()
        {
            //initialize pool, positions, and liquidity 
            WhirlpoolsTestFixture testFixture = await InitializeTestPool();
            IDex dex = new OrcaDex(_context);
            var testInfo = testFixture.GetTestInfo(); 
            
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //pool before liquidity decrease 
            Whirlpool poolBefore = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, Commitment.Processed)
            ).ParsedResult;
            
            //position 
            Position positionBefore = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            //generate liquidity removal quote
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
            
            //generate the transaction
            Transaction tx = await dex.DecreaseLiquidity(
                testInfo.Positions[0].PublicKey,
                removalQuote.LiquidityAmount,
                removalQuote.TokenMinA, 
                removalQuote.TokenMinB,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var decreaseResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(decreaseResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(decreaseResult.Result, _defaultCommitment));

            BigInteger remainingLiquidity = positionBefore.Liquidity - removalQuote.LiquidityAmount;

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;

            Assert.NotNull(position);
            Assert.That(position.Liquidity, Is.EqualTo(remainingLiquidity));

            //ticks
            TickArray tickArray = (await _context.WhirlpoolClient.GetTickArrayAsync(
                testInfo.Positions[0].TickArrayLower
            )).ParsedResult;

            AssertUtils.AssertTick(tickArray.Ticks[56], true, remainingLiquidity, remainingLiquidity);
            AssertUtils.AssertTick(tickArray.Ticks[70], true, remainingLiquidity, -remainingLiquidity);

            //pool before vs. after 
            Whirlpool poolAfter = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(remainingLiquidity));
        }
        
        [Test]
        [Description("all things for decreasing liquidity with closed atas")]
        public static async Task DecreaseLiquidityWithClosedAtas()
        {
            //initialize pool, positions, and liquidity 
            WhirlpoolsTestFixture testFixture = await InitializeTestPool(tokenAIsNative: true);
            IDex dex = new OrcaDex(_context);
            var testInfo = testFixture.GetTestInfo(); 
            
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //pool before liquidity decrease 
            Whirlpool poolBefore = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, Commitment.Finalized)
            ).ParsedResult;
            
            // Close the ata accounts
            Assert.IsTrue(poolBefore.TokenMintA.ToString().Equals(AddressConstants.NATIVE_MINT));
            await TokenUtils.CloseAta(_context.RpcClient, poolBefore.TokenMintA, _context.WalletAccount, _context.WalletAccount);
            await TokenUtils.CloseAta(_context.RpcClient, poolBefore.TokenMintB, _context.WalletAccount, _context.WalletAccount);

            //position 
            Position positionBefore = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey.ToString(),
                _defaultCommitment
            )).ParsedResult;
            
            //generate liquidity removal quote
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
            
            //generate the transaction
            Transaction tx = await dex.DecreaseLiquidity(
                testInfo.Positions[0].PublicKey,
                removalQuote.LiquidityAmount,
                removalQuote.TokenMinA, 
                removalQuote.TokenMinB,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var decreaseResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(decreaseResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(decreaseResult.Result, _defaultCommitment));

            BigInteger remainingLiquidity = positionBefore.Liquidity - removalQuote.LiquidityAmount;

            //position 
            Position position = (await _context.WhirlpoolClient.GetPositionAsync(
                testInfo.Positions[0].PublicKey.ToString(),
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
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey
            )).ParsedResult;

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(remainingLiquidity));
        }
    }
}