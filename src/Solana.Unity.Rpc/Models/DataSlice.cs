// unset

namespace Solana.Unity.Rpc.Models
{
    /// <summary>
    /// Represents the <c>data slice</c> for the <see cref="IRpcClient.GetProgramAccounts"/> method.
    /// </summary>
    public class DataSlice
    {
        /// <summary>
        /// The offset into program account data will be taken.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// The amount of bytes to take from the account, starting from the offset..
        /// </summary>
        public int Length { get; set; }
    }
}