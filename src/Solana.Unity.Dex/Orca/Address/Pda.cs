using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Address
{
    /// <summary>
    /// Data holder type for a program-derived address with bump. 
    /// </summary>
    public class Pda
    {
        /// <summary>
        /// Gets the public key. 
        /// </summary>
        public PublicKey PublicKey { get; private set; }

        /// <summary>
        /// Gets the byte bump offset.
        /// </summary>
        public byte Bump { get; private set; }
        
        public Pda(PublicKey publicKey, byte bump = 255)
        {
            this.PublicKey = publicKey;
            this.Bump = bump;
        }
        
        public static implicit operator PublicKey(Pda pda) 
        {
            return (pda != null) ? pda.PublicKey : null;
        }
    }
}
