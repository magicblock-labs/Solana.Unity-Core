using Solana.Unity.Dex.Math;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;

using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Quotes;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Quotes.Swap;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.Orca;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Ticks;
using System.Linq;
using Solana.Unity.Rpc.Core.Http;

namespace Orca
{
    /// <summary> 
    /// Concrete implementation of IDex for Orca Whirlpools. 
    /// </summary> 
    public class OrcaDex: Solana.Unity.Dex.Orca.TxApi.Dex
    {
        /// <summary>
        /// Public constructor; accepts and holds the whirlpool context.
        /// </summary>
        /// <param name="context">Whirlpool context object.</param>
        public OrcaDex(IWhirlpoolContext context) : base(context) { }

        /// <summary>
        /// Public constructor; Create the IWhirlpoolContext. 
        /// </summary>
        /// <param name="account"></param>
        /// <param name="rpcClient"></param>
        /// <param name="programId"></param>
        /// <param name="streamingRpcClient"></param>
        /// <param name="commitment"></param>
        public OrcaDex(
            PublicKey account, 
            IRpcClient rpcClient, 
            IStreamingRpcClient streamingRpcClient = null, 
            PublicKey programId = null,
            Commitment? commitment = null) : this(
            new WhirlpoolContext(
                programId != null ? programId : AddressConstants.WHIRLPOOLS_PUBKEY,
                rpcClient, 
                streamingRpcClient, 
                account, 
                commitment.GetValueOrDefault(Commitment.Finalized))
            ) { }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            BigInteger amount,
            PublicKey inputTokenMintAddress,
            bool amountSpecifiedIsInput = true,
            double slippage = 0.01,
            bool unwrapSol = true,
            Commitment? commitment = null
        )
        {
            SwapQuote swapQuote = await GetSwapQuoteFromWhirlpool(
                whirlpoolAddress, 
                amount, 
                inputTokenMintAddress, 
                slippage,
                amountSpecifiedIsInput,
                commitment: commitment.GetValueOrDefault(DefaultCommitment));
            return await SwapWithQuote(whirlpoolAddress, swapQuote, unwrapSol, commitment);
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<Transaction> SwapWithQuote(
            PublicKey whirlpoolAddress,
            SwapQuote swapQuote,
            bool unwrapSol = true,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey; 
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment)
            );

            var tb = new TransactionBuilder();

            // instruction to create token account A 
            var ataA = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );

            // instruction to create token account B
            var ataB = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );

            // Wrap to wSOL if necessary
            if (whirlpool.TokenMintA.Equals(AddressConstants.NATIVE_MINT_PUBKEY) && swapQuote.AtoB)
            {
                SyncIfNative(account, whirlpool.TokenMintA, swapQuote.Amount, tb);
            }
            
            if (whirlpool.TokenMintB.Equals(AddressConstants.NATIVE_MINT_PUBKEY) && !swapQuote.AtoB)
            {
                SyncIfNative(account, whirlpool.TokenMintB, swapQuote.Amount, tb);
            }

            //generate the transaction 
            tb.AddInstruction(
                await OrcaInstruction.GenerateSwapInstruction(
                    Context,
                    whirlpool,
                    swapQuote
                )
            );
            
            // Close wrapped sol account
            if (unwrapSol && whirlpool.TokenMintA.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                tb.AddInstruction(TokenProgram.CloseAccount(
                    ataA,
                    account,
                    account,
                    TokenProgram.ProgramIdKey)
                );
            }
            
            // Close wrapped sol account
            if (unwrapSol && whirlpool.TokenMintB.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                tb.AddInstruction(TokenProgram.CloseAccount(
                    ataB,
                    account,
                    account,
                    TokenProgram.ProgramIdKey)
                );
            }

            //set fee payer and recent blockhash 
            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        /// <exception cref="System.Exception">Thrown if the specified position already exists.</exception>
        public override async Task<Transaction> OpenPosition(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex, 
            int tickUpperIndex,
            bool withMetadata = false,
            PublicKey funderAccount = null, 
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throw if whirlpool doesn't exist
            await InstructionUtil.TryGetWhirlpoolAsync(Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            //throw if position already exists 
            if (await InstructionUtil.PositionExists(Context, positionMintAccount, commitment.GetValueOrDefault(DefaultCommitment)))
                throw new Exception($"A position for mint {positionMintAccount} on whirlpool {whirlpoolAddress} already exists");

            //funder defaults to account 
            if (funderAccount == null)
                funderAccount = account;             
                
            //generate the transaction 
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateOpenPositionInstruction(
                    Context,
                    account,
                    funderAccount,
                    whirlpoolAddress,
                    positionMintAccount,
                    tickLowerIndex,
                    tickUpperIndex,
                    withMetadata
                )
            );

            //set fee payer and recent blockhash 
            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        /// <exception cref="System.Exception">Thrown if the specified position already exists.</exception>
        public override async Task<Transaction> OpenPositionWithMetadata(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAccount,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey funderAccount = null, 
            Commitment? commitment = null
        )
        {
            return await OpenPosition(
                whirlpoolAddress,
                positionMintAccount, 
                tickLowerIndex, 
                tickUpperIndex, 
                withMetadata: true,
                commitment: commitment
            );
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        /// <exception cref="System.Exception">Thrown if the specified position already exists.</exception>
        public override async Task<Transaction> OpenPositionWithLiquidity(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            BigInteger tokenAmountA,
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throw if position already exists 
            if (await InstructionUtil.PositionExists(Context, positionMintAddress, commitment.GetValueOrDefault(DefaultCommitment)))
                throw new Exception($"A position for mint {positionMintAddress} on whirlpool {whirlpoolAddress} already exists");

            //throw if whirlpool doesn't exist 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment)); 
            
            TransactionBuilder tb = new();

            //need to initialize tick arrays? then do so
            int lowerStartTick = await CreateInitializeTickArrayInstructionIfNotExtant(
                whirlpoolAddress, funderAccount, tickLowerIndex, whirlpool.TickSpacing, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            //only initialize the second one if different from the first 
            int upperStartTick = TickUtils.GetStartTickIndex(tickUpperIndex, whirlpool.TickSpacing);
            if (upperStartTick != lowerStartTick) 
            {
                await CreateInitializeTickArrayInstructionIfNotExtant(
                    whirlpoolAddress, funderAccount, tickUpperIndex, whirlpool.TickSpacing, tb, commitment.GetValueOrDefault(DefaultCommitment)
                );
            }

            //instruction to open position 
            tb.AddInstruction(
                OrcaInstruction.GenerateOpenPositionInstruction(
                    Context,
                    account,
                    funderAccount,
                    whirlpoolAddress,
                    positionMintAddress,
                    tickLowerIndex,
                    tickUpperIndex,
                    withMetadata
                )
            );

            //get position address 
            Pda positionPda = PdaUtils.GetPosition(Context.ProgramId, positionMintAddress);
            
            BigInteger tokenMaxA = TokenMath.AdjustForSlippage(
                tokenAmountA, slippageTolerance, true
            );
            BigInteger tokenMaxB = TokenMath.AdjustForSlippage(
                tokenAmountB, slippageTolerance, true
            );

            IncreaseLiquidityQuote quote = new()
            {
                TokenEstA = tokenAmountA,
                TokenEstB = tokenAmountB,
                TokenMaxA = tokenMaxA,
                TokenMaxB = tokenMaxB
            };

            await IncreaseLiquidityInstructions(
                tb,
                whirlpool: whirlpool,
                quote: quote,
                account: account,
                positionMintAddress: positionMintAddress,
                tickLowerIndex: tickLowerIndex,
                tickUpperIndex: tickUpperIndex,
                positionAddress: positionPda,
                positionAuthority: account,
                commitment: commitment.GetValueOrDefault(DefaultCommitment)
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        /// <exception cref="System.Exception">Thrown if the specified position already exists.</exception>
        public override async Task<Transaction> OpenPositionWithLiquidityWithQuote(
            PublicKey whirlpoolAddress,
            PublicKey positionMintAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throw if position already exists 
            if (await InstructionUtil.PositionExists(Context, positionMintAddress, commitment.GetValueOrDefault(DefaultCommitment)))
                throw new Exception($"A position for mint {positionMintAddress} on whirlpool {whirlpoolAddress} already exists");

            //throw if whirlpool doesn't exist 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment)); 
            
            TransactionBuilder tb = new();

            //need to initialize tick arrays? then do so
            int lowerStartTick = await CreateInitializeTickArrayInstructionIfNotExtant(
                whirlpoolAddress, funderAccount, tickLowerIndex, whirlpool.TickSpacing, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            //only initialize the second one if different from the first 
            int upperStartTick = TickUtils.GetStartTickIndex(tickUpperIndex, whirlpool.TickSpacing);
            if (upperStartTick != lowerStartTick) 
            {
                await CreateInitializeTickArrayInstructionIfNotExtant(
                    whirlpoolAddress, funderAccount, tickUpperIndex, whirlpool.TickSpacing, tb, commitment.GetValueOrDefault(DefaultCommitment)
                );
            }

            //instruction to open position 
            tb.AddInstruction(
                OrcaInstruction.GenerateOpenPositionInstruction(
                    Context,
                    account,
                    funderAccount,
                    whirlpoolAddress,
                    positionMintAddress,
                    tickLowerIndex,
                    tickUpperIndex,
                    withMetadata
                )
            );

            //get position address 
            Pda positionPda = PdaUtils.GetPosition(Context.ProgramId, positionMintAddress);

            await IncreaseLiquidityInstructions(
                tb,
                whirlpool: whirlpool,
                quote: increaseLiquidityQuote,
                account: account,
                positionMintAddress: positionMintAddress,
                tickLowerIndex: tickLowerIndex,
                tickUpperIndex: tickUpperIndex,
                positionAddress: positionPda,
                positionAuthority: account,
                commitment: commitment.GetValueOrDefault(DefaultCommitment)
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress = null,
            PublicKey positionAuthority = null, 
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //receiver is the closer/owner if not otherwise specified 
            if (receiverAddress == null)
                receiverAddress = account;
                
            //retrieve the position 
            Position position = await InstructionUtil.TryGetPositionAsync(
                Context, 
                positionAddress, 
                commitment.GetValueOrDefault(DefaultCommitment));
            
            //retrieve the whirlpool
            Whirlpool whirlpool = (await Context.WhirlpoolClient.GetWhirlpoolAsync(
                    position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment))).ParsedResult;
            
            //generate the transaction 
            TransactionBuilder tb = new();
            
            //create token accounts if they don't exist
            HashSet<PublicKey> mints = new() { whirlpool.TokenMintA, whirlpool.TokenMintB };
            whirlpool.RewardInfos.ToList().ForEach(rewardInfo => mints.Add(rewardInfo.Mint));
            mints.ToList().ForEach(async mint =>
            {
                await CreateAssociatedTokenAccountInstructionIfNotExtant(
                    account, mint, tb, commitment.GetValueOrDefault(DefaultCommitment)
                );
            });
            
            //add instruction to remove liquidity, if position has liquidity 
            if (position.Liquidity > 0) 
            {
                tb.AddInstruction(
                    await OrcaInstruction.GenerateDecreaseLiquidityInstruction(
                        Context, 
                        account,
                        position,
                        positionAddress,
                        positionAuthority,
                        position.Liquidity,
                        0, 0, 
                        commitment.GetValueOrDefault(DefaultCommitment)
                    )
                );
            }

            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectFeesInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );
            
            //add instruction to collect rewards, if available
            for (var rewardIndex = 0; rewardIndex < position.RewardInfos.Length; rewardIndex++)
            {
                // Skip if no rewards
                if(whirlpool.RewardInfos[rewardIndex].Mint.Equals(PublicKey.DefaultPublicKey))continue;
                
                PublicKey rewardTokenOwnerAddress = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(account,
                    whirlpool.RewardInfos[rewardIndex].Mint);

                //instruction to collect rewards 
                tb.AddInstruction(
                    await OrcaInstruction.GenerateCollectRewardsInstruction(
                        Context,
                        account,
                        position,
                        positionAddress,
                        whirlpool.RewardInfos[rewardIndex].Mint,
                        whirlpool.RewardInfos[rewardIndex].Vault,
                        rewardTokenOwnerAddress,
                        positionAuthority,
                        (byte)rewardIndex,
                        commitment.GetValueOrDefault(DefaultCommitment)
                    )
                );
            }

            //close the position 
            tb.AddInstruction(
                await OrcaInstruction.GenerateClosePositionInstruction(
                    Context,
                    account,
                    receiverAddress,
                    positionAddress,
                    positionAuthority,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            //set fee payer and recent blockhash 
            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenAmountA, 
            BigInteger tokenAmountB,
            double slippageTolerance = 0,
            PublicKey positionAuthority = null, 
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throw if position doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
           
            //retrieve the whirlpool
            Whirlpool whirlpool = (await Context.WhirlpoolClient.GetWhirlpoolAsync(
                position.Whirlpool)).ParsedResult;
            
            BigInteger tokenMaxA = TokenMath.AdjustForSlippage(
                tokenAmountA, slippageTolerance, true
            );
            BigInteger tokenMaxB = TokenMath.AdjustForSlippage(
                tokenAmountB, slippageTolerance, true
            );

            TransactionBuilder tb = new();
            IncreaseLiquidityQuote increaseLiquidityQuote = new()
            {
                TokenEstA = tokenAmountA,
                TokenEstB = tokenAmountB,
                TokenMaxA = tokenMaxA,
                TokenMaxB = tokenMaxB
            };
            
            await IncreaseLiquidityInstructions(tb, 
                whirlpool, 
                increaseLiquidityQuote, 
                account, 
                position, 
                positionAddress, 
                positionAuthority,
                commitment.GetValueOrDefault(DefaultCommitment)
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> IncreaseLiquidityWithQuote(
            PublicKey positionAddress,
            IncreaseLiquidityQuote increaseLiquidityQuote,
            PublicKey positionAuthority = null, 
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throw if position doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));

            //retrieve the whirlpool
            Whirlpool whirlpool = (await Context.WhirlpoolClient.GetWhirlpoolAsync(
                position.Whirlpool)).ParsedResult;
            
            TransactionBuilder tb = new();
            
            await IncreaseLiquidityInstructions(tb, 
                whirlpool, 
                increaseLiquidityQuote, 
                account, 
                position, 
                positionAddress, 
                positionAuthority,
                commitment.GetValueOrDefault(DefaultCommitment)
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            BigInteger tokenMinA, 
            BigInteger tokenMinB,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            //throws if position doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );

            TransactionBuilder tb = new();
            
            // instruction to create token account A 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );

            // instruction to create token account B
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            tb.AddInstruction(
                await OrcaInstruction.GenerateDecreaseLiquidityInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    liquidityAmount,
                    tokenMinA, 
                    tokenMinB,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey; 
            
            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            // tick array addresses
            Pda tickArrayLowerPda = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickLowerIndex, whirlpool.TickSpacing)
            );

            Pda tickArrayUpperPda = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickUpperIndex, whirlpool.TickSpacing)
            );
            
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    Context,
                    position,
                    positionAddress,
                    tickArrayLowerPda.PublicKey,
                    tickArrayUpperPda.PublicKey,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );

            TransactionBuilder tb = new();
            
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment.GetValueOrDefault(DefaultCommitment)
            ); 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment.GetValueOrDefault(DefaultCommitment)
            ); 
            
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectFeesInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;
            
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );

            TransactionBuilder tb = new();
            
            //optional instruction to create reward token account (if it doesn't exist) 
            PublicKey rewardTokenOwnerAddress = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.RewardInfos[rewardIndex].Mint, tb, commitment.GetValueOrDefault(DefaultCommitment)
            ); 
            
            //instruction to collect rewards 
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectRewardsInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    whirlpool.RewardInfos[rewardIndex].Mint, 
                    whirlpool.RewardInfos[rewardIndex].Vault,
                    rewardTokenOwnerAddress,
                    positionAuthority,
                    rewardIndex,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            // tick array addresses
            Pda tickArrayLower = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickLowerIndex, whirlpool.TickSpacing)
            );

            Pda tickArrayUpper = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickUpperIndex, whirlpool.TickSpacing)
            );

            //instruction to udpate fees/rewards 
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    Context,
                    position,
                    positionAddress,
                    tickArrayLower,
                    tickArrayUpper,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )

                //instruction to collect fees 
            );
            
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment.GetValueOrDefault(DefaultCommitment)
            ); 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment.GetValueOrDefault(DefaultCommitment)
            ); 
                
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectFeesInstruction(
                    Context,
                    account,
                    position, 
                    positionAddress,
                    positionAuthority,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            byte rewardIndex,
            PublicKey positionAuthority = null,
            Commitment? commitment = null
        )
        {
            PublicKey account = Context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            
            // get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment)
            );
            
            // tick array addresses
            Pda tickArrayLower = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickLowerIndex, whirlpool.TickSpacing)
            );

            Pda tickArrayUpper = PdaUtils.GetTickArray(
                Context.ProgramId,
                position.Whirlpool,
                TickUtils.GetStartTickIndex(position.TickUpperIndex, whirlpool.TickSpacing)
            );

            //instruction to update fees/rewards
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    Context,
                    position,
                    positionAddress,
                    tickArrayLower,
                    tickArrayUpper,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            //optional instruction to create reward token account (if it doesn't exist) 
            PublicKey rewardTokenOwnerAddress = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.RewardInfos[rewardIndex].Mint, tb, commitment.GetValueOrDefault(DefaultCommitment)
            );

            //instruction to collect rewards
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectRewardsInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    whirlpool.RewardInfos[rewardIndex].Mint, 
                    whirlpool.RewardInfos[rewardIndex].Vault,
                    rewardTokenOwnerAddress,
                    positionAuthority,
                    rewardIndex,
                    commitment.GetValueOrDefault(DefaultCommitment)
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        public override async Task<bool> WhirlpoolExists(
            PublicKey tokenMintA, 
            PublicKey tokenMintB,
            PublicKey configAccountAddress = null,
            ushort tickSpacing = TickSpacing.HundredTwentyEight,
            Commitment? commitment = null
        )
        {
            if (configAccountAddress == null) 
                configAccountAddress = AddressConstants.WHIRLPOOLS_CONFIG_PUBKEY;
                
            Pda whirlpoolPda = PdaUtils.GetWhirlpool(
                Context.ProgramId,
                configAccountAddress,
                tokenMintA, tokenMintB, tickSpacing
            );
            
            //attempt to get whirlpool 
            var whirlpoolResult = await Context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, commitment); 
            
            //return true if whirlpool retrieved successfully
            return whirlpoolResult.WasSuccessful && whirlpoolResult.ParsedResult != null; 
        }

        /// <inheritdoc />
        public override async Task<Tuple<PublicKey, Whirlpool>> FindWhirlpool(
            PublicKey tokenMintA,
            PublicKey tokenMintB, 
            ushort tickSpacing = 128,
            PublicKey configAccountAddress = null, 
            Commitment? commitment = null
        )
        {
            var result = await TryFindWhirlpool(tokenMintA, tokenMintB, tickSpacing, configAccountAddress, commitment); 
            
            if (result.Item2 == null) 
            {
                var promises = new List<Task<Tuple<PublicKey, Whirlpool>>>(); 
                
                promises.Add(TryFindWhirlpool(tokenMintA, tokenMintB, 1, configAccountAddress, commitment));
                
                for (ushort ts = 8; ts <= 256; ts*=2) 
                {
                    promises.Add(TryFindWhirlpool(tokenMintA, tokenMintB, ts, configAccountAddress, commitment));
                }
                
                await Task.WhenAll(promises);
                foreach(var p in promises) 
                {
                    if (p.Result != null && p.Result.Item2 != null && p.Result.Item2.Liquidity > 0) 
                    {
                        result = p.Result;
                        break;
                    }
                }
            }
            
            return result;
        }

        /// <inheritdoc />
        public override async Task<Whirlpool> GetWhirlpool(
            PublicKey whirlpoolAddress, 
            Commitment? commitment = null)
        {
            return await InstructionUtil.TryGetWhirlpoolAsync(Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment));
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<SwapQuote> GetSwapQuoteFromWhirlpool(
            Whirlpool whirlpool,
            BigInteger tokenAmount,
            PublicKey inputTokenMintAddress,
            double slippageTolerance = 0.01,
            bool amountSpecifiedIsInput = true
        )
        {
            return SwapQuoteUtils.SwapQuoteWithParams(
                await SwapQuoteUtils.SwapQuoteByToken(
                    Context,
                    whirlpool,
                    whirlpool.Address,
                    inputTokenMintAddress,
                    tokenAmount,
                    amountSpecifiedIsInput,
                    Context.ProgramId
                ),
                slippageTolerance
            );
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            BigInteger inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment? commitment = null
        )
        {
            //throws if whirlpool doesn't exist 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(Context, whirlpoolAddress, commitment.GetValueOrDefault(DefaultCommitment));

            return IncreaseLiquidityQuoteUtils.GenerateIncreaseQuote(
                inputTokenMintAddress,
                inputTokenAmount,
                tickLowerIndex,
                tickUpperIndex,
                Percentage.FromDouble(slippageTolerance), 
                whirlpool
            );
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<DecreaseLiquidityQuote> GetDecreaseLiquidityQuote(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            double slippageTolerance,
            Commitment? commitment = null
        )
        {
            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(Context, position.Whirlpool, commitment.GetValueOrDefault(DefaultCommitment));

            return DecreaseLiquidityQuoteUtils.GenerateDecreaseQuote(
                liquidityAmount,
                Percentage.FromDouble(slippageTolerance),
                position,
                whirlpool
            );
        }

        /// <inheritdoc />
        public override async Task<IList<PublicKey>> GetPositions(
            PublicKey owner = null, 
            Commitment? commitment = null)
        {
            List<PublicKey> positions = new();
            if (owner == null)
                owner = Context.WalletPubKey;
            var tokenAccountsRes = await Context.RpcClient.GetTokenAccountsByOwnerAsync(
                owner, 
                null,
                TokenProgram.ProgramIdKey,
                commitment: commitment.GetValueOrDefault(DefaultCommitment));
            List<TokenAccount> tokenAccounts = tokenAccountsRes.Result.Value;

            foreach (var tk in tokenAccounts)
            {
                var mint = tk.Account.Data.Parsed.Info.Mint;
                var positionAddress = PdaUtils.GetPosition(Context.ProgramId, new PublicKey(mint));
                try
                {
                    await InstructionUtil.TryGetPositionAsync(Context, positionAddress, commitment.GetValueOrDefault(DefaultCommitment));
                    positions.Add(positionAddress);
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
            return positions;
        }


        #region Utils

        /// <summary> 
        /// Adds a recent blockhash, sets the fee payer, and converts to a Transaction instance.
        /// </summary> 
        private static async Task<Transaction> PrepareTransaction(
            TransactionBuilder tb, 
            PublicKey feePayer,
            IRpcClient rpcClient,
            Commitment commitment
        )
        {
            tb.SetFeePayer(feePayer);
            var latestBlockHash = await rpcClient.GetLatestBlockHashAsync();
            tb.SetRecentBlockHash(latestBlockHash.Result.Value.Blockhash);

            return Transaction.Deserialize(tb.Serialize());
        }

        /// <summary> 
        /// Determines whether or not an associated token account with the given characteristics can 
        /// be found, and adds an instruction to create one to the given TransactionBuilder, if not. 
        /// </summary> 
        private async Task<PublicKey> CreateAssociatedTokenAccountInstructionIfNotExtant(
            PublicKey ownerAddress,
            PublicKey mintAddress,
            TransactionBuilder builder,
            Commitment commitment
        )
        {
            if (mintAddress.Equals(PublicKey.DefaultPublicKey)) return null;
            bool exists = await InstructionUtil.AssociatedTokenAccountExists(
                Context, ownerAddress, mintAddress, commitment
            );
            if (!exists)
            {
                builder.AddInstruction(
                    AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        ownerAddress, ownerAddress, mintAddress, idempotent: true
                    )
                );
            }
            return AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAddress, mintAddress);
        }

        private void SyncIfNative(
            PublicKey ownerAddress,
            PublicKey mintAddress,
            BigInteger amount,
            TransactionBuilder builder)
        {
            if (mintAddress.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                var ata = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAddress, mintAddress);
                var transfer = SystemProgram.Transfer(
                    ownerAddress, 
                    ata,
                    (ulong)(amount));
                var nativeSync = TokenProgram.SyncNative(ata);

                builder.AddInstruction(transfer);
                builder.AddInstruction(nativeSync);
            }
        }

        /// <summary> 
        /// Determines whether an appropriate tick array has been initialized for the given characteristics, 
        /// and adds an instruction to create one to the given TransactionBuilder, if not. 
        /// </summary> 
        /// <returns>The start index generated from the given index and tickSpacing.</returns>
        private async Task<int> CreateInitializeTickArrayInstructionIfNotExtant(
            PublicKey whirlpoolAddress,
            PublicKey funderAccount,
            int tickIndex,
            ushort tickSpacing,
            TransactionBuilder builder,
            Commitment commitment
        )
        {
            int startTickIndex = TickUtils.GetStartTickIndex(tickIndex, tickSpacing);
            if (!await InstructionUtil.TickArrayIsInitialized(
                Context, whirlpoolAddress, startTickIndex, commitment)
            )
            {
                builder.AddInstruction(
                    OrcaInstruction.GenerateInitializeTickArrayInstruction(
                        Context,
                        whirlpoolAddress,
                        funderAccount,
                        startTickIndex,
                        tickSpacing,
                        commitment
                    )
                );
            }
            return startTickIndex;
        }

        /// <summary> 
        /// Used by <see cref="FindWhirlpool">FindWhirlpool</see> to find a whirlpool.
        /// </summary> 
        private async Task<Tuple<PublicKey, Whirlpool>> TryFindWhirlpool(
            PublicKey tokenMintA,
            PublicKey tokenMintB,
            ushort tickSpacing,
            PublicKey configAccountAddress = null,
            Commitment? commitment = null
        )
        {
            if (configAccountAddress == null)
                configAccountAddress = AddressConstants.WHIRLPOOLS_CONFIG_PUBKEY;

            //get whirlpool address
            Pda whirlpoolPda = PdaUtils.GetWhirlpool(
                Context.ProgramId,
                configAccountAddress,
                tokenMintA,
                tokenMintB,
                tickSpacing
            );

            //try to retrieve the whirlpool
            Whirlpool whirlpool = (
                await Context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey.ToString(), commitment)
            ).ParsedResult;

            //if that didn't work, try to reverse the token mints
            if (whirlpool == null)
            {
                Pda whirlpoolPdaAlt = PdaUtils.GetWhirlpool(
                    Context.ProgramId,
                    configAccountAddress,
                    tokenMintB,
                    tokenMintA,
                    tickSpacing
                );

                //try to retrieve the whirlpool
                whirlpool = (
                    await Context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPdaAlt.PublicKey.ToString(), commitment)
                ).ParsedResult;

                //if that worked, return the alt address 
                if (whirlpool != null)
                    whirlpoolPda = whirlpoolPdaAlt;
            }

            return Tuple.Create<PublicKey, Whirlpool>(whirlpoolPda, whirlpool);
        }

        #endregion

        #region IstructionUtils

        private async Task IncreaseLiquidityInstructions(
            TransactionBuilder tb,
            Whirlpool whirlpool, 
            IncreaseLiquidityQuote quote,
            PublicKey account,
            Position position,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment)
        {
            // instruction to create token account A 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment
            );
            // Wrap to wSOL if necessary
            if (whirlpool.TokenMintA.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                SyncIfNative(account, whirlpool.TokenMintA, quote.TokenMaxA, tb);
            }

            // instruction to create token account B
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment
            );
            // Wrap to wSOL if necessary
            if (whirlpool.TokenMintB.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                SyncIfNative(account, whirlpool.TokenMintB, quote.TokenMaxB, tb);
            }

            tb.AddInstruction(
                await OrcaInstruction.GenerateIncreaseLiquidityInstruction(
                    Context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    quote.TokenEstA,
                    quote.TokenEstB,
                    quote.TokenMaxA,
                    quote.TokenMaxB,
                    commitment
                )
            );
        }
        
        
        private async Task IncreaseLiquidityInstructions(
            TransactionBuilder tb,
            Whirlpool whirlpool, 
            IncreaseLiquidityQuote quote,
            PublicKey account,
            PublicKey positionMintAddress,
            int tickLowerIndex,
            int tickUpperIndex,
            PublicKey positionAddress,
            PublicKey positionAuthority,
            Commitment commitment)
        {
            // instruction to create token account A 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment
            );
            // Wrap to wSOL if necessary
            if (whirlpool.TokenMintA.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                SyncIfNative(account, whirlpool.TokenMintA, quote.TokenMaxA, tb);
            }

            // instruction to create token account B
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment
            );
            // Wrap to wSOL if necessary
            if (whirlpool.TokenMintB.Equals(AddressConstants.NATIVE_MINT_PUBKEY))
            {
                SyncIfNative(account, whirlpool.TokenMintB, quote.TokenMaxB, tb);
            }

            tb.AddInstruction(
                await OrcaInstruction.GenerateIncreaseLiquidityInstruction(
                    Context,
                    ownerAccount: account,
                    whirlpoolAddress: whirlpool.Address,
                    positionAddress: positionAddress,
                    positionMintAddress: positionMintAddress,
                    positionAuthority: positionAuthority,
                    tickLowerIndex: tickLowerIndex,
                    tickUpperIndex: tickUpperIndex,
                    tokenAmountA: quote.TokenEstA,
                    tokenAmountB: quote.TokenEstB,
                    tokenMaxA: quote.TokenMaxA,
                    tokenMaxB: quote.TokenMaxB,
                    commitment: commitment
                )
            );
        }

        #endregion
    }
}