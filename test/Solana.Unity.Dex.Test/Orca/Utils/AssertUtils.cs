using NUnit.Framework; 

using System; 
using System.Numerics; 
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Core.Http;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Core.Types;
using Solana.Unity.Dex.Orca.Core.Errors;

namespace Solana.Unity.Dex.Test.Orca.Utils
{
    public static class AssertUtils
    {
        public static void AssertTick(
            Tick tick, 
            bool initialized, 
            BigInteger liquidityGross, 
            BigInteger liquidityNet
        )
        {
            Assert.That(tick.Initialized, Is.EqualTo(initialized));
            Assert.That(tick.LiquidityGross, Is.EqualTo(liquidityGross));
            Assert.That(tick.LiquidityNet, Is.EqualTo(liquidityNet));
        }

        public static async Task AssertTicksFail(
            TestWhirlpoolContext ctx, 
            PublicKey whirlpoolKey, 
            int lowerTick, 
            int upperTick, 
            bool withMetadata = false
        )
        {
            var result = await PositionTestUtils.OpenPositionAsync(
                ctx,
                PositionTestUtils.GenerateOpenParams(
                    ctx,
                    whirlpoolKey,
                    lowerTick,
                    upperTick,
                    withMetadata: withMetadata
                )
            );
                
            AssertUtils.AssertFailedWithCustomError(result, WhirlpoolErrorType.InvalidTickIndex);
        }
        
        public static void AssertFailedWithCustomError(RequestResult<string> result, WhirlpoolErrorType errorType)
        {
            AssertFailedWithError(result, (int)errorType);
        }

        public static void AssertFailedWithStandardError(RequestResult<string> result, string errorCodeHex)
        {
            if (result.WasSuccessful) 
            {
                Console.WriteLine("why tho");
            }
            Assert.IsFalse(result.WasSuccessful);
            Assert.IsTrue(result.Reason.EndsWith($"custom program error: {errorCodeHex}"));
        }

        public static void AssertFailedWithError(RequestResult<string> result, string errorCodeHex)
        {
            Assert.IsFalse(result.WasSuccessful);
            Assert.IsTrue(result.Reason.EndsWith($"custom program error: {errorCodeHex}"));
        }

        public static void AssertFailedWithError(RequestResult<string> result, int errorCode)
        {
            AssertFailedWithError(result, $"0x{errorCode.ToString("x")}");
        }

        public static void AssertFailedWithStandardError(RequestResult<string> result, StandardErrorType errorCode)
        {
            AssertFailedWithError(result, (int)errorCode);
        }

        public static void AssertSignatureError(RequestResult<string> result)
        {
            Assert.IsFalse(result.WasSuccessful); 
            Assert.IsTrue(result.Reason != null && result.Reason.EndsWith("signature verification failure")); 
        }
    }
}