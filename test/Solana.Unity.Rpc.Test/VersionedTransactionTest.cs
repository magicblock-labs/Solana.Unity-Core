using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System;
using System.Collections.Generic;

namespace Solana.Unity.Rpc.Test
{
    [TestClass]
    public class VersionedTransactionTest
    {
        // Text Fixtures
        private const string Base64SerializedVersionedTx = "AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAQAID0PC8escBdHr+DqWKtpHK7G3Pjdg2Ck7knmE3xgnbQOjdjA4EKgxNXOjIIkHRHfkut/Vvp8rP22jsbUgEwuWYySfUxeumK36JBIaYA4PqEKsw0TjomhTo8daZDNy5TKRyKIdmklnVTL3GrXKI61rcFwE4jsO/6OYROzRbst6fL4CvWac4W46kaGG3DB/ybn8y3wbxeSOBMe+ys/dGPs7iqjfsLhf7Ff9NofcPgIyAbMytm5ggTyKwmR+JqgXUXARVfJea6+zakfoAfAfG3uw1Hd6+x+V6Sy31FQ0u6YV9iBFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADBkZv5SEXMv/srbpyw5vnvIzlu8X3EmssQ5s6QAAAAAR51VvyMcBu7nTFbs5oFQf9sbLeo/SOUQKxzaJWvBOPBpuIV/6rgYT7aH9jRhjANdrEOdwa6ztVmKDwAAAAAAEG3fbh12Whk9nL4UbO63msHLSF7V9bN5E6jPWFfv8AqU9LbA5BCP0qaiR46uCsxZ2yG7Tt9RHBYs9gLR4MCQH7jJclj04kifG7PRApFI4NgwtaE5na/xCEBI572Nvp+Fm0P/on9df2SnTAmx8pWHneSwmrNt/J3VFLMhqns4zl6LMkU+jQNMN+ACdixGmE5BkdVUZx1+dwwNeMrT47LC3ZBwgABQLAXBUADQYABAAKBwsBAQcCAAQMAgAAAADh9QUAAAAACwEEARENBgACACIHCwEBCTMLDAAEAwUCCiIJCQ4JHwsMHgMZBRwbGh0mHwsMGAMXARYVFAYlIyAhDAEFEhMREAskJA8uwSCbM0HWnIEHAwAAABEBRgADEQEeAAIJZAIDAOH1BQAAAACIDRwAAAAAADIAAAsDBAAAAQkDnKEf7dZ6lPW72wJ6PGEMo6GObunTTrduTS0+A/SoNtQFzcnIxscGAMTF6GXLuwkpzcAg7dDeOseRGYg4EsfNop9xrUgbIpA5iAsD3kEFIB8dHBsBIaPIqiSwTwiZiBypIhYrNygPC7k5xWLT/nfZXo4VmqMKBmJlZGNmYQFn";
        private const string VersionedCompiledMessage = "gAEACA9DwvHrHAXR6/g6liraRyuxtz43YNgpO5J5hN8YJ20Do3YwOBCoMTVzoyCJB0R35Lrf1b6fKz9to7G1IBMLlmMkn1MXrpit+iQSGmAOD6hCrMNE46JoU6PHWmQzcuUykciiHZpJZ1Uy9xq1yiOta3BcBOI7Dv+jmETs0W7Leny+Ar1mnOFuOpGhhtwwf8m5/Mt8G8XkjgTHvsrP3Rj7O4qo37C4X+xX/TaH3D4CMgGzMrZuYIE8isJkfiaoF1FwEVXyXmuvs2pH6AHwHxt7sNR3evsflekst9RUNLumFfYgRQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwZGb+UhFzL/7K26csOb57yM5bvF9xJrLEObOkAAAAAEedVb8jHAbu50xW7OaBUH/bGy3qP0jlECsc2iVrwTjwabiFf+q4GE+2h/Y0YYwDXaxDncGus7VZig8AAAAAABBt324ddloZPZy+FGzut5rBy0he1fWzeROoz1hX7/AKlPS2wOQQj9KmokeOrgrMWdshu07fURwWLPYC0eDAkB+4yXJY9OJInxuz0QKRSODYMLWhOZ2v8QhASOe9jb6fhZtD/6J/XX9kp0wJsfKVh53ksJqzbfyd1RSzIap7OM5eizJFPo0DTDfgAnYsRphOQZHVVGcdfncMDXjK0+Oywt2QcIAAUCwFwVAA0GAAQACgcLAQEHAgAEDAIAAAAA4fUFAAAAAAsBBAERDQYAAgAiBwsBAQkzCwwABAMFAgoiCQkOCR8LDB4DGQUcGxodJh8LDBgDFwEWFRQGJSMgIQwBBRITERALJCQPLsEgmzNB1pyBBwMAAAARAUYAAxEBHgACCWQCAwDh9QUAAAAAiA0cAAAAAAAyAAALAwQAAAEJA5yhH+3WepT1u9sCejxhDKOhjm7p0063bk0tPgP0qDbUBc3JyMbHBgDExehly7sJKc3AIO3Q3jrHkRmIOBLHzaKfca1IGyKQOYgLA95BBSAfHRwbASGjyKoksE8ImYgcqSIWKzcoDwu5OcVi0/532V6OFZqjCgZiZWRjZmEBZw==";
        private const string TxSignature = "FdaS+HGFi7gVXum3hQ2Sb3SRog7Dy+wWmYxMR3PrO+neBtLutfEa4gWLmPUNNsx1zPy3+yC8HRug1YgEsS3PDQ==";
        private readonly Wallet.Wallet _testWallet = new(new PrivateKey("4j3PbCCYvcAz1FbKGs7fBoQ5cd8piWCsQm5k6wNnTTzEtE6aM8JZ2AJaaJTjZJgGk9LywyonNHcVopHAwrMqh6kr").KeyBytes, "", SeedMode.Bip39);

        
        [TestMethod]
        public void DeserializeVersionedTransactions()
        {
            // Deserialize the versioned transaction
            Transaction versionedTx = Transaction.Deserialize(Base64SerializedVersionedTx);
            
            // Assert that the transaction was deserialized correctly using VersionedTransaction class
            Assert.IsNotNull(versionedTx);
            Assert.IsNotNull(((VersionedTransaction)versionedTx).AddressTableLookups);
            Assert.AreEqual(((VersionedTransaction)versionedTx).AddressTableLookups.Count, 3);
            
            // Assert that the message is the same as the expected compiled message
            var message = versionedTx.CompileMessage();
            CollectionAssert.AreEqual( Convert.FromBase64String(VersionedCompiledMessage), message);
        }
        
        [TestMethod]
        public void DeserializeSerializeVersionedTransactions()
        {
            // Deserialize the versioned transaction
            Transaction versionedTx = Transaction.Deserialize(Base64SerializedVersionedTx);
            Assert.IsNotNull(versionedTx);
            
            // Serialize the versioned transaction
            var serializedVersionedTx = versionedTx.Serialize();
            Assert.AreEqual(Base64SerializedVersionedTx, Convert.ToBase64String(serializedVersionedTx));
        }
        
        [TestMethod]
        public void TestSignVersionedTransactions()
        {
            // Deserialize the versioned transaction
            Transaction versionedTx = Transaction.Deserialize(Base64SerializedVersionedTx);
            Assert.IsNotNull(versionedTx);

            // Check that the signature is as expected
            versionedTx.Signatures = new List<SignaturePubKeyPair>();
            versionedTx.Sign(_testWallet.Account);
            Assert.AreEqual(TxSignature, Convert.ToBase64String(versionedTx.Signatures[0].Signature));
        }
    }
}