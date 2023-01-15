using Solana.Unity.Wallet;
using Solana.Unity.Rpc; 
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca.Core;


namespace Solana.Unity.Dex.Orca
{
    public class WhirlpoolContext : IWhirlpoolContext
    {
        /// <summary>
        /// Gets the Whirlpools program ID.
        /// </summary>
        public PublicKey ProgramId { get; private set; }
        
        /// <summary>
        /// Gets an objects that can make curated RPC calls to the Whirlpools program. 
        /// </summary>
        public WhirlpoolClient WhirlpoolClient { get; private set; }
        
        /// <summary>
        /// Gets an objects that can make raw RPC calls to any Solana program. 
        /// </summary>
        public virtual IRpcClient RpcClient => WhirlpoolClient.RpcClient;
        public virtual IStreamingRpcClient StreamingRpcClient => WhirlpoolClient.StreamingRpcClient;

        /// <summary>
        /// Gets the public key of the Wallet.
        /// </summary>
        public virtual PublicKey WalletPubKey { get; private set; }

        /// <summary>
        /// Public constructor, takes programId as a string. 
        /// </summary>
        /// <param name="programId">String representation of Whirlpools program id.</param>
        /// <param name="rpcClient">Object that can make RPC calls to Solana programs.</param>
        /// <param name="streamingClient">Object that can interact with Solana programs via websocket.</param>
        /// <param name="walletPubKey">Wallet public key.</param>
        public WhirlpoolContext(
            string programId, 
            IRpcClient rpcClient, 
            IStreamingRpcClient streamingClient, 
            PublicKey walletPubKey
        ) : 
            this(new PublicKey(programId), rpcClient, streamingClient, walletPubKey)
        {
        }

        /// <summary>
        /// Public constructor, takes programId as a PublicKey. 
        /// </summary>
        /// <param name="programId">PublicKey representation of Whirlpools program id.</param>
        /// <param name="rpcClient">Object that can make RPC calls to Solana programs.</param>
        /// <param name="streamingClient">Object that can interact with Solana programs via websocket.</param>
        /// <param name="walletPubKey">Wallet public key.</param>
        public WhirlpoolContext(
            PublicKey programId, 
            IRpcClient rpcClient, 
            IStreamingRpcClient streamingClient,
            PublicKey walletPubKey, 
            Commitment defaultCommitment = Commitment.Finalized
        )
        {
            ProgramId = programId;
            WalletPubKey = walletPubKey;
            WhirlpoolClient = new WhirlpoolClient(rpcClient, streamingClient, programId);
            WhirlpoolClient.DefaultCommitment = defaultCommitment;
        }
    }
}