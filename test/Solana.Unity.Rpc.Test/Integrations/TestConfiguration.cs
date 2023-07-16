using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Rpc.Test.Integrations
{
    //TODO: (LOW) read this all in from file
    /// <summary>
    /// Configuration settings necessary to run tests. 
    /// </summary>
    public static class TestConfiguration
    {
        /// <summary>
        /// This commitment will be used by default (unless otherwise explicitly specified in a call) for each transaction 
        /// and preflight simulation called through WhirlpoolClient. 
        /// </summary>
        public static Commitment DefaultCommitment => Commitment.Confirmed;

        private static readonly byte[] TestSecretByteArray =
        {
            186, 78, 68, 54, 63, 205, 1, 141, 2, 89, 45, 80, 77, 168, 215, 120, 56, 57, 72, 222, 50, 140, 31, 236,
            254, 35, 208, 163, 138, 186, 225, 18, 67, 194, 241, 235, 28, 5, 209, 235, 248, 58, 150, 42, 218, 71, 43,
            177, 183, 62, 55, 96, 216, 41, 59, 146, 121, 132, 223, 24, 39, 109, 3, 163,
        };
        public static Wallet.Wallet TestWallet => new(TestSecretByteArray, seedMode: SeedMode.Bip39);

        public static IRpcClient RpcClient()
        {
            return ClientFactory.GetClient("http://localhost:8899");
        }
        
        public static IStreamingRpcClient WsRpcClient()
        {
            return ClientFactory.GetStreamingClient("http://localhost:8900");
        }

    }
}