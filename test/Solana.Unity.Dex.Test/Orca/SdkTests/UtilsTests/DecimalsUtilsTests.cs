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
                var valueDouble = DecimalUtil.FromUlong(value, decimals);
                Assert.IsTrue(valueDouble.Equals(100.0));
                
                Assert.IsTrue(DecimalUtil.ToUlong(valueDouble, decimals).Equals(value));
                Assert.IsTrue(DecimalUtil.ToUlong((float)valueDouble, decimals).Equals(value));

                Assert.IsTrue(DecimalUtil.FromBigInteger(value, decimals).Equals(valueDouble));
            }
            
        }
    }
}