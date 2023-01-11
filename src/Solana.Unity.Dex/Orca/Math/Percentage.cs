using System;

namespace Solana.Unity.Dex.Orca.Math
{
    public class Percentage
    {
        /// <summary>
        /// Class attributes/members
        /// </summary>
        long _numerator;
        long _denominator;

        /// <summary>
        /// Constructors
        /// </summary>
        public Percentage()
        {
            Initialize(0, 1);
        }

        public Percentage(long wholeNumber)
        {
            Initialize(wholeNumber, 1);
        }

        public Percentage(double decimalValue)
        {
            Percentage temp = FromDouble(decimalValue);
            Initialize(temp.Numerator, temp.Denominator);
        }

        public Percentage(string s)
        {
            Percentage temp = Parse(s);
            Initialize(temp.Numerator, temp.Denominator);
        }

        public Percentage(long numerator, long denominator)
        {
            Initialize(numerator, denominator);
        }

        /// <summary>
        /// Internal function for constructors
        /// </summary>
        private void Initialize(long numerator, long denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            ReducePercentage(this);
        }

        /// <summary>
        /// Properites
        /// </summary>
        public long Denominator
        {
            get
            { return _denominator; }
            set
            {
                if (value != 0)
                    _denominator = value;
                else
                    throw new PercentageException("Denominator cannot be assigned a ZERO Value");
            }
        }

        public long Numerator
        {
            get
            { return _numerator; }
            set
            { _numerator = value; }
        }

        public long Value
        {
            set
            {
                _numerator = value;
                _denominator = 1;
            }
        }

        /// <summary>
        /// The function returns the current Percentage object as double
        /// </summary>
        public double ToDouble()
        {
            return ((double)this.Numerator / this.Denominator);
        }

        /// <summary>
        /// The function returns the current Percentage object as a string
        /// </summary>
        public override string ToString()
        {
            string str;
            if (this.Denominator == 1)
                str = this.Numerator.ToString();
            else
                str = this.Numerator + "/" + this.Denominator;
            return str;
        }
        /// <summary>
        /// The function takes an string as an argument and returns its corresponding reduced Percentage
        /// the string can be an in the form of and integer, double or Percentage.
        /// e.g it can be like "123" or "123.321" or "123/456"
        /// </summary>
        public static Percentage Parse(string s)
        {
            int i;
            for (i = 0; i < s.Length; i++)
                if (s[i] == '/')
                    break;

            if (i == s.Length)     // if string is not in the form of a Percentage
                // then it is double or integer
                return (Convert.ToDouble(s));
            //return ( ToPercentage( Convert.ToDouble(s) ) );

            // else string is in the form of Numerator/Denominator
            long numerator = Convert.ToInt64(s.Substring(0, i));
            long denominator = Convert.ToInt64(s.Substring(i + 1));
            return new Percentage(numerator, denominator);
        }


        /// <summary>
        /// The function takes a floating point number as an argument 
        /// and returns its corresponding reduced Percentage
        /// </summary>
        public static Percentage FromDouble(double dValue)
        {
            try
            {
                checked
                {
                    Percentage frac;
                    if (dValue % 1 == 0)    // if whole number
                    {
                        frac = new Percentage((long)dValue);
                    }
                    else
                    {
                        double dTemp = dValue;
                        long iMultiple = 1;
                        string strTemp = dValue.ToString();
                        while (strTemp.IndexOf("E") > 0)    // if in the form like 12E-9
                        {
                            dTemp *= 10;
                            iMultiple *= 10;
                            strTemp = dTemp.ToString();
                        }
                        int i = 0;
                        while (strTemp[i] != '.')
                            i++;
                        int iDigitsAfterDecimal = strTemp.Length - i - 1;
                        while (iDigitsAfterDecimal > 0)
                        {
                            dTemp *= 10;
                            iMultiple *= 10;
                            iDigitsAfterDecimal--;
                        }
                        frac = new Percentage((int)System.Math.Round(dTemp), iMultiple);
                    }
                    return frac;
                }
            }
            catch (OverflowException)
            {
                throw new PercentageException("Conversion not possible due to overflow");
            }
            catch (Exception)
            {
                throw new PercentageException("Conversion not possible");
            }
        }

        public static Percentage FromFraction(long numerator, long denominator)
        {
            return new Percentage(numerator, denominator);
        }


        /// <summary>
        /// The function replicates current Percentage object
        /// </summary>
        public Percentage Duplicate()
        {
            Percentage frac = new Percentage();
            frac.Numerator = Numerator;
            frac.Denominator = Denominator;
            return frac;
        }

        /// <summary>
        /// The function returns the inverse of a Percentage object
        /// </summary>
        public static Percentage Inverse(Percentage frac1)
        {
            if (frac1.Numerator == 0)
                throw new PercentageException("Operation not possible (Denominator cannot be assigned a ZERO Value)");

            long numerator = frac1.Denominator;
            long denominator = frac1.Numerator;
            return (new Percentage(numerator, denominator));
        }


        /// <summary>
        /// Operators for the Percentage object
        /// includes -(unary), and binary opertors such as +,-,*,/
        /// also includes relational and logical operators such as ==,!=,<,>,<=,>=
        /// </summary>
        public static Percentage operator -(Percentage frac1)
        { return (Negate(frac1)); }

        public static Percentage operator +(Percentage frac1, Percentage frac2)
        { return (Add(frac1, frac2)); }

        public static Percentage operator +(int num, Percentage frac1)
        { return (Add(frac1, new Percentage(num))); }

        public static Percentage operator +(Percentage frac1, int num)
        { return (Add(frac1, new Percentage(num))); }

        public static Percentage operator +(double dbl, Percentage frac1)
        { return (Add(frac1, Percentage.FromDouble(dbl))); }

        public static Percentage operator +(Percentage frac1, double dbl)
        { return (Add(frac1, Percentage.FromDouble(dbl))); }

        public static Percentage operator -(Percentage frac1, Percentage frac2)
        { return (Add(frac1, -frac2)); }

        public static Percentage operator -(int num, Percentage frac1)
        { return (Add(-frac1, new Percentage(num))); }

        public static Percentage operator -(Percentage frac1, int num)
        { return (Add(frac1, -(new Percentage(num)))); }

        public static Percentage operator -(double dbl, Percentage frac1)
        { return (Add(-frac1, Percentage.FromDouble(dbl))); }

        public static Percentage operator -(Percentage frac1, double dbl)
        { return (Add(frac1, -Percentage.FromDouble(dbl))); }

        public static Percentage operator *(Percentage frac1, Percentage frac2)
        { return (Multiply(frac1, frac2)); }

        public static Percentage operator *(int num, Percentage frac1)
        { return (Multiply(frac1, new Percentage(num))); }

        public static Percentage operator *(Percentage frac1, int num)
        { return (Multiply(frac1, new Percentage(num))); }

        public static Percentage operator *(double dbl, Percentage frac1)
        { return (Multiply(frac1, Percentage.FromDouble(dbl))); }

        public static Percentage operator *(Percentage frac1, double dbl)
        { return (Multiply(frac1, Percentage.FromDouble(dbl))); }

        public static Percentage operator /(Percentage frac1, Percentage frac2)
        { return (Multiply(frac1, Inverse(frac2))); }

        public static Percentage operator /(int num, Percentage frac1)
        { return (Multiply(Inverse(frac1), new Percentage(num))); }

        public static Percentage operator /(Percentage frac1, int num)
        { return (Multiply(frac1, Inverse(new Percentage(num)))); }

        public static Percentage operator /(double dbl, Percentage frac1)
        { return (Multiply(Inverse(frac1), Percentage.FromDouble(dbl))); }

        public static Percentage operator /(Percentage frac1, double dbl)
        { return (Multiply(frac1, Percentage.Inverse(Percentage.FromDouble(dbl)))); }

        public static bool operator ==(Percentage frac1, Percentage frac2)
        { return frac1.Equals(frac2); }

        public static bool operator !=(Percentage frac1, Percentage frac2)
        { return (!frac1.Equals(frac2)); }

        public static bool operator ==(Percentage frac1, int num)
        { return frac1.Equals(new Percentage(num)); }

        public static bool operator !=(Percentage frac1, int num)
        { return (!frac1.Equals(new Percentage(num))); }

        public static bool operator ==(Percentage frac1, double dbl)
        { return frac1.Equals(new Percentage(dbl)); }

        public static bool operator !=(Percentage frac1, double dbl)
        { return (!frac1.Equals(new Percentage(dbl))); }

        public static bool operator <(Percentage frac1, Percentage frac2)
        { return frac1.Numerator * frac2.Denominator < frac2.Numerator * frac1.Denominator; }

        public static bool operator >(Percentage frac1, Percentage frac2)
        { return frac1.Numerator * frac2.Denominator > frac2.Numerator * frac1.Denominator; }

        public static bool operator <=(Percentage frac1, Percentage frac2)
        { return frac1.Numerator * frac2.Denominator <= frac2.Numerator * frac1.Denominator; }

        public static bool operator >=(Percentage frac1, Percentage frac2)
        { return frac1.Numerator * frac2.Denominator >= frac2.Numerator * frac1.Denominator; }


        /// <summary>
        /// overloaed user defined conversions: from numeric data types to Percentages
        /// </summary>
        public static implicit operator Percentage(long lNo)
        { return new Percentage(lNo); }
        public static implicit operator Percentage(double dNo)
        { return new Percentage(dNo); }
        public static implicit operator Percentage(string strNo)
        { return new Percentage(strNo); }


        /// <summary>
        /// overloaed user defined conversions: from Percentages to double and string
        /// </summary>
        public static explicit operator double(Percentage frac)
        { return frac.ToDouble(); }

        public static implicit operator string(Percentage frac)
        { return frac.ToString(); }

        /// <summary>
        /// checks whether two Percentages are equal
        /// </summary>
        public override bool Equals(object obj)
        {
            Percentage frac = (Percentage)obj;
            return (Numerator == frac.Numerator && Denominator == frac.Denominator);
        }

        /// <summary>
        /// returns a hash code for this Percentage
        /// </summary>
        public override int GetHashCode()
        {
            return (Convert.ToInt32((Numerator ^ Denominator) & 0xFFFFFFFF));
        }

        /// <summary>
        /// internal function for negation
        /// </summary>
        private static Percentage Negate(Percentage frac1)
        {
            long numerator = -frac1.Numerator;
            long denominator = frac1.Denominator;
            return (new Percentage(numerator, denominator));

        }

        /// <summary>
        /// internal functions for binary operations
        /// </summary>
        private static Percentage Add(Percentage frac1, Percentage frac2)
        {
            try
            {
                checked
                {
                    long numerator = frac1.Numerator * frac2.Denominator + frac2.Numerator * frac1.Denominator;
                    long denominator = frac1.Denominator * frac2.Denominator;
                    return (new Percentage(numerator, denominator));
                }
            }
            catch (OverflowException)
            {
                throw new PercentageException("Overflow occurred while performing arithemetic operation");
            }
            catch (Exception)
            {
                throw new PercentageException("An error occurred while performing arithemetic operation");
            }
        }

        private static Percentage Multiply(Percentage frac1, Percentage frac2)
        {
            try
            {
                checked
                {
                    long numerator = frac1.Numerator * frac2.Numerator;
                    long denominator = frac1.Denominator * frac2.Denominator;
                    return (new Percentage(numerator, denominator));
                }
            }
            catch (OverflowException)
            {
                throw new PercentageException("Overflow occurred while performing arithemetic operation");
            }
            catch (Exception)
            {
                throw new PercentageException("An error occurred while performing arithemetic operation");
            }
        }

        /// <summary>
        /// The function returns GCD of two numbers (used for reducing a Percentage)
        /// </summary>
        private static long GCD(long num1, long num2)
        {
            // take absolute values
            if (num1 < 0) num1 = -num1;
            if (num2 < 0) num2 = -num2;

            do
            {
                if (num1 < num2)
                {
                    long tmp = num1;  // swap the two operands
                    num1 = num2;
                    num2 = tmp;
                }
                num1 = num1 % num2;
            } while (num1 != 0);
            return num2;
        }

        /// <summary>
        /// The function reduces(simplifies) a Percentage object by dividing both its numerator 
        /// and denominator by their GCD
        /// </summary>
        public static void ReducePercentage(Percentage frac)
        {
            try
            {
                if (frac.Numerator == 0)
                {
                    frac.Denominator = 1;
                    return;
                }

                long iGCD = GCD(frac.Numerator, frac.Denominator);
                frac.Numerator /= iGCD;
                frac.Denominator /= iGCD;

                if (frac.Denominator < 0)   // if -ve sign in denominator
                {
                    //pass -ve sign to numerator
                    frac.Numerator *= -1;
                    frac.Denominator *= -1;
                }
            } // end try
            catch (Exception exp)
            {
                throw new PercentageException("Cannot reduce Percentage: " + exp.Message);
            }
        }

    }   //end class Percentage


    /// <summary>
    /// Exception class for Percentage, derived from System.Exception
    /// </summary>
    public class PercentageException : Exception
    {
        public PercentageException() : base()
        { }

        public PercentageException(string message) : base(message)
        { }

        public PercentageException(string message, Exception InnerException) : base(message, InnerException)
        { }
    }   //end class PercentageException
}