using Orca;
using Solana.Unity.Dex.Models;
using System;
using System.Numerics; 
using System.Threading.Tasks;

using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Core;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Ticks;
using System.Collections.Generic;
using System.Linq;

namespace Solana.Unity.Dex.Orca.TxApi
{
    /// <summary> 
    /// Abstract implementation of IDex. 
    /// </summary> 
    public abstract class Dex : IDex
    {
        /// <summary>
        /// The whirlpool context object.
        /// </summary>
        protected readonly IWhirlpoolContext Context;
        
        
        /// <summary>
        /// The default commitment level.
        /// </summary>
        protected readonly Commitment DefaultCommitment;
        
        /// <summary>
        /// Gets the RpcClient from the context object.
        /// </summary>
        protected IRpcClient RpcClient => Context.RpcClient;
        
        /// <summary>
        /// Gets the WhirlpoolClient from the context object.
        /// </summary>
        protected WhirlpoolClient WhirlpoolClient => Context.WhirlpoolClient;

        /// <summary>
        /// Create a new instance of Dex using the whirlpool context.
        /// </summary>
        /// <param name="context"></param>
        protected Dex(IWhirlpoolContext context)
        {
            Context = context;
            DefaultCommitment = Context.WhirlpoolClient.DefaultCommitment;
        }

        /// <inheritdoc />
        public abstract Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            BigInteger amount,
            PublicKey inputTokenMintAddress,
            bool amountSpecifiedIsInput = true,
            double slippage = 0.01,
            bool unwrapSol = true,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> SwapWithQuote(
            PublicKey whirlpoolAddress,
            SwapQuote swapQuote,
            bool unwrapSol = true,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> OpenPosition(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            bool withMetadata = false,
            PublicKey funderAccount = null, 
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithMetadata(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey funderAccount = null, 
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithLiquidity(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithLiquidityWithQuote(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress = null,
            PublicKey positionAuthority = null, 
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenAmountA, 
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> IncreaseLiquidityWithQuote(
            PublicKey positionAddress,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount, 
            BigInteger tokenMinA, 
            BigInteger tokenMinB,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<bool> WhirlpoolExists(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            PublicKey configAccountAddress = null,
            ushort tickSpacing = TickSpacing.Standard,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public async Task<PublicKey> FindWhirlpoolAddress(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing = TickSpacing.Standard,
            PublicKey configAccountAddress = null,
            Commitment? commitment = null
        )
        {
            (PublicKey whirlpoolAddress, _) = await FindWhirlpool(tokenMintA, tokenMintB, tickSpacing, configAccountAddress, commitment);
            return whirlpoolAddress;
        }

        /// <summary>
        /// Attempts to find an existing whirlpool with the specified properties, using address derivation 
        /// and then checking for the existence of such an account. 
        /// </summary> 
        /// <param name="tokenMintA">Mint address of any token associated with the pool, preferably token A.</param> 
        /// <param name="tokenMintB">Mint address of any token associated with the pool, preferably token B.</param> 
        /// <param name="tickSpacing">Preferred tickSpacing associated with the pool; if not found, others will be queried.</param> 
        /// <param name="configAccountAddress">Public key of the whirlpool config address account.</param>
        /// <param name="commitment">Transaction commitment to use for chain queries.</param> 
        /// <returns>A tuple of the whirlpool's address, and the Whirlpool instance.</returns>
        public abstract Task<Tuple<PublicKey, Whirlpool>> FindWhirlpool(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing = TickSpacing.Standard,
            PublicKey configAccountAddress = null,
            Commitment? commitment = null
        );
        
        /// <summary>
        /// Attempts to retrieve whirlpool information
        /// </summary>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="commitment"></param>
        /// <returns></returns>
        public abstract Task<Whirlpool> GetWhirlpool(
            PublicKey whirlpoolAddress,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public async Task<SwapQuote> GetSwapQuote(
            PublicKey inputTokenMintAddress,
            PublicKey outputTokenMintAddress,
            BigInteger tokenAmount,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true,
            Commitment? commitment = null
        )
        {
            (PublicKey _, Whirlpool whrp)  = await FindWhirlpool(
                inputTokenMintAddress, 
                outputTokenMintAddress, 
                commitment: commitment);
            return await GetSwapQuoteFromWhirlpool(
                whrp, 
                tokenAmount, 
                inputTokenMintAddress,
                slippageTolerance,
                amountSpecifiedIsInput);
        }

        /// <inheritdoc />
        public async Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            PublicKey whirlpoolAddress,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true,
            Commitment? commitment = null
        )
        {
            Whirlpool whrp = await GetWhirlpool(whirlpoolAddress, commitment);
            return await GetSwapQuoteFromWhirlpool(
                whrp, 
                tokenAmount, 
                inputTokenMintAddress,
                slippageTolerance,
                amountSpecifiedIsInput);
        }


        /// <summary>
        /// Retrieves a swap quote from a specified whirlpool.
        /// </summary>
        /// <param name="whirlpool"></param>
        /// <param name="tokenAmount"></param>
        /// <param name="inputTokenMintAddress"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="amountSpecifiedIsInput"></param>
        /// <returns></returns>
        public abstract Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            Whirlpool whirlpool,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true
        );

        /// <inheritdoc />
        public abstract Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            BigInteger inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<DecreaseLiquidityQuote> GetDecreaseLiquidityQuote(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            double slippageTolerance,
            Commitment? commitment = null
        );

        /// <inheritdoc />
        public abstract Task<IList<PublicKey>> GetPositions(
            PublicKey owner = null,
            Commitment? commitment = null);

        /// <inheritdoc />
        public async Task<IList<TokenData>> GetTokens()
        {
            return await OrcaTokens.GetTokens(Context.RpcClient.NodeAddress.ToString().Contains("devnet") ? 
                Cluster.DevNet : Cluster.MainNet );
        }

        /// <inheritdoc />
        public async Task<TokenData> GetTokenBySymbol(string symbol)
        {
            IList<TokenData> tokens = await GetTokens();
            
            return tokens.First(t => 
                string.Equals(t.Symbol, symbol, StringComparison.CurrentCultureIgnoreCase) || 
                string.Equals(t.Symbol, $"${symbol}", StringComparison.CurrentCultureIgnoreCase));
        }
        
        /// <inheritdoc />
        public async Task<TokenData> GetTokenByMint(string mint)
        {
            IList<TokenData> tokens = await GetTokens();
            
            return tokens.First(t => 
                string.Equals(t.Mint, mint, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}