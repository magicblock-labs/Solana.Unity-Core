using NUnit.Framework;
using Orca;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
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
 
        private static async Task<PublicKey> InitializeTestPool(bool tokenAIsNative = false)
        {
            //initialize test pool 
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPoolWithTokens(
                _context,
                ConfigTestUtils.GenerateParams(_context),
                tokenAIsNative: tokenAIsNative,
                tickSpacing: TickSpacing.HundredTwentyEight
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
            ulong amount,
            bool onlyTokenA = false
        )
        {
            //get whirlpool 
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, TestConfiguration.DefaultCommitment)).ParsedResult;

            Transaction createAndMintToAssociatedTokenAccountA = await TokenUtilsTransaction.CreateAndMintToAssociatedTokenAccount(
                _context.RpcClient, whirlpool.TokenMintA, amount,
                feePayer: _context.WalletAccount,
                destination: receiver
            );
            
            var resultTokenA = await _context.RpcClient.SendTransactionAsync(
                createAndMintToAssociatedTokenAccountA.Serialize(), 
                skipPreflight:true,
                _context.WhirlpoolClient.DefaultCommitment);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(resultTokenA.Result, _context.WhirlpoolClient.DefaultCommitment));
            if(onlyTokenA) return;
            
            Transaction createAndMintToAssociatedTokenAccountB = await TokenUtilsTransaction.CreateAndMintToAssociatedTokenAccount(
                _context.RpcClient, whirlpool.TokenMintB, amount,
                feePayer: _context.WalletAccount,
                destination: receiver
            );
            
            var resultTokenB = await _context.RpcClient.SendTransactionAsync(
                createAndMintToAssociatedTokenAccountB.Serialize(), 
                skipPreflight:true,
                _context.WhirlpoolClient.DefaultCommitment);
            
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(resultTokenB.Result, _context.WhirlpoolClient.DefaultCommitment));
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
                swapAmount,
                whirlpool.TokenMintA
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
            Assert.AreEqual(swapAmount, (ulong)swapQuote.EstimatedAmountIn);
            Assert.AreEqual(tokenA.AmountUlong, tokenAPost.AmountUlong + swapAmount);
            Assert.AreEqual(tokenB.AmountUlong, tokenBPost.AmountUlong - (ulong)swapQuote.EstimatedAmountOut);
        }
        
        [Test]
        [Description("successful swap across one tick array with a quote")]
        public static async Task SwapWithQuoteOutputAmountUsingApi()
        {
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, _context.WalletAccount, 1_000_000);
            
            //transfer some of token B for A
            ulong swapAmount = 24912; 
            
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
                amountSpecifiedIsInput: false,
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
            Assert.AreEqual(swapAmount, (ulong)swapQuote.EstimatedAmountOut);
            Assert.AreEqual(tokenA.AmountUlong, tokenAPost.AmountUlong + (ulong)swapQuote.EstimatedAmountIn);
            Assert.AreEqual(tokenB.AmountUlong, tokenBPost.AmountUlong - swapAmount);
        }
        
        [Test]
        [Description("Native swap with sol unwrapping")]
        public static async Task SwapNative()
        {
            // Create a new account
            Account owner = _context.WalletAccount;//await NewInitializedAccount();

            
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool(tokenAIsNative: true);
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, owner.PublicKey, 1_000_000);
            
            //transfer some of token A for B
            ulong swapAmount = 1000; 
            
            IDex dex = new OrcaDex(owner.PublicKey, _context.RpcClient);
            
            //get the balance
            var tokenB =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    owner.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;

            //get the swap quote 
            SwapQuote swapQuote = await dex.GetSwapQuoteFromWhirlpool(
                whirlpoolAddr, 
                swapAmount,
                whirlpool.TokenMintA,
                slippageTolerance: 0.1,
                amountSpecifiedIsInput: true,
                commitment: _defaultCommitment
            );

            //get the swap transaction 
            Transaction tx = await dex.SwapWithQuote(
                whirlpoolAddr,
                swapQuote,
                unwrapSol: true
            );

            // sign and execute the transaction 
            tx.Sign(owner);
            var swapResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                skipPreflight: false,
                commitment: _defaultCommitment
            ); 
            
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
            
            //get the post balance
            var tokenAPostRes =
                await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    owner.PublicKey, 
                    whirlpool.TokenMintA,
                    _defaultCommitment
                );
            var tokenBPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    owner.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;
            
            // Assert wrapped SOL ata has been closed
            Assert.IsFalse(tokenAPostRes.WasSuccessful);
            Assert.IsTrue(tokenAPostRes.Reason.Contains("could not find account"));

            Assert.AreEqual(tokenB.AmountUlong, tokenBPost.AmountUlong - (ulong)swapQuote.EstimatedAmountOut);
        }

        [Test]
        [Description("successful swap sol for a token when both ATAs are closed")]
        public static async Task SwapWithQuoteAndClosedAtas()
        {
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool(tokenAIsNative: true);
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, _context.WalletAccount, 1_000_000);
            
            //transfer some of token A for B
            ulong swapAmount = 1000; 
            
            //close the ATAs
            await TokenUtils.CloseAta(_context.RpcClient, whirlpool.TokenMintA, _context.WalletAccount, _context.WalletAccount);
            await TokenUtils.CloseAta(_context.RpcClient, whirlpool.TokenMintB, _context.WalletAccount, _context.WalletAccount);
            
            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);
            
            //get the balance
            var tokenARes =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintA,
                    commitment: _defaultCommitment
                ));
            var tokenBRes =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                ));
            
            // Assert wrapped SOL ata has been closed
            Assert.IsTrue(tokenARes.Reason.Contains("could not find account"));
            // Assert token ata has been closed
            Assert.IsTrue(tokenBRes.Reason.Contains("could not find account"));

            //get the swap quote 
            SwapQuote swapQuote = await dex.GetSwapQuoteFromWhirlpool(
                whirlpoolAddr, 
                swapAmount,
                whirlpool.TokenMintA,
                slippageTolerance: 0.1,
                amountSpecifiedIsInput: true,
                commitment: _defaultCommitment
            );

            //get the swap transaction 
            Transaction tx = await dex.SwapWithQuote(
                whirlpoolAddr,
                swapQuote,
                unwrapSol: false
            );

            // sign and execute the transaction 
            tx.Sign(_context.WalletAccount);
            var swapResult = await _context.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: _defaultCommitment
            ); 
            
            //assertions 
            Assert.IsTrue(swapResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(swapResult.Result, _defaultCommitment));
            
            //get the post balance
            var tokenBPost =
                (await _context.RpcClient.GetTokenBalanceByOwnerAsync(
                    _context.WalletAccount.PublicKey, 
                    whirlpool.TokenMintB,
                    commitment: _defaultCommitment
                )).Result.Value;
            
            // Assert token B balance is what expected
            Assert.AreEqual((ulong)swapQuote.EstimatedAmountOut, tokenBPost.AmountUlong);
        }
        
        [Test]
        [Description("successful swap a token for sol when WSOL ata is closed")]
        public static async Task SwapWithQuoteBtoAWithClosedWSol()
        {
            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool(tokenAIsNative: true);
            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddr, Commitment.Finalized)).ParsedResult;
            await GetWhirlpoolTokens(whirlpoolAddr, _context.WalletAccount, 1_000_000, onlyTokenA: true);
            
            //transfer some of token A for B
            ulong swapAmount = 1000; 
            
            //close the ATAs
            await TokenUtils.CloseAta(_context.RpcClient, AddressConstants.NATIVE_MINT_PUBKEY, _context.WalletAccount, _context.WalletAccount);

            IDex dex = new OrcaDex(_context.WalletAccount, _context.RpcClient);

            //get the swap quote 
            SwapQuote swapQuote = await dex.GetSwapQuoteFromWhirlpool(
                whirlpoolAddr, 
                swapAmount,
                whirlpool.TokenMintB,
                slippageTolerance: 0.1,
                amountSpecifiedIsInput: false,
                commitment: _defaultCommitment
            );

            //get the swap transaction 
            Transaction tx = await dex.SwapWithQuote(
                whirlpoolAddr,
                swapQuote,
                unwrapSol: false
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
            Assert.AreEqual((ulong)swapQuote.EstimatedAmountOut, tokenAPost.AmountUlong);
        }
    }
}