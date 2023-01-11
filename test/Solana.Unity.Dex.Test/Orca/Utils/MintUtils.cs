using NUnit.Framework;
using System;
using System.Threading.Tasks; 

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Rpc.Models;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class MintUtils
    {
        public static async Task<Tuple<PublicKey, PublicKey>> CreateInOrderMints(
            IWhirlpoolContext ctx,
            Account authority,
            bool tokenAIsNative = false
        )
        {
            PublicKey tokenXMintPubKey = AddressConstants.NATIVE_MINT_PUBKEY;
            if (!tokenAIsNative)
            {
                Account tokenXMint = new Account();
                tokenXMintPubKey = tokenXMint.PublicKey;
                Transaction createMintTransaction = await TokenUtilsTransaction.CreateMint(ctx.RpcClient, authority, tokenXMint);
                var createMintRes = await ctx.RpcClient.SendTransactionAsync(createMintTransaction.Serialize(), commitment: ctx.WhirlpoolClient.DefaultCommitment);
                Assert.IsTrue(createMintRes.WasSuccessful);
                Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(createMintRes.Result, ctx.WhirlpoolClient.DefaultCommitment));
            }
            
            Account tokenYMint = new Account();
            PublicKey tokenYMintPubKey = tokenYMint.PublicKey;
            Transaction createMintTransactionY = await TokenUtilsTransaction.CreateMint(ctx.RpcClient, authority, tokenYMint);
            var createMintResY = await ctx.RpcClient.SendTransactionAsync(createMintTransactionY.Serialize(), commitment: ctx.WhirlpoolClient.DefaultCommitment);
            Assert.IsTrue(createMintResY.WasSuccessful);
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(createMintResY.Result, ctx.WhirlpoolClient.DefaultCommitment));
            
            PublicKey tokenAMintPubKey;
            PublicKey tokenBMintPubKey;

            if (AddressUtils.ComparePublicKeys(tokenXMintPubKey, tokenYMintPubKey) < 0) 
            {
                tokenAMintPubKey = tokenXMintPubKey;
                tokenBMintPubKey = tokenYMintPubKey;
            }
            else
            {
                tokenAMintPubKey = tokenYMintPubKey;
                tokenBMintPubKey = tokenXMintPubKey;
            }
            
            return Tuple.Create(tokenAMintPubKey, tokenBMintPubKey);
        }
    }
}