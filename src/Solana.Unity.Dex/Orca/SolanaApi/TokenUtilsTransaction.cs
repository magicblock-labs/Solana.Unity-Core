using Orca;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Programs;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Core.Http;

namespace Solana.Unity.Dex.Orca.SolanaApi
{
    /// <summary>
    /// Interacts with the Token program. 
    /// </summary>
    public static class TokenUtilsTransaction
    {

        /// <summary>
        /// Creates a mint account on the Solana Token Program for a token. 
        /// </summary>
        /// <param name="rpc">The connected rpc.</param>
        /// <param name="authority">The mint authority</param>
        /// <param name="mintAccount">Public key of the mint account to create.</param>
                /// <returns></returns>
        public static async Task<Transaction> CreateMint(
            IRpcClient rpc,
            Account authority,
            Account mintAccount) 
        {
            ulong minBalanceForExemptionMint =
                (await rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;

            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await rpc.GetLatestBlockHashAsync();
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
                .SetFeePayer(authority)
                .AddInstruction(SystemProgram.CreateAccount(
                    fromAccount: authority,
                    newAccountPublicKey: mintAccount,
                    lamports: minBalanceForExemptionMint,
                    space: TokenProgram.MintAccountDataSize,
                    programId: TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeMint(
                    mint: mintAccount,
                    decimals: 0,
                    mintAuthority: authority))
                .Build(new List<Account> { authority, mintAccount });
            return Transaction.Deserialize(tx); 
        }

        /// <summary>
        /// Transaction to create an associated token account and mints a given number of tokens to it.
        /// </summary>
        /// <param name="rpc">The connected rpc.</param>
        /// <param name="mint">Address of the token mint.</param>
        /// <param name="amount">Number of tokens to mint.</param>
        /// <param name="feePayer">Account to pay transaction fees.</param>
        /// <param name="destination">Account to which to mint.</param>
        /// <param name="commitment"></param>
        /// <returns>Address of the token account.</returns>
        public static async Task<Transaction> CreateAndMintToAssociatedTokenAccount (
            IRpcClient rpc, 
            PublicKey mint, 
            BigInteger amount, 
            Account feePayer,
            PublicKey destination,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey associatedTokenAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(destination, mint);
            Transaction transaction;
            
            //special handling for native mint 
            if (mint.Equals(AddressConstants.NATIVE_MINT_PUBKEY)) 
            {
                ulong rentExemptionMin =
                    (await rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;
                
                var tempAccount = new Account();
                var sta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(tempAccount, mint);
                RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await rpc.GetLatestBlockHashAsync();
                var trxBuild=  new TransactionBuilder()    
                    .SetFeePayer(feePayer.PublicKey)
                    .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
                    .AddInstruction(SystemProgram.CreateAccount(
                        feePayer,
                        tempAccount,
                        (ulong)(amount + rentExemptionMin),
                        TokenProgram.TokenAccountDataSize,   
                        TokenProgram.ProgramIdKey))
                    .AddInstruction(TokenProgram.InitializeAccount(
                        tempAccount,
                        AddressConstants.NATIVE_MINT_PUBKEY,
                        feePayer));
                bool exists = await TokenAccountExists(
                    rpc, associatedTokenAddress, commitment: commitment
                );
                if (!exists)
                {
                    trxBuild.AddInstruction(
                        AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                            feePayer, destination, mint
                        )
                    );
                }
                var trx = trxBuild.AddInstruction(
                    AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        feePayer, tempAccount, mint
                    )
                )
                .AddInstruction(TokenProgram.SyncNative(sta))
                .AddInstruction(TokenProgram.Transfer(
                    tempAccount,
                    associatedTokenAddress,
                    (ulong)amount,
                    feePayer))
                .AddInstruction(TokenProgram.SyncNative(associatedTokenAddress))
                .Build(new List<Account>(){feePayer, tempAccount});
                transaction = Transaction.Deserialize(trx);
            }
            else
            {
                RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await rpc.GetLatestBlockHashAsync();
                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
                    .SetFeePayer(feePayer.PublicKey)
                    .AddInstruction(
                        AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                            feePayer, destination, mint
                        )
                    )
                    .AddInstruction(TokenProgram.MintTo(
                        mint,
                        associatedTokenAddress,
                        (ulong)amount,
                        feePayer
                    ));
                if (mint.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
                {
                    txBuilder.AddInstruction(TokenProgram.SyncNative(associatedTokenAddress));
                }
                var tx= txBuilder.Build(feePayer);
                transaction = Transaction.Deserialize(tx);
            }

            return transaction;
        }


        /// <summary>
        /// Mint tokens to an owner account. 
        /// </summary>
        /// <param name="rpc">The connected rpc.</param>
        /// <param name="mint"></param>
        /// <param name="destination"></param>
        /// <param name="amount"></param>
        /// <param name="feePayer"></param>
        /// <returns></returns>
        public static async Task<Transaction> MintToByAuthority(
            IRpcClient rpc,
            PublicKey mint,
            PublicKey destination,
            BigInteger amount,
            Account feePayer
        )
        {
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await rpc.GetLatestBlockHashAsync();
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
                .SetFeePayer(feePayer.PublicKey)
                .AddInstruction(TokenProgram.MintTo(
                    mint,
                    destination,
                    (ulong)amount,
                    feePayer))
                .Build(feePayer);

            return Transaction.Deserialize(tx);
        }
        
        /// <summary>
        /// Determines whether a token account with the given address exists.
        /// </summary>
        /// <param name="rpc">The rpc istance</param>
        /// <param name="accountKey">Public key of token account.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <returns>True if account is found to exist.</returns>
        public static async Task<bool> TokenAccountExists(
            IRpcClient rpc,
            PublicKey accountKey,
            Commitment commitment
        )
        {
            var accountInfoResult = await rpc.GetAccountInfoAsync(accountKey.ToString(), commitment); 
            AccountInfo accountInfo = null; 
            if (accountInfoResult.WasSuccessful) 
            {
                accountInfo = accountInfoResult.Result.Value;
            }
            
            return (accountInfo != null);
        }
    }
}