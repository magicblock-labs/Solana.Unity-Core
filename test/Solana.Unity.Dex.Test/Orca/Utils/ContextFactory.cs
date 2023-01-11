using Solana.Unity.Rpc;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public enum SolEnv 
    {
        LocalNet, 
        DevNet,
        TestNet,
        MainNet
    }
    
    public static class ContextFactory
    {
        public static TestWhirlpoolContext CreateTestWhirlpoolContext(SolEnv env)
        {
            //defaults 
            IRpcClient rpcClient;
            IStreamingRpcClient wsClient;
            PublicKey programId = AddressConstants.WHIRLPOOLS_PUBKEY;

            Cluster? cluster = env switch
            {
                SolEnv.DevNet => Cluster.DevNet,
                SolEnv.TestNet => Cluster.TestNet,
                SolEnv.MainNet => Cluster.MainNet,
                _ => null
            };

            if (cluster != null) {
                rpcClient = ClientFactory.GetClient(cluster.Value);
                wsClient = ClientFactory.GetStreamingClient(cluster.Value);
            }
            else {
                rpcClient = ClientFactory.GetClient("http://localhost:8899");
                wsClient = ClientFactory.GetStreamingClient("http://localhost:8900");
                programId = TestConfiguration.LocalNetWhirlpoolAddress;
            }

            return new TestWhirlpoolContext(
                programId, 
                rpcClient, 
                wsClient,
                CreateTestWallet(),
                TestConfiguration.DefaultCommitment
            ); 
        }

        private static Wallet.Wallet CreateTestWallet()
        {
            return TestConfiguration.TestWallet;
        }
    }
}