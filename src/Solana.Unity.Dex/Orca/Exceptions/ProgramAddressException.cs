using System;

namespace Solana.Unity.Dex.Orca.Exceptions
{
    /// <summary>
    /// Error finding or creating a program address.
    /// </summary>
    public class ProgramAddressException : System.Exception
    {
        public ProgramAddressException(string message) : base(message) { }
    }
}
