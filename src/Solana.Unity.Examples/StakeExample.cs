using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System;
using System.Collections.Generic;
using static Solana.Unity.Programs.Models.Stake.State;
using Solana.Unity.Wallet.Bip39;
using Solana.Unity.Wallet.Utilities;
using System.Threading.Tasks;

namespace Solana.Unity.Examples
{
    public class CreateAccountFromSeedExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";
        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            await rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();
            ulong minBalance = (await rpcClient.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;
            Account fromAccount = wallet.Account;
            PublicKey.TryCreateWithSeed(fromAccount.PublicKey, "yrdy1", StakeProgram.ProgramIdKey, out PublicKey stakeAccount);
            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.CreateAccountWithSeed(
                     fromAccount,
                     stakeAccount,
                     fromAccount,
                     "yrdy1",
                     3 * minBalance,
                     200,
                     StakeProgram.ProgramIdKey))
                .Build(new List<Account> { fromAccount });
            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx, skipPreflight: true);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
    public class AuthorizeWithSeedExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";
        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            var seed = wallet.DeriveMnemonicSeed();
            var b58 = new Base58Encoder();
            string f = b58.EncodeData(seed);
            rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();
            ulong minbalanceforexception = (await rpcClient.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;
            Account fromAccount = wallet.Account;
            Account toAccount = wallet.GetAccount(1);
            rpcClient.RequestAirdropAsync(toAccount.PublicKey, 100_000_000);
            PublicKey.TryCreateWithSeed(fromAccount.PublicKey, "dog5", StakeProgram.ProgramIdKey, out PublicKey stakeAccount);

            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(StakeProgram.AuthorizeWithSeed(
                    stakeAccount,
                    fromAccount,
                    f,
                    fromAccount,
                    toAccount,
                    StakeAuthorize.Staker,
                    fromAccount))
                .Build(new List<Account> { fromAccount });

            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx, skipPreflight: true);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
    public class AuthorizeExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";
        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();

            Account fromAccount = wallet.Account;
            Account toAccount = wallet.GetAccount(1);
            PublicKey.TryCreateWithSeed(fromAccount.PublicKey, "dog1", StakeProgram.ProgramIdKey, out PublicKey stakeAccount);

            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(StakeProgram.Authorize(
                    stakeAccount,
                    fromAccount,
                    toAccount,
                    StakeAuthorize.Staker,
                    fromAccount))
                .Build(new List<Account> { fromAccount });
            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx, skipPreflight: true);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
    public class CreateAccountWithSeedAndInitializeStakeExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";
        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();
            ulong minbalanceforexception = (await rpcClient.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;
            Account fromAccount = wallet.Account;
            PublicKey.TryCreateWithSeed(fromAccount.PublicKey, "dog5", StakeProgram.ProgramIdKey, out PublicKey stakeAccount);
            Authorized authorized = new Authorized()
            {
                Staker = fromAccount,
                Withdrawer = fromAccount
            };
            Lockup lockup = new Lockup()
            {
                Custodian = fromAccount.PublicKey,
                Epoch = 0,
                UnixTimestamp = 0
            };

            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.CreateAccountWithSeed(
                    fromAccount.PublicKey,
                    stakeAccount,
                    fromAccount.PublicKey,
                    "dog5",
                    333 * minbalanceforexception + 42,
                    StakeProgram.StakeAccountDataSize,
                    StakeProgram.ProgramIdKey))
                .AddInstruction(StakeProgram.Initialize(
                    stakeAccount,
                    authorized,
                    lockup))
                .Build(new List<Account> { fromAccount });

            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
    public class CreateAccountAndInitializeStakeExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";

        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();
            ulong minbalanceforexception = (await rpcClient.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;
            Account fromAccount = wallet.Account;
            Account stakeAccount = wallet.GetAccount(22);

            Authorized authorized = new Authorized()
            {
                Staker = fromAccount,
                Withdrawer = fromAccount
            };
            Lockup lockup = new Lockup()
            {
                Custodian = fromAccount.PublicKey,
                Epoch = 0,
                UnixTimestamp = 0
            };

            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.CreateAccount(
                    fromAccount.PublicKey,
                    stakeAccount,
                    minbalanceforexception + 42,
                    StakeProgram.StakeAccountDataSize,
                    StakeProgram.ProgramIdKey))
                .AddInstruction(StakeProgram.Initialize(
                    stakeAccount.PublicKey,
                    authorized,
                    lockup))
                .Build(new List<Account> { fromAccount, stakeAccount });
            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
    public class MasterStakeBytesExample : IExample
    {
        private static readonly IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
           "clerk shoe noise umbrella apple gold alien swap desert rubber truck okay twenty fiscal near talent drastic present leg put balcony leader access glimpse";

        public async void Run()
        {
            var wallet = new Wallet.Wallet(new Mnemonic(MnemonicWords));
            rpcClient.RequestAirdropAsync(wallet.Account.PublicKey, 100_000_000);
            RequestResult<ResponseValue<BlockHash>> blockHash = await rpcClient.GetRecentBlockHashAsync();
            ulong minBalance = (await rpcClient.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;

            Account a6 = wallet.GetAccount(6);
            Account a5 = wallet.GetAccount(5);
            Account a4 = wallet.GetAccount(4);
            Account a3 = wallet.GetAccount(3);

            Console.WriteLine($"BlockHash >> {blockHash.Result.Value.Blockhash}");

            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(a6)
                .AddInstruction(SystemProgram.CreateAccountWithSeed(
                    a6.PublicKey,
                    a5,
                    a6,
                    "dog1",
                    3 * minBalance + 42,
                    StakeProgram.StakeAccountDataSize,
                    StakeProgram.ProgramIdKey))
                .AddInstruction(SystemProgram.Transfer(
                    a6,
                    a5,
                    5
                    ))
                .CompileMessage();
            Console.WriteLine($"Tx base64: {Convert.ToBase64String(tx)}");
            RequestResult<ResponseValue<SimulationLogs>> txSim = await rpcClient.SimulateTransactionAsync(tx);

            string logs = Examples.PrettyPrintTransactionSimulationLogs(txSim.Result.Value.Logs);
            Console.WriteLine($"Transaction Simulation:\n\tError: {txSim.Result.Value.Error}\n\tLogs: \n" + logs);
            RequestResult<string> firstSig = await rpcClient.SendTransactionAsync(tx);
            Console.WriteLine($"First Tx Result: {firstSig.Result}");
        }
    }
}
