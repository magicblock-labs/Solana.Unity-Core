namespace Solana.Unity.Rpc.Models
{
    /// <summary>
    /// Represents a versioned transaction instruction before being compiled into the transaction's message.
    /// </summary>
    public class VersionedTransactionInstruction : TransactionInstruction
    {
        /// <summary>
        /// The keys associated with the instruction.
        /// </summary>
        public byte[] KeyIndices { get; init; }
    }
}