using System.Numerics;
using System.Threading.Tasks; 

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http; 

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Rpc.Models;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class FeeTierTestUtils
    {
        private const ushort DefaultFeeRate = 3000; 
        private const ushort DefaultTickSpacing = TickSpacing.Standard;
        
        public static TransactionInstruction InitializeFeeTierInstruction(
            TestWhirlpoolContext ctx,
            InitializeFeeTierParams feeTierParams = null,
            Account feePayer = null
        )
        {
            feeTierParams ??= GenerateDefaultParams(ctx);
            feePayer ??= ctx.WalletAccount;

            SigningCallback signer = new SigningCallback(new Account[]{
                feePayer, feeTierParams.FeeAuthorityKeypair
            }, ctx.WalletAccount);
            
            return WhirlpoolProgram.InitializeFeeTier(
                feeTierParams.Accounts,
                feeTierParams.TickSpacing,
                feeTierParams.DefaultFeeRate,
                ctx.ProgramId
            );
        }
        
        public static async Task<RequestResult<string>> InitializeFeeTierAsync(
            TestWhirlpoolContext ctx,
            InitializeFeeTierParams feeTierParams = null,
            Account feePayer = null
        )
        {
            if (feeTierParams == null) 
                feeTierParams = GenerateDefaultParams(ctx);

            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            SigningCallback signer = new SigningCallback(new Account[]{
                feePayer, feeTierParams.FeeAuthorityKeypair
            }, ctx.WalletAccount);
            
            //make the rpc call 
            RequestResult<string> result = await ctx.WhirlpoolClient.SendInitializeFeeTierAsync(
                feeTierParams.Accounts,
                feeTierParams.TickSpacing,
                feeTierParams.DefaultFeeRate,
                feeTierParams.Accounts.Funder, 
                (msg, pubKey) => signer.Sign(msg, pubKey),
                ctx.ProgramId
            );

            return result;
        }

        public static InitializeFeeTierParams GenerateDefaultParams(
            TestWhirlpoolContext ctx,
            ushort tickSpacing = TickSpacing.Standard,
            ushort defaultFeeRate = 3000
        )
        {
            return new InitializeFeeTierParams
            {
                TickSpacing = tickSpacing,
                DefaultFeeRate = defaultFeeRate, 
                FeeAuthorityKeypair = ctx.WalletAccount
            };
        }
        
        public static InitializeFeeTierParams GenerateParams(
            TestWhirlpoolContext ctx,
            InitializeConfigParams initConfigParams,
            ushort tickSpacing = DefaultTickSpacing,
            ushort defaultFeeRate = DefaultFeeRate,
            Account funder = null
        )
        {
            return GenerateParams(
                ctx,
                initConfigParams.ConfigKeypair,
                initConfigParams.FeeAuthorityKeypair,
                tickSpacing,
                defaultFeeRate,
                funder
            );
        }
        
        public static InitializeFeeTierParams GenerateParams(
            TestWhirlpoolContext ctx,
            Account whirlpoolsConfig,
            Account whirlpoolFeeAuthority,
            ushort tickSpacing = DefaultTickSpacing,
            ushort defaultFeeRate = DefaultFeeRate,
            Account funder = null
        )
        {
            Pda feeTierPda = PdaUtils.GetFeeTier(ctx.ProgramId, whirlpoolsConfig.PublicKey, tickSpacing);

            return new InitializeFeeTierParams
            {
                Accounts = new InitializeFeeTierAccounts
                {
                    Config = whirlpoolsConfig,
                    FeeTier = feeTierPda.PublicKey,
                    Funder = funder != null ? funder : ctx.WalletAccount,
                    FeeAuthority = whirlpoolFeeAuthority,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY
                },
                FeeTierPda = feeTierPda,
                TickSpacing = tickSpacing,
                DefaultFeeRate = defaultFeeRate,
                FeeAuthorityKeypair = whirlpoolFeeAuthority
            };
        }
    }
}