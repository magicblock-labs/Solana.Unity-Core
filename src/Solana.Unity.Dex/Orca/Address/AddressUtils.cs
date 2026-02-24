using System;
using System.Collections.Generic;

using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Orca.Address
{
    /// <summary>
    /// Utility methods for handling addresses, accounts, and public keys. 
    /// </summary>
    public static class AddressUtils
    {

        /// <summary>
        /// Valid program addresses must fall off the ed25519 curve. Iterates a nonce until it finds one
        /// that when combined with the seeds results in a valid program address.
        /// </summary>
        /// <param name="seeds">Array of byte arrays to use to generate addresses.</param>
        /// <param name="programId">Id of the program for which to find an address.</param>
        /// <returns>A Pda (program-derived address) off the curve, or null.</returns>
        public static Pda FindProgramAddress(IEnumerable<byte[]> seeds, PublicKey programId)
        {
            return PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey pubkey, out byte bump) 
                ? new Pda(pubkey, bump) : null;
        }

        /// <summary> 
        /// Compares two public keys using a specialized IComparer instance, and returns a 
        /// value indicating their relationship. 
        /// </summary> 
        /// <param name="a">PublicKey instance to compare</param>
        /// <param name="b">PublicKey instance to compare</param>
        /// <returns>The result of comparison (negative, zero, or positive)</returns>
        public static int ComparePublicKeys(PublicKey a, PublicKey b) 
        {
            return new PublicKeyComparer().Compare(a, b); 
        }

        
        /// <summary>
        /// IComparer for Public keys. This is used when creating mints for a pool, in a specific order (the 
        /// comparer is used to put the mint addreses in expected order)
        /// </summary>
        private class PublicKeyComparer : IComparer<PublicKey>
        {
            public int Compare(PublicKey a, PublicKey b) 
            {
                //they might just be equal 
                if (Object.ReferenceEquals(a, b) || a.Equals(b))
                    return 0; 
                
                //compare each byte
                int minLen = System.Math.Min(a.KeyBytes.Length, b.KeyBytes.Length); 
                for (int n=0; n<minLen; n++) 
                {
                    int cmp = a.KeyBytes[n].CompareTo(b.KeyBytes[n]);
                    if (cmp != 0)
                        return cmp;
                }
                
                //if all bytes so far are equal, return the longer one 
                return (a.KeyBytes.Length.CompareTo(b.KeyBytes.Length)); 
            }
        }
    }
}
