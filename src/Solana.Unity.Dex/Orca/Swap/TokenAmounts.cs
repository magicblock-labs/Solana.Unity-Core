using System;
using System.Numerics;
using System.Collections.Generic;

namespace Solana.Unity.Dex.Orca.Swap
{
    /// <summary>
    /// Stores the numeric quantities of Token A and Token B.
    /// </summary>
    public struct TokenAmounts
    {
        /// <summary>
        /// Gets the quantity of token A. 
        /// </summary>
        public BigInteger TokenA { get; set; }

        /// <summary>
        /// Gets the quantity of token B. 
        /// </summary>
        public BigInteger TokenB { get; set; }

        public static TokenAmounts FromValue(BigInteger value)
        {
            return new TokenAmounts
            {
                TokenA = value,
                TokenB = value
            };
        }

        public static TokenAmounts FromValues(BigInteger a, BigInteger b)
        {
            return new TokenAmounts
            {
                TokenA = a,
                TokenB = b
            };
        }
    }
}
