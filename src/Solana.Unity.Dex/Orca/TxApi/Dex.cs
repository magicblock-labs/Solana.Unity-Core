using System;
using System.Numerics; 
using System.Threading.Tasks;

using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Orca.Quotes.Swap;
using Solana.Unity.Dex.Orca.Core;
using Solana.Unity.Dex.Orca.Core.Accounts;

namespace Solana.Unity.Dex.Orca.TxApi
{
    /// <summary> 
    /// Abstract implementation of IDex. 
    /// </summary> 
    public abstract class Dex : IDex
    {
        protected readonly IWhirlpoolContext _context;
        
        //Gets the RpcClient from the context object. 
        protected IRpcClient RpcClient => _context.RpcClient;
        
        //Gets the WhirlpoolClient from the context object. 
        protected WhirlpoolClient WhirlpoolClient => _context.WhirlpoolClient;

        protected Dex(IWhirlpoolContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public abstract Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            ulong amount,
            bool aToB,
            PublicKey tokenAuthority = null,
            Commitment commitment = Commitment.Finalized
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> OpenPosition(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            bool withMetadata,
            PublicKey funderAccount,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithMetadata(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey funderAccount,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> OpenPositionWithLiquidity(
            PublicKey positionMintAccount,
            PublicKey whirlpoolAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            BigInteger tokenMaxA,
            BigInteger tokenMaxB,
            bool withMetadata,
            PublicKey funderAddress,
            Commitment commitment
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenMaxA, 
            BigInteger tokenMaxB,
            PublicKey positionAuthority,
            Commitment commitment
        );
        
        /// <inheritdoc />
        public abstract Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount, 
            BigInteger tokenMinA, 
            BigInteger tokenMinB,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex,
            PublicKey positionAuthority,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<bool> WhirlpoolExists(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            PublicKey configAccountAddress,
            ushort tickSpacing,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<Tuple<PublicKey, Whirlpool>> FindWhirlpool(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing,
            PublicKey configAccountAddress,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<SwapQuote> GetSwapQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            BigInteger tokenAmount,
            Percentage slippageTolerance,
            TokenType amountSpecifiedTokenType,
            bool amountSpecifiedIsInput,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            double inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment commitment
        );

        /// <inheritdoc />
        public abstract Task<DecreaseLiquidityQuote> GetDecreaseLiquidityQuote(
            PublicKey positionAddress,
            ulong liquidityAmount,
            double slippageTolerance,
            Commitment commitment
        );
    }
}