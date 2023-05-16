using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System.Collections.Generic;

namespace Solana.Unity.Programs;

/// <summary>
/// Implements the ComputeBudget Program methods.
/// <remarks>
/// For more information see: https://spl.solana.com/memo
/// </remarks>
/// </summary>
 
public class ComputeBudgetProgram
{
    
        /// <summary>
        /// The public key of the ComputeBudget Program.
        /// </summary>
        public static readonly PublicKey ProgramIdKey = new("ComputeBudget111111111111111111111111111111");


        /// <summary>
        /// The program's name.
        /// </summary>
        private const string ProgramName = "Compute Budget Program";



    /// <summary>
    /// Request HeapFrame Instruction related to Priority Fees
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static TransactionInstruction RequestHeapFrame(uint bytes)
    {
        List<AccountMeta> keys = new();

        byte[] instructionBytes = new byte[5];
        instructionBytes.WriteU8(1, 0);
        instructionBytes.WriteU32(bytes, 1);

        return new TransactionInstruction
        {
            ProgramId = ProgramIdKey.KeyBytes,
            Keys = keys,
            Data = instructionBytes
        };
    }

    /// <summary>
    /// Set Compute Unit Limit Instruction for Priority Fees
    /// </summary>
    /// <param name="units"></param>
    /// <param name="additionalFee"></param>
    /// <returns></returns>
    public static TransactionInstruction SetComputeUnitLimit(uint units)
    {
        List<AccountMeta> keys = new();

        byte[] instructionBytes = new byte[5];
        instructionBytes.WriteU8(2, 0);
        instructionBytes.WriteU32(units, 1);

        return new TransactionInstruction
        {
            ProgramId = ProgramIdKey.KeyBytes,
            Keys = keys,
            Data = instructionBytes
        };
    }
    /// <summary>
    /// Set Compute Unit Price Instruction for Priority Fees
    /// </summary>
    /// <param name="priority_rate"></param>
    /// <returns></returns>
    public static TransactionInstruction SetComputeUnitPrice(ulong priority_rate)
    {
        List<AccountMeta> keys = new();
        
        byte[] instructionBytes = new byte[9];
        instructionBytes.WriteU8(3, 0);
        instructionBytes.WriteU64(priority_rate, 1);

        return new TransactionInstruction
        {
            ProgramId = ProgramIdKey.KeyBytes,
            Keys = keys,
            Data = instructionBytes
        };
    }

}