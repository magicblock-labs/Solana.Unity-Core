using System;

namespace Solana.Unity.Dex.Orca.Exceptions
{
    /// <summary>
    /// Application-specific base exception class. 
    /// </summary>
    public class WhirlpoolsException : System.Exception
    {
        /// <summary>
        /// Gets the error code unique to a specific type of error (see WhirlpoolsErrorCodes). 
        /// </summary>
        public WhirlpoolsErrorCode ErrorCode { get; private set; }

        /// <summary>
        /// Public constructor. 
        /// </summary>
        /// <param name="message">Standard string error message. </param>
        /// <param name="errorCode">Whirlpools-specific error code instance.</param>
        public WhirlpoolsException(string message, WhirlpoolsErrorCode errorCode) : this(message)
        {
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Public constructor. 
        /// </summary>
        /// <param name="message">Standard string error message. </param>
        public WhirlpoolsException(string message) : base(message)
        {
            this.ErrorCode = WhirlpoolsErrorCode.Default;
        }

        /// <summary>
        /// Determines whether or not the given Exception instance is of base type WhirlpoolsException.
        /// </summary>
        /// <param name="e">Any System.Exception instance or null.</param>
        /// <returns>True if the exception is non-null and base type is WhirlpoolsException</returns>
        public static bool IsWhirlpoolsError(System.Exception e)
        {
            return (e != null && e is WhirlpoolsException);
        }

        /// <summary>
        /// Determines whether or not the given exception instance is a WhirlpoolsException with a
        /// specific type of error code. 
        /// </summary>
        /// <param name="e">Any System.Exception instance or null.</param>
        /// <typeparam name="T">Type identifier for a WhirlpoolsErrorCode subtype.</typeparam>
        /// <returns>True if the given exception is non-null and has an error code of the given type.</returns>
        public static bool IsWhirlpoolsError<T>(System.Exception e) where T : WhirlpoolsErrorCode
        {
            WhirlpoolsException wex = e as WhirlpoolsException;
            return (wex != null && wex.ErrorCode.GetType() == typeof(T));
        }

        /// <summary>
        /// Determines whether or not the given exception instance is a WhirlpoolsException with a
        /// specific type and value of error code. 
        /// </summary>
        /// <param name="e">Any System.Exception instance or null.</param>
        /// <typeparam name="T">Type identifier for a WhirlpoolsErrorCode subtype.</typeparam>
        /// <param name="errorCode">Specific error code value to query.</param>
        /// <returns>True if the given exception is non-null, its base is WhirlpoolsError, and its
        /// error code is of the given type and value.</returns>
        public static bool IsWhirlpoolsErrorCode<T>(System.Exception e, WhirlpoolsErrorCode errorCode) where T : WhirlpoolsErrorCode
        {
            WhirlpoolsException wex = e as WhirlpoolsException;
            return (wex != null) ? (wex.ErrorCode.GetType() == typeof(T) && wex.ErrorCode == errorCode) : false;
        }
    }
}
