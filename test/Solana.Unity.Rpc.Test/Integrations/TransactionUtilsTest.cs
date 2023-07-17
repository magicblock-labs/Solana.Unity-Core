using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solana.Unity.Rpc.Test.Integrations;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using System.Threading.Tasks;

namespace Solana.Unity.Rpc.Test
{
    [TestClass]
    public class TransactionUtilsTest : SolanaRpcClientTestBase
    {
        [TestMethod]
        public async Task TestConfirmMainnetTransactionConfirmed()
        {
            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.MainNet);
            var commitment = Commitment.Confirmed;
            string txSignature =
                "5LK4C9kkcdFY9Qgu9QYJzFkn63HCjHHvhiVRrYn6FGJgz7HdnukKsVXSkFixbokbwjFnUBmo1SvUBTkSh6w9uaSR";
            var confirmed = await rpcClient.ConfirmTransaction(txSignature, commitment);
            Assert.IsTrue(confirmed);
            var txRes = await rpcClient.GetTransactionAsync(txSignature, Commitment.Confirmed);
            Assert.IsTrue(txRes.WasSuccessful);
        }
        
        [TestMethod]
        public async Task TestInvalidMainnetTransaction()
        {
            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.MainNet);
            var commitment = Commitment.Confirmed;
            string txSignature = "sdfsdfsdfsf";
            var confirmed = await rpcClient.ConfirmTransaction(txSignature, commitment);
            Assert.IsFalse(confirmed);
        }
        
        [TestMethod]
        public async Task TestConfirmFailedTransaction()
        {
            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.MainNet);
            var commitment = Commitment.Finalized;
            string txSignature =
                "2kiCpX5eBCLF58mGCiU2mbfoWvNvWTT5DETji7g2khRzmjVu4EvzjrnYp3mTR4wKLcEzvBP1mYqaUX2vCMbyGMjR";
            var confirmed = await rpcClient.ConfirmTransaction(txSignature, commitment);
            Assert.IsTrue(confirmed);
        }
        
        [TestMethod]
        public async Task TestConfirmMainnetTransactionFinalized()
        {
            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.MainNet);
            var commitment = Commitment.Finalized;
            string txSignature =
                "5LK4C9kkcdFY9Qgu9QYJzFkn63HCjHHvhiVRrYn6FGJgz7HdnukKsVXSkFixbokbwjFnUBmo1SvUBTkSh6w9uaSR";
            var confirmed = await rpcClient.ConfirmTransaction(txSignature, commitment);
            Assert.IsTrue(confirmed);
            var txRes = await rpcClient.GetTransactionAsync(txSignature, Commitment.Confirmed);
            Assert.IsTrue(txRes.WasSuccessful);
        }
        
        [TestMethod]
        public async Task TestConfirmTransactionConfirmed()
        {
            IRpcClient rpcClient = TestConfiguration.RpcClient();
            var commitment = Commitment.Confirmed;
            Account account1 = new();
            var res = await rpcClient.RequestAirdropAsync(
                account1.PublicKey, 
                1000000000, 
                commitment
            );
            await rpcClient.ConfirmTransaction(res.Result, commitment);
            var txRes = await rpcClient.GetTransactionAsync(res.Result, Commitment.Confirmed);
            Assert.IsTrue(txRes.WasSuccessful);
        }
        
    }
}