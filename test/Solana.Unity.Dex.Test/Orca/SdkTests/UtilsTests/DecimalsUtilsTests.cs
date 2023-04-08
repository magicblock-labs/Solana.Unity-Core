using NUnit.Framework;
using Solana.Unity.Dex.Math;

namespace Solana.Unity.Dex.Test.Orca.SdkTests.UtilsTests
{
    public static class TestDecimalsUtils
    {
        [TestFixture]
        public static class TestDecimalUtils
        {
            [Test]
            [Description("Convert to and from")]
            public static void ConvertToAndFrom() 
            {
                ulong value = 100_000_000;
                int decimals = 6;
                var valueDecimals = DecimalUtil.FromUlong(value, decimals);
                Assert.IsTrue(valueDecimals.Equals((decimal)100.0));
                
                Assert.IsTrue(DecimalUtil.ToUlong(valueDecimals, decimals).Equals(value));
                Assert.IsTrue(DecimalUtil.ToUlong((float)valueDecimals, decimals).Equals(value));

                Assert.IsTrue(DecimalUtil.FromBigInteger(value, decimals).Equals(valueDecimals));
            }
            
        }
    }
}