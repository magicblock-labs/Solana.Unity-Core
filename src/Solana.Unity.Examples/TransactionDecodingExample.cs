using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solana.Unity.Examples
{
    public class TransactionDecodingExample : IExample
    {

        private static readonly IRpcClient RpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
            "route clerk disease box emerge airport loud waste attitude film army tray " +
            "forward deal onion eight catalog surface unit card window walnut wealth medal";

        public async void Run()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);

            ulong minBalanceForExemptionAcc =
                (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;
            ulong minBalanceForExemptionMint =
                (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize)).Result;

            Console.WriteLine($"MinBalanceForRentExemption Account >> {minBalanceForExemptionAcc}");
            Console.WriteLine($"MinBalanceForRentExemption Mint Account >> {minBalanceForExemptionMint}");

            Account ownerAccount = wallet.GetAccount(10);
            Account mintAccount = wallet.GetAccount(1002);
            Account initialAccount = wallet.GetAccount(1102);
            Console.WriteLine($"OwnerAccount: {ownerAccount}");
            Console.WriteLine($"MintAccount: {mintAccount}");
            Console.WriteLine($"InitialAccount: {initialAccount}");

            var latestBlockHashItem = await RpcClient.GetLatestBlockHashAsync();

            byte[] msgData = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
                .SetFeePayer(ownerAccount)
                .AddInstruction(TokenProgram.InitializeMint(
                    mintAccount.PublicKey,
                    2,
                    ownerAccount.PublicKey,
                    ownerAccount.PublicKey))
                .AddInstruction(SystemProgram.AllocateWithSeed(
                    new PublicKey("EME9GxLahsC1mjopepKMJg9RtbUu37aeLaQyHVdEd7vZ"),
                    new PublicKey("Gg12mmahG97PDACxKiBta7ch2kkqDkXUzjn5oAcbPZct"),
                    "Some Seed",
                    165UL,
                    new PublicKey("J6WZY5nuYGJmfFtBGZaXgwZSRVuLWxNR6gd4d3XTHqTk")))
                .AddInstruction(SystemProgram.TransferWithSeed(
                    new PublicKey("Gg12mmahG97PDACxKiBta7ch2kkqDkXUzjn5oAcbPZct"),
                    new PublicKey("EME9GxLahsC1mjopepKMJg9RtbUu37aeLaQyHVdEd7vZ"),
                    "Some Seed",
                    new PublicKey("5omQJtDUHA3gMFdHEQg1zZSvcBUVzey5WaKWYRmqF1Vj"),
                    new PublicKey("EME9GxLahsC1mjopepKMJg9RtbUu37aeLaQyHVdEd7vZ"),
                    25000UL))
                .AddInstruction(TokenProgram.InitializeAccount(
                    initialAccount.PublicKey,
                    mintAccount.PublicKey,
                    ownerAccount.PublicKey))
                .AddInstruction(TokenProgram.MintTo(
                    mintAccount.PublicKey,
                    initialAccount.PublicKey,
                    1_000_000,
                    ownerAccount))
                .AddInstruction(MemoProgram.NewMemo(initialAccount, "Hello from Sol.Net"))
                .CompileMessage();


            Console.WriteLine($"Message: {Convert.ToBase64String(msgData)}");
            var txx = Transaction.Populate(Convert.ToBase64String(msgData),
                new List<byte[]> { ownerAccount.Sign(msgData), mintAccount.Sign(msgData), initialAccount.Sign(msgData) });

            byte[] txBytes = txx.Serialize();

            var txSim = await RpcClient.SimulateTransactionAsync(txBytes);
            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);

            Console.WriteLine("\tDECODING TRANSACTION FROM WIRE FORMAT\t");
            Transaction tx = Transaction.Deserialize(txBytes);
            Console.WriteLine($"FeePayer: {tx.FeePayer}");
            Console.WriteLine($"BlockHash/Nonce: {tx.RecentBlockHash}");
            foreach (SignaturePubKeyPair signaturePubKeyPair in tx.Signatures)
            {
                Console.WriteLine(
                    $"Signer: {signaturePubKeyPair.PublicKey} \tSignature: {Encoders.Base58.EncodeData(signaturePubKeyPair.Signature)}");
            }

            foreach (TransactionInstruction txInstruction in tx.Instructions)
            {
                Console.WriteLine(
                    $"ProgramKey: {Encoders.Base58.EncodeData(txInstruction.ProgramId)}\n\tInstructionData: {Convert.ToBase64String(txInstruction.Data)}");
                foreach (AccountMeta accountMeta in txInstruction.Keys)
                {
                    Console.WriteLine(
                        $"\tAccountMeta: {accountMeta.PublicKey}\tWritable: {accountMeta.IsWritable}\tSigner: {accountMeta.IsSigner}");
                }
            }

            var txDecBytes = tx.Serialize();
            var txDecSim = await RpcClient.SimulateTransactionAsync(txDecBytes);
            string decLogs = Examples.PrettyPrintTransactionSimulationLogs(txDecSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txDecSim.Result.Value.Error}\n\tLogs: \n" + decLogs);
        }
    }
}