using System;

namespace Solana.Unity.Dex.Orca.Exceptions
{
    /// <summary>
    /// Abstract base class for all classes that represent a specific type of
    /// Whirlpools error code. 
    /// </summary>
    public abstract class WhirlpoolsErrorCode
    {
        /// <summary>
        /// Gets a name that's unique to the error code. 
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the instance's name. 
        /// </summary>
        public override string ToString() => this.Name;

        /// <summary>
        /// Returns true if this is the default error code type (none, unknown). 
        /// </summary>
        public bool IsDefault => String.IsNullOrEmpty(this.Name);

        /// <summary>
        /// Gets an instance of the default error code (none, unknown). 
        /// </summary>
        public static WhirlpoolsErrorCode Default => new DefaultErrorCode();
        public static WhirlpoolsErrorCode None => WhirlpoolsErrorCode.Default;
        public static WhirlpoolsErrorCode Unknown => WhirlpoolsErrorCode.Default;

        /// <summary>
        /// Public constructor. 
        /// </summary>
        /// <param name="name">A string name that uniquely identifies the instance within its subtype.</param>
        protected WhirlpoolsErrorCode(string name)
        {
            this.Name = name;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (
                !Object.ReferenceEquals(obj, null) && 
                (obj is WhirlpoolsErrorCode) && 
                (this.Name == ((WhirlpoolsErrorCode)obj).Name)
            );
        }

        public static bool operator == (WhirlpoolsErrorCode a, WhirlpoolsErrorCode b)
        {
            return (!Object.ReferenceEquals(a, null) && !Object.ReferenceEquals(b, null)) && (a.Name == b.Name);
        }

        public static bool operator !=(WhirlpoolsErrorCode a, WhirlpoolsErrorCode b)
        {
            return !(a == b);
        }
    }
    
    
    public class DefaultErrorCode : WhirlpoolsErrorCode
    {
        public DefaultErrorCode(): base(String.Empty) {}
    }

    public class MathErrorCode : WhirlpoolsErrorCode
    {
        public static readonly MathErrorCode MultiplicationOverflow = new MathErrorCode("MultiplicationOverflow");
        public static readonly MathErrorCode MulDivOverflow = new MathErrorCode("MulDivOverflow");
        public static readonly MathErrorCode MultiplicationShiftRightOverflow = new MathErrorCode("MultiplicationShiftRightOverflow");
        public static readonly MathErrorCode DivideByZero = new MathErrorCode("DivideByZero");

        private MathErrorCode(string name) : base(name)
        { }
    }

    public class TokenErrorCode : WhirlpoolsErrorCode
    {
        public static readonly TokenErrorCode TokenMaxExceeded = new TokenErrorCode("TokenMaxExceeded");
        public static readonly TokenErrorCode TokenMinSubceeded = new TokenErrorCode("TokenMinSubceeded");

        private TokenErrorCode(string name) : base(name)
        { }
    }

    public class SwapErrorCode : WhirlpoolsErrorCode
    {
        public static readonly SwapErrorCode InvalidDevFeePercentage = new SwapErrorCode("InvalidDevFeePercentage");
        public static readonly SwapErrorCode InvalidSqrtPriceLimitDirection = new SwapErrorCode("InvalidSqrtPriceLimitDirection");
        public static readonly SwapErrorCode SqrtPriceOutOfBounds = new SwapErrorCode("SqrtPriceOutOfBounds");
        public static readonly SwapErrorCode ZeroTradableAmount = new SwapErrorCode("ZeroTradableAmount");
        public static readonly SwapErrorCode AmountOutBelowMinimum = new SwapErrorCode("AmountOutBelowMinimum");
        public static readonly SwapErrorCode AmountInAboveMaximum = new SwapErrorCode("AmountInAboveMaximum");
        public static readonly SwapErrorCode TickArrayCrossingAboveMax = new SwapErrorCode("TickArrayCrossingAboveMax");
        public static readonly SwapErrorCode TickArrayIndexNotInitialized = new SwapErrorCode("TickArrayIndexNotInitialized");
        public static readonly SwapErrorCode TickArraySequenceInvalid = new SwapErrorCode("TickArraySequenceInvalid");

        private SwapErrorCode(string name) : base(name)
        { }
    }
}
