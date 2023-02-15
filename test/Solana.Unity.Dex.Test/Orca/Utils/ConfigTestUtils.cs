using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Programs.Models;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Rpc.Models;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class ConfigTestUtils
    {
        public static TransactionInstruction InitializeConfigInstruction(
            TestWhirlpoolContext ctx, 
            InitializeConfigParams initConfigParams = null
        )
        {
            initConfigParams ??= GenerateParams(ctx);
            TransactionInstruction instr = WhirlpoolProgram.InitializeConfig(
                initConfigParams.Accounts,
                initConfigParams.FeeAuthorityKeypair.PublicKey,
                initConfigParams.CollectProtocolFeesAuthority,
                initConfigParams.RewardEmissionsSuperAuthority,
                initConfigParams.DefaultProtocolFeeRate,
                ctx.ProgramId);
            return instr;
        }
        
        public static async Task<RequestResult<string>> InitializeConfigAsync(
            TestWhirlpoolContext ctx, 
            InitializeConfigParams initConfigParams = null,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount; 

            if (initConfigParams == null) 
                initConfigParams = GenerateParams(ctx);
                
            SigningCallback signer = new SigningCallback(new Account[]{
                initConfigParams.ConfigKeypair, feePayer}, 
                ctx.WalletAccount);
                
            return await ctx.WhirlpoolClient.SendInitializeConfigAsync(
                initConfigParams.Accounts,
                initConfigParams.FeeAuthorityKeypair.PublicKey,
                initConfigParams.CollectProtocolFeesAuthority,
                initConfigParams.RewardEmissionsSuperAuthority,
                initConfigParams.DefaultProtocolFeeRate,
                initConfigParams.Accounts.Funder,
                (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                ctx.ProgramId
            );
        }
        
        public static async Task<AccountResultWrapper<WhirlpoolsConfig>> GetConfig(
            TestWhirlpoolContext ctx, 
            InitializeConfigParams initConfigParams
        )
        {
            return await ctx.WhirlpoolClient.GetWhirlpoolsConfigAsync(
                initConfigParams.Accounts.Config
            );
        }
    
        public static InitializeConfigParams GenerateParams(
            TestWhirlpoolContext ctx,
            Account funder = null
        )
        {
            WhirlpoolsConfigKeyPairs configKeyPairs = new WhirlpoolsConfigKeyPairs
            {
                FeeAuthorityKeyPair = new Account(),
                CollectProtocolFeesAuthorityKeyPair = new Account(),
                RewardEmissionsSuperAuthorityKeyPair = new Account()
            };

            Account configKeypair = new(); 
            
            InitializeConfigParams configInitInfo = new()
            {
                Accounts = new InitializeConfigAccounts
                {
                    Funder = (funder != null) ? funder : ctx.WalletAccount,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                    Config = configKeypair
                },
                ConfigKeypair = configKeypair,
                FeeAuthority = configKeyPairs.FeeAuthorityKeyPair,
                FeeAuthorityKeypair = configKeyPairs.FeeAuthorityKeyPair,
                CollectProtocolFeesAuthority = configKeyPairs.CollectProtocolFeesAuthorityKeyPair.PublicKey,
                RewardEmissionsSuperAuthority = configKeyPairs.RewardEmissionsSuperAuthorityKeyPair.PublicKey,
                DefaultProtocolFeeRate = 300
            };

            configInitInfo.ConfigKeyPairs = configKeyPairs;
            return configInitInfo;
        }
    }   
}