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
            
            var blockHash = await rpc.GetRecentBlockHashAsync();
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
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
        /// <returns>Address of the token account.</returns>
        public static async Task<Transaction> CreateAndMintToAssociatedTokenAccount (
            IRpcClient rpc, 
            PublicKey mint, 
            BigInteger amount, 
            Account feePayer,
            PublicKey destination
        )
        {
            PublicKey associatedTokenAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(destination, mint);
            Transaction transaction;
            
            //special handling for native mint 
            if (mint.Equals(AddressConstants.NATIVE_MINT_PUBKEY)) 
            {
                ulong rentExemptionMin =
                    (await rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;
                
                transaction = CreateWSolAccountTransaction(destination, BigInteger.Zero, rentExemptionMin, associatedTokenAddress);
            }
            else
            {
                var blockHash = await rpc.GetRecentBlockHashAsync();
                byte[] tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
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
                    ))
                    .Build(feePayer);
                transaction = Transaction.Deserialize(tx);
            }

            return transaction;
        }
        
        
        /// <summary>
        /// Creates and returns a transaction to create an account and initialize it with the native
        /// mint address. 
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="amountIn"></param>
        /// <param name="rentExemptLamports">Minimum balance to avoid rent payment for account.</param>
        /// <param name="associatedTokenAccountAddress">Address of the associated token account</param>
        /// <returns>Raw bytes of the new transaction. </returns>
        private static Transaction CreateWSolAccountTransaction(
            PublicKey walletAddress, 
            BigInteger amountIn, 
            ulong rentExemptLamports,
            PublicKey associatedTokenAccountAddress
        )
        {

            var trx=  new TransactionBuilder()    
                .AddInstruction(SystemProgram.CreateAccount(
                    walletAddress,
                    associatedTokenAccountAddress,
                    (ulong)(amountIn + rentExemptLamports),
                    TokenProgram.MintAccountDataSize,   
                    TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeAccount(
                    associatedTokenAccountAddress,
                    AddressConstants.NATIVE_MINT_PUBKEY,
                    walletAddress)
                ).Build(new List<Account>());
            return Transaction.Deserialize(trx);
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
            var blockHash = await rpc.GetRecentBlockHashAsync();
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(feePayer.PublicKey)
                .AddInstruction(TokenProgram.MintTo(
                    mint,
                    destination,
                    (ulong)amount,
                    feePayer))
                .Build(feePayer);

            return Transaction.Deserialize(tx);
        }
    }
}