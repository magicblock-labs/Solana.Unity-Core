using NUnit.Framework;
using Orca;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class OpenPositionTxApiTests
    {
        public static Account WalletAccount;
        
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;
        
        private static int _lowerTickIndex = 7168;
        private static int _upperTickIndex = 8960;
        private static int _currentTick = 500;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            WalletAccount =  _context.WalletAccount;
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<PublicKey> InitializeTestPool()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(poolInitResult.WasSuccessful);
            return poolInitResult.InitPoolParams.WhirlpoolPda;
        }

        private static async Task<WhirlpoolsTestFixture> InitializeTestFixture()
        {
            int currentTick = 500;
            ushort tickSpacing = TickSpacing.HundredTwentyEight;

            WhirlpoolsTestFixture fixture = await WhirlpoolsTestFixture.CreateInstance(
                _context,
                tickSpacing: tickSpacing,
                initialSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(currentTick)
            );
            
            return fixture;
        }

        private static async Task<IWhirlpoolContext> InitializeContext(Account walletAccount)
        {
            //fund the account 
            await SolUtils.FundTestAccountAsync(_context, walletAccount);

            return new WhirlpoolContext(
                _context.ProgramId,
                _context.RpcClient,
                _context.StreamingRpcClient,
                walletAccount.PublicKey
            );
        }

        [Test]
        [Description("all things for open position in one transaction")]
        public static async Task OpenPositionSingleTransaction()
        {
            //create new account to be position opener, and a new context 

            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            IWhirlpoolContext newContext = await InitializeContext(WalletAccount);
            IDex dex = new OrcaDex(WalletAccount, newContext.RpcClient);
            
            int tickLowerIndex = 0;
            int tickUpperIndex = 128;
            
            //position mint account 
            Account positionMintAccount = new();
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount.PublicKey);

            //get the transaction to open the position 
            Transaction tx = await dex.OpenPosition(
                whirlpoolAddr,
                positionMintAccount,
                tickLowerIndex,
                tickUpperIndex, 
                commitment: TestConfiguration.DefaultCommitment
            );
            
            //sign and execute the transaction 
            tx.Sign(WalletAccount);
            tx.Sign(positionMintAccount);
            var openResult = await newContext.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));


            //retrieve the position
            var positionResult =
                await _context.WhirlpoolClient.GetPositionAsync(positionPda.PublicKey,
                _defaultCommitment
            );

            //asserts 
            Assert.IsTrue(positionResult.WasSuccessful);
            Position position = positionResult.ParsedResult;

            Assert.That(position.TickLowerIndex, Is.EqualTo(tickLowerIndex));
            Assert.That(position.TickUpperIndex, Is.EqualTo(tickUpperIndex));
            Assert.That(position.Whirlpool, Is.EqualTo(whirlpoolAddr));
            Assert.That(position.PositionMint, Is.EqualTo(positionMintAccount.PublicKey));
            Assert.That(position.Liquidity, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeOwedA, Is.EqualTo(0));
            Assert.That(position.FeeOwedB, Is.EqualTo(0));
        }

        [Test]
        [Description("open a position and increase its liquidity in one transaction")]
        public static async Task OpenPositionIncreaseLiquiditySingleTransaction()
        {
            WhirlpoolsTestFixture fixture = await InitializeTestFixture(); 

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //get whirlpool 
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
            )).ParsedResult;
            
            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);
            Account positionMintAccount = new(); 
            
            TokenAmounts tokenAmounts = TokenAmounts.FromValues(1_000_000, 0);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                _currentTick,
                _lowerTickIndex,
                _upperTickIndex,
                tokenAmounts
            );

            //get the transaction to open the position 
            Transaction tx = await dex.OpenPositionWithLiquidity(
                whirlpoolPda,
                positionMintAccount,
                _lowerTickIndex,
                _upperTickIndex,
                tokenAmounts.TokenA,
                tokenAmounts.TokenB,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            tx.Sign(positionMintAccount);
            var openResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));
            
            //retrieve the position
            PublicKey positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount);

            Position positionAfter = (await _context.WhirlpoolClient.GetPositionAsync(
                positionPda,
                _defaultCommitment
            )).ParsedResult;
            
            Assert.NotNull(positionAfter);
            Assert.That(positionAfter.Liquidity, Is.EqualTo(liquidityAmount));
        }
        
        [Test]
        [Description("open a position and increase its liquidity with quote in one transaction")]
        public static async Task OpenPositionIncreaseLiquidityWithQuoteSingleTransaction()
        {
            int lowerTickIndex = 7168;
            int upperTickIndex = 8960;
            int currentTick = 500;
            WhirlpoolsTestFixture fixture = await InitializeTestFixture(); 

            var testInfo = fixture.GetTestInfo();
            Pda whirlpoolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //get whirlpool 
            Whirlpool poolBefore = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                whirlpoolPda.PublicKey.ToString()
            )).ParsedResult;
            
            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);
            Account positionMintAccount = new(); 
            
            TokenAmounts tokenAmounts = TokenAmounts.FromValues(1_000_000, 0);
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                currentTick,
                lowerTickIndex,
                upperTickIndex,
                tokenAmounts
            );

            IncreaseLiquidityQuote increaseLiquidityQuote = await dex.GetIncreaseLiquidityQuote(
                whirlpoolPda,
                poolBefore.TokenMintA,
                tokenAmounts.TokenA,
                0.01,
                _lowerTickIndex,
                _upperTickIndex,
                commitment: TestConfiguration.DefaultCommitment);

            //get the transaction to open the position 
            Transaction tx = await dex.OpenPositionWithLiquidityWithQuote(
                whirlpoolPda,
                positionMintAccount,
                lowerTickIndex,
                upperTickIndex,
                increaseLiquidityQuote,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            tx.Sign(positionMintAccount);
            var openResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));
            
            //retrieve the position
            PublicKey positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount);

            Position positionAfter = (await _context.WhirlpoolClient.GetPositionAsync(
                positionPda,
                _defaultCommitment
            )).ParsedResult;
            
            Assert.NotNull(positionAfter);
            Assert.That(positionAfter.Liquidity, Is.EqualTo(liquidityAmount));
        }
    }
}