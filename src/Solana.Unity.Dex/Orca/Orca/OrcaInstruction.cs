using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Programs;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Core.Program;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Swap;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Quotes;

namespace Solana.Unity.Dex.Orca.Orca 
{
    /// <summary>
    /// Utility class for generating Transaction objects for specific commands and purposes.
    /// </summary>
    internal static class OrcaInstruction
    {

        /// <summary>
        /// Generates an instruction for a swap. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="whirlpool">Whirlpool object representing the pool on which to swap.</param>
        /// <param name="swapQuote">The swap quote</param>
        /// <param name="tokenAuthority">The token authority</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static Task<TransactionInstruction> GenerateSwapInstruction(
            IWhirlpoolContext context,
            Whirlpool whirlpool,
            SwapQuote swapQuote,
            PublicKey tokenAuthority = null
        )
        {
            if(tokenAuthority == null)
                tokenAuthority = context.WalletPubKey; 
                
            //derive associated token account addresses 
            PublicKey tokenOwnerAcctA =
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                    context.WalletPubKey, whirlpool.TokenMintA
                );

            PublicKey tokenOwnerAcctB =
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                    context.WalletPubKey, whirlpool.TokenMintB
                );

            //oracle Pda 
            var oraclePda = PdaUtils.GetOracle(AddressConstants.WHIRLPOOLS_PUBKEY, whirlpool.Address);

            //add swap instruction 
            return Task.FromResult(WhirlpoolProgram.Swap(
                new SwapAccounts
                {
                    Whirlpool = whirlpool.Address,
                    TokenAuthority = tokenAuthority,
                    TokenOwnerAccountA = tokenOwnerAcctA,
                    TokenVaultA = whirlpool.TokenVaultA,
                    TokenOwnerAccountB = tokenOwnerAcctB,
                    TokenVaultB = whirlpool.TokenVaultB,
                    TickArray0 = swapQuote.TickArray0,
                    TickArray1 = swapQuote.TickArray1,
                    TickArray2 = swapQuote.TickArray2,
                    Oracle = oraclePda,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                (ulong)swapQuote.Amount,
                otherAmountThreshold: (ulong)swapQuote.OtherAmountThreshold,
                sqrtPriceLimit: swapQuote.SqrtPriceLimit,
                amountSpecifiedIsInput: swapQuote.AmountSpecifiedIsInput,
                aToB: swapQuote.AtoB,
                programId: AddressConstants.WHIRLPOOLS_PUBKEY
            ));
        }

        /// <summary>
        /// Generates an instruction to open a position. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position's owner.</param>
        /// <param name="funderAccount">Address of the position's funder.</param>
        /// <param name="whirlpoolAddress">Address of the pool on which to open a position.</param>
        /// <param name="positionMintAccount">Address of the mint for position token.</param>
        /// <param name="tickLowerIndex">Lower tick array bound.</param>
        /// <param name="tickUpperIndex">Upper tick array bound.</param>
        /// <param name="withMetadata">True to create metadata for position token.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static TransactionInstruction GenerateOpenPositionInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            PublicKey funderAccount,
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            bool withMetadata = false
        )
        {
            if (funderAccount == null) 
                funderAccount = context.WalletPubKey; 
                
            //get the caller's associated token account address for the new mint
            
            PublicKey tokenAccountAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount, 
                positionMintAccount);

            //get the address of the position 
            Pda positionPda = PdaUtils.GetPosition(context.ProgramId, positionMintAccount);

            //get metadata address if there's any metadata
            Pda metadataPda = null;
            if (withMetadata)
            {
                metadataPda = PdaUtils.GetPositionMetadata(positionMintAccount);

                //create transaction 
                return WhirlpoolProgram.OpenPositionWithMetadata(
                    new OpenPositionWithMetadataAccounts
                    {
                        Funder = funderAccount,
                        Owner = ownerAccount,
                        Position = positionPda,
                        PositionMetadataAccount = metadataPda.PublicKey,
                        PositionMint = positionMintAccount,
                        PositionTokenAccount = tokenAccountAddress,
                        Whirlpool = whirlpoolAddress,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                        SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                        MetadataProgram = AddressConstants.METADATA_PROGRAM_PUBKEY,
                        AssociatedTokenProgram = AddressConstants.ASSOCIATED_TOKEN_PROGRAM_PUBKEY,
                        MetadataUpdateAuth = AddressConstants.METADATA_UPDATE_AUTH_PUBKEY,
                        Rent = AddressConstants.RENT_PUBKEY
                    },
                    new OpenPositionWithMetadataBumps
                    {
                        PositionBump = positionPda.Bump,
                        MetadataBump = metadataPda.Bump
                    },
                    tickLowerIndex,
                    tickUpperIndex,
                    context.ProgramId
                );
            }
            else
            {
                //create transaction 
                return WhirlpoolProgram.OpenPosition(
                    new OpenPositionAccounts
                    {
                        Funder = funderAccount,
                        Owner = ownerAccount,
                        Position = positionPda,
                        PositionMint = positionMintAccount,
                        PositionTokenAccount = tokenAccountAddress,
                        Whirlpool = whirlpoolAddress,
                        TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY,
                        SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY,
                        Rent = AddressConstants.RENT_PUBKEY,
                        AssociatedTokenProgram = AddressConstants.ASSOCIATED_TOKEN_PROGRAM_PUBKEY
                    },
                        new OpenPositionBumps
                        {
                            PositionBump = positionPda.Bump
                        },
                        tickLowerIndex,
                        tickUpperIndex,
                        context.ProgramId
                );
            }
        }

        /// <summary>
        /// Generates an instruction to close an open position. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of position owner.</param>
        /// <param name="receiverAddress">Receiver of position funds when closed.</param>
        /// <param name="positionAddress">Address of the position to close.</param>
        /// <param name="positionAuthority">Optional; if null, it's the context's wallet public key.</param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateClosePositionInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            PublicKey receiverAddress,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment
        )
        {
            if (positionAuthority == null) 
                positionAuthority = context.WalletPubKey;
                
            //retrieve the position
            Position position = await InstructionUtil.TryGetPositionAsync(
                context, positionAddress, commitment
            );

            //create transaction 
            return WhirlpoolProgram.ClosePosition(
                new ClosePositionAccounts
                {
                    PositionAuthority = positionAuthority,
                    Receiver = receiverAddress,
                    Position = positionAddress,
                    PositionMint = position.PositionMint,
                    
                    PositionTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                        context.WalletPubKey,
                            position.PositionMint
                    ),
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                context.ProgramId
            );
        }

        /// <summary>
        /// Generates an instruction to add to a position's liquidity. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position's owner.</param>
        /// <param name="position">Object representing the position to which to add liquidity.</param>
        /// <param name="positionAddress">Address of the position to which to add liquidity.</param>
        /// <param name="positionAuthority">Optional; if null, it's the context's wallet public key.</param>
        /// <param name="tokenAmountA">Desired liquidity for whirlpool's token A.</param>
        /// <param name="tokenAmountB">Desired liquidity for whirlpool's token B.</param>
        /// <param name="tokenMaxA">Max liquidity for whirlpool's token A.</param>
        /// <param name="tokenMaxB">Max liquidity for whirlpool's token B.</param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateIncreaseLiquidityInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            Position position,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            BigInteger tokenMaxA,
            BigInteger tokenMaxB,
            Commitment commitment
        )
        {
            return await GenerateIncreaseLiquidityInstruction(
                context, 
                ownerAccount, 
                position.Whirlpool,
                positionAddress,
                position.PositionMint, 
                positionAuthority,
                position.TickLowerIndex,
                position.TickUpperIndex, 
                tokenAmountA,
                tokenAmountB,
                tokenMaxA, 
                tokenMaxB, 
                commitment
            );
        }

        /// <summary>
        /// Generates an instruction to add liquidity to an open position. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position's owner.</param>
        /// <param name="whirlpoolAddress"></param>
        /// <param name="positionAddress"></param>
        /// <param name="positionMintAddress"></param>
        /// <param name="positionAuthority"></param>
        /// <param name="tickLowerIndex"></param>
        /// <param name="tickUpperIndex"></param>
        /// <param name="tokenAmountA"></param>
        /// <param name="tokenAmountB"></param>
        /// <param name="tokenMaxA"></param>
        /// <param name="tokenMaxB"></param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateIncreaseLiquidityInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            PublicKey whirlpoolAddress,
            PublicKey positionAddress,
            PublicKey positionMintAddress, 
            PublicKey positionAuthority,
            int tickLowerIndex, 
            int tickUpperIndex,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            BigInteger tokenMaxA,
            BigInteger tokenMaxB,
            Commitment commitment
        )
        {
            if (positionAuthority == null) 
                positionAuthority = context.WalletPubKey; 
                
            //position token account 
            PublicKey positionTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                positionMintAddress
            );

            //get whirlpool 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(context, whirlpoolAddress, commitment);

            //user token accounts 
            PublicKey tokenAccountA = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                whirlpool.TokenMintA
            );
            PublicKey tokenAccountB = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                whirlpool.TokenMintB
            );

            //estimate liquidity 
            BigInteger liquidityAmount = PoolUtils.EstimateLiquidityFromTokenAmounts(
                whirlpool.TickCurrentIndex,
                tickLowerIndex,
                tickUpperIndex,
                TokenAmounts.FromValues(tokenAmountA, tokenAmountB)
            );

            //tickarray addresses
            Pda tickArrayLower = PdaUtils.GetTickArray(
                context.ProgramId,
                whirlpoolAddress,
                TickUtils.GetStartTickIndex(tickLowerIndex, whirlpool.TickSpacing)
            );

            Pda tickArrayUpper = PdaUtils.GetTickArray(
                context.ProgramId,
                whirlpoolAddress,
                TickUtils.GetStartTickIndex(tickUpperIndex, whirlpool.TickSpacing)
            );

            //create instruction 
            return WhirlpoolProgram.IncreaseLiquidity(
                programId: context.ProgramId,
                accounts: new IncreaseLiquidityAccounts
                {
                    Whirlpool = whirlpoolAddress,
                    PositionAuthority = positionAuthority,
                    Position = positionAddress,
                    PositionTokenAccount = positionTokenAccount,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    TokenVaultA = whirlpool.TokenVaultA,
                    TokenVaultB = whirlpool.TokenVaultB,
                    TickArrayLower = tickArrayLower,
                    TickArrayUpper = tickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                liquidityAmount: liquidityAmount,
                tokenMaxA: (ulong)tokenMaxA,
                tokenMaxB: (ulong)tokenMaxB
            );
        }

        /// <summary>
        /// Generates an instruction to remove liquidity from an open position. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position's owner.</param>
        /// <param name="position"></param>
        /// <param name="positionAddress">The address of the position. </param>
        /// <param name="positionAuthority"></param>
        /// <param name="liquidityAmount"></param>
        /// <param name="tokenMinA"></param>
        /// <param name="tokenMinB"></param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateDecreaseLiquidityInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            Position position,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            BigInteger liquidityAmount,
            BigInteger tokenMinA,
            BigInteger tokenMinB,
            Commitment commitment
        )
        {
            if (positionAuthority == null)
                positionAuthority = context.WalletPubKey;

            //position token account 
            PublicKey positionTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                position.PositionMint
            );

            //get whirlpool 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(context, position.Whirlpool, commitment);

            //user token accounts 
            PublicKey tokenAccountA = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                whirlpool.TokenMintA
                
            );
            PublicKey tokenAccountB = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                ownerAccount,
                whirlpool.TokenMintB
            );

            //tickarray addresses
            Pda tickArrayLower = PdaUtils.GetTickArray(
                context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickLowerIndex, whirlpool.TickSpacing)
            );

            Pda tickArrayUpper = PdaUtils.GetTickArray(
                context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickUpperIndex, whirlpool.TickSpacing)
            );

            //create instruction 
            return WhirlpoolProgram.DecreaseLiquidity(
                programId: context.ProgramId,
                accounts: new DecreaseLiquidityAccounts
                {
                    Whirlpool = position.Whirlpool,
                    PositionAuthority = positionAuthority,
                    Position = positionAddress,
                    PositionTokenAccount = positionTokenAccount,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    TokenVaultA = whirlpool.TokenVaultA,
                    TokenVaultB = whirlpool.TokenVaultB,
                    TickArrayLower = tickArrayLower,
                    TickArrayUpper = tickArrayUpper,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                liquidityAmount: liquidityAmount,
                tokenMinA: (ulong)tokenMinA,
                tokenMinB: (ulong)tokenMinB
            );
        }

        /// <summary>
        /// Generates an instruction to update a position's fees and rewards.
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="position">Object representing the position.</param>
        /// <param name="positionAddress">The address of the position. </param>
        /// <param name="tickArrayLower">Tick array lower bound.</param>
        /// <param name="tickArrayUpper">Tick array upper bound.</param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static TransactionInstruction GenerateUpdateFeesAndRewardsInstruction(
            IWhirlpoolContext context,
            Position position,
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            Commitment commitment = Commitment.Finalized
        )
        {
            return WhirlpoolProgram.UpdateFeesAndRewards(
                new UpdateFeesAndRewardsAccounts
                {
                    Whirlpool = position.Whirlpool,
                    Position = positionAddress,
                    TickArrayLower = tickArrayLower,
                    TickArrayUpper = tickArrayUpper
                },
                programId: context.ProgramId
            );
        }

        /// <summary>
        /// Generates an instruction to collect a position's fees.
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position owner.</param>
        /// <param name="position">Object representing the position.</param>
        /// <param name="positionAddress">The address of the position. </param>
        /// <param name="positionAuthority">Optional; defaults to position owner address if null.</param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateCollectFeesInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            Position position,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment = Commitment.Finalized
        )
        {
            if (positionAuthority == null)
                positionAuthority = context.WalletPubKey;

            //retrieve the whirlpool 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(context, position.Whirlpool, commitment);

            //get caller's token accounts addresses
            PublicKey positionTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAccount, position.PositionMint);
            PublicKey tokenAccountA = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount( ownerAccount, whirlpool.TokenMintA);
            PublicKey tokenAccountB = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAccount, whirlpool.TokenMintB);

            return WhirlpoolProgram.CollectFees(
                new CollectFeesAccounts
                {
                    Whirlpool = position.Whirlpool,
                    PositionAuthority = positionAuthority,
                    Position = positionAddress,
                    PositionTokenAccount = positionTokenAccount,
                    TokenVaultA = whirlpool.TokenVaultA,
                    TokenVaultB = whirlpool.TokenVaultB,
                    TokenOwnerAccountA = tokenAccountA,
                    TokenOwnerAccountB = tokenAccountB,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                programId: context.ProgramId
            );
        }

        /// <summary>
        /// Generates an instruction to collect a position's rewards.
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="ownerAccount">Address of the position owner.</param>
        /// <param name="position">Object representing the position.</param>
        /// <param name="positionAddress">The address of the position. </param>
        /// <param name="rewardMintAddress"></param>
        /// <param name="rewardVaultAddress"></param>
        /// <param name="rewardTokenOwnerAddress"></param>
        /// <param name="positionAuthority">Optional; defaults to position owner address if null.</param>
        /// <param name="rewardIndex"></param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static async Task<TransactionInstruction> GenerateCollectRewardsInstruction(
            IWhirlpoolContext context,
            PublicKey ownerAccount,
            Position position,
            PublicKey positionAddress,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            PublicKey rewardTokenOwnerAddress,
            PublicKey positionAuthority,
            byte rewardIndex = 0,
            Commitment commitment = Commitment.Finalized
        )
        {
            if (positionAuthority == null)
                positionAuthority = context.WalletPubKey;
                
            //retrieve the whirlpool 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(context, position.Whirlpool, commitment);

            //get caller's token accounts addresses
            PublicKey positionTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAccount, position.PositionMint);

            TransactionBuilder tb = new TransactionBuilder();

            return WhirlpoolProgram.CollectReward(
                new CollectRewardAccounts
                {
                    Whirlpool = position.Whirlpool,
                    PositionAuthority = positionAuthority,
                    Position = positionAddress,
                    PositionTokenAccount = positionTokenAccount,
                    RewardOwnerAccount = rewardTokenOwnerAddress,
                    RewardVault = rewardVaultAddress,
                    TokenProgram = AddressConstants.TOKEN_PROGRAM_PUBKEY
                },
                rewardIndex: rewardIndex,
                programId: context.ProgramId
            );
        }

        /// <summary>
        /// Generates an instruction to add a tick array to a whirlpool. 
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        /// <param name="whirlpoolAddress">Address of the whirlpool for which to initialize tick array.</param>
        /// <param name="funderAccount"></param>
        /// <param name="tickIndex">Unadjusted start index of the tick array.</param>
        /// <param name="tickSpacing">The whirlpool's tick spacing.</param>
        /// <param name="commitment">Commitment to be used for any queries necessary to build the transaction.</param>
        /// <returns>A TransactionInstruction object.</returns>
        public static TransactionInstruction GenerateInitializeTickArrayInstruction(
            IWhirlpoolContext context,
            PublicKey whirlpoolAddress,
            PublicKey funderAccount, 
            int tickIndex,
            ushort tickSpacing,
            Commitment commitment = Commitment.Finalized
        )
        {
            //funder has a default 
            if (funderAccount == null) 
                funderAccount = context.WalletPubKey;
                
            //derive start index and pda
            int startTickIndex = TickUtils.GetStartTickIndex(tickIndex, tickSpacing);
            Pda tickArrayPda = PdaUtils.GetTickArray(context.ProgramId, whirlpoolAddress, startTickIndex);
            
            return WhirlpoolProgram.InitializeTickArray(
                new InitializeTickArrayAccounts
                {
                    Whirlpool = whirlpoolAddress,
                    TickArray = tickArrayPda, 
                    Funder = funderAccount, 
                    SystemProgram = AddressConstants.SYSTEM_PROGRAM_PUBKEY
                },
                startTickIndex, 
                context.ProgramId
            ); 
        }
    }
}