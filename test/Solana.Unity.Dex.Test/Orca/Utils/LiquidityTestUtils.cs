using System.Numerics;
using System.Threading.Tasks; 
using System.Collections.Generic;
using System.Linq;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class LiquidityTestUtils
    {
        public static IncreaseLiquidityParams GenerateIncreaseParams(
            TestWhirlpoolContext ctx,
            PublicKey whirlpoolKey,
            InitializePoolParams initPoolParams,
            FundedPositionInfo position,
            BigInteger liquidityAmount,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            ulong tokenMaxA,
            ulong tokenMaxB,
            Account positionAuthority = null
        )
        {
            if (positionAuthority == null)
                positionAuthority = ctx.WalletAccount;

            return new IncreaseLiquidityParams
            {
                Accounts = new IncreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolKey,
                    TokenVaultA = initPoolParams.TokenVaultAKeyPair,
                    TokenVaultB = initPoolParams.TokenVaultBKeyPair,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    PositionAuthority = positionAuthority,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                LiquidityAmount = liquidityAmount,
                TokenMaxA = tokenMaxA,
                TokenMaxB = tokenMaxB, 
                PositionAuthorityKeypair = positionAuthority
            };
        }

        public static IncreaseLiquidityParams GenerateIncreaseParams(
            TestWhirlpoolContext ctx,
            PublicKey whirlpoolKey,
            InitializePoolParams initPoolParams,
            OpenPositionParams openPositionParams,
            InitializeTickArrayParams initTickParams,
            BigInteger liquidityAmount,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            ulong tokenMaxA,
            ulong tokenMaxB,
            Account positionAuthority = null
        )
        {
            if (positionAuthority == null)
                positionAuthority = ctx.WalletAccount;

            return new IncreaseLiquidityParams
            {
                Accounts = new IncreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolKey,
                    TokenVaultA = initPoolParams.TokenVaultAKeyPair,
                    TokenVaultB = initPoolParams.TokenVaultBKeyPair,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    Position = openPositionParams.PositionPda,
                    PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                    PositionAuthority = positionAuthority,
                    TickArrayLower = initTickParams.Accounts.TickArray,
                    TickArrayUpper = initTickParams.Accounts.TickArray,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                LiquidityAmount = liquidityAmount,
                TokenMaxA = tokenMaxA,
                TokenMaxB = tokenMaxB,
                PositionAuthorityKeypair = positionAuthority
            };
        }

        public static IncreaseLiquidityParams GenerateIncreaseParams(
            TestWhirlpoolContext ctx,
            WhirlpoolsTestFixture.TestFixtureInfo testInfo,
            BigInteger liquidityAmount,
            ulong tokenMaxA,
            ulong tokenMaxB,
            Account positionAuthority = null
        )
        {
            return GenerateIncreaseParams(
                ctx,
                testInfo.InitPoolParams.WhirlpoolPda,
                testInfo.InitPoolParams,
                testInfo.Positions[0],
                liquidityAmount,
                testInfo.TokenAccountA,
                testInfo.TokenAccountB,
                tokenMaxA,
                tokenMaxB,
                positionAuthority
            );
        }

        public static DecreaseLiquidityParams GenerateDecreaseParams(
            TestWhirlpoolContext ctx,
            PublicKey whirlpoolKey,
            InitializePoolParams initPoolParams,
            FundedPositionInfo position,
            BigInteger liquidityAmount,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            ulong tokenMinA,
            ulong tokenMinB,
            Account positionAuthority = null
        )
        {
            if (positionAuthority == null)
                positionAuthority = ctx.WalletAccount;

            return new DecreaseLiquidityParams
            {
                Accounts = new DecreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolKey,
                    TokenVaultA = initPoolParams.TokenVaultAKeyPair,
                    TokenVaultB = initPoolParams.TokenVaultBKeyPair,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    Position = position.PublicKey,
                    PositionTokenAccount = position.TokenAccount,
                    PositionAuthority = positionAuthority,
                    TickArrayLower = position.TickArrayLower,
                    TickArrayUpper = position.TickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                LiquidityAmount = liquidityAmount,
                TokenMinA = tokenMinA,
                TokenMinB = tokenMinB,
                PositionAuthorityKeypair = positionAuthority
            };
        }

        public static DecreaseLiquidityParams GenerateDecreaseParams(
            TestWhirlpoolContext ctx,
            PublicKey whirlpoolKey,
            InitializePoolParams initPoolParams,
            OpenPositionParams openPositionParams,
            InitializeTickArrayParams initTickParams,
            BigInteger liquidityAmount,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            ulong tokenMinA,
            ulong tokenMinB,
            Account positionAuthority = null
        )
        {
            if (positionAuthority == null)
                positionAuthority = ctx.WalletAccount;

            return new DecreaseLiquidityParams
            {
                Accounts = new DecreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolKey,
                    TokenVaultA = initPoolParams.TokenVaultAKeyPair,
                    TokenVaultB = initPoolParams.TokenVaultBKeyPair,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    Position = openPositionParams.PositionPda,
                    PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                    PositionAuthority = positionAuthority,
                    TickArrayLower = initTickParams.Accounts.TickArray,
                    TickArrayUpper = initTickParams.Accounts.TickArray,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                LiquidityAmount = liquidityAmount,
                TokenMinA = tokenMinA,
                TokenMinB = tokenMinB,
                PositionAuthorityKeypair = positionAuthority
            };
        }

        public static DecreaseLiquidityParams GenerateDecreaseParams(
            TestWhirlpoolContext ctx,
            WhirlpoolsTestFixture.TestFixtureInfo testInfo,
            BigInteger liquidityAmount,
            ulong tokenMinA,
            ulong tokenMinB,
            Account positionAuthority = null
        )
        {
            if (positionAuthority == null)
                positionAuthority = ctx.WalletAccount;

            return GenerateDecreaseParams(
                ctx,
                whirlpoolKey: testInfo.InitPoolParams.WhirlpoolPda,
                initPoolParams: testInfo.InitPoolParams,
                position: testInfo.Positions[0],
                liquidityAmount: liquidityAmount,
                tokenAccountA: testInfo.TokenAccountA,
                tokenAccountB: testInfo.TokenAccountB,
                tokenMinA: tokenMinA,
                tokenMinB: tokenMinB,
                positionAuthority: positionAuthority
            );
        }

        public static async Task<RequestResult<string>> IncreaseLiquidityAsync(
            TestWhirlpoolContext ctx,
            IncreaseLiquidityParams increaseParams,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;
                
            SigningCallback signer = new SigningCallback(new System.Collections.Generic.List<Account>{
                feePayer,
                increaseParams.PositionAuthorityKeypair
            }, ctx.WalletAccount);

            return await ctx.WhirlpoolClient.SendIncreaseLiquidityAsync(
                increaseParams.Accounts,
                increaseParams.LiquidityAmount,
                increaseParams.TokenMaxA,
                increaseParams.TokenMaxB,
                feePayer: feePayer,
                signingCallback: (byte[] msg, PublicKey key) => signer.Sign(msg,key),
                programId: ctx.ProgramId
            );
        }

        public static async Task<RequestResult<string>> DecreaseLiquidityAsync(
            TestWhirlpoolContext ctx,
            DecreaseLiquidityParams decreaseParams,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            SigningCallback signer = new SigningCallback(new Account[]{
                feePayer,
                decreaseParams.PositionAuthorityKeypair 
            }, ctx.WalletAccount);

            return await ctx.WhirlpoolClient.SendDecreaseLiquidityAsync(
                decreaseParams.Accounts,
                decreaseParams.LiquidityAmount,
                decreaseParams.TokenMinA,
                decreaseParams.TokenMinB,
                feePayer: feePayer,
                signingCallback: (byte[] msg, PublicKey key) => signer.Sign(msg, key),
                programId: ctx.ProgramId
            );
        }
    }
}