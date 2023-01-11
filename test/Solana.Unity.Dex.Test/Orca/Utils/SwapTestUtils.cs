using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Programs;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class SwapTestUtils
    {
        public static SwapParams GenerateParams(
            TestWhirlpoolContext ctx, 
            PoolInitResult poolInitResult,
            PublicKey whirlpoolAddress, 
            PublicKey[] tickArrays,
            PublicKey oracleAddress, 
            BigInteger? amount = null, 
            BigInteger? otherThresholdAmount = null, 
            BigInteger? sqrtPriceLimit = null, 
            bool amountSpecifiedIsInput = true, 
            bool aToB = true
        ) 
        {
            return GenerateParams(
                ctx, 
                poolInitResult.TokenAccountA, 
                poolInitResult.TokenAccountB,
                poolInitResult.InitPoolParams.TokenVaultAKeyPair.PublicKey,
                poolInitResult.InitPoolParams.TokenVaultBKeyPair.PublicKey,
                whirlpoolAddress,
                tickArrays,
                oracleAddress,
                amount,
                otherThresholdAmount,
                sqrtPriceLimit,
                amountSpecifiedIsInput,
                aToB
            );
        }
        
        public static SwapParams GenerateParams(
            TestWhirlpoolContext ctx, 
            PublicKey tokenAccountA, 
            PublicKey tokenAccountB, 
            PublicKey tokenVaultA, 
            PublicKey tokenVaultB,
            PublicKey whirlpoolAddress, 
            PublicKey[] tickArrays,
            PublicKey oracleAddress, 
            BigInteger? amount = null, 
            BigInteger? otherThresholdAmount = null, 
            BigInteger? sqrtPriceLimit = null, 
            bool amountSpecifiedIsInput = true, 
            bool aToB = true
        ) 
        {
            SwapAccounts accounts = new SwapAccounts
            {
                TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                TokenAuthority = ctx.WalletPubKey,
                Whirlpool = whirlpoolAddress,
                TokenOwnerAccountA = tokenAccountA,
                TokenVaultA = tokenVaultA,
                TokenOwnerAccountB = tokenAccountB,
                TokenVaultB = tokenVaultB,
                TickArray0 = tickArrays[0],
                TickArray1 = tickArrays[1],
                TickArray2 = tickArrays[2],
                Oracle = oracleAddress
            }; 
            
            return new SwapParams
            {
                Accounts = accounts,
                Amount = amount != null ? amount.Value : 0, 
                OtherThresholdAmount = otherThresholdAmount != null ? otherThresholdAmount.Value : 0, 
                SqrtPriceLimit = sqrtPriceLimit != null ? sqrtPriceLimit.Value : 0, 
                AmountSpecifiedIsInput = amountSpecifiedIsInput,
                AtoB = aToB
            };
        }
        
        public static async Task<RequestResult<string>> SwapAsync(
            TestWhirlpoolContext ctx, 
            SwapParams swapParams, 
            Account feePayer = null
        ) 
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;
            
            SigningCallback signer = new SigningCallback(ctx.WalletAccount, feePayer); 
            
            return await ctx.WhirlpoolClient.SendSwapAsync(
                swapParams.Accounts,
                programId: ctx.ProgramId,
                amount: (ulong)swapParams.Amount,
                otherAmountThreshold: (ulong)swapParams.OtherThresholdAmount,
                sqrtPriceLimit: swapParams.SqrtPriceLimit,
                amountSpecifiedIsInput: swapParams.AmountSpecifiedIsInput,
                aToB: swapParams.AtoB,
                feePayer: feePayer.PublicKey,
                signingCallback: (byte[] msg, PublicKey pub) => signer.Sign(msg, pub)
            );
        }

        public static int ArrayTickIndexToTickIndex(ArrayTickIndex index, ushort tickSpacing)
        {
            return index.ArrayIndex * TickConstants.TICK_ARRAY_SIZE * tickSpacing + index.OffsetIndex * tickSpacing;
        }

        public static FundedPositionParams BuildPosition(
            ArrayTickIndex lower,
            ArrayTickIndex upper,
            ushort tickSpacing,
            BigInteger liquidityAmount
        )
        {
            return new FundedPositionParams
            {
                LiquidityAmount = liquidityAmount,
                TickLowerIndex = ArrayTickIndexToTickIndex(lower, tickSpacing), 
                TickUpperIndex = ArrayTickIndexToTickIndex(upper, tickSpacing) 
            };
        }
    }
}