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
using Solana.Unity.Dex.Swap;
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
        protected readonly IWhirlpoolContext _context;
        
        //Gets the RpcClient from the context object. 
        protected IRpcClient RpcClient => _context.RpcClient;
        
        //Gets the WhirlpoolClient from the context object. 
        protected WhirlpoolClient WhirlpoolClient => _context.WhirlpoolClient;

        protected Dex(IWhirlpoolContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public abstract Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            BigInteger amount,
            PublicKey inputTokenMintAddress,
            double slippage = 0.01,
            TokenType amountSpecifiedTokenType = TokenType.TokenA,
            PublicKey tokenAuthority = null,
            Commitment commitment = Commitment.Finalized
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> SwapWithQuote(
            PublicKey whirlpoolAddress,
            SwapQuote swapQuote,
            Commitment commitment = Commitment.Finalized
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> OpenPosition(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            bool withMetadata,
            PublicKey funderAccount,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithMetadata(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey funderAccount,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithLiquidity(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            BigInteger tokenMaxA,
            BigInteger tokenMaxB,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment commitment = Commitment.Finalized
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress = null,
            PublicKey positionAuthority = null, 
            Commitment commitment = Commitment.Finalized
        );

        /// <inheritdoc />
        public abstract Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenMaxA, 
            BigInteger tokenMaxB,
            PublicKey positionAuthority,
            Commitment commitment
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount, 
            BigInteger tokenMinA, 
            BigInteger tokenMinB,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<bool> WhirlpoolExists(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            PublicKey configAccountAddress = null,
            ushort tickSpacing = TickSpacing.Standard,
            Commitment commitment = Commitment.Finalized
        );

        /// <inheritdoc />
        public async Task<PublicKey> FindWhirlpoolAddress(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing = TickSpacing.Standard,
            PublicKey configAccountAddress = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            (PublicKey whirlpollAddress, _) = await FindWhirlpool(tokenMintA, tokenMintB, tickSpacing, configAccountAddress, commitment);
            return whirlpollAddress;
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
            Commitment commitment = Commitment.Finalized
        );
        
        /// <summary>
        /// Attempts to retrieve whirlpool information
        /// </summary>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="commitment"></param>
        /// <returns></returns>
        public abstract Task<Whirlpool> GetWhirlpool(
            PublicKey whirlpoolAddress,
            Commitment commitment = Commitment.Finalized
        );

        /// <inheritdoc />
        public async Task<SwapQuote> GetSwapQuote(
            PublicKey inputTokenMintAddress,
            PublicKey outputTokenMintAddress,
            BigInteger tokenAmount,
            double slippageTolerance = 0.01,
            TokenType amountSpecifiedTokenType = TokenType.TokenA,
            bool amountSpecifiedIsInput = true,
            Commitment commitment = Commitment.Finalized
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
                amountSpecifiedTokenType, 
                amountSpecifiedIsInput);
        }

        /// <inheritdoc />
        public async Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            PublicKey whirlpoolAddress,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            TokenType amountSpecifiedTokenType = TokenType.TokenA,
            bool amountSpecifiedIsInput = true,
            Commitment commitment = Commitment.Finalized
        )
        {
            Whirlpool whrp = await GetWhirlpool(whirlpoolAddress, commitment);
            return await GetSwapQuoteFromWhirlpool(
                whrp, 
                tokenAmount, 
                inputTokenMintAddress,
                slippageTolerance, 
                amountSpecifiedTokenType, 
                amountSpecifiedIsInput);
        }


        /// <summary>
        /// Retrieves a swap quote from a specified whirlpool.
        /// </summary>
        /// <param name="whirlpool"></param>
        /// <param name="tokenAmount"></param>
        /// <param name="inputTokenMintAddress"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="amountSpecifiedTokenType"></param>
        /// <param name="amountSpecifiedIsInput"></param>
        /// <returns></returns>
        public abstract Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            Whirlpool whirlpool,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            TokenType amountSpecifiedTokenType = TokenType.TokenA,
            bool amountSpecifiedIsInput = true
        );

        /// <inheritdoc />
        public abstract Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            double inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<DecreaseLiquidityQuote> GetDecreaseLiquidityQuote(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            double slippageTolerance,
            Commitment commitment
        );

        /// <inheritdoc />
        public async Task<IList<TokenData>> GetTokens()
        {
            return await Tokens.GetTokens(_context.RpcClient.NodeAddress.ToString().Contains("devnet") ? 
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
    }
}