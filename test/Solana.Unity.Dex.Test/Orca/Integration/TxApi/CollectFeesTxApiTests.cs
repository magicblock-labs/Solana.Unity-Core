using NUnit.Framework;
using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class CollectFeesTxApiTests
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
            int tickLowerIndex = 29440;
            int tickUpperIndex = 33536;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                positions: new[]  {
                    new FundedPositionParams  { // In range position
                        TickLowerIndex = tickLowerIndex,
                        TickUpperIndex = tickUpperIndex,
                        LiquidityAmount = 10_000_000
                    },
                    new FundedPositionParams  { // Out of range position
                        TickLowerIndex = 0,
                        TickUpperIndex = 128,
                        LiquidityAmount = 10_000_000
                    }
                }
            );
            
            return fixture;
        }
        
        private static async Task DoSwap(
            WhirlpoolsTestFixture.TestFixtureInfo testInfo, 
            PublicKey tickArrayPda,
            BigInteger sqrtPriceLimit,
            bool aToB
        )
        {
            Pda whirlpoolPda = testInfo.PoolInitResult.InitPoolParams.WhirlpoolPda; 
            Pda oraclePda = PdaUtils.GetOracle(_context.ProgramId, whirlpoolPda);
            
            SwapParams swapParams = SwapTestUtils.GenerateParams(
                _context,
                poolInitResult: testInfo.PoolInitResult,
                whirlpoolAddress: whirlpoolPda,
                tickArrays: new PublicKey[]{
                    tickArrayPda, tickArrayPda, tickArrayPda
                },
                oracleAddress: oraclePda,
                amount: 200_000,
                amountSpecifiedIsInput: true,
                aToB: aToB,
                sqrtPriceLimit: sqrtPriceLimit
            );
            swapParams.Accounts.TokenAuthority = _context.WalletAccount;
            swapParams.Accounts.TokenOwnerAccountA = testInfo.TokenAccountA;
            swapParams.Accounts.TokenOwnerAccountB = testInfo.TokenAccountB;
            swapParams.Accounts.TokenVaultA = testInfo.InitPoolParams.TokenVaultAKeyPair;
            swapParams.Accounts.TokenVaultB = testInfo.InitPoolParams.TokenVaultBKeyPair;

            // Accrue fees in token 
            var swapResult = await SwapTestUtils.SwapAsync(_context, swapParams);

            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
        }
        
        private static async Task UpdateFeesAndRewards(
            IDex dex,
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

        [Test]
        [Description("all things for collect fees in one transaction")]
        public static async Task CollectFeesSingleTransaction()
        {
            //initialize pool, positions, and liquidity 
            WhirlpoolsTestFixture testFixture = await InitializeTestPool();
            IDex dex = new OrcaDex(_context);
            
            //get data from test pool fixture 
            var testInfo = testFixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;
            FundedPositionInfo position = testInfo.Positions[0];
            Pda tickArrayPda = PdaUtils.GetTickArray(_context.ProgramId, whirlpoolPda, 22528);

            //examine position before swaps
            var positionBeforeSwaps = (await _context.WhirlpoolClient.GetPositionAsync(
                position.PublicKey, _defaultCommitment
            )).ParsedResult;

            //before swaps, position fees owed are 0
            Assert.That(positionBeforeSwaps.FeeOwedA, Is.EqualTo(0));
            Assert.That(positionBeforeSwaps.FeeOwedB, Is.EqualTo(0));

            //do swaps to accrue fees in both tokens
            await DoSwap(testInfo, tickArrayPda, ArithmeticUtils.DecimalToX64BigInt(4), true);
            await DoSwap(testInfo, tickArrayPda, ArithmeticUtils.DecimalToX64BigInt(5), false);

            //update fees and rewards 
            await UpdateFeesAndRewards(
                dex, position.PublicKey, tickArrayPda, tickArrayPda
            );

            //get position before fee collection
            Position positionBeforeCollect = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey, _defaultCommitment)
            ).ParsedResult;

            //assert that fees owed are correct 
            Assert.That(positionBeforeCollect.FeeOwedA, Is.EqualTo(581));
            Assert.That(positionBeforeCollect.FeeOwedB, Is.EqualTo(581));
            
            // get collect fees transaction 
            Transaction tx = await dex.CollectFees(
                position.PublicKey,
                commitment: _defaultCommitment
            );

            //sign and send tx 
            tx.Sign(_context.WalletAccount);
            var collectResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(), 
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(collectResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(collectResult.Result, _defaultCommitment));
            

            //get position after
            Position positionAfterCollect = (
                await _context.WhirlpoolClient.GetPositionAsync(position.PublicKey, _defaultCommitment)
            ).ParsedResult;
            
            Assert.That(positionAfterCollect.FeeOwedA, Is.EqualTo(0));
            Assert.That(positionAfterCollect.FeeOwedB, Is.EqualTo(0));
        }
    }
}