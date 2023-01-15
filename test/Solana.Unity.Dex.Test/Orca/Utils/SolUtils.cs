using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Core.Http;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.SolanaApi;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class SolUtils
    {
        public static async Task<RequestResult<string>> FundTestAccountAsync(
            TestWhirlpoolContext ctx,
            PublicKey address, 
            ulong lamports = SolConstants.TENTH_SOL
        ) 
        {
            var result = await SystemUtils.TransferSolAsync(
                ctx, ctx.WalletAccount, address, lamports, Commitment.Confirmed
            );
            Thread.Sleep(3000); 
            return result;
        }
    }
}
