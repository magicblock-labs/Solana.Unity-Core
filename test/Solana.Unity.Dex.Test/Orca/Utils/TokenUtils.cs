using NUnit.Framework;
using Orca;
using Solana.Unity.Dex.Orca;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Rpc;
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

            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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

            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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

            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync();
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync();
            byte[] tx = new TransactionBuilder().SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await ctx.RpcClient.GetLatestBlockHashAsync(commitment);
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash)
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
        
        public static async Task CloseAta(IRpcClient rpc, PublicKey mint, Account authority, PublicKey destination)
        {
            var ata =  AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(authority, mint);
            bool exists = await TokenUtilsTransaction.TokenAccountExists(rpc, ata, Commitment.Finalized);
            if(!exists) return;
            ulong balance = 0;
            if (!mint.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                balance = (await rpc.GetTokenBalanceByOwnerAsync(
                    authority.PublicKey, mint)).Result.Value.AmountUlong;
            }
            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<LatestBlockHash>> latestBlockHashItem = await rpc.GetLatestBlockHashAsync(commitment: Commitment.Finalized);
            TransactionBuilder txb = new();
            txb
                .SetFeePayer(authority)
                .SetRecentBlockHash(latestBlockHashItem.Result.Value.Blockhash);
            if (balance > 0)
            {
                // Send the balance to a random ATA, close fails if balance is not 0 for not native tokens
                Account randomAccount = new();
                var ataR =  AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(randomAccount, mint);
                txb.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    authority, 
                    randomAccount, 
                    mint));
                txb.AddInstruction(TokenProgram.Transfer(ata, ataR, balance, authority));
            }
            txb
                .AddInstruction(TokenProgram.CloseAccount(
                    ata,
                    destination,
                    authority,
                    TokenProgram.ProgramIdKey)
                );
            var closeRes = await rpc.SendTransactionAsync(txb.Build(authority), commitment: Commitment.Finalized);
            Assert.IsTrue(closeRes.WasSuccessful);
            Assert.IsTrue(await rpc.ConfirmTransaction(closeRes.Result));
        }
    }
}