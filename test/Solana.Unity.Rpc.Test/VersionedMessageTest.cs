using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solana.Unity.Rpc.Models;
using System;

namespace Solana.Unity.Rpc.Test
{
    [TestClass]
    public class VersionedMessageTest
    {
        [TestMethod]
        public void DeserializeMessageVersion()
        {
            // Buffer with legacy prefix
            byte[] bufferWithLegacyPrefix = new byte[] { 1 };
            Assert.AreEqual("legacy", VersionedMessage.DeserializeMessageVersion(bufferWithLegacyPrefix));

            // Buffer with version prefixes
            byte[] bufferWithVersionPrefix;
            for (byte version = 0; version <= 127; version++)
            {
                bufferWithVersionPrefix = new byte[] { (byte)(1 << 7 | version) };
                Assert.AreEqual(version.ToString(), VersionedMessage.DeserializeMessageVersion(bufferWithVersionPrefix));
            }
        }
        
        [TestMethod]
        public void DeserializeFailure()
        {
            byte[] bufferWithV1Prefix = new byte[] { (byte)(1 << 7 | 1) };

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                VersionedMessage.Deserialize(bufferWithV1Prefix);
            });
        }
    }
}