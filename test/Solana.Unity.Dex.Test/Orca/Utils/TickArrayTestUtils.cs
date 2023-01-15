using NUnit.Framework;
using System.Threading.Tasks; 
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class TickArrayTestUtils
    {
        public static InitializeTickArrayParams GenerateParams(
            TestWhirlpoolContext ctx, 
            PublicKey whirlpool, 
            int startTick, 
            Account funder = null
        ) 
        {
            Pda tickArrayPda = PdaUtils.GetTickArray(ctx.ProgramId, whirlpool, startTick); 
            
            return new InitializeTickArrayParams
            {
                Accounts = new InitializeTickArrayAccounts
                {
                    Funder = (funder != null) ? funder : ctx.WalletAccount,
                    Whirlpool = whirlpool,
                    TickArray = tickArrayPda.PublicKey,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY
                },
                StartTick = startTick,
                TickArrayPda = tickArrayPda
            };
        }

        public static async Task<RequestResult<string>> InitializeTickArrayAsync(
            TestWhirlpoolContext ctx,
            PublicKey whirlpool,
            int startTick,
            Account funder = null
        )
        {
            var tickArraysResult = await InitializeTickArrayAsync(
                ctx, 
                GenerateParams(ctx, whirlpool, startTick, funder), 
                funder
            );
            Assert.IsTrue(tickArraysResult.WasSuccessful, $"Failed to InitializeTickArray: {tickArraysResult.Reason}");
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(tickArraysResult.Result, ctx.WhirlpoolClient.DefaultCommitment));
            return tickArraysResult;
        }

        public static async Task<RequestResult<string>> InitializeTickArrayAsync(
            TestWhirlpoolContext ctx, 
            InitializeTickArrayParams tickArrayParams, 
            Account funder = null
        )
        {
            if (funder == null) 
                funder = ctx.WalletAccount;
            
            tickArrayParams.Accounts.Funder = funder;
            
            SigningCallback signer = new SigningCallback(funder, ctx.WalletAccount);
            
            return await ctx.WhirlpoolClient.SendInitializeTickArrayAsync(
                accounts: tickArrayParams.Accounts, 
                startTickIndex: tickArrayParams.StartTick, 
                feePayer: tickArrayParams.Accounts.Funder, 
                signingCallback: (byte[] msg, PublicKey pub) => signer.Sign(msg, pub), 
                programId: ctx.ProgramId
            );
        }

        public static async Task<IList<Pda>> InitializeTickArrayRangeAsync(
            TestWhirlpoolContext ctx,
            PublicKey whirlpool,
            int startTickIndex, 
            int arrayCount, 
            ushort tickSpacing = TickSpacing.Standard,
            bool aToB = false,
            Account funder = null
        )
        {
            int ticksInArray = tickSpacing * TickConstants.TICK_ARRAY_SIZE;
            int direction = aToB ? -1 : 1;
            List<Pda> output = new(); 
            
            for (int i=0; i< arrayCount; i++) 
            {
                InitializeTickArrayParams tickArrayParams = GenerateParams(
                    ctx,
                    whirlpool: whirlpool,
                    startTick: startTickIndex + direction * ticksInArray * i,
                    funder: funder
                );
                var initResult = await InitializeTickArrayAsync(
                    ctx,
                    tickArrayParams, 
                    funder
                );
                
                Assert.IsTrue(initResult.WasSuccessful, $"Failed to InitializeTickArray: {initResult.Reason}"); 
                Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(initResult.Result)); 
                output.Add(tickArrayParams.TickArrayPda);
            }
            
            return output; 
        }
    }
}