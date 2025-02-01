using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Bip39;
using System;
using System.Threading.Tasks;

namespace Solana.Unity.Examples
{
    public class HelloWorldExample : IExample
    {
        public async void Run()
        {
            var wallet = new Wallet.Wallet(WordCount.TwentyFour, WordList.English);

            Console.WriteLine("Hello World!");
            Console.WriteLine($"Mnemonic: {wallet.Mnemonic}");
            Console.WriteLine($"PubKey: {wallet.Account.PublicKey.Key}");
            Console.WriteLine($"PrivateKey: {wallet.Account.PrivateKey.Key}");

            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

            var balance = await rpcClient.GetBalanceAsync(wallet.Account.PublicKey);

            Console.WriteLine($"Balance: {balance.Result.Value}");

            var transactionHash = await rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);

            Console.WriteLine($"TxHash: {transactionHash.Result}");

            IStreamingRpcClient streamingRpcClient = ClientFactory.GetStreamingClient(Cluster.TestNet);

            streamingRpcClient.ConnectAsync().Wait();

            var subscription = streamingRpcClient.SubscribeSignature(transactionHash.Result, async (sub, data) =>
            {
                if (data.Value.Error == null)
                {
                    balance = await rpcClient.GetBalanceAsync(wallet.Account.PublicKey);

                    Console.WriteLine($"Balance: {balance.Result.Value}");

                    var memoInstruction = MemoProgram.NewMemoV2("Hello Solana World, using Solana.Unity :)");

                    var latestBlockHashItem = await rpcClient.GetLatestBlockHashAsync();

                    var tx = new TransactionBuilder().AddInstruction(memoInstruction).SetFeePayer(wallet.Account)
                        .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash).Build(wallet.Account);

                    var txHash = await rpcClient.SendTransactionAsync(tx);

                    Console.WriteLine($"TxHash: {txHash.Result}");
                }
                else
                {
                    Console.WriteLine($"Transaction error: {data.Value.Error.Type}");
                }
            });

            Console.ReadLine();
        }
    }
}