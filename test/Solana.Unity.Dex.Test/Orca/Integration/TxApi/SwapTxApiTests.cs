using NUnit.Framework;
using Orca;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Types;
using TokenUtils = Solana.Unity.Dex.Test.Orca.Utils.TokenUtils;

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
        public static async Task SimpleSwapUsingApi()
        {
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, _context.WalletAccount, 1_000_000);
            
            //transfer some of token A for B
            ulong swapAmount = 1000;
            
            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);
            
            //get the balance
            var tokenA =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintA,
                    commitment: _defaultCommitment
                )).Result.Value;
            var tokenB =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;

            //get the swap quote 
            SwapQuote swapQuote = await dex.GetSwapQuoteFromWhirlpool(
                whirlpoolAddr, 
                swapAmount,
                whirlpool.TokenMintA,
                slippageTolerance: 0.1,
                commitment: _defaultCommitment
            );

            //get the swap transaction 
            Transaction tx = await dex.Swap(
                whirlpoolAddr,
                swapAmount
            );

            // sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var swapResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            ); 
            
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
            
            //get the post balance
            var tokenAPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintA,
                    _defaultCommitment
                )).Result.Value;
            var tokenBPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;
            Assert.AreEqual(tokenA.AmountUlong, tokenAPost.AmountUlong + swapAmount);
            Assert.AreEqual(tokenB.AmountUlong, tokenBPost.AmountUlong - (ulong)swapQuote.EstimatedAmountOut);
        }

        [Test]
        [Description("successful swap across one tick array with a quote")]
        public static async Task SwapWithQuoteUsingApi()
        {
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, _context.WalletAccount, 1_000_000);
            
            //transfer some of token A for B
            ulong swapAmount = 1000;
            
            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);
            
            //get the balance
            var tokenA =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintA,
                    commitment: _defaultCommitment
                )).Result.Value;
            var tokenB =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;

            //get the swap quote 
            SwapQuote swapQuote = await dex.GetSwapQuoteFromWhirlpool(
                whirlpoolAddr, 
                swapAmount,
                whirlpool.TokenMintA,
                slippageTolerance: 0.1,
                commitment: _defaultCommitment
            );

            //get the swap transaction 
            Transaction tx = await dex.SwapWithQuote(
                whirlpoolAddr,
                swapQuote
            );

            // sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var swapResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            ); 
            
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
            
            //get the post balance
            var tokenAPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintA,
                    _defaultCommitment
                )).Result.Value;
            var tokenBPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;
            Assert.AreEqual(tokenA.AmountUlong, tokenAPost.AmountUlong + swapAmount);
            Assert.AreEqual(tokenB.AmountUlong, tokenBPost.AmountUlong - (ulong)swapQuote.EstimatedAmountOut);
        }
    }
}