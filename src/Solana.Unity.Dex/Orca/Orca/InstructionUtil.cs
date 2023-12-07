using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Programs;

namespace Orca
{
    /// <summary> 
    /// Utilities used in creating transaction instructions for the Tx Api. 
    /// </summary> 
    internal static class InstructionUtil 
    {
        /// <summary>
        /// Attempts to get a Whirlpool account at the given address; throws if not found. 
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="whirlpoolAddress">Address of the desired whirlpool.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <exception cref="System.Exception">Thrown if whirlpool not found.</exception>
        /// <returns>A Whirlpool object if found.</returns>
        public static async Task<Whirlpool> TryGetWhirlpoolAsync(
            IWhirlpoolContext context,
            PublicKey whirlpoolAddress,
            Commitment commitment
        )
        {
            var whirlpoolResult = await context.WhirlpoolClient.GetWhirlpoolAsync(whirlpoolAddress.ToString(), commitment);
            if (whirlpoolResult == null || !whirlpoolResult.WasSuccessful)
                throw new Exception($"Failed to retrieve whirlpool at {whirlpoolAddress}");
            return whirlpoolResult.ParsedResult;
        }
        
        /// <summary>
        /// Determines whether a position with the given characteristics exists.
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="positionMintAddress">The desired position's token mint address.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <returns>True if position is found to exist.</returns>
        public static async Task<bool> PositionExists(
            IWhirlpoolContext context,
            PublicKey positionMintAddress,
            Commitment commitment
        )
        {
            //derive position address
            PublicKey positionAddress = PdaUtils.GetPosition(context.ProgramId, positionMintAddress);

            //attempt to get position 
            var positionResult =
                await context.WhirlpoolClient.GetPositionAsync(positionAddress, commitment);
                
            return (positionResult.WasSuccessful && 
                positionResult.ParsedResult != null && 
                positionResult.ParsedResult.PositionMint == positionMintAddress
            );
        }

        /// <summary>
        /// Attempts to get a Position account at the given address; throws if not found. 
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="positionAddress">The address of the desired position.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <exception cref="System.Exception">Thrown if position not found.</exception>
        /// <returns>A Position object if found.</returns>
        public static async Task<Position> TryGetPositionAsync(
            IWhirlpoolContext context,
            PublicKey positionAddress,
            Commitment commitment
        )
        {
            var positionResult =
                await context.WhirlpoolClient.GetPositionAsync(positionAddress, commitment);
            if (!positionResult.WasSuccessful)
                throw new Exception($"Unable to retrieve position {positionAddress}");

            return positionResult.ParsedResult;
        }

        /// <summary>
        /// Determines whether an associated token account with the given characteristics exists.
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="ownerAddress">The address of the token owner.</param>
        /// <param name="mintAddress">The address of the token mint.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <returns>True if account is found to exist.</returns>
        public static async Task<bool> AssociatedTokenAccountExists(
            IWhirlpoolContext context,
            PublicKey ownerAddress,
            PublicKey mintAddress,
            Commitment commitment
        )
        {
            return await TokenAccountExists(
                context,
                AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(ownerAddress, mintAddress),
                commitment
            );
        }

        /// <summary>
        /// Determines whether a token account with the given address exists.
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="accountKey">Public key of token account.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <returns>True if account is found to exist.</returns>
        public static async Task<bool> TokenAccountExists(
            IWhirlpoolContext context,
            PublicKey accountKey,
            Commitment commitment
        )
        {
            var accountInfoResult = await context.RpcClient.GetAccountInfoAsync(accountKey.ToString(), commitment); 
            AccountInfo accountInfo = null; 
            if (accountInfoResult.WasSuccessful) 
            {
                accountInfo = accountInfoResult.Result.Value;
            }
            
            return (accountInfo != null);
        }
        
        /// <summary>
        /// Determines whether a specified tick array is initialized on the whirlpool. 
        /// </summary>
        /// <param name="context">Whirlpool context.</param>
        /// <param name="whirlpoolAddress">Address of the hypothetical tick array's whirlpool.</param>
        /// <param name="startTickIndex">Adjusted start tick of the tick array.</param>
        /// <param name="commitment">Commitment level to consider.</param>
        /// <returns>True if tick array found to be extant/initialized.</returns>
        public static async Task<bool> TickArrayIsInitialized(
            IWhirlpoolContext context,
            PublicKey whirlpoolAddress, 
            int startTickIndex,
            Commitment commitment
        )
        {
            //get tick array address
            Pda tickArrayPda = PdaUtils.GetTickArray(context.ProgramId, whirlpoolAddress, startTickIndex);
            
            //try to retrieve the tick array
            var result = await context.RpcClient.GetAccountInfoAsync(tickArrayPda.PublicKey, commitment);
            
            //determine if initialized 
            return result.WasSuccessful && result.Result.Value != null;
        }
    }
}