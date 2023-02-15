using System.Numerics;

namespace Solana.Unity.Dex.Math;

public class DecimalUtil
{
    public static double FromUlong(ulong value, int shift = 0)
    {
        return value / System.Math.Pow(10, shift);
    }
    
    public static double FromBigInteger(BigInteger value, int shift = 0)
    {
        return FromUlong((ulong)value, shift);
    }
    
    public static ulong ToUlong(double value, int shift = 0)
    {
        return (ulong)(value * System.Math.Pow(10, shift));
    }
    
    public static ulong ToUlong(float value, int shift = 0)
    {
        return ToUlong((double)value, shift);
    }
}