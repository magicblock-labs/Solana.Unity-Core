using NUnit.Framework;
using Solana.Unity.Dex.Orca;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Rpc.Models;

//TODO: (MID) standardize these methods: parameter names, usage, ordering, formatting, calling, everything 
namespace Solana.Unity.Dex.Test.Orca.Utils
{
    /// <summary>
    /// Interacts with the Token program. 
    /// </summary>
    public static class TokenUtils
    {
        /// <summary>
        /// Creates a mint account on the Solana Token Program for a token. 
        /// </summary>
        /// <param name="ctx">Application context.</param>
        /// <param name="authority">The mint authority</param>
        /// <param name="mintAccount">The mint account</param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<PublicKey> CreateMintAsync(
            IWhirlpoolContext ctx,
            Account authority,
            Account mintAccount = null,
            Commitment commitment = Commitment.Finalized
        ) 
        {
            ulong minBalanceForExemptionMint =
                (await ctx.RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;

            mintAccount = (mintAccount == null) ? new Account() : mintAccount;
            Account ownerAccount = authority;

            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(authority)
                .AddInstruction(SystemProgram.CreateAccount(
                    fromAccount: ownerAccount.PublicKey,
                    newAccountPublicKey: mintAccount.PublicKey,
                    lamports: minBalanceForExemptionMint,
                    space: TokenProgram.MintAccountDataSize,
                    programId: TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeMint(
                    mint: mintAccount.PublicKey,
                    decimals: 0,
                    mintAuthority: ownerAccount.PublicKey))
                .Build(new List<Account> { authority, mintAccount });

            await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);

            return mintAccount.PublicKey;
        }

        /// <summary>
        /// Create a new token account. 
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="mint"></param>
        /// <param name="ownerAccount"></param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<PublicKey> CreateTokenAccountAsync(
            IWhirlpoolContext ctx,
            PublicKey mint,
            Account ownerAccount,
            Commitment commitment = Commitment.Finalized
        )
        {
            Account tokenAccount = new Account();

            ulong minBalanceForExemption =
                (await ctx.RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;

            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(ownerAccount)
                .AddInstruction(SystemProgram.CreateAccount(
                    fromAccount: ownerAccount,
                    newAccountPublicKey: tokenAccount.PublicKey,
                    lamports: minBalanceForExemption,
                    space: TokenProgram.TokenAccountDataSize,
                    programId: AddressConstants.TOKEN_PROGRAM_PUBKEY
                ))
                .AddInstruction(TokenProgram.InitializeAccount(
                    tokenAccount.PublicKey,
                    mint, 
                    ownerAccount.PublicKey
                ))
                .Build(new List<Account> { ownerAccount, tokenAccount });

            var result = await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
            Assert.IsTrue(result.WasSuccessful, $"Failed to CreateTokenAccount: {result.Reason}");

            return tokenAccount.PublicKey;
        }
        
        /// <summary>
        /// Create a new token account, and mint a specified number of tokens to it. 
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="fromAccount"></param>
        /// <param name="mint"></param>
        /// <param name="amount"></param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<PublicKey> CreateAndMintToTokenAccountAsync(
            IWhirlpoolContext ctx,
            Account fromAccount,
            PublicKey mint, 
            BigInteger amount,
            Commitment commitment = Commitment.Finalized
        )
        {
            Account tokenAccount = new Account();

            ulong minBalanceForExemption =
                (await ctx.RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;

            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.CreateAccount(    //create account 
                    fromAccount: fromAccount,
                    newAccountPublicKey: tokenAccount.PublicKey,
                    lamports: minBalanceForExemption,
                    space: TokenProgram.TokenAccountDataSize,
                    programId: AddressConstants.TOKEN_PROGRAM_PUBKEY
                ))
                .AddInstruction(TokenProgram.InitializeAccount(     //initialize account 
                    tokenAccount.PublicKey,
                    mint,
                    ctx.WalletPubKey
                ))
                .AddInstruction(TokenProgram.MintTo(            //mint to account 
                    mint,
                    tokenAccount,
                    (ulong)amount,
                    ctx.WalletPubKey))
                .Build(new List<Account> { fromAccount, tokenAccount });

            var result = await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
            Assert.IsTrue(result.WasSuccessful, $"Failed to CreateTokenAccount: {result.Reason}");

            return tokenAccount.PublicKey;
        }

        /// <summary>
        /// Approve a token delegate spender.
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="tokenAccount"></param>
        /// <param name="delegateAccount"></param>
        /// <param name="amount"></param>
        /// <param name="ownerAccount"></param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<RequestResult<string>> ApproveTokenAsync(
            IWhirlpoolContext ctx, 
            PublicKey tokenAccount, 
            PublicKey delegateAccount,
            ulong amount, 
            Account ownerAccount,
            Commitment commitment = Commitment.Finalized
        )
        {
            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(ownerAccount)
                .AddInstruction(TokenProgram.Approve(
                    source: tokenAccount,
                    delegatePublicKey: delegateAccount,
                    authority: ownerAccount, 
                    amount))
                .Build(new List<Account> { ownerAccount });

            return await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
        }

        /// <summary>
        /// Changes an account with token authority (of specified type) for another account. 
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="tokenAccount"></param>
        /// <param name="newAuthority"></param>
        /// <param name="authorityType"></param>
        /// <param name="authorityAccount"></param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<RequestResult<string>> SetAuthorityAsync(
            IWhirlpoolContext ctx, 
            PublicKey tokenAccount,
            PublicKey newAuthority,
            AuthorityType authorityType,
            Account authorityAccount,
            Commitment commitment = Commitment.Finalized
        )
        {
            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);

            byte[] tx = new TransactionBuilder().SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(authorityAccount)
                .AddInstruction(TokenProgram.SetAuthority(
                    account: tokenAccount, 
                    authorityType,
                    authorityAccount,  
                    newAuthority,
                    signers: System.Array.Empty<PublicKey>()))
                .Build(new List<Account> { authorityAccount });

            return await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
        }

        //TODO: should return tuple with result 
        /// <summary>
        /// Creates an associated token account and mints a given number of tokens to it.
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="mint">Address of the token mint.</param>
        /// <param name="amount">Number of tokens to mint.</param>
        /// <param name="feePayer">Account to pay transaction fees.</param>
        /// <param name="destination">Account to which to mint.</param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns>Address of the token account.</returns>
        public static async Task CreateAndMintToAssociatedTokenAccountAsync(IWhirlpoolContext ctx,
            PublicKey mint,
            BigInteger amount,
            Account feePayer,
            PublicKey destination = null,
            Commitment commitment = Commitment.Finalized)
        {
            if (destination == null)
                destination = ctx.WalletPubKey;
                
            PublicKey associatedTokenAddress; 
            
            //special handling for native mint 
            if (mint.Equals(AddressConstants.NATIVE_MINT_PUBKEY)) 
            {
                ulong rentExemptionMin =
                    (await ctx.RpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize)).Result;
                
                byte[] tx = CreateWSolAccountInstructions(destination, BigInteger.Zero, rentExemptionMin, out associatedTokenAddress);
                RequestResult<string> response = await ctx.RpcClient.SendTransactionAsync(tx, commitment:commitment);                
            }
            else
            {
                associatedTokenAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(destination, mint);

                var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
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

                var result = await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
            }
        }

        /// <summary>
        /// Mint tokens to an owner account. 
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="mint"></param>
        /// <param name="destination"></param>
        /// <param name="amount"></param>
        /// <param name="feePayer"></param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<RequestResult<string>> MintToByAuthorityAsync(
            IWhirlpoolContext ctx,
            PublicKey mint,
            PublicKey destination,
            BigInteger amount,
            Account feePayer, 
            Commitment commitment = Commitment.Finalized
        )
        {
            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(feePayer.PublicKey)
                .AddInstruction(TokenProgram.MintTo(
                    mint,
                    destination,
                    (ulong)amount,
                    feePayer))
                .Build(feePayer);

            return await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
        }

        /// <summary>
        /// Create an associated token account for the given mint and owner. 
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="feePayer">Account to pay fees for the transaction.</param>
        /// <param name="mint">Address of token mint.</param>
        /// <param name="owner">Address of token owner.</param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<Tuple<RequestResult<string>, PublicKey>> CreateAssociatedTokenAccountAsync(
            IWhirlpoolContext ctx,
            Account feePayer,
            PublicKey mint,
            PublicKey owner = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            if (owner == null)
                owner = ctx.WalletPubKey;

            //derive the address 
            PublicKey associatedTokenAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint);

            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(feePayer.PublicKey)
                .AddInstruction(
                    AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        feePayer, owner, mint
                    )
                )
                .Build(feePayer);

            var result = await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
            return Tuple.Create<RequestResult<string>, PublicKey>(result, associatedTokenAddress); 
        }

        /// <summary>
        /// Transafer tokens from one token account to another.
        /// </summary>
        /// <param name="ctx">Application context object.</param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="amount"></param>
        /// <param name="feePayer">Account which will pay transaction fees.</param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns></returns>
        public static async Task<RequestResult<string>> TransferTokensAsync(
            IWhirlpoolContext ctx, 
            PublicKey source, 
            PublicKey destination, 
            BigInteger amount,
            Account feePayer,
            Commitment commitment = Commitment.Finalized
        )
        {
            var blockHash = await ctx.RpcClient.GetRecentBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(ctx.WalletPubKey)
                .AddInstruction(
                    TokenProgram.Transfer(
                        source, 
                        destination, 
                        (ulong)amount, 
                        ctx.WalletPubKey
                    )
                )
                .Build(feePayer);  

            return await ctx.RpcClient.SendAndConfirmTransactionAsync(tx, preFlightCommitment: commitment, commitment: commitment);
        }

        /// <summary>
        /// Derives the address for an associated token account. 
        /// </summary>
        public static PublicKey GetAssociatedTokenAddress(
            PublicKey mint,
            PublicKey owner,
            bool? allowOwnerOffCurve = null
        )
        {
            return GetAssociatedTokenAddress(
                AddressConstants.ASSOCIATED_TOKEN_PROGRAM_PUBKEY,
                AddressConstants.TOKEN_PROGRAM_PUBKEY,
                mint, owner, allowOwnerOffCurve
            );
        }

        /// <summary>
        /// Derives the address for an associated token account. 
        /// </summary>
        /// <param name="associatedProgramId"></param>
        /// <param name="programId"></param>
        /// <param name="mint"></param>
        /// <param name="owner"></param>
        /// <param name="allowOwnerOffCurve"></param>
        /// <returns></returns>
        public static PublicKey GetAssociatedTokenAddress(
            PublicKey associatedProgramId,
            PublicKey programId,
            PublicKey mint,
            PublicKey owner,
            bool? allowOwnerOffCurve = null
        )
        {
            return AddressUtils.FindProgramAddress(
                new byte[][]
                {
                    owner.KeyBytes,
                    programId.KeyBytes,
                    mint.KeyBytes,
                },
                associatedProgramId
            ).PublicKey;
        }

        /// <summary>
        /// Creates and returns a transaction to create an account and initialize it with the native
        /// mint address. 
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="amountIn"></param>
        /// <param name="rentExemptLamports">Minimum balance to avoid rent payment for account.</param>
        /// <param name="tokenAddress">Returns the address of the new account as an output parameter.</param>
        /// <param name="commitment">The preflight and transaction commitment to use.</param>
        /// <returns>Raw bytes of the new transaction. </returns>
        private static byte[] CreateWSolAccountInstructions(
            PublicKey walletAddress, 
            BigInteger amountIn, 
            ulong rentExemptLamports,
            out PublicKey tokenAddress,
            Commitment commitment = Commitment.Finalized
        )
        {
            //create new account 
            Account tempAccount = new Account(); 
            tokenAddress = tempAccount.PublicKey;

            return new TransactionBuilder()    
                .AddInstruction(SystemProgram.CreateAccount(
                    walletAddress,
                    tempAccount.PublicKey,
                    (ulong)(amountIn + rentExemptLamports),
                    TokenProgram.MintAccountDataSize,   
                    TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeAccount(
                    tempAccount.PublicKey,
                    AddressConstants.NATIVE_MINT_PUBKEY,
                    walletAddress)
                ).Build(new List<Account>());
        }   
    }
}