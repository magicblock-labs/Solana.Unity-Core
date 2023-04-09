using System.Numerics;

namespace Solana.Unity.Dex.Math;

public class DecimalUtil
{
    public static decimal FromUlong(ulong value, int shift = 0)
    {
        return value / (decimal)System.Math.Pow(10, shift);
    }
    
    public static decimal FromBigInteger(BigInteger value, int shift = 0)
    {
        return FromUlong((ulong)value, shift);
    }
    
    public static ulong ToUlong(double value, int shift = 0)
    {
        return ToUlong((decimal)value, shift);
    }
    
    public static ulong ToUlong(float value, int shift = 0)
    {
        return ToUlong((decimal)value, shift);
    }
    
    public static ulong ToUlong(decimal value, int shift = 0)
    {
        return (ulong)(value * (decimal)System.Math.Pow(10, shift));;
    }
}