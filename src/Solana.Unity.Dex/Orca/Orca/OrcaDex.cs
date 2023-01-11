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
using Solana.Unity.Dex.Orca.Orca;
using Solana.Unity.Dex.Quotes;
using Solana.Unity.Dex.Swap;
using Solana.Unity.Dex.Ticks;

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

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<Transaction> Swap(
            PublicKey whirlpoolAddress,
            ulong amount,
            bool aToB = true,
            PublicKey tokenAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey; 
            
            //get the specified whirlpool
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(
                _context, whirlpoolAddress, commitment
            );

            //get associated token account addresses 
            var tokenOwnerAcctA =
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(account, whirlpool.TokenMintA);

            var tokenOwnerAcctB =
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(account, whirlpool.TokenMintB);

            var tb = new TransactionBuilder();

            //instruction to create token account A 
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintA, tb, commitment
            );

            //instruction to create token account B
            await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, whirlpool.TokenMintB, tb, commitment
            );
            
            //generate the transaction 
            tb.AddInstruction(
                await OrcaInstruction.GenerateSwapInstruction(
                    _context,
                    whirlpool,
                    whirlpoolAddress,
                    tokenAuthority: tokenAuthority,
                    amount,
                    aToB: aToB,
                    commitment
                )
            );

            //set fee payer and recent blockhash 
            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
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
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;
            
            //throw if whirlpool doesn't exist
            await InstructionUtil.TryGetWhirlpoolAsync(_context, whirlpoolAddress, commitment);
            
            //throw if position already exists 
            if (await InstructionUtil.PositionExists(_context, positionMintAccount, commitment))
                throw new Exception($"A position for mint {positionMintAccount} on whirlpool {whirlpoolAddress} already exists");

            //funder defaults to account 
            if (funderAccount == null)
                funderAccount = account;             
                
            //generate the transaction 
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateOpenPositionInstruction(
                    _context,
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
            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
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
            Commitment commitment = Commitment.Finalized
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
            BigInteger tokenMaxA,
            BigInteger tokenMaxB,
            bool withMetadata = false,
            PublicKey funderAccount = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;
            
            //throw if position already exists 
            if (await InstructionUtil.PositionExists(_context, positionMintAddress, commitment))
                throw new Exception($"A position for mint {positionMintAddress} on whirlpool {whirlpoolAddress} already exists");

            //throw if whirlpool doesn't exist 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(_context, whirlpoolAddress, commitment); 
            
            TransactionBuilder tb = new TransactionBuilder();

            //need to initialize tick arrays? then do so
            int lowerStartTick = await CreateInitializeTickArrayInstructionIfNotExtant(
                whirlpoolAddress, funderAccount, tickLowerIndex, whirlpool.TickSpacing, tb, commitment
            );
            
            //only initialize the second one if different from the first 
            int upperStartTick = TickUtils.GetStartTickIndex(tickUpperIndex, whirlpool.TickSpacing);
            if (upperStartTick != lowerStartTick) 
            {
                await CreateInitializeTickArrayInstructionIfNotExtant(
                    whirlpoolAddress, funderAccount, tickUpperIndex, whirlpool.TickSpacing, tb, commitment
                );
            }

            //instruction to open position 
            tb.AddInstruction(
                OrcaInstruction.GenerateOpenPositionInstruction(
                    _context,
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
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAddress);

            //instruction to add liquidity 
            tb.AddInstruction(
                await OrcaInstruction.GenerateIncreaseLiquidityInstruction(
                    _context,
                    ownerAccount: account,
                    whirlpoolAddress: whirlpoolAddress,
                    positionAddress: positionPda,
                    positionMintAddress: positionMintAddress,
                    positionAuthority: account,
                    tickLowerIndex: tickLowerIndex,
                    tickUpperIndex: tickUpperIndex,
                    tokenMaxA: tokenMaxA,
                    tokenMaxB: tokenMaxB,
                    commitment: commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> ClosePosition(
            PublicKey positionAddress,
            PublicKey receiverAddress = null,
            PublicKey positionAuthority = null, 
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;
            
            //receiver is the closer/owner if not otherwise specified 
            if (receiverAddress == null)
                receiverAddress = account;
                
            //retrieve the position 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);
            
            //generate the transaction 
            TransactionBuilder tb = new TransactionBuilder();
            
            //add instruction to remove liquidity, if position has liquidity 
            if (position.Liquidity > 0) 
            {
                tb.AddInstruction(
                    await OrcaInstruction.GenerateDecreaseLiquidityInstruction(
                        _context, 
                        account,
                        position,
                        positionAddress,
                        positionAuthority,
                        position.Liquidity,
                        0, 0, 
                        commitment
                    )
                );
            }

            //close the position 
            tb.AddInstruction(
                await OrcaInstruction.GenerateClosePositionInstruction(
                    _context,
                    account,
                    receiverAddress,
                    positionAddress,
                    positionAuthority,
                    commitment
                )
            );

            //set fee payer and recent blockhash 
            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> IncreaseLiquidity(
            PublicKey positionAddress,
            BigInteger tokenMaxA, 
            BigInteger tokenMaxB,
            PublicKey positionAuthority = null, 
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;
            
            //throw if position doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);

            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                await OrcaInstruction.GenerateIncreaseLiquidityInstruction(
                    _context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    tokenMaxA,
                    tokenMaxB,
                    commitment
                )
            );
            
            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> DecreaseLiquidity(
            PublicKey positionAddress,
            BigInteger liquidityAmount,
            BigInteger tokenMinA, 
            BigInteger tokenMinB,
            PublicKey positionAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;
            
            //throws if position doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);
            
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                await OrcaInstruction.GenerateDecreaseLiquidityInstruction(
                    _context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    liquidityAmount,
                    tokenMinA, 
                    tokenMinB,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateFeesAndRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper, 
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey; 
            
            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);
            
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    _context,
                    position,
                    positionAddress,
                    tickArrayLower,
                    tickArrayUpper,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> CollectFees(
            PublicKey positionAddress,
            PublicKey positionAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);
            
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                await OrcaInstruction.GenerateCollectFeesInstruction(
                    _context,
                    account,
                    position,
                    positionAddress,
                    positionAuthority,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> CollectRewards(
            PublicKey positionAddress,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex = 0,
            PublicKey positionAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);

            TransactionBuilder tb = new TransactionBuilder();
            
            //optional instruction to create reward token account (if it doesn't exist) 
            PublicKey rewardTokenOwnerAddress = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, rewardMintAddress, tb, commitment
            ); 
            
            //instruction to collect rewards 
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectRewardsInstruction(
                    _context,
                    account,
                    position,
                    positionAddress,
                    rewardMintAddress, 
                    rewardVaultAddress,
                    rewardTokenOwnerAddress,
                    positionAuthority,
                    rewardIndex,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateAndCollectFees(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey positionAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);

            //instruction to udpate fees/rewards 
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    _context,
                    position,
                    positionAddress,
                    tickArrayLower,
                    tickArrayUpper,
                    commitment
                )
                
            //instruction to collect fees 
            ).AddInstruction(
                await OrcaInstruction.GenerateCollectFeesInstruction(
                    _context,
                    account,
                    position, 
                    positionAddress,
                    positionAuthority,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified position doesn't exist.</exception>
        public override async Task<Transaction> UpdateAndCollectRewards(
            PublicKey positionAddress,
            PublicKey tickArrayLower,
            PublicKey tickArrayUpper,
            PublicKey rewardMintAddress,
            PublicKey rewardVaultAddress,
            byte rewardIndex = 0,
            PublicKey positionAuthority = null,
            Commitment commitment = Commitment.Finalized
        )
        {
            PublicKey account = _context.WalletPubKey;

            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);

            //instruction to update fees/rewards
            TransactionBuilder tb = new TransactionBuilder().AddInstruction(
                OrcaInstruction.GenerateUpdateFeesAndRewardsInstruction(
                    _context,
                    position,
                    positionAddress,
                    tickArrayLower,
                    tickArrayUpper,
                    commitment
                )
            );

            //optional instruction to create reward token account (if it doesn't exist) 
            PublicKey rewardTokenOwnerAddress = await CreateAssociatedTokenAccountInstructionIfNotExtant(
                account, rewardMintAddress, tb, commitment
            );

            //instruction to collect rewards
            tb.AddInstruction(
                await OrcaInstruction.GenerateCollectRewardsInstruction(
                    _context,
                    account,
                    position,
                    positionAddress,
                    rewardMintAddress, 
                    rewardVaultAddress,
                    rewardTokenOwnerAddress,
                    positionAuthority,
                    rewardIndex,
                    commitment
                )
            );

            return await PrepareTransaction(tb, account, this.RpcClient, commitment);
        }

        /// <inheritdoc />
        public override async Task<bool> WhirlpoolExists(
            PublicKey tokenMintA, 
            PublicKey tokenMintB,
            PublicKey configAccountAddress = null,
            ushort tickSpacing = TickSpacing.Standard,
            Commitment commitment = Commitment.Finalized
        )
        {
            if (configAccountAddress == null) 
                configAccountAddress = AddressConstants.WHIRLPOOLS_CONFIG_PUBKEY;
                
            Pda whirlpoolPda = PdaUtils.GetWhirlpool(
                _context.ProgramId,
                configAccountAddress,
                tokenMintA, tokenMintB, tickSpacing
            );
            
            //attempt to get whirlpool 
            var whirlpoolResult = await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey, commitment); 
            
            //return true if whirlpool retrieved successfully
            return whirlpoolResult.WasSuccessful && whirlpoolResult.ParsedResult != null; 
        }

        /// <inheritdoc />
        public override async Task<Tuple<PublicKey, Whirlpool>> FindWhirlpool(
            PublicKey tokenMintA,
            PublicKey tokenMintB, 
            ushort tickSpacing = 128,
            PublicKey configAccountAddress = null, 
            Commitment commitment = Commitment.Finalized
        )
        {
            var result = await TryFindWhirlpool(tokenMintA, tokenMintB, tickSpacing, configAccountAddress, commitment); 
            
            if (result.Item2 == null) 
            {
                var promises = new List<Task<Tuple<PublicKey, Whirlpool>>>(); 
                
                for (ushort ts = 8; ts <= 256; ts*=2) 
                {
                    promises.Add(TryFindWhirlpool(tokenMintA, tokenMintB, ts, configAccountAddress, commitment));
                }
                
                await Task.WhenAll(promises);
                foreach(var p in promises) 
                {
                    if (p.Result != null && p.Result.Item2 != null) 
                    {
                        result = p.Result;
                        break;
                    }
                }
            }
            
            return result;
        }
        
        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public override async Task<SwapQuote> GetSwapQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            BigInteger tokenAmount,
            Percentage slippageTolerance,
            TokenType amountSpecifiedTokenType = TokenType.TokenA,
            bool amountSpecifiedIsInput = true,
            Commitment commitment = Commitment.Finalized
        )
        {
            //throws if whirlpool doesn't exist
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(_context, whirlpoolAddress, commitment);

            return SwapQuoteUtils.SwapQuoteWithParams(
                await SwapQuoteUtils.SwapQuoteByToken(
                    _context,
                    whirlpool,
                    whirlpoolAddress,
                    inputTokenMintAddress,
                    tokenAmount,
                    amountSpecifiedTokenType,
                    amountSpecifiedIsInput,
                    _context.ProgramId
                ),
                slippageTolerance
            );
        }

        /// <inheritdoc />
        /// <exception cref="System.Exception">Thrown if the specified whirlpool doesn't exist.</exception>
        public async override Task<IncreaseLiquidityQuote> GetIncreaseLiquidityQuote(
            PublicKey whirlpoolAddress,
            PublicKey inputTokenMintAddress,
            double inputTokenAmount,
            double slippageTolerance,
            int tickLowerIndex,
            int tickUpperIndex,
            Commitment commitment = Commitment.Finalized
        )
        {
            //throws if whirlpool doesn't exist 
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(_context, whirlpoolAddress, commitment);

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
            ulong liquidityAmount,
            double slippageTolerance,
            Commitment commitment = Commitment.Finalized
        )
        {
            //throws if whirlpool doesn't exist 
            Position position = await InstructionUtil.TryGetPositionAsync(_context, positionAddress, commitment);
            Whirlpool whirlpool = await InstructionUtil.TryGetWhirlpoolAsync(_context, position.Whirlpool, commitment);

            return DecreaseLiquidityQuoteUtils.GenerateDecreaseQuote(
                liquidityAmount,
                Percentage.FromDouble(slippageTolerance),
                position,
                whirlpool
            );
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
            tb.SetRecentBlockHash((await rpcClient.GetRecentBlockHashAsync(commitment)).Result.Value.Blockhash);

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
            bool exists = await InstructionUtil.AssociatedTokenAccountExists(
                _context, ownerAddress, mintAddress, commitment
            );
            if (!exists)
            {
                builder.AddInstruction(
                    AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        ownerAddress, ownerAddress, mintAddress
                    )
                );
            }

            return AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAddress, mintAddress);
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
                _context, whirlpoolAddress, startTickIndex, commitment)
            )
            {
                builder.AddInstruction(
                    OrcaInstruction.GenerateInitializeTickArrayInstruction(
                        _context,
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
            Commitment commitment = Commitment.Finalized
        )
        {
            if (configAccountAddress == null)
                configAccountAddress = AddressConstants.WHIRLPOOLS_CONFIG_PUBKEY;

            //get whirlpool address
            Pda whirlpoolPda = PdaUtils.GetWhirlpool(
                _context.ProgramId,
                configAccountAddress,
                tokenMintA,
                tokenMintB,
                tickSpacing
            );

            //try to retrieve the whirlpool
            Whirlpool whirlpool = (
                await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPda.PublicKey.ToString(), commitment)
            ).ParsedResult;

            //if that didn't work, try to reverse the token mints
            if (whirlpool == null)
            {
                Pda whirlpoolPdaAlt = PdaUtils.GetWhirlpool(
                    _context.ProgramId,
                    configAccountAddress,
                    tokenMintB,
                    tokenMintA,
                    tickSpacing
                );

                //try to retrieve the whirlpool
                whirlpool = (
                    await _context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolPdaAlt.PublicKey.ToString(), commitment)
                ).ParsedResult;

                //if that worked, return the alt address 
                if (whirlpool != null)
                    whirlpoolPda = whirlpoolPdaAlt;
            }

            return Tuple.Create<PublicKey, Whirlpool>(whirlpoolPda, whirlpool);
        }

        #endregion
    }
}