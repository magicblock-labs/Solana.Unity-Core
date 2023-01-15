using System;
using System.Numerics;

using InnerDecimal = Solana.Unity.Dex.Orca.Math.SpecialTypes.BigDecimal;

namespace Solana.Unity.Dex.Orca.Math
{
    /// <summary>
    /// Necessary to represent floating point numbers that are bigger than the normal limited-size floating
    /// and fixed-point types. Often in calculations this is used in converting to and from BigInteger, so
    /// this type is necessary in order to prevent overflow/underflow.
    ///
    /// Like any primitive type, this type is immutable and a value type.  
    ///
    /// This implementation wraps SpecialTypes.BigDecimal (adapted from ExtendedNumerics.BigDecimal) 
    /// and provides some extra functionality on top. 
    /// </summary>
    public readonly struct BigDecimal : IEquatable<BigDecimal>
        , IEquatable<BigInteger>
        , IComparable<BigDecimal>
        , IComparable<BigInteger>
        , IComparable<int>
        , IComparable<int?>
        , IComparable<decimal>
        , IComparable<double>
        , IComparable<float>
    {
        private readonly InnerDecimal _value;

        public static readonly BigDecimal Zero = new BigDecimal(InnerDecimal.Zero);
        public static readonly BigDecimal One = new BigDecimal(InnerDecimal.One);
        public static BigDecimal MaxValue => System.Decimal.MaxValue;
        public static BigDecimal MinValue => System.Decimal.MinValue;
        public static BigDecimal PositiveInfinity => Double.PositiveInfinity;
        public static BigDecimal NegativeInfinity => Double.NegativeInfinity;
        public bool IsWholeNumber => this.Mod(1) == 0;

        /// <summary>
        /// Gets a value indicating whether or not the current encapsulated value is within the range of
        /// System.Decimal. 
        /// </summary>
        private bool InRangeOfSysDecimal => (_value <= System.Decimal.MaxValue && _value >= System.Decimal.MinValue);

        #region Instantiation 

        public BigDecimal(long n)
        {
            _value = n;
        }

        public BigDecimal(double d)
        {
            _value = (InnerDecimal)d;
        }

        public BigDecimal(System.Decimal d)
        {
            _value = (InnerDecimal)d;
        }

        public BigDecimal(BigInteger num)
        {
            _value = InnerDecimal.Parse(num.ToString());
        }

        public BigDecimal(InnerDecimal num)
        {
            _value = num;
        }

        public static BigDecimal FromU64AndShift(BigInteger num, int shiftBytes = 0)
        {
            return new BigDecimal(num) / new BigDecimal(System.Math.Pow(10, shiftBytes));
        }

        public static BigDecimal FromIntegerAndShift(long num, int shiftBytes = 0)
        {
            return new BigDecimal(num) / new BigDecimal(System.Math.Pow(10, shiftBytes));
        }

        /// <summary>
        /// Parses a string representation into a BigDecimal value. 
        /// </summary>
        /// <param name="s">String representation of a BigDecimal value.</param>
        /// <returns>A BigDecimal instance.</returns>
        public static BigDecimal Parse(string s)
        {
            return new BigDecimal(InnerDecimal.Parse(s));
        }

        #endregion

        #region Instance Methods

        /// <summary>
        /// Raises the current value to a given power and returns the result.
        /// </summary>
        /// <param name="power">The power to which to raise.</param>
        /// <returns>The result as BigDecimal</returns>
        public BigDecimal Pow(int power)
        {
            //TODO: (LOW) there might be more complex rounding involved 
            InnerDecimal d = _value;
            InnerDecimal origValue = _value;
            int absPower = System.Math.Abs(power);
            for (int n = 0; n < absPower; n++)
            {
                d = d * origValue;
            }

            return (power < 0) ?
                BigDecimal.One / new BigDecimal(d) :
                new BigDecimal(d);
        }

        /// <summary>
        /// Modulus function. 
        /// </summary>
        /// <param name="value">The value to mod.</param>
        /// <returns>The result as BigDecimal</returns>
        public BigDecimal Mod(BigDecimal value)
        {
            return new BigDecimal(InnerDecimal.Mod(_value, value._value));
        }

        /// <summary>
        /// Modulus function. 
        /// </summary>
        /// <param name="value">The value to mod.</param>
        /// <returns>The result as BigDecimal</returns>
        public BigDecimal Mod(BigInteger value)
        {
            return new BigDecimal(InnerDecimal.Mod(_value, value));
        }

        /// <summary>
        /// Modulus function. 
        /// </summary>
        /// <param name="value">The value to mod.</param>
        /// <returns>The result as BigDecimal</returns>
        public BigDecimal Mod(int value)
        {
            return new BigDecimal(InnerDecimal.Mod(_value, value));
        }

        /// <summary>
        /// Calculates the square root of the currently held value. 
        /// </summary>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the current value is negative.</exception>
        /// <exception cref="NotImplementedException"></exception>
        public BigDecimal Sqrt(decimal epsilon = 0.0M)
        {
            if (_value < 0)
                throw new ArgumentOutOfRangeException("Cannot calculate square root from a negative number");

            if (this.InRangeOfSysDecimal)
            {
                decimal current = (decimal)System.Math.Sqrt((double)_value), previous;
                do
                {
                    previous = current;
                    if (previous == 0.0M) return 0;
                    current = (previous + (decimal)_value / previous) / 2;
                }

                while (System.Math.Abs(previous - current) > epsilon);
                return current;
            }
            else
            {
                throw new NotImplementedException("Sqrt not yet implemented for really big numbers");
            }
        }

        public BigDecimal AdjustDecimals(int shiftBytes = 0)
        {
            return this / new BigDecimal(System.Math.Pow(10, shiftBytes));
        }

        /// <summary>
        /// Returns the largest integer less than or equal to the current value.
        /// </summary>
        /// <returns>A BigDecimal value representing the integer.</returns>
        public BigDecimal Floor()
        {
            if (this.IsWholeNumber)
                return this;

            if (this.InRangeOfSysDecimal)
                return System.Math.Floor((System.Decimal)_value);

            string s = this.ToString();
            if (s.Contains("."))
            {
                string[] parts = s.Split('.');
                s = parts[0];
            }

            return BigDecimal.Parse(s);
        }

        /// <summary>
        /// Returns the smallest integer value that is greater than or equal to the current value.
        /// </summary>
        /// <returns>A BigDecimal value representing the integer.</returns>
        public BigDecimal Ceiling()
        {
            return (this.IsWholeNumber) ? this : this.Floor() + 1;
        }

        /// <summary>
        /// Rounds the decimal portion towards zero (rounds up if value < 0, down if > 0).
        /// </summary>
        /// <returns>The rounded value.</returns>
        public BigDecimal Truncate()
        {
            return this._value > 0 ? this.Floor() : this.Ceiling(); 
        }

        /// <summary>
        /// Returns a string representation of the current instance state.
        /// </summary>
        public override string ToString()
        {
            return _value.ToString();
        }

        #endregion

        #region Implicit/Explicit Cast Operators

        public static implicit operator BigDecimal(decimal value)
        {
            return new BigDecimal(value);
        }
        public static implicit operator BigDecimal(double value)
        {
            return new BigDecimal(value);
        }
        public static implicit operator BigDecimal(float value)
        {
            return new BigDecimal(value);
        }
        public static implicit operator BigDecimal(long value)
        {
            return new BigDecimal(value);
        }
        public static implicit operator BigDecimal(int value)
        {
            return new BigDecimal(value);
        }
        public static implicit operator BigDecimal(short value)
        {
            return new BigDecimal(value);
        }

        public static explicit operator decimal(BigDecimal value)
        {
            return (decimal)value._value;
        }
        public static explicit operator double(BigDecimal value)
        {
            return (double)value._value;
        }
        public static explicit operator float(BigDecimal value)
        {
            return (float)(value._value);
        }
        public static explicit operator int(BigDecimal value)
        {
            return (int)(value._value);
        }
        public static explicit operator long(BigDecimal value)
        {
            return Int64.Parse(value._value.ToString());
        }
        public static explicit operator short(BigDecimal value)
        {
            return (short)(value._value);
        }
        public static explicit operator InnerDecimal(BigDecimal value)
        {
            return (value._value);
        }

        #endregion

        #region Operator Overloads

        public static BigDecimal operator +(BigDecimal a, BigDecimal b)
        {
            return new BigDecimal(InnerDecimal.Add(a._value, b._value));
        }

        public static BigDecimal operator -(BigDecimal a, BigDecimal b)
        {
            return new BigDecimal(InnerDecimal.Subtract(a._value, b._value));
        }

        public static BigDecimal operator *(BigDecimal a, BigDecimal b)
        {
            return new BigDecimal(InnerDecimal.Multiply(a._value, b._value));
        }

        public static BigDecimal operator /(BigDecimal a, BigDecimal b)
        {
            return new BigDecimal(InnerDecimal.Divide(a._value, b._value));
        }

        public static bool operator ==(BigDecimal a, BigDecimal b)
        {
            return (a._value.Equals(b._value));
        }

        public static bool operator !=(BigDecimal a, BigDecimal b)
        {
            return !(a._value.Equals(b._value));
        }

        #endregion

        #region Static Methods 

        public static BigDecimal Pow(BigDecimal d, int power)
        {
            //TODO: (LOW) there might be more complex rounding involved 
            return d.Pow(power);
        }

        #endregion

        #region Equality/Inequality

        public bool Equals(BigDecimal other)
        {
            return _value.Equals(other._value);
        }

        bool IEquatable<BigInteger>.Equals(BigInteger other)
        {
            return _value.Equals(new BigDecimal(other));
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                if (obj is BigDecimal)
                    return _value.Equals(((BigDecimal)obj)._value);
                if (obj is InnerDecimal)
                    return _value.Equals(_value);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        #endregion

        #region IComparable 

        public int CompareTo(BigDecimal other)
        {
            return _value.CompareTo(other._value);
        }

        int IComparable<BigInteger>.CompareTo(BigInteger other)
        {
            return this.CompareTo(new BigDecimal(other));
        }

        int IComparable<int>.CompareTo(int other)
        {
            return _value.CompareTo(other);
        }

        int IComparable<int?>.CompareTo(int? other)
        {
            return _value.CompareTo(other);
        }

        int IComparable<double>.CompareTo(double other)
        {
            return _value.CompareTo(other);
        }

        int IComparable<float>.CompareTo(float other)
        {
            return _value.CompareTo(other);
        }

        int IComparable<decimal>.CompareTo(decimal other)
        {
            return _value.CompareTo(other);
        }

        #endregion
    }
}