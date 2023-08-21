using Solana.Unity.Dex.Models;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Rpc.Models;
using System.Numerics;
using Solana.Unity.Wallet;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solana.Unity.Dex
{
    /// <summary> 
    /// A repository of functions that create and return full unsigned transactions to execute specified 
    /// actions on the Whirlpool, e.g. creating a funded position or executing a swap. 
    /// </summary> 
    /// <remarks> 
    /// The transactions are returned as Transaction instances, not signed. To complete the action each 
    /// transaction must be signed, serialized, and sent to the RPC node. Each transaction may consist of 
    /// a single instruction or multiple instructions. 
    /// </remarks>
    public interface IDexAggregator
    {
        
        /// <summary> 
        /// Creates a quote for a swap for a specified pair of input/output token mint. 
        /// </summary> 
        /// <param name="inputMint">The mint address of the input token (the token to swap).</param> 
        /// <param name="outputMint">The mint address of the output token (the token to swap for).</param> 
        /// <param name="amount">The amount to swap (could be of the input token or output token).</param> 
        /// <param name="slippageBps">The slippage % in BPS. If the output token amount exceeds the slippage then the swap transaction will fail.</param> 
        /// <param name="excludeDexes">Default is that all DEXes are included. You can pass in the DEXes that you want to exclude and separate them by ,. For example, Aldrin,Saber.</param> 
        /// <param name="onlyDirectRoutes">Default is false. Direct Routes limits Jupiter routing to single hop routes only.</param> 
        /// <param name="platformFeeBps">If you want to charge the user a fee, you can specify the fee in BPS. Fee % is taken out of the output token.</param> 
        /// <param name="maxAccounts">Rough estimate of the max accounts to be used for the quote, so that you can compose with your own accounts</param> 
        /// <returns>A SwapQuote instance containing information about the swap amounts.</returns>
        Task<SwapQuoteAg> GetSwapQuote(
            PublicKey inputMint,
            PublicKey outputMint,
            BigInteger amount,
            ushort? slippageBps = null,
            List<string> excludeDexes = null,
            bool onlyDirectRoutes = false,
            ushort? platformFeeBps = null,
            ushort? maxAccounts = null
        );

        /// <summary>
        /// Creates a transaction to execute a swap for a specified pair of input/output token mint.
        /// </summary>
        /// <param name="quoteResponse"></param>
        /// <param name="userPublicKey"></param>
        /// <param name="destinationTokenAccount"></param>
        /// <param name="wrapAndUnwrapSol"></param>
        /// <param name="useSharedAccounts"></param>
        /// <param name="feeAccount"></param>
        /// <param name="computeUnitPriceMicroLamports"></param>
        /// <param name="useTokenLedger"></param>
        /// <returns></returns>
        Task<Transaction> Swap(
            SwapQuoteAg quoteResponse,
            PublicKey userPublicKey = null,
            PublicKey destinationTokenAccount = null,
            bool wrapAndUnwrapSol = true,
            bool useSharedAccounts = true,
            PublicKey feeAccount = null,
            BigInteger? computeUnitPriceMicroLamports = null,
            bool useTokenLedger = false
        );
        
        /// <summary>
        /// Get the list of tokens that are supported by the dex
        /// </summary>
        /// <returns></returns>
        Task<IList<TokenData>> GetTokens(TokenListType tokenListType = TokenListType.Strict);
        
        /// <summary>
        /// Get a token details given the symbol
        /// </summary>
        /// <param name="symbol">the token symbol</param>
        /// <returns></returns>
        Task<TokenData> GetTokenBySymbol(string symbol);

        /// <summary>
        /// Get a token details given the mint address
        /// </summary>
        /// <param name="mint"></param>
        /// <returns></returns>
        Task<TokenData> GetTokenByMint(string mint);
    }
}