using Solana.Unity.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solana.Unity.Programs
{
    /// <summary>
    /// Represents the System Variables
    /// </summary>
    public static class SysVars
    {
        /// <summary>
        /// The public key of the Recent Block Hashes System Variable. 
        /// </summary>
        public static readonly PublicKey RecentBlockHashesKey = new("SysvarRecentB1ockHashes11111111111111111111");

        /// <summary>
        /// The public key of the Recent Slot Hashes System Variable. 
        /// </summary>
        public static readonly PublicKet RecentSlotHashesKey = new("SysvarS1otHashes111111111111111111111111111");

        /// <summary>
        /// The public key of the Rent System Variable.
        /// </summary>
        public static readonly PublicKey RentKey = new("SysvarRent111111111111111111111111111111111");
        /// <summary>
        /// The public key of the Clock System Variable.
        /// </summary>
        public static readonly PublicKey ClockKey = new("SysvarC1ock11111111111111111111111111111111");
        /// <summary>
        /// The public key of the Stake History System Variable.
        /// </summary>
        public static readonly PublicKey StakeHistoryKey = new("SysvarStakeHistory1111111111111111111111111");
        /// <summary>
        /// The public key of the Instruction Account Variable.
        /// </summary>
        public static readonly PublicKey InstructionAccount = new("Sysvar1nstructions1111111111111111111111111");
    }
}
