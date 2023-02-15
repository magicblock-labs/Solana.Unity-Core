using NUnit.Framework;
using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class OpenPositionWithMetadataTxApiTests
    {
        private const int LowerTickIndex = 7168;
        private const int UpperTickIndex = 8960;
        private const int CurrentTick = 500;

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
            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(CurrentTick),
                positions: new FundedPositionParams[]{
                    new()
                    {
                        TickLowerIndex = LowerTickIndex, TickUpperIndex = UpperTickIndex, LiquidityAmount = 0
                    }
                }
            );
            
            return fixture;
        }

        [Test]
        [Description("all things for opening a position and increasing liquidity in one transaction")]
        public static async Task OpenPositionWithLiquiditySingleTransaction()
        {
            //initialize everything 
            var testFixture = await InitializeTestPool();
            var testInfo = testFixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            
            IDex dex = new OrcaDex(_context);
            
            Account positionMint = new();
            PublicKey positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMint);

            var openPositionTx = await dex.OpenPosition(
                testInfo.InitPoolParams.WhirlpoolPda,
                positionMint, 
                LowerTickIndex,
                UpperTickIndex,
                commitment: _defaultCommitment);
            
            openPositionTx.Sign(_context.WalletAccount);
            openPositionTx.Sign(positionMint);
            
            var openResult = await _context.RpcClient.SendTransactionAsync(
                openPositionTx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));

            
            //get pool snapshot before liquidity increase
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                testInfo.InitPoolParams.WhirlpoolPda.PublicKey.ToString()
            )).ParsedResult;

            //get liquidity amounts 
            TokenAmounts tokenAmounts = TokenAmounts.FromValues(1_000_000, 0);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                CurrentTick,
                LowerTickIndex,
                UpperTickIndex,
                tokenAmounts
            );
            
            //create increase liquidity tx
            Transaction tx = await dex.IncreaseLiquidity(
                positionPda,
                tokenAmounts.TokenA,
                tokenAmounts.TokenB,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var increaseResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );
            
            Assert.IsTrue(increaseResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(increaseResult.Result, _defaultCommitment));

            
            Whirlpool poolAfter = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, TestConfiguration.DefaultCommitment)
            ).ParsedResult;
            
            //token balances 
            string tokenBalanceA = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolAfter.TokenVaultA,
                _defaultCommitment
            )).Result.Value.Amount;

            string tokenBalanceB = (await _context.RpcClient.GetTokenAccountBalanceAsync(
                poolAfter.TokenVaultB,
                _defaultCommitment
            )).Result.Value.Amount;

            Assert.That(tokenBalanceA, Is.EqualTo(tokenAmounts.TokenA.ToString()));
            Assert.That(tokenBalanceB, Is.EqualTo(tokenAmounts.TokenB.ToString()));

            //position 
            Position positionAfter = (await _context.WhirlpoolClient.GetPositionAsync(
                positionPda,
                _defaultCommitment
            )).ParsedResult;

            Assert.NotNull(positionAfter);
            Assert.That(positionAfter.Liquidity, Is.EqualTo(liquidityAmount));

            Assert.That(poolAfter.RewardLastUpdatedTimestamp, Is.GreaterThan(poolBefore.RewardLastUpdatedTimestamp));
            Assert.That(poolAfter.Liquidity, Is.EqualTo(BigInteger.Zero));
        }
    }
}