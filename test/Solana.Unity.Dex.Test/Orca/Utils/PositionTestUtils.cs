using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class PositionTestUtils
    {
        public static async Task<RequestResult<string>> OpenPositionAsync(
            TestWhirlpoolContext ctx,
            OpenPositionParams openPositionParams,
            Account feePayer = null,
            OpenWithMetadataOverrides overrides = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            if (openPositionParams.WithMetadata && openPositionParams.MetadataPda == null)
                throw new ArgumentNullException("OpenPositionAsync: If WithMetadata is true, then MetadataPda must be non-null");

            if (openPositionParams.Accounts.TokenProgram == null)
                openPositionParams.Accounts.TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY;
                
            if (openPositionParams.WithMetadata)
            {
                SigningCallback signer = new SigningCallback(
                    new Account[]{
                        feePayer,
                        openPositionParams.FunderKeypair,
                        openPositionParams.PositionMintKeypair
                    },
                    ctx.WalletAccount
                );

                OpenPositionWithMetadataAccounts accounts = new OpenPositionWithMetadataAccounts
                {
                    Funder = openPositionParams.Accounts.Funder,
                    Owner = openPositionParams.Accounts.Owner,
                    Position = openPositionParams.Accounts.Position,
                    PositionMint = openPositionParams.Accounts.PositionMint,
                    PositionMetadataAccount = openPositionParams.MetadataPda?.PublicKey,
                    PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                    Whirlpool = openPositionParams.Accounts.Whirlpool,
                    TokenProgram = openPositionParams.Accounts.TokenProgram,
                    SystemProgram = openPositionParams.Accounts.SystemProgram,
                    Rent = openPositionParams.Accounts.Rent,
                    AssociatedTokenProgram = openPositionParams.Accounts.AssociatedTokenProgram,
                    MetadataProgram = AddressConstants.METADATA_PROGRAM_PUBKEY,
                    MetadataUpdateAuth = AddressConstants.METADATA_UPDATE_AUTH_PUBKEY  
                };

                OpenPositionWithMetadataBumps bumps = new OpenPositionWithMetadataBumps
                {
                    PositionBump = openPositionParams.Bumps.PositionBump,
                    MetadataBump = openPositionParams.MetadataPda != null ? openPositionParams.MetadataPda.Bump : (byte)255
                };
                
                if (overrides != null) 
                {
                    if (overrides.MetadataProgram != null)
                        accounts.MetadataProgram = overrides.MetadataProgram;
                    if (overrides.MetadataUpdateAuth != null)
                        accounts.MetadataProgram = overrides.MetadataUpdateAuth;
                }
                
                return await ctx.WhirlpoolClient.SendOpenPositionWithMetadataAsync(
                    accounts,
                    bumps,
                    openPositionParams.TickLowerIndex,
                    openPositionParams.TickUpperIndex,
                    feePayer: feePayer,

                    signingCallback: (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                    programId: ctx.ProgramId
                );
            }
            else
            {
                SigningCallback signer = new SigningCallback(
                    new Account[]{
                        feePayer,
                        openPositionParams.FunderKeypair,
                        openPositionParams.PositionMintKeypair
                    },
                    ctx.WalletAccount
                );
                
                return await ctx.WhirlpoolClient.SendOpenPositionAsync(
                    openPositionParams.Accounts,
                    openPositionParams.Bumps,
                    openPositionParams.TickLowerIndex,
                    openPositionParams.TickUpperIndex,
                    feePayer: feePayer,

                    (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                    ctx.ProgramId
                );
            }
        }

        public static async Task<RequestResult<string>> OpenPositionWithMetadataAsync(
            TestWhirlpoolContext ctx,
            OpenPositionParams openPositionParams,
            Account feePayer = null, 
            OpenWithMetadataOverrides overrides = null
        )
        {
            openPositionParams.WithMetadata = true;
            return await OpenPositionAsync(ctx, openPositionParams, feePayer, overrides);
        }

        public static async Task<RequestResult<string>> ClosePositionAsync(
            TestWhirlpoolContext ctx, 
            ClosePositionParams closePositionParams,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;
                
            if (closePositionParams.Accounts.TokenProgram == null) 
                closePositionParams.Accounts.TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY;

            SigningCallback signer = new SigningCallback(feePayer, ctx.WalletAccount);

            return await ctx.WhirlpoolClient.SendClosePositionAsync(
                closePositionParams.Accounts,
                feePayer,
                (byte[] msg, PublicKey pubKey) => signer.Sign(msg, pubKey),
                ctx.ProgramId
            );
        }

        public static OpenPositionParams GenerateOpenParams(
            TestWhirlpoolContext ctx,
            PublicKey whirlpoolAddr,
            int tickLowerIndex = 0,
            int tickUpperIndex = 128,
            PublicKey owner = null,
            Account funder = null,
            bool withMetadata = false
        )
        {
            if (owner == null)
                owner = ctx.WalletPubKey;
            if (funder == null)
                funder = ctx.WalletAccount;

            Account positionMintKeypair = new Account();

            Pda positionPda = PdaUtils.GetPosition(ctx.ProgramId, positionMintKeypair.PublicKey);

            PublicKey tokenAccountAddress = TokenUtils.GetAssociatedTokenAddress(
                positionMintKeypair.PublicKey,
                owner
            );

            Pda metadataPda = null;
            if (withMetadata)
            {
                metadataPda = PdaUtils.GetPositionMetadata(positionMintKeypair.PublicKey);
            }

            return new OpenPositionParams
            {
                Accounts = new OpenPositionAccounts
                {
                    Funder = funder,
                    Owner = owner,
                    Position = positionPda,
                    PositionMint = positionMintKeypair,
                    PositionTokenAccount = tokenAccountAddress,
                    Whirlpool = whirlpoolAddr,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                    Rent = AddressConstants.RENT_PUBKEY,
                    AssociatedTokenProgram = AddressConstants.ASSOCIATED_TOKEN_PROGRAM_PUBKEY
                },
                Bumps = new OpenPositionBumps
                {
                    PositionBump = positionPda.Bump
                },
                TickLowerIndex = tickLowerIndex,
                TickUpperIndex = tickUpperIndex,
                PositionPda = positionPda,
                MetadataPda = metadataPda,
                WithMetadata = withMetadata, 
                FunderKeypair = funder,
                PositionMintKeypair = positionMintKeypair
            };
        }
        
        public static ClosePositionParams GenerateCloseParams(
            TestWhirlpoolContext ctx,
            OpenPositionParams openPositionParams,
            Account receiver = null,
            PublicKey positionAuthority = null
        )
        {
            if (receiver == null)
                receiver = new Account();

            if (positionAuthority == null)
                positionAuthority = ctx.WalletPubKey;

            return new ClosePositionParams
            {
                Accounts = new ClosePositionAccounts
                {
                    PositionAuthority = positionAuthority,
                    Receiver = receiver.PublicKey,
                    Position = openPositionParams.PositionPda,
                    PositionMint = openPositionParams.Accounts.PositionMint,
                    PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                }
            };
        }

        public static async Task<IList<FundedPositionInfo>> FundPositionsAsync(
            TestWhirlpoolContext ctx,
            PoolInitResult poolInitResult,
            IEnumerable<FundedPositionParams> fundParams,
            Account feePayer = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            return await FundPositionsAsync(
                ctx,
                initPoolParams: poolInitResult.InitPoolParams,
                tokenAccountA: poolInitResult.TokenAccountA,
                tokenAccountB: poolInitResult.TokenAccountB,
                fundParams: fundParams,
                feePayer
            );
        }

        public static async Task<IList<FundedPositionInfo>> FundPositionsAsync(
            TestWhirlpoolContext ctx,
            InitializePoolParams initPoolParams,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            IEnumerable<FundedPositionParams> fundParams,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            //collect up all promises in a list 
            List<Task<FundedPositionInfo>> promises = new();
            foreach (FundedPositionParams fundParam in fundParams)
            {
                promises.Add(FundPositionAsync(ctx,
                    initPoolParams,
                    tokenAccountA,
                    tokenAccountB,
                    fundParam
                ));
            }

            List<FundedPositionInfo> output = new();

            //await all promises 
            var results = await Task.WhenAll(
                promises
            );

            //collect up the results and return in a list 
            foreach (Task<FundedPositionInfo> promise in promises)
            {
                output.Add(promise.Result);
            }
            return output;
        }

        private static async Task<FundedPositionInfo> FundPositionAsync(
            TestWhirlpoolContext ctx,
            InitializePoolParams initPoolParams,
            PublicKey tokenAccountA,
            PublicKey tokenAccountB,
            FundedPositionParams fundParam,
            Account feePayer = null
        )
        {
            if (feePayer == null)
                feePayer = ctx.WalletAccount;

            //create params to open position 
            OpenPositionParams openPositionParams = GenerateOpenParams(
                ctx,
                initPoolParams.WhirlpoolPda,
                fundParam.TickLowerIndex,
                fundParam.TickUpperIndex
            );

            //open the position 
            var positionResult = await OpenPositionAsync(ctx, openPositionParams);
            Assert.IsTrue(positionResult.WasSuccessful, $"Failed to open position: {positionResult.Reason}"); 
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(positionResult.Result)); 

            //get lower tick array 
            Pda tickArrayLowerPda = PdaUtils.GetTickArray(
                ctx.ProgramId,
                whirlpoolAddress: initPoolParams.WhirlpoolPda,
                startTick: TickUtils.GetStartTickIndex(
                    fundParam.TickLowerIndex,
                    tickSpacing: initPoolParams.TickSpacing
                )
            );

            //get upper tick array 
            Pda tickArrayUpperPda = PdaUtils.GetTickArray(
                ctx.ProgramId,
                whirlpoolAddress: initPoolParams.WhirlpoolPda,
                startTick: TickUtils.GetStartTickIndex(
                    fundParam.TickUpperIndex,
                    tickSpacing: initPoolParams.TickSpacing
                )
            );

            //if there is liquidity, we must increase liquidity 
            if (fundParam.LiquidityAmount > 0)
            {
                //get token amounts 
                TokenAmounts tokenAmounts = PoolUtils.GetTokenAmountsFromLiquidity(
                    liquidity: fundParam.LiquidityAmount,
                    currentSqrtPrice: initPoolParams.InitSqrtPrice,
                    lowerSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(fundParam.TickLowerIndex),
                    upperSqrtPrice: PriceMath.TickIndexToSqrtPriceX64(fundParam.TickUpperIndex),
                    roundUp: true
                );

                var increaseResult = await LiquidityTestUtils.IncreaseLiquidityAsync(
                    ctx,
                    new IncreaseLiquidityParams
                    {
                        Accounts = new IncreaseLiquidityAccounts
                        {
                            PositionAuthority = ctx.WalletPubKey,
                            Whirlpool = initPoolParams.WhirlpoolPda,
                            Position = openPositionParams.PositionPda,
                            PositionTokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                            TokenOwnerAccountA = tokenAccountA,
                            TokenOwnerAccountB = tokenAccountB,
                            TokenVaultA = initPoolParams.TokenVaultAKeyPair.PublicKey,
                            TokenVaultB = initPoolParams.TokenVaultBKeyPair.PublicKey,
                            TickArrayLower = tickArrayLowerPda,
                            TickArrayUpper = tickArrayUpperPda,
                            TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                        },
                        LiquidityAmount = fundParam.LiquidityAmount,
                        TokenMaxA = (ulong)tokenAmounts.TokenA,
                        TokenMaxB = (ulong)tokenAmounts.TokenB,
                        PositionAuthorityKeypair = ctx.WalletAccount
                    }
                );
                
                Assert.IsTrue(increaseResult.WasSuccessful, $"Failed to IncreaseLiquidity: {increaseResult.Reason}");
                Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(increaseResult.Result)); 

            }

            return new FundedPositionInfo
            {
                OpenPositionParams = openPositionParams,
                PublicKey = openPositionParams.PositionPda,
                TokenAccount = openPositionParams.Accounts.PositionTokenAccount,
                MintKeyPair = openPositionParams.PositionMintKeypair,
                TickArrayLower = tickArrayLowerPda,
                TickArrayUpper = tickArrayUpperPda
            };
        }


        public class OpenWithMetadataOverrides
        {
            public PublicKey MetadataProgram { get; set; }
            public PublicKey MetadataUpdateAuth { get; set; }
        }
    }
}