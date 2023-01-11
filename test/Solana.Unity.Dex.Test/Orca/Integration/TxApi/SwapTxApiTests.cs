using NUnit.Framework;
using Orca;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Quotes.Swap;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class SwapTxApiTests
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
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tickSpacing: TickSpacing.Standard
            );

            Assert.IsTrue(poolInitResult.WasSuccessful);

            Pda whirlpoolPda = poolInitResult.InitPoolParams.WhirlpoolPda;

            //initialize tickarrays 
            await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx: _context,
                whirlpool: whirlpoolPda,
                startTickIndex: 22528, // to 33792
                arrayCount: 3,
                aToB: false
            );

            FundedPositionParams[] fundParams = {
                new()
                {
                    LiquidityAmount = 10_000_000,
                    TickLowerIndex = 29440,
                    TickUpperIndex = 33536
                }
            };

            //fund positions 
            await PositionTestUtils.FundPositionsAsync(
                _context,
                poolInitResult: poolInitResult,
                fundParams: fundParams
            );
            
            return whirlpoolPda.PublicKey;
        }

        private static async Task GetWhirlpoolTokens(
            PublicKey whirlpoolAddr, 
            PublicKey receiver, 
            ulong amount
        )
        {
            //get whirlpool 
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, TestConfiguration.DefaultCommitment)).ParsedResult;

            await TokenUtils.CreateAndMintToAssociatedTokenAccountAsync(
                _context, 
                whirlpool.TokenMintA, 
                amount, 
                _context.WalletAccount, 
                receiver, 
                TestConfiguration.DefaultCommitment
            );

            await TokenUtils.CreateAndMintToAssociatedTokenAccountAsync(
                _context,
                whirlpool.TokenMintB,
                amount,
                _context.WalletAccount,
                receiver,
                TestConfiguration.DefaultCommitment
            );
        }
        
        [Test]
        [Description("successful swap across one tick array")]
        public static async Task SwapSingleTransaction()
        {
            //create new account to be swapper, and a new context 
            Account walletAccount = _context.WalletAccount;
            
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool(); 
            IWhirlpoolContext newContext = _context;
            Dex.Orca.TxApi.Dex dex = new OrcaDex(newContext);
            
            //transfer some of token A and B to account 
            await GetWhirlpoolTokens(whirlpoolAddr, walletAccount, 1_000_000); 
            
            //get the swap transaction 
            Transaction tx = await dex.Swap(
                whirlpoolAddr, 
                100_000, false,
                commitment: TestConfiguration.DefaultCommitment
            ); 
            
            //sign and execute the transaction 
            tx.Sign(walletAccount);
            var swapResult = await newContext.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            ); 
            
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));

            //TODO: add more assertions, like token balances 
        }

        [Test]
        [Description("successful swap with a swap quote")]
        public static async Task SwapWithQuote()
        {
            //create new account to be swapper, and a new context 
            Account walletAccount = _context.WalletAccount; //AddressUtils.Generate();
        
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            IWhirlpoolContext newContext = _context; //await InitializeContext(walletAccount);
            OrcaDex dex = new (newContext);
            
            //get whirlpool for quote 
            Whirlpool whirlpool = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, TestConfiguration.DefaultCommitment)
            ).ParsedResult; 
        
            //transfer some of token A and B to account 
            await GetWhirlpoolTokens(whirlpoolAddr, newContext.WalletPubKey, 1_000_000);
        
            //SwapQuoteByInputToken
            SwapQuote swapQuote = await SwapQuoteUtils.SwapQuoteByInputToken(
                _context,
                whirlpool,
                whirlpoolAddress: whirlpoolAddr,
                inputTokenMint: whirlpool.TokenMintB,
                tokenAmount: 100000,
                slippageTolerance: Percentage.FromFraction(1, 100),
                programId: _context.ProgramId
            );
            
            //get the swap transaction 
            Transaction tx = await dex.Swap(
                whirlpoolAddr, 
                (ulong)swapQuote.Amount,
                commitment: _defaultCommitment
            );
        
            //sign and execute the transaction 
            tx.Sign(walletAccount);
            var swapResult = await newContext.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );
        
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
        
            //TODO: add more assertions, like token balances 
        }
    }
}