using Solana.Unity.Dex.Models;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Dex.Quotes;
using System.Collections.Generic;

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
    public interface IDex
    {
        /// <summary> 
        /// Constructs a transaction to perform a swap involving the two tokens managed by the specified 
        /// whirlpool. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - tokenAuthority
        /// </remarks>
        /// <param name="whirlpoolAddress"></param> 
        /// <param name="amount"></param>
        /// <param name="inputTokenMintAddress"></param>
        /// <param name="amountSpecifiedIsInput"></param>
        /// <param name="slippage"></param>
        /// <param name="unwrapSol"></param>
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            BigInteger amount,
            PublicKey inputTokenMintAddress,
            bool amountSpecifiedIsInput = true,
            double slippage = 0.01,
            bool unwrapSol = true,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to perform a swap involving the two tokens managed by the specified 
        /// whirlpool. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - tokenAuthority
        /// </remarks>
        /// <param name="whirlpoolAddress"></param> 
        /// <param name="swapQuote"></param>
        /// <param name="unwrapSol"></param>
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> SwapWithQuote(
            PublicKey whirlpoolAddress,
            SwapQuote swapQuote,
            bool unwrapSol = true,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to open a liquidity-providing position on the specified whirlpool without 
        /// providing actual liquidity.
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - funder
        /// - positionMint
        /// </remarks>
        /// <param name="positionMintAccount">A generated empty Keypair that will be initialized to a token mint.</param> 
        /// <param name="whirlpoolAddress">The address of the whirlpool on which to open the position.</param> 
        /// <param name="tickLowerIndex"></param> 
        /// <param name="tickUpperIndex"></param> 
        /// <param name="withMetadata">True if position metadata is to be added to the position token.</param> 
        /// <param name="funderAddress">Position liquidity funder (optional, if different from the wallet).</param>
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> OpenPosition(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            bool withMetadata = false,
            PublicKey funderAddress = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to open a liquidity-providing position on the specified whirlpool 
        /// without providing actual liquidity.
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - funder
        /// - positionMint
        /// </remarks>
        /// <param name="positionMintAccount"></param> 
        /// <param name="whirlpoolAddress"></param> 
        /// <param name="tickLowerIndex"></param> 
        /// <param name="tickUpperIndex"></param> 
        /// <param name="funderAddress"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> OpenPositionWithMetadata(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey funderAddress,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a single transaction to open a position, initialize tick arrays (if necessary), and add 
        /// liquidity to the position. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - funder
        /// - positionMint
        /// 
        /// At minimum, the transaction may contain only a single instruction, to open the position (this 
        /// would be if liquidity specified is zero). 
        /// At most, the transaction would contain instructions to: 
        /// - initialize lower tick array (if necessary)
        /// - initialize upper tick array (if necessary) 
        /// - open the position 
        /// - add liquidity to the position 
        /// </remarks> 
        /// <param name="positionMintAccount"></param> 
        /// <param name="whirlpoolAddress"></param> 
        /// <param name="tickLowerIndex"></param> 
        /// <param name="tickUpperIndex"></param> 
        /// <param name="tokenAmountA"></param> 
        /// <param name="tokenAmountB"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="withMetadata"></param> 
        /// <param name="funderAccount"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> OpenPositionWithLiquidity(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a single transaction to open a position, initialize tick arrays (if necessary), and add 
        /// liquidity to the position, given a quote. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - funder
        /// - positionMint
        /// 
        /// At minimum, the transaction may contain only a single instruction, to open the position (this 
        /// would be if liquidity specified is zero). 
        /// At most, the transaction would contain instructions to: 
        /// - initialize lower tick array (if necessary)
        /// - initialize upper tick array (if necessary) 
        /// - open the position 
        /// - add liquidity to the position 
        /// </remarks> 
        /// <param name="positionMintAccount"></param> 
        /// <param name="whirlpoolAddress"></param> 
        /// <param name="tickLowerIndex"></param> 
        /// <param name="tickUpperIndex"></param>
        /// <param name="increaseLiquidityQuote"></param>
        /// <param name="withMetadata"></param> 
        /// <param name="funderAccount"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> OpenPositionWithLiquidityWithQuote(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a single transaction to close a position with liquidity, by first removing all liquidity, 
        /// and then closing the position. 
        /// </summary>
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        ///
        /// If the position has no liquidity, the instruction to remove liquidity will be omitted.
        /// </remarks> 
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="receiverAddress"></param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress = null,
            PublicKey positionAuthority = null, 
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to add liquidity to an open position. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        /// </remarks>
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="tokenAmountA"></param> 
        /// <param name="tokenAmountB"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );
        
        /// <summary> 
        /// Constructs a transaction to add liquidity to an open position, given a quote. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        /// </remarks>
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="increaseLiquidityQuote"></param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> IncreaseLiquidityWithQuote(
            PublicKey positionAddress,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to remove liquidity from an open position. 
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        /// </remarks>
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="liquidityAmount"></param> 
        /// <param name="tokenMinA"></param> 
        /// <param name="tokenMinB"></param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            BigInteger tokenMinA,
            BigInteger tokenMinB,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a transaction to update fees and rewards for a given position. 
        /// </summary> 
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Collects fees that have accrued in a specified position.
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        /// </remarks>
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Collects accrued rewards in a specified position, into a specified wallet.
        /// </summary> 
        /// <remarks> 
        /// Signers: 
        /// - positionAuthority
        /// </remarks>
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="rewardIndex"></param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a single transaction to update fees and rewards, and then collect fees. 
        /// </summary> 
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Constructs a single transaction to update fees and rewards, and then collect rewards. 
        /// </summary> 
        /// <param name="positionAddress">The unique position identifier.</param> 
        /// <param name="rewardIndex"></param> 
        /// <param name="positionAuthority"></param> 
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>The generated Transaction instance</returns>
        Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary>
        /// Determines whether or not a whirlpool with the given (or similar) characteristics can be found.
        /// </summary> 
        /// <param name="tokenMintA">Mint address of any token associated with the pool, preferably token A.</param> 
        /// <param name="tokenMintB">Mint address of any token associated with the pool, preferably token B.</param> 
        /// <param name="tickSpacing">Preferred tickSpacing associated with the pool; if not found, others will be queried.</param> 
        /// <param name="configAccountAddress">Public key of the whirlpool config address account.</param>
        /// <param name="commitment">Transaction commitment to use for chain queries.</param> 
        /// <returns>A boolean value, true if the whirlpool was found.</returns>
        Task<bool> WhirlpoolExists(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            PublicKey configAccountAddress = null,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary>
        /// Determines whether or not a whirlpool with the given (or similar) characteristics can be found.
        /// </summary>
        /// <param name="tokenMintA">Mint address of any token associated with the pool, preferably token A.</param>
        /// <param name="tokenMintB">Mint address of any token associated with the pool, preferably token B.</param>
        /// <param name="tickSpacing">Preferred tickSpacing associated with the pool; if not found, others will be queried.</param>
        /// <param name="configAccountAddress">Public key of the whirlpool config address account.</param>
        /// <param name="commitment">Transaction commitment to use for chain queries.</param>
        /// <returns>The pool if found</returns>
        public Task<Pool> FindWhirlpoolAddress(PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            PublicKey configAccountAddress = null,
            Commitment? commitment = Commitment.Finalized);
        
        /// <summary> 
        /// Creates a quote for a swap for a specified pair of input/output token mint. 
        /// </summary> 
        /// <param name="inputTokenMintAddress">The mint address of the input token (the token to swap).</param> 
        /// <param name="outputTokenMintAddress">The mint address of the output token (the token to swap for).</param> 
        /// <param name="tokenAmount">The amount to swap (could be of the input token or output token).</param> 
        /// <param name="slippageTolerance"></param> 
        /// <param name="amountSpecifiedIsInput">True if the <paramref name="tokenAmount">tokenAmount</paramref> 
        /// refers to the input token.</param> 
        /// <param name="commitment">Transaction commitment on which to use for any chain queries.</param> 
        /// <returns>A SwapQuote instance containing information about the swap amounts.</returns>
        Task<SwapQuote> GetSwapQuote(
            PublicKey inputTokenMintAddress,
            PublicKey outputTokenMintAddress,
            BigInteger tokenAmount,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Creates a quote for a swap within a specific whirlpool. 
        /// </summary> 
        /// <param name="whirlpoolAddress">The address of the whirlpool on which the swap is to take place.</param> 
        /// <param name="inputTokenMintAddress">The mint address of the input token (the token to swap).</param> 
        /// <param name="tokenAmount">The amount to swap (could be of the input token or output token).</param> 
        /// <param name="slippageTolerance"></param> 
        /// <param name="amountSpecifiedIsInput">True if the <paramref name="tokenAmount">tokenAmount</paramref> 
        /// refers to the input token.</param> 
        /// <param name="commitment">Transaction commitment on which to use for any chain queries.</param> 
        /// <returns>A SwapQuote instance containing information about the swap amounts.</returns>
        Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            PublicKey whirlpoolAddress,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Creates a quote to add liquidity to a position. 
        /// </summary> 
        /// <param name="whirlpoolAddress">Address of the whirlpool to which the position belongs</param>
        /// <param name="inputTokenMintAddress"></param>
        /// <param name="inputTokenAmount"></param>
        /// <param name="slippageTolerance"></param>
        /// <param name="tickLowerIndex"></param>
        /// <param name="tickUpperIndex"></param>
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>A quote to increase liquidity.</returns>
        Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            BigInteger inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment? commitment = Commitment.Finalized
        );

        /// <summary> 
        /// Creates a quote to remove liquidity from a position. 
        /// </summary> 
        /// <param name="positionAddress">Address of the position from which to remove liquidity.</param>
        /// <param name="liquidityAmount">The amount of liquidity to remove.</param>
        /// <param name="slippageTolerance"></param>
        /// <param name="commitment">Transaction commitment on which to base the transaction, and to use for 
        /// any chain queries.</param> 
        /// <returns>A quote to decrease liquidity.</returns>
        Task<DecreaseLiquidityQuote> GetDecreaseLiquidityQuote(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            double slippageTolerance,
            Commitment? commitment = Commitment.Finalized
        );
        
        /// <summary>
        /// Get the list of tokens that are supported by the dex
        /// </summary>
        /// <returns></returns>
        Task<IList<TokenData>> GetTokens();
        
        /// <summary>
        /// Get a token details given the symbol
        /// </summary>
        /// <param name="symbol">the token symbol</param>
        /// <returns></returns>
        Task<TokenData> GetTokenBySymbol(string symbol);

        /// <summary>
        /// Get a token details given the mint address
        /// </summary>
        /// <param name="orcaMint"></param>
        /// <returns></returns>
        Task<TokenData> GetTokenByMint(string orcaMint);
        
        /// <summary>
        /// Get the list of positions for a given owner
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="commitment"></param>
        /// <returns></returns>
        public abstract Task<IList<PublicKey>> GetPositions(
            PublicKey owner = null,
            Commitment? commitment = Commitment.Finalized);
    }
}