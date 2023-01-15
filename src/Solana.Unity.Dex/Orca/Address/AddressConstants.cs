using System;

using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Address
{
    /// <summary>
    /// Commonly used known addresses and ids. 
    /// </summary>
    public static class AddressConstants
    {
        /// <summary>
        /// Solana system program. 
        /// </summary>
        public static readonly string SYSTEM_PROGRAM_ID = Solana.Unity.Programs.SystemProgram.ProgramIdKey;
        
        /// <summary>
        /// Solana token program. 
        /// </summary>
        public static readonly string TOKEN_PROGRAM_ID = Solana.Unity.Programs.TokenProgram.ProgramIdKey;

        /// <summary>
        /// Associated Token Program public key. 
        /// </summary>
        public static readonly string ASSOCIATED_TOKEN_PROGRAM_ID = Solana.Unity.Programs.AssociatedTokenAccountProgram.ProgramIdKey;

        /// <summary>
        /// Orca's Whirlpools program public key.
        /// </summary>
        public static readonly string WHIRLPOOLS_PROGRAM_ID =  "whirLbMiicVdio4qvUfM5KAg6Ct8VwpYzGff3uctyCc";

        /// <summary>
        /// Program ID hosting Orca's Whirlpool program.
        /// </summary>
        public static readonly string WHIRLPOOLS_CONFIG_PROGRAM_ID = "2LecshUwdy9xi7meFgHtFJQNSKk4KdTrcpvaB56dP2NQ";

        /// <summary>
        /// Address of the metadata program in the current environment (except for local).
        /// </summary>
        public static readonly string METADATA_PROGRAM_ID = "metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s";
        
        public static readonly string METADATA_UPDATE_AUTH_ID = "3axbTs2z5GBy6usVbNVoqEgZMng3vZvMnAoX29BFfwhr"; 

        /// <summary>
        /// The native Solana mint address. 
        /// </summary>
        public static readonly string NATIVE_MINT = "So11111111111111111111111111111111111111112";

        public static readonly string RENT = Solana.Unity.Programs.SysVars.RentKey;


        /// <summary>
        /// Solana system program. 
        /// </summary>
        public static readonly PublicKey SYSTEM_PROGRAM_PUBKEY = new PublicKey(
            SYSTEM_PROGRAM_ID
        );
        
        /// <summary>
        /// Solana token program. 
        /// </summary>
        public static readonly PublicKey TOKEN_PROGRAM_PUBKEY = new PublicKey(
            TOKEN_PROGRAM_ID
        );

        /// <summary>
        /// Associated Token Program public key. 
        /// </summary>
        public static readonly PublicKey ASSOCIATED_TOKEN_PROGRAM_PUBKEY = new PublicKey(
            ASSOCIATED_TOKEN_PROGRAM_ID
        );

        /// <summary>
        /// Orca's Whirlpools program public key.
        /// </summary>
        public static readonly PublicKey WHIRLPOOLS_PUBKEY = new PublicKey(
            WHIRLPOOLS_PROGRAM_ID
        );

        /// <summary>
        /// Program ID hosting Orca's Whirlpool program.
        /// </summary>
        public static readonly PublicKey WHIRLPOOLS_CONFIG_PUBKEY = new PublicKey(
            WHIRLPOOLS_CONFIG_PROGRAM_ID
        );

        /// <summary>
        /// @category Constants
        /// </summary>
        public static readonly PublicKey METADATA_PROGRAM_PUBKEY = new PublicKey(
            METADATA_PROGRAM_ID
        );

        public static readonly PublicKey METADATA_UPDATE_AUTH_PUBKEY = new PublicKey(
            METADATA_UPDATE_AUTH_ID
        );

        /// <summary>
        /// From solana/web3, PublicKey.default
        /// </summary>
        public static readonly PublicKey DEFAULT_PUBLIC_KEY = new PublicKey(
            "11111111111111111111111111111111"
        );
        
        public static readonly PublicKey NATIVE_MINT_PUBKEY = new PublicKey(
            NATIVE_MINT
        );

        public static readonly PublicKey RENT_PUBKEY = new PublicKey(
            RENT
        );
    }
}