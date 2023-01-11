using System;
using System.Numerics;

using Solana.Unity.Dex.Orca.Ticks;

namespace Solana.Unity.Dex.Orca.Math
{
    /// <summary>
    /// Mathemetical utilities specifically for calculating and manipulating prices. 
    /// </summary>
    public static class PriceMath
    {
        private static readonly int BIT_PRECISION = 14;
        private static readonly string LOG_B_2_X32 = "59543866431248";
        private static readonly string LOG_B_P_ERR_MARGIN_LOWER_X64 = "184467440737095516";
        private static readonly string LOG_B_P_ERR_MARGIN_UPPER_X64 = "15793534762490258745";
        
        public static BigInteger PriceToSqrtPriceX64(BigDecimal price, int decimalsA, int decimalsB)
        {
            return ArithmeticUtils.DecimalToX64BigInt((price * new BigDecimal(10).Pow(decimalsB - decimalsA)).Sqrt());
        }

        public static BigDecimal SqrtPriceX64ToPrice(BigInteger sqrtPriceX64, int decimalsA, int decimalsB)
        {
            return ArithmeticUtils.DecimalFromX64BigInt(sqrtPriceX64).Pow(2) * new BigDecimal(10).Pow(decimalsB - decimalsA);
        }

        public static BigInteger TickIndexToSqrtPriceX64(int tickIndex)
        {
            return (tickIndex > 0) ? 
                TickIndexToSqrtPricePositive(tickIndex) : 
                TickIndexToSqrtPriceNegative(tickIndex);
        }

        public static int SqrtPriceX64ToTickIndex(BigInteger sqrtPriceX64)
        {
            if (sqrtPriceX64 > BigInteger.Parse(MathConstants.MAX_SQRT_PRICE))
            {
                throw new System.Exception("Provided sqrtPrice is not within the supported sqrtPrice range.");
            }

            int msb = ArithmeticUtils.BigIntBitLength(sqrtPriceX64) -1;
            BigInteger adjustedMsb = msb - 64;
            BigInteger log2pIntegerX32 = SignedShiftLeft(adjustedMsb, 32, 128);
            
            BigInteger bit = BigInteger.Parse("9223372036854775808");
            BigInteger log2pFractionX64 = BigInteger.Zero;
            int precision = 0; 
            
            var r = msb >= 64 ? sqrtPriceX64 >> (msb - 63) : sqrtPriceX64 << (63 - msb);
            
            while (bit > BigInteger.Zero && precision < BIT_PRECISION)
            {
                r = r * r;
                BigInteger rMoreThanTwo = r >> 127;
                r = r >> ((int)(63 + rMoreThanTwo));
                log2pFractionX64 = log2pFractionX64 + (bit * (rMoreThanTwo));
                bit = bit >> 1;
                precision += 1;
            }

            BigInteger log2pFractionX32 = log2pFractionX64 >> 32;

            BigInteger log2pX32 = log2pIntegerX32 + log2pFractionX32;
            BigInteger logbpX64 = log2pX32 * BigInteger.Parse(LOG_B_2_X32);

            int tickLow = (int)SignedShiftRight(
                logbpX64 - BigInteger.Parse(LOG_B_P_ERR_MARGIN_LOWER_X64),
                64,
                128
            );
            int tickHigh = (int)SignedShiftRight(
                logbpX64 + BigInteger.Parse(LOG_B_P_ERR_MARGIN_UPPER_X64),
                64,
                128
            );

            if (tickLow == tickHigh)
            {
                return tickLow;
            }
            else
            {
                BigInteger derivedTickHighSqrtPriceX64 = PriceMath.TickIndexToSqrtPriceX64(tickHigh);
                return (derivedTickHighSqrtPriceX64 <= sqrtPriceX64) ? tickHigh : tickLow;
            }
        }

        public static BigDecimal TickIndexToPrice(int tickIndex, int decimalsA, int decimalsB)
        {
            return PriceMath.SqrtPriceX64ToPrice(
                PriceMath.TickIndexToSqrtPriceX64(tickIndex),
                decimalsA,
                decimalsB
            );
        }

        public static int PriceToTickIndex(
            BigDecimal price, 
            int decimalsA, 
            int decimalsB
        )
        {
            return PriceMath.SqrtPriceX64ToTickIndex(
                PriceMath.PriceToSqrtPriceX64(price, decimalsA, decimalsB)
            );
        }

        public static long PriceToInitializableTickIndex(
            BigDecimal price, 
            int decimalsA, 
            int decimalsB,
            ushort tickSpacing
        )
        {
            return TickUtils.GetInitializableTickIndex(
               PriceMath.PriceToTickIndex(price, decimalsA, decimalsB),
               tickSpacing
            );
        }
        
        private static BigInteger TickIndexToSqrtPricePositive(int tick)
        {
            BigInteger ratio = ((tick & 1) != 0) ? 
                BigInteger.Parse("79232123823359799118286999567") : 
                BigInteger.Parse("79228162514264337593543950336");

            string[] multiplicands =
            {
                "79236085330515764027303304731",
                "79244008939048815603706035061",
                "79259858533276714757314932305",
                "79291567232598584799939703904",
                "79355022692464371645785046466",
                "79482085999252804386437311141",
                "79736823300114093921829183326",
                "80248749790819932309965073892",
                "81282483887344747381513967011",
                "83390072131320151908154831281",
                "87770609709833776024991924138",
                "97234110755111693312479820773",
                "119332217159966728226237229890",
                "179736315981702064433883588727",
                "407748233172238350107850275304",
                "2098478828474011932436660412517",
                "55581415166113811149459800483533",
                "38992368544603139932233054999993551"
            };

            int pow2 = 2; 
            for (int n = 0; n < multiplicands.Length; n++)
            {
                if ((tick & pow2) != 0)  {
                    ratio = SignedShiftRight(ratio * BigInteger.Parse(multiplicands[n]), 96, 256);
                }
                pow2 *= 2;
            }

            return SignedShiftRight(ratio, 32, 256);
        }
        
        private static BigInteger TickIndexToSqrtPriceNegative(int tick)
        {
            tick = System.Math.Abs(tick);
            
            BigInteger ratio = ((tick & 1) != 0) ? 
                BigInteger.Parse("18445821805675392311") : 
                BigInteger.Parse("18446744073709551616");

            string[] multiplicands =
            {
                "18444899583751176498",
                "18443055278223354162",
                "18439367220385604838",
                "18431993317065449817",
                "18417254355718160513",
                "18387811781193591352",
                "18329067761203520168",
                "18212142134806087854",
                "17980523815641551639",
                "17526086738831147013",
                "16651378430235024244",
                "15030750278693429944",
                "12247334978882834399",
                "8131365268884726200",
                "3584323654723342297",
                "696457651847595233",
                "26294789957452057",
                "37481735321082"
            };

            int pow2 = 2; 
            for (int n = 0; n < multiplicands.Length; n++)
            {
                if ((tick & pow2) != 0)  {
                    ratio = (ratio * BigInteger.Parse(multiplicands[n])) >> 64;
                }
                pow2 *= 2;
            }
            
            return ratio;
        }

        private static BigInteger SignedShiftLeft(BigInteger n, int shiftBy, int bitWidth)
        {
            //TODO: (HIGH) I believe this is already a signed shift (which raises some questions) 
            return n << shiftBy ;

            //BigInteger twoN = ArithmeticUtils.ToTwos(n, bitWidth) << shiftBy;
            //twoN = ArithmeticUtils.IMaskN(bitWidth + 1);
            //return ArithmeticUtils.FromTwos(bitWidth);
        }
        
        private static BigInteger SignedShiftRight(BigInteger n, int shiftBy, int bitWidth)
        {
            //TODO: (HIGH) I believe this is already a signed shift (which raises some questions) test against legacy
            return n >> shiftBy;

            //BigInteger twoN = ArithmeticUtils.ToTwos(n, bitWidth) >> shiftBy;
            // twoN = ArithmeticUtils.IMaskN(bitWidth - shiftBy + 1);
            //return ArithmeticUtils.FromTwos(bitWidth - shiftBy); 
        }
    }
}
