using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Core;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Test.Orca
{
    public sealed class TestWhirlpoolContext : IWhirlpoolContext
    {
        /// <summary>
        /// Gets the Solana wallet used for basic purposes. 
        /// </summary>
        private Wallet.Wallet Wallet { get; set; }

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
        public IRpcClient RpcClient => this.WhirlpoolClient.RpcClient;
        public IStreamingRpcClient StreamingRpcClient => this.WhirlpoolClient.StreamingRpcClient;

        /// <summary>
        /// Gets the public key of the Wallet.
        /// </summary>
        public PublicKey WalletPubKey => Wallet.Account.PublicKey;

        /// <summary>
        /// Gets the keypair of the Wallet.
        /// </summary>
        public Account WalletAccount => Wallet.Account;

        /// <summary>
        /// Public constructor, takes programId as a string. 
        /// </summary>
        /// <param name="programId">String representation of Whirlpools program id.</param>
        /// <param name="rpcClient">Object that can make RPC calls to Solana programs.</param>
        /// <param name="streamingClient">Object that can interact with Solana programs via websocket.</param>
        /// <param name="wallet">Wallet object.</param>
        public TestWhirlpoolContext(
            string programId,
            IRpcClient rpcClient,
            IStreamingRpcClient streamingClient,
            Wallet.Wallet wallet
        ) :
            this(new PublicKey(programId), rpcClient, streamingClient, wallet)
        {
        }

        /// <summary>
        /// Public constructor, takes programId as a PublicKey. 
        /// </summary>
        /// <param name="programId">PublicKey representation of Whirlpools program id.</param>
        /// <param name="rpcClient">Object that can make RPC calls to Solana programs.</param>
        /// <param name="streamingClient">Object that can interact with Solana programs via websocket.</param>
        /// <param name="wallet">Wallet object.</param>
        /// <param name="defaultCommitment">The default commitment</param>
        public TestWhirlpoolContext(
            PublicKey programId,
            IRpcClient rpcClient,
            IStreamingRpcClient streamingClient,
            Wallet.Wallet wallet,
            Commitment defaultCommitment = Commitment.Finalized
        )
        {
            ProgramId = programId;
            Wallet = wallet;
            WhirlpoolClient = new WhirlpoolClient(rpcClient, streamingClient, programId)
            {
                DefaultCommitment = defaultCommitment
            };
        }

        public byte[] Sign(byte[] msg)
        {
            return WalletAccount.Sign(msg);
        }
    }
}