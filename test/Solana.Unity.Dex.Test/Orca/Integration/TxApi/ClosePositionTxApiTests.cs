using NUnit.Framework;
using Orca;
using System;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Types;
using TokenUtils = Solana.Unity.Dex.Test.Orca.Utils.TokenUtils;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class ClosePositionTxApiTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<PublicKey> InitializeTestPool()
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(poolInitResult.WasSuccessful);

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            return whirlpoolPda.PublicKey;
        }

        private static async Task<WhirlpoolsTestFixture> InitializeTestFixture()
        {
            int currentTick = 500;
            ushort tickSpacing = TickSpacing.Standard;

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

        private static async Task<Tuple<PublicKey, Position>> OpenPosition(
            IWhirlpoolContext context, 
            Account walletAccount, 
            PublicKey whirlpoolAddr,
            IDex dex,
            ulong liquidityAmount = 0
        )
        {
            int tickLowerIndex = 7168;
            int tickUpperIndex = 8960;

            //position mint account 
            Account positionMintAccount = new Account();
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount.PublicKey);

            //get the transaction toopen the position 
            Transaction tx;
            if (liquidityAmount > 0)
            {
                tx = await dex.OpenPositionWithLiquidity(
                    whirlpoolAddr,
                    positionMintAccount,
                    tickLowerIndex,
                    tickUpperIndex,
                    liquidityAmount,
                    0,
                    commitment: TestConfiguration.DefaultCommitment
                );
            }
            else
            {
                tx = await dex.OpenPosition(
                    whirlpoolAddr,
                    positionMintAccount,
                    tickLowerIndex,
                    tickUpperIndex,
                    commitment: TestConfiguration.DefaultCommitment
                );
            }

            //sign and execute the transaction 
            tx.Sign(new[]{walletAccount, positionMintAccount});
            var openResult = await context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));

            //retrieve the position
            var positionResult =
                await _context.WhirlpoolClient.GetPositionAsync(
                    positionPda.PublicKey, 
                    TestConfiguration.DefaultCommitment
                );

            //asserts 
            Assert.IsTrue(positionResult.WasSuccessful);
            return Tuple.Create(
                positionPda.PublicKey, 
                positionResult.ParsedResult
            ); 
        }

        [Test]
        [Description("all things for closing a position in one transaction")]
        public static async Task ClosePositionSingleTransaction()
        {
            //create new account to be swapper, and a new context 
            Account walletAccount = new Account();

            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            IWhirlpoolContext newContext = await InitializeContext(walletAccount);
            IDex dex = new OrcaDex(newContext);
            
            //open a position 
            var (positionAddr, position) = await OpenPosition(newContext, walletAccount, whirlpoolAddr, dex);

            //get the transaction to close the position 
            Transaction tx = await dex.ClosePosition(
                positionAddr,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(walletAccount);
            var closeResult = await newContext.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(closeResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closeResult.Result, _defaultCommitment));
            

            //get token supply
            var tokenResult = await _context.RpcClient.GetTokenSupplyAsync(position.PositionMint, _defaultCommitment);
            Assert.IsTrue(tokenResult.WasSuccessful);

            PublicKey positionTokenAddr = TokenUtils.GetAssociatedTokenAddress(
                position.PositionMint,
                walletAccount
            );

            //position after closing 
            var positionAccount = (await _context.RpcClient.GetAccountInfoAsync(positionAddr, _defaultCommitment)).Result.Value;
            var positionTokenAccount = (await _context.RpcClient.GetAccountInfoAsync(positionTokenAddr, _defaultCommitment)).Result.Value;
            var receiverAccount = (await _context.RpcClient.GetAccountInfoAsync(walletAccount.PublicKey, _defaultCommitment)).Result.Value;

            Assert.That(tokenResult.Result.Value.AmountUlong, Is.EqualTo(0));

            Assert.IsNull(positionAccount);
            Assert.IsNull(positionTokenAccount);
            Assert.IsNotNull(receiverAccount);
            Assert.That(receiverAccount.Lamports, Is.GreaterThan(0));

            var closedPosition = await _context.WhirlpoolClient.GetPositionAsync(
                positionAddr
            );

            Assert.IsFalse(closedPosition.WasSuccessful);
            Assert.IsNull(closedPosition.ParsedResult);
        }

        [Test]
        [Description("close a position that has liquidity")]
        public static async Task ClosePositionWithLiquidity()
        {
            Account walletAccount = _context.WalletAccount; 
            
            //initialize everything 
            WhirlpoolsTestFixture fixture = await InitializeTestFixture();
            IDex dex = new OrcaDex(_context);
            var testInfo = fixture.GetTestInfo(); 
            Pda whirlpolPda = testInfo.InitPoolParams.WhirlpoolPda;

            //open a position 
            var (positionAddr, position) = await OpenPosition(_context, walletAccount, whirlpolPda, dex, 100000);
            
            //get the transaction to close the position 
            Transaction tx = await dex.ClosePosition(
                positionAddr,
                commitment: TestConfiguration.DefaultCommitment
            );

            //sign and execute the transaction 
            tx.Sign(walletAccount);
            var closeResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(closeResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(closeResult.Result, _defaultCommitment));

            //get token supply
            var tokenResult = await _context.RpcClient.GetTokenSupplyAsync(position.PositionMint, _defaultCommitment);

            PublicKey positionTokenAddr = TokenUtils.GetAssociatedTokenAddress(
                position.PositionMint,
                walletAccount
            );

            //position after closing 
            var positionAccount = (await _context.RpcClient.GetAccountInfoAsync(positionAddr, _defaultCommitment)).Result.Value;
            var positionTokenAccount = (await _context.RpcClient.GetAccountInfoAsync(positionTokenAddr, _defaultCommitment)).Result.Value;
            var receiverAccount = (await _context.RpcClient.GetAccountInfoAsync(walletAccount.PublicKey, _defaultCommitment)).Result.Value;

            Assert.That(tokenResult.Result.Value.AmountUlong, Is.EqualTo(0));

            Assert.IsNull(positionAccount);
            Assert.IsNull(positionTokenAccount);
            Assert.IsNotNull(receiverAccount);
            Assert.That(receiverAccount.Lamports, Is.GreaterThan(0));

            var closedPosition = await _context.WhirlpoolClient.GetPositionAsync(
                positionAddr
            );

            Assert.IsFalse(closedPosition.WasSuccessful);
            Assert.IsNull(closedPosition.ParsedResult);
        }
    }
}