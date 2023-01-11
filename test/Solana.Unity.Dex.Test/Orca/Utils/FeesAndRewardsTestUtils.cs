using NUnit.Framework;
using System;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Rpc.Models;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class FeesAndRewardsTestUtils
    {
        public const int NUM_REWARDS = 3;

        public static async Task<RequestResult<string>> UpdateFeesAndRewardsAsync(
            TestWhirlpoolContext ctx,
            UpdateFeesAndRewardsAccounts accounts,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            return await ctx.WhirlpoolClient.SendUpdateFeesAndRewardsAsync(
                accounts,
                feePayer,
                (byte[] msg, PublicKey pubKey) => feePayer.Sign(msg),
                programId: ctx.ProgramId
            );
        }

        public static async Task<RequestResult<string>> CollectFeesAsync(
            TestWhirlpoolContext ctx,
            CollectFeesParams collectParams,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            SigningCallback signer = new SigningCallback(feePayer);
            if (feePayer.PublicKey != collectParams.PositionAuthorityKeypair)
                signer.AddSigner(collectParams.PositionAuthorityKeypair);

            return await ctx.WhirlpoolClient.SendCollectFeesAsync(
                collectParams.Accounts,
                feePayer,
                (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                programId: ctx.ProgramId
            );
        }

        public static async Task<RequestResult<string>> CollectRewardsAsync(
            TestWhirlpoolContext ctx,
            CollectRewardsParams collectRewardsParams,
            Account signer = null
        )
        {
            return await CollectRewardsAsync(
                ctx,
                collectRewardsParams.Accounts,
                collectRewardsParams.RewardIndex,
                signer
            );
        }

        public static async Task<RequestResult<string>> CollectRewardsAsync(
            TestWhirlpoolContext ctx,
            CollectRewardAccounts accounts,
            byte rewardIndex,
            Account signer = null
        )
        {
            if (signer == null)
                signer = ctx.WalletAccount;

            if (accounts.TokenProgram == null)
                accounts.TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY;

            return await ctx.WhirlpoolClient.SendCollectRewardAsync(
                accounts,
                rewardIndex,
                signer.PublicKey,
                (byte[] msg, PublicKey pubKey) => signer.Sign(msg),
                programId: ctx.ProgramId
            );
        }

        public static async Task<Tuple<InitializedRewardInfo, RequestResult<string>>> InitializeRewardAsync(
            TestWhirlpoolContext ctx,
            Account rewardAuthority,
            PublicKey whirlpool,
            byte rewardIndex,
            Account funder = null
        )
        {
            //funder defaults to wallet 
            if (funder == null)
                funder = ctx.WalletAccount;

            //mint and vault 
            PublicKey rewardMintKey = await TokenUtils.CreateMintAsync(
                ctx,
                authority: ctx.WalletAccount, 
                commitment: Commitment.Confirmed
            );
            Account rewardVaultAccount = new Account();

            //generate params 
            InitializeRewardParams initRewardParams = GenerateInitRewardParams(
                ctx,
                rewardAuthority,
                whirlpool,
                rewardMintKey,
                rewardVaultAccount,
                rewardIndex,
                funder
            );

            //get result 
            SigningCallback signer = new SigningCallback(
                new Account[] { funder, rewardAuthority, rewardVaultAccount },
                ctx.WalletAccount
            );

            //reward authority
            //reward vault 
            var result = await ctx.WhirlpoolClient.SendInitializeRewardAsync(
                accounts: initRewardParams.Accounts,
                rewardIndex: initRewardParams.RewardIndex,
                feePayer: funder,
                signingCallback: (msg, pubKey) => signer.Sign(msg, pubKey),
                programId: ctx.ProgramId
            );

            System.Diagnostics.Debug.Assert(result.WasSuccessful, $"Failed to SendInitializeReward: {result.Reason}");

            //return params & result 
            return Tuple.Create<InitializedRewardInfo, RequestResult<string>>(
                new InitializedRewardInfo
                {
                    RewardMint = rewardMintKey,
                    RewardVaultKeyPair = rewardVaultAccount
                }, result
            );
        }

        public static CollectRewardsParams GenerateCollectRewardsParams(
            TestWhirlpoolContext ctx,
            Pda whirlpoolPda,
            FundedPositionInfo position,
            PublicKey rewardOwnerAccountKey,
            PublicKey rewardVaultKey,
            PublicKey positionAuthorityKey = null
        )
        {
            if (positionAuthorityKey == null)
                positionAuthorityKey = ctx.WalletPubKey;

            return new CollectRewardsParams
            {
                Accounts = new CollectRewardAccounts
                {
                    Whirlpool = whirlpoolPda,
                    PositionAuthority = positionAuthorityKey,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    RewardOwnerAccount = rewardOwnerAccountKey,
                    RewardVault = rewardVaultKey
                },
                RewardIndex = 0
            };
        }

        public static InitializeRewardParams GenerateInitRewardParams(
            TestWhirlpoolContext ctx,
            Account rewardAuthority,
            PublicKey whirlpool,
            PublicKey rewardMintKey,
            Account rewardVaultAccount,
            byte rewardIndex,
            Account funder = null
        )
        {
            if (funder == null)
                funder = ctx.WalletAccount;

            return new InitializeRewardParams
            {
                Accounts = new InitializeRewardAccounts
                {
                    RewardAuthority = rewardAuthority,
                    Whirlpool = whirlpool,
                    RewardMint = rewardMintKey,
                    RewardVault = rewardVaultAccount,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                    Funder = funder,
                    Rent = AddressConstants.RENT_PUBKEY
                },
                RewardIndex = rewardIndex
            };
        }

        public static async Task<Tuple<InitializedRewardInfo, RequestResult<string>>> InitializeRewardsSetEmissions(
            TestWhirlpoolContext ctx,
            Account rewardAuthority,
            PublicKey whirlpool,
            byte rewardIndex,
            BigInteger vaultAmount,
            BigInteger emissionsPerSecondX64,
            Account funder = null
        )
        {
            //funder defaults to context wallet 
            if (funder == null)
                funder = ctx.WalletAccount;

            //initialize rewards 
            var (initRewardInfo, initResult) = await InitializeRewardAsync(
                ctx,
                rewardAuthority,
                whirlpool,
                rewardIndex,
                funder
            );
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(initResult.Result, ctx.WhirlpoolClient.DefaultCommitment));

            //mint to it 
            Transaction mintToByAuthority = await TokenUtilsTransaction.MintToByAuthority(
                ctx.RpcClient,
                initRewardInfo.RewardMint,
                initRewardInfo.RewardVaultKeyPair,
                vaultAmount,
                feePayer: ctx.WalletAccount
            );
            var mintToByAuthorityResult = await ctx.RpcClient.SendTransactionAsync(mintToByAuthority.Serialize(), commitment: ctx.WhirlpoolClient.DefaultCommitment);
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(mintToByAuthorityResult.Result, ctx.WhirlpoolClient.DefaultCommitment));
            

            SigningCallback signer = new(new[]{
                rewardAuthority, initRewardInfo.RewardVaultKeyPair, funder
            }, ctx.WalletAccount);
            
            //set emissions 
            var setEmissionsResult = await ctx.WhirlpoolClient.SendSetRewardEmissionsAsync(
                new SetRewardEmissionsAccounts
                {
                    Whirlpool = whirlpool,
                    RewardAuthority = rewardAuthority,
                    RewardVault = initRewardInfo.RewardVaultKeyPair
                },
                rewardIndex,
                emissionsPerSecondX64,
                funder,
                (msg, pub) => signer.Sign(msg, pub),
                programId: ctx.ProgramId
            );

            Assert.IsTrue(setEmissionsResult.WasSuccessful, $"Failed to SendSetRewardEmissions: {setEmissionsResult.Reason}");
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(setEmissionsResult.Result, ctx.WhirlpoolClient.DefaultCommitment));

            //return info & result 
            return Tuple.Create(
                initRewardInfo, setEmissionsResult
            );
        }
    }

    public class InitializedRewardInfo
    {
        public PublicKey RewardMint { get; set; }
        public Account RewardVaultKeyPair { get; set; }
    }
}