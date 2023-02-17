using System; 
using System.Numerics;
using System.Threading.Tasks; 
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;
using NUnit.Framework;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Decimal = Solana.Unity.Dex.Orca.Math.BigDecimal;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class PoolTestUtils
    {
        private const ushort DefaultFeeRate = 3000;

        public static async Task<RequestResult<string>> InitializePoolAsync(
            TestWhirlpoolContext ctx,
            InitializePoolParams initPoolParams,
            Account feePayer = null
        )
        {
            feePayer ??= ctx.WalletAccount; 
                
            SigningCallback signer = new(new[]{
                feePayer, initPoolParams.TokenVaultAKeyPair, initPoolParams.TokenVaultBKeyPair
            }, ctx.WalletAccount); 
                
            return await ctx.WhirlpoolClient.SendInitializePoolAsync(
                initPoolParams.Accounts,
                initPoolParams.Bumps,
                initPoolParams.TickSpacing,
                initPoolParams.InitSqrtPrice,
                feePayer: feePayer,
                (msg, pubKey) => signer.Sign(msg, pubKey),
                ctx.ProgramId
            );
        }

        //TODO: (MID) some of these parameters can be removed most likely 
        public static InitializePoolParams GenerateParams(
            TestWhirlpoolContext ctx,
            PublicKey tokenAMintAddr,
            PublicKey tokenBMintAddr,
            InitializeConfigParams initConfigParams = null,
            InitializeFeeTierParams initFeeTierParams = null, 
            BigInteger? initSqrtPrice = null,
            Account funder = null,
            bool tokenAIsNative = false
        )
        {
            initConfigParams ??= ConfigTestUtils.GenerateParams(ctx);

            initFeeTierParams ??= FeeTierTestUtils.GenerateDefaultParams(ctx);
                
            initSqrtPrice ??= ArithmeticUtils.DecimalToX64BigInt(5);
            
            Account tokenVaultAKeypair = new Account();
            Account tokenVaultBKeypair = new Account();

            PublicKey feeTierKey = initFeeTierParams.FeeTierPda;

            //get whirlpools Pda 
            Pda whirlpoolPda = PdaUtils.GetWhirlpool(
                ctx.ProgramId,
                initConfigParams.Accounts.Config,
                tokenAMintAddr,
                tokenBMintAddr,
                initFeeTierParams.TickSpacing
            );
            
            //create params 
            return new InitializePoolParams
            {
                Accounts = new InitializePoolAccounts
                {
                    Whirlpool = whirlpoolPda,
                    TokenMintA = tokenAMintAddr,
                    TokenMintB = tokenBMintAddr,
                    TokenVaultA = tokenVaultAKeypair,
                    TokenVaultB = tokenVaultBKeypair,
                    WhirlpoolsConfig = initConfigParams.Accounts.Config,
                    FeeTier = feeTierKey,
                    Funder = funder == null ? ctx.WalletAccount : funder,
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                    Rent = AddressConstants.RENT_PUBKEY
                },
                Bumps = new Solana.Unity.Dex.Orca.Core.Types.WhirlpoolBumps
                {
                    WhirlpoolBump = whirlpoolPda.Bump
                },
                InitSqrtPrice = initSqrtPrice.Value,
                WhirlpoolPda = whirlpoolPda,
                TickSpacing = initFeeTierParams.TickSpacing,
                TokenVaultAKeyPair = tokenVaultAKeypair,
                TokenVaultBKeyPair = tokenVaultBKeypair
            };
        }

        //TODO: (MID) perhaps we need a way to pass in parameters like initSqrtPrice
        public static async Task<PoolInitResult> BuildPool(
            TestWhirlpoolContext ctx,
            InitializeConfigParams initConfigParams = null,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            ushort defaultFeeRate = 3000,
            BigInteger? initSqrtPrice = null,
            bool tokenAIsNative = false, 
            bool skipInitConfig = false,
            InitializeFeeTierParams initFeeTierParams = null
        )
        {
            initConfigParams ??= ConfigTestUtils.GenerateParams(ctx);
                
            initFeeTierParams ??= FeeTierTestUtils.GenerateDefaultParams(
                ctx,
                tickSpacing: tickSpacing,
                defaultFeeRate: defaultFeeRate
            );

            if (!skipInitConfig) 
            {
                //init config 
                var initConfigResult = await ConfigTestUtils.InitializeConfigAsync(ctx, initConfigParams);
                Assert.IsTrue(initConfigResult.WasSuccessful, $"Failed to initialize config: {initConfigResult.Reason}"); 
                Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(initConfigResult.Result, ctx.WhirlpoolClient.DefaultCommitment)); 

                //init fee tier params 
                initFeeTierParams = FeeTierTestUtils.GenerateParams(
                    ctx,
                    initConfigParams,
                    defaultFeeRate: initFeeTierParams.DefaultFeeRate,
                    tickSpacing: initFeeTierParams.TickSpacing
                );

                //init fee tier 
                var initFeeTierResult = await FeeTierTestUtils.InitializeFeeTierAsync(
                    ctx,
                    initFeeTierParams
                );
                
                Assert.IsTrue(initFeeTierResult.WasSuccessful, $"Failed to initialize fee tier: {initFeeTierResult.Reason}");
                Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(initFeeTierResult.Result, ctx.WhirlpoolClient.DefaultCommitment));
            }
            
            //create mints 
            var (tokenAMintAddr, tokenBMintAddr) = await MintUtils.CreateInOrderMints(ctx, ctx.WalletAccount, tokenAIsNative);
            Assert.IsTrue(
                !string.IsNullOrEmpty(tokenAMintAddr) && 
                !string.IsNullOrEmpty(tokenBMintAddr),
                "Failed to create mints in PoolTestUtils.BuildPool"
            );

            //generate params 
            InitializePoolParams initPoolParams = GenerateParams(
                ctx,
                tokenAMintAddr,
                tokenBMintAddr,
                initConfigParams,
                initFeeTierParams,
                initSqrtPrice,
                tokenAIsNative: tokenAIsNative
            );

            //send the request
            var initPoolResult = await InitializePoolAsync(ctx, initPoolParams);
            Assert.IsTrue(initPoolResult.WasSuccessful, $"Failed to Initialize Pool: {initPoolResult.Reason}");
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(initPoolResult.Result, ctx.WhirlpoolClient.DefaultCommitment));

            
            return new PoolInitResult
            {
                RpcResult = initPoolResult,
                InitConfigParams = initConfigParams,
                InitFeeTierParams = initFeeTierParams,
                InitPoolParams = initPoolParams,
                TokenAccountA = tokenAMintAddr, 
                TokenAccountB = tokenBMintAddr
            };
        }
        
        public static async Task<PoolInitResult> BuildPoolWithTokens(
            TestWhirlpoolContext ctx,
            InitializeConfigParams initConfigParams,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            ushort defaultFeeRate = 3000,
            BigInteger? initSqrtPrice = null,
            BigInteger? mintAmount = null,
            bool tokenAIsNative = false,
            bool skipInitConfig = false,
            InitializeFeeTierParams initFeeTierParams = null
        )
        {
            if (mintAmount == null)
                mintAmount = 15_000_000_000;   
            PoolInitResult result = await BuildPool(
                ctx, 
                initConfigParams,
                tickSpacing, 
                defaultFeeRate,
                initSqrtPrice,
                tokenAIsNative,
                skipInitConfig,
                initFeeTierParams
            ); 
            
            var (tokenAccountA, tokenAccountB) = await CreateTokens(
                ctx, 
                mintAmount.Value, 
                result.TokenAccountA, 
                result.TokenAccountB
            );
            
            result.TokenAccountA = tokenAccountA;
            result.TokenAccountB = tokenAccountB; 
            
            return result;
        }
        
        public static async Task<PoolInitWithLiquidityResult> BuildPoolWithLiquidity(
            TestWhirlpoolContext ctx,
            InitializeConfigParams initConfigParams = null,
            InitializeFeeTierParams initFeeTierParams = null,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            ushort defaultFeeRate = 3000,
            BigInteger? initSqrtPrice = null,
            BigInteger? mintAmount = null,
            bool tokenAIsNative = false,
            bool aToB = false,
            bool skipInitConfig = false
        )
        {
            if (initConfigParams == null)
                initConfigParams = ConfigTestUtils.GenerateParams(ctx);
            if (initFeeTierParams == null)
                initFeeTierParams = FeeTierTestUtils.GenerateDefaultParams(ctx); 
            
            PoolInitResult initResult = await BuildPoolWithTokens(
                ctx, 
                initConfigParams, 
                tickSpacing, 
                defaultFeeRate,
                initSqrtPrice, 
                mintAmount,
                tokenAIsNative,
                skipInitConfig
            );
            
            IList<Pda> tickArrays = await TickArrayTestUtils.InitializeTickArrayRangeAsync(
                ctx, 
                whirlpool: initResult.InitPoolParams.WhirlpoolPda,
                startTickIndex: 22528,
                arrayCount: 3, 
                tickSpacing: initResult.InitPoolParams.TickSpacing,
                aToB: false
            );

            FundedPositionParams[] fundParams = {
                new()
                {
                    LiquidityAmount = 100_000,
                    TickLowerIndex = 27904,
                    TickUpperIndex = 33408
                }
            };

            var fundedPositions = await PositionTestUtils.FundPositionsAsync(
                ctx,
                initPoolParams: initResult.InitPoolParams, 
                tokenAccountA: initResult.TokenAccountA,
                tokenAccountB: initResult.TokenAccountB,
                fundParams: fundParams
            );

            PoolInitWithLiquidityResult output = new(initResult);
            output.TickArrays = tickArrays;
            
            //TODO: (MID) this should be a collection, not a single one 
            output.InitPositionParams = fundedPositions[0].OpenPositionParams; 

            return output; 
        }
        
        private static async Task<Tuple<PublicKey, PublicKey>> CreateTokens(
            TestWhirlpoolContext ctx, 
            BigInteger mintAmount, 
            PublicKey tokenMintA, 
            PublicKey tokenMintB
        )
        {
            Transaction createAndMintToAssociatedTokenAccountA = await TokenUtilsTransaction.CreateAndMintToAssociatedTokenAccount(
                ctx.RpcClient, tokenMintA, mintAmount,
                feePayer: ctx.WalletAccount,
                destination: ctx.WalletAccount
            );
            var resultTokenA = await ctx.RpcClient.SendTransactionAsync(
                createAndMintToAssociatedTokenAccountA.Serialize(), 
                skipPreflight:true,
                ctx.WhirlpoolClient.DefaultCommitment);
            var tokenAccountA = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ctx.WalletAccount, tokenMintA);
            
            Transaction createAndMintToAssociatedTokenAccountB = await TokenUtilsTransaction.CreateAndMintToAssociatedTokenAccount(
                ctx.RpcClient, tokenMintB, mintAmount,
                feePayer: ctx.WalletAccount,
                destination: ctx.WalletAccount
            );
            var resultTokenB = await ctx.RpcClient.SendTransactionAsync(
                createAndMintToAssociatedTokenAccountB.Serialize(), 
                skipPreflight:true,
                ctx.WhirlpoolClient.DefaultCommitment);
            var tokenAccountB = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ctx.WalletAccount, tokenMintB);
            
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(resultTokenA.Result, ctx.WhirlpoolClient.DefaultCommitment));
            Assert.IsTrue(await ctx.RpcClient.ConfirmTransaction(resultTokenB.Result, ctx.WhirlpoolClient.DefaultCommitment));
            return Tuple.Create(tokenAccountA, tokenAccountB); 
        }
    }
    public class PoolInitResult
    {
        public RequestResult<string> RpcResult { get; set; }
        public InitializeConfigParams InitConfigParams { get; set; }
        public InitializeFeeTierParams InitFeeTierParams { get; set; }
        public InitializePoolParams InitPoolParams { get; set; }
        public PublicKey TokenAccountA { get; set; }
        public PublicKey TokenAccountB { get; set; }
        
        public bool WasSuccessful => RpcResult != null && RpcResult.WasSuccessful;
        
        public PoolInitResult()
        {
            this.TokenAccountA = new PublicKey("");
            this.TokenAccountB = new PublicKey("");
        }
    }

    public class PoolInitWithLiquidityResult : PoolInitResult
    {
        public OpenPositionParams InitPositionParams { get; set; }
        public IList<Pda> TickArrays { get; set; }

        public PoolInitWithLiquidityResult()
        {
            this.TickArrays = new List<Pda>();
        }

        public PoolInitWithLiquidityResult(PoolInitResult copy) : this()
        {
            this.RpcResult = copy.RpcResult;
            this.InitConfigParams = copy.InitConfigParams;
            this.InitFeeTierParams = copy.InitFeeTierParams;
            this.InitPoolParams = copy.InitPoolParams;
            this.TokenAccountA = copy.TokenAccountA;
            this.TokenAccountB = copy.TokenAccountB;
        }
    }

}