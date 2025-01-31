using Solana.Unity.Programs;
using Solana.Unity.Programs.TokenSwap;
using Solana.Unity.Programs.TokenSwap.Models;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System;

namespace Solana.Unity.Examples
{
    public class TokenSwapExample : IExample
    {

        private static readonly IRpcClient RpcClient = ClientFactory.GetClient(Cluster.TestNet);

        private const string MnemonicWords =
            "route clerk disease box emerge airport loud waste attitude film army tray " +
            "forward deal onion eight catalog surface unit card window walnut wealth medal";

        public async void Run()
        {
            //how to load current state
            IRpcClient mainnetRpc = ClientFactory.GetClient(Cluster.MainNet);
            var resp = await mainnetRpc.GetAccountInfoAsync("GAM8dQkm4LwYJgPZbML61mKPUCQX7uAquxu67p9oifSK");
            var obj = TokenSwapAccount.Deserialize(Convert.FromBase64String(resp.Result.Value.Data[0]));
            Console.WriteLine($"Pool Mint: {obj.PoolMint}");

            Wallet.Wallet wallet = new Wallet.Wallet(MnemonicWords);

            var tokenAMint = new Account();
            var tokenAUserAccount = new Account();
            var tokenBMint = new Account();
            var tokenBUserAccount = new Account();

            //setup some mints and tokens owned by wallet
            var tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    tokenAMint,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize)).Result,
                    TokenProgram.MintAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    tokenBMint,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize)).Result,
                    TokenProgram.MintAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    tokenAUserAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    tokenBUserAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeMint(
                    tokenAMint,
                    9,
                    wallet.Account
                ))
                .AddInstruction(TokenProgram.InitializeMint(
                    tokenBMint,
                    9,
                    wallet.Account
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    tokenAUserAccount,
                    tokenAMint,
                    wallet.Account
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    tokenBUserAccount,
                    tokenBMint,
                    wallet.Account
                ))
                .AddInstruction(TokenProgram.MintTo(
                    tokenAMint,
                    tokenAUserAccount,
                    1_000_000_000_000,
                    wallet.Account
                ))
                .AddInstruction(TokenProgram.MintTo(
                    tokenBMint,
                    tokenBUserAccount,
                    1_000_000_000_000,
                    wallet.Account
                ))
                .Build(new Account[] { wallet.Account, tokenAMint, tokenBMint, tokenAUserAccount, tokenBUserAccount });
            var txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            var swap = new Account();
            var program = new TokenSwapProgram();
            var swapAuthority = program.CreateAuthority(swap).pubkey;

            var swapTokenAAccount= new Account();
            var swapTokenBAccount = new Account();

            //init the swap authority's token accounts
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    swapTokenAAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    swapTokenAAccount,
                    tokenAMint,
                    swapAuthority
                ))
                .AddInstruction(TokenProgram.Transfer(
                    tokenAUserAccount,
                    swapTokenAAccount,
                    5_000_000_000,
                    wallet.Account
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    swapTokenBAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    swapTokenBAccount,
                    tokenBMint,
                    swapAuthority
                ))
                .AddInstruction(TokenProgram.Transfer(
                    tokenBUserAccount,
                    swapTokenBAccount,
                    5_000_000_000,
                    wallet.Account
                ))
                .Build(new Account[] { wallet.Account, swapTokenAAccount, swapTokenBAccount });
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            var poolMint = new Account();
            var poolUserAccount = new Account();
            var poolFeeAccount = new Account();

            //create the pool mint and the user and fee pool token accounts
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    poolMint,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize)).Result,
                    TokenProgram.MintAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeMint(
                    poolMint,
                    9,
                    swapAuthority
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    poolUserAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    poolUserAccount,
                    poolMint,
                    wallet.Account
                ))
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    poolFeeAccount,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    poolFeeAccount,
                    poolMint,
                    program.OwnerKey
                ))
                .Build(new Account[] { wallet.Account, poolMint, poolUserAccount, poolFeeAccount });
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //create the swap
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(SystemProgram.CreateAccount(
                    wallet.Account,
                    swap,
                    (await RpcClient.GetMinimumBalanceForRentExemptionAsync((long)TokenSwapProgram.TokenSwapAccountDataSize)).Result,
                    TokenSwapProgram.TokenSwapAccountDataSize,
                    program.ProgramIdKey
                ))
                .AddInstruction(program.Initialize(
                    swap,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    poolMint,
                    poolFeeAccount,
                    poolUserAccount,
                    new Fees()
                    {
                        TradeFeeNumerator = 25,
                        TradeFeeDenominator = 10000,
                        OwnerTradeFeeNumerator = 5,
                        OwnerTradeFeeDenomerator = 10000,
                        OwnerWithrawFeeNumerator = 0,
                        OwnerWithrawFeeDenomerator = 0,
                        HostFeeNumerator = 20,
                        HostFeeDenomerator = 100
                    },
                    SwapCurve.ConstantProduct
                ))
                .Build(new Account[] { wallet.Account, swap });
            Console.WriteLine($"Swap Account: {swap}");
            Console.WriteLine($"Swap Auth Account: {swapAuthority}");
            Console.WriteLine($"Pool Mint Account: {poolMint}");
            Console.WriteLine($"Pool User Account: {poolUserAccount}");
            Console.WriteLine($"Pool Fee Account: {poolFeeAccount}");
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //now a user can swap in the pool
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(program.Swap(
                    swap,
                    wallet.Account,
                    tokenAUserAccount,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    tokenBUserAccount,
                    poolMint,
                    poolFeeAccount,
                    null,
                    1_000_000_000,
                    500_000))
                .Build(wallet.Account);
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //user can add liq
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(program.DepositAllTokenTypes(
                    swap,
                    wallet.Account,
                    tokenAUserAccount,
                    tokenBUserAccount,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    poolMint,
                    poolUserAccount,
                    1_000_000,
                    100_000_000_000,
                    100_000_000_000))
                .Build(wallet.Account);
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //user can remove liq
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(program.WithdrawAllTokenTypes(
                    swap,
                    wallet.Account,
                    poolMint,
                    poolUserAccount,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    tokenAUserAccount,
                    tokenBUserAccount,
                    poolFeeAccount,
                    1_000_000,
                    1_000,
                    1_000))
                .Build(wallet.Account);
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //user can deposit single
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(program.DepositSingleTokenTypeExactAmountIn(
                    swap,
                    wallet.Account,
                    tokenAUserAccount,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    poolMint,
                    poolUserAccount,
                    1_000_000_000,
                    1_000))
                .Build(wallet.Account);
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);

            //user can withdraw single
            tx = new TransactionBuilder()
                .SetRecentBlockHash(RpcClient.GetLatestBlockHashAsync().Result.Result.Value.Blockhash)
                .SetFeePayer(wallet.Account)
                .AddInstruction(program.WithdrawSingleTokenTypeExactAmountOut(
                    swap,
                    wallet.Account,
                    poolMint,
                    poolUserAccount,
                    swapTokenAAccount,
                    swapTokenBAccount,
                    tokenAUserAccount,
                    poolFeeAccount,
                    1_000_000,
                    100_000))
                .Build(wallet.Account);
            txSig = await Examples.SubmitTxSendAndLog(tx);
            Examples.PollConfirmedTx(txSig);
        }
    }
}