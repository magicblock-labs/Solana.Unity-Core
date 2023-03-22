using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using System;
using System.Collections.Generic;

namespace Solana.Unity.Rpc.Test
{
    [TestClass]
    public class TransactionBuilderTest
    {
        #region consts and fixture

        private const string MnemonicWords =
            "route clerk disease box emerge airport loud waste attitude film army tray" +
            " forward deal onion eight catalog surface unit card window walnut wealth medal";

        private const string Blockhash = "5cZja93sopRB9Bkhckj5WzCxCaVyriv2Uh5fFDPDFFfj";

        private const string AddSignatureBlockHash = "F2EzHpSp2WYRDA1roBN2Q4Wzw7ePxU2z1zWfh8ejUEyh";
        private const string AddSignatureTransaction = "AThRcCA7YPqwXF1JrA3lTHKU0OTZdSbh1jn1oEUkOXh" +
            "lZlNfUZnJyC5I3h6ldRGY444BBKpjRNTYO2n5x8t9swABAAIER2mrlyBLqD+wyu4X94aPHgdOUhWBoNidlDedq" +
            "mW3F7J7rHLZwOnCKOnqrRmjOO1w2JcV0XhPLlWiw5thiFgQQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAABUpTUPhdyILWFKVWcniKKW3fHqur0KYGeIhJMvTu9qDQVQOHggZl4ubetKawWVznB6EGcsLPkeO3Skl7n" +
            "XGaZAICAgABDAIAAACAlpgAAAAAAAMBABVIZWxsbyBmcm9tIFNvbC5OZXQgOik=";
        private const string AddSignatureSignature = "28Jo82xATR1U2u1PfhEjhdn3m3ciXEbxi7SocxVaj9YvyxJHkZb3yyn9QYtAubqrTcXRqTvG8DKRLGnjs5mTi5yy";

        private const string ExpectedTransactionHashWithTransferAndMemo =
            "AV9Xyi1t5dscb5+097PVDAP8fq/6HDRoNTQx9ZD2picvZNDUy9seCEKgsTNKgeTXtQ+pNEYB" +
            "4PfxPX+9bQI7hgkBAAIEUy4zulRg8z2yKITZaNwcnq6G6aH8D0ITae862qbJ+3eE3M6r5DRw" +
            "ldquwlqOuXDDOWZagXmbHnAU3w5Dg44kogAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAABUpTUPhdyILWFKVWcniKKW3fHqur0KYGeIhJMvTu9qBEixald4nI54jqHpYLSWViej50" +
            "bnmzhen0yUOsH2zbbgICAgABDAIAAACAlpgAAAAAAAMBABVIZWxsbyBmcm9tIFNvbC5OZXQgOik=";

        private const string ExpectedTransactionHashCreateInitializeAndMintTo =
            "A056qhN8bf9baCZ6SwzUlM6ge4X19TzoKANpDjg9CUGQTvIOYu27MvTcscgGov0aMkuiM9N8g" +
            "1D2bMJSvYBpWwi2IP+9oPzCj4b0AWm6uLxLv+JrMwVB8gJBYf4JtXotWDY504QIm9IqEemgUK" +
            "vWkb+9dNatYsR3d9xcqxQ14mAEAq147oIAH+FQbHj2PhdP61KXqTN7T0EclKQMJLyhkqeyREF" +
            "10Ttg99bcwTuXMxfR5rstI/kg/0Cagr/Ua+SoAQMABAdHaauXIEuoP7DK7hf3ho8eB05SFYGg" +
            "2J2UN52qZbcXsk0+Jb2M++6vIpkqr8zv+aohVvbSqnzuJeRSoRYepWULT6cip03g/pgXJNLrh" +
            "xqTpZ3aHH1CxvB/iB89zlU8m8UAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVKU1" +
            "D4XciC1hSlVnJ4iilt3x6rq9CmBniISTL07vagBqfVFxksXFEhjMlMPUrxf1ja7gibof1E49v" +
            "ZigAAAAAG3fbh12Whk9nL4UbO63msHLSF7V9bN5E6jPWFfv8AqeD/Y3arpTMrvjv2uP0ZD3LV" +
            "kDTmRAfOpQ603IYXOGjCBgMCAAI0AAAAAGBNFgAAAAAAUgAAAAAAAAAG3fbh12Whk9nL4UbO6" +
            "3msHLSF7V9bN5E6jPWFfv8AqQYCAgVDAAJHaauXIEuoP7DK7hf3ho8eB05SFYGg2J2UN52qZb" +
            "cXsgFHaauXIEuoP7DK7hf3ho8eB05SFYGg2J2UN52qZbcXsgMCAAE0AAAAAPAdHwAAAAAApQA" +
            "AAAAAAAAG3fbh12Whk9nL4UbO63msHLSF7V9bN5E6jPWFfv8AqQYEAQIABQEBBgMCAQAJB6hh" +
            "AAAAAAAABAEBEkhlbGxvIGZyb20gU29sLk5ldA==";

        private const string Nonce = "2S1kjspXLPs6jpNVXQfNMqZzzSrKLbGdr9Fxap5h1DLN";

        private static readonly byte[] CompiledMessageBytes =
        {
            1, 0, 2, 5, 71, 105, 171, 151, 32, 75, 168, 63, 176, 202, 238, 23, 247, 134, 143, 30, 7, 78, 82, 21,
            129, 160, 216, 157, 148, 55, 157, 170, 101, 183, 23, 178, 132, 220, 206, 171, 228, 52, 112, 149, 218,
            174, 194, 90, 142, 185, 112, 195, 57, 102, 90, 129, 121, 155, 30, 112, 20, 223, 14, 67, 131, 142, 36,
            162, 223, 244, 229, 56, 86, 243, 0, 74, 86, 58, 56, 142, 17, 130, 113, 147, 61, 1, 136, 126, 243, 22,
            226, 173, 108, 74, 212, 104, 81, 199, 120, 180, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 167, 213, 23, 25, 44, 86, 142, 224, 138, 132, 95, 115, 210,
            151, 136, 207, 3, 92, 49, 69, 178, 26, 179, 68, 216, 6, 46, 169, 64, 0, 0, 21, 68, 15, 82, 0, 49, 0,
            146, 241, 176, 13, 84, 249, 55, 39, 9, 212, 80, 57, 8, 193, 89, 211, 49, 162, 144, 45, 140, 117, 21, 46,
            83, 2, 3, 3, 2, 4, 0, 4, 4, 0, 0, 0, 3, 2, 0, 1, 12, 2, 0, 0, 0, 0, 202, 154, 59, 0, 0, 0, 0
        };
        

        private static readonly byte[] PartialMintTransaction =
        {
            3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 216, 198, 122,
            80, 175, 126, 116, 30, 75, 168, 187, 152, 16, 64, 179, 59, 224, 144, 151, 36, 13, 14, 133, 233, 5, 42,
            138, 93, 244, 114, 229, 38, 153, 36, 32, 215, 4, 156, 111, 156, 79, 10, 195, 169, 206, 14, 224, 61, 161,
            203, 221, 84, 147, 156, 111, 105, 39, 221, 254, 232, 16, 80, 244, 8, 24, 64, 247, 58, 100, 155, 188,
            246, 211, 245, 31, 223, 197, 38, 83, 76, 134, 88, 154, 28, 231, 252, 137, 93, 161, 123, 28, 58, 97, 194,
            60, 3, 229, 21, 171, 34, 4, 12, 23, 250, 52, 113, 38, 144, 219, 72, 173, 77, 83, 21, 135, 13, 231, 246,
            33, 225, 50, 23, 124, 93, 71, 73, 98, 9, 3, 0, 9, 15, 99, 83, 83, 192, 154, 229, 156, 233, 167, 140, 68,
            210, 223, 30, 190, 172, 183, 186, 71, 55, 181, 160, 248, 28, 102, 222, 141, 177, 113, 95, 226, 177, 94,
            105, 164, 46, 221, 109, 208, 199, 6, 167, 45, 242, 202, 40, 66, 144, 122, 22, 144, 46, 136, 209, 118,
            203, 170, 28, 65, 50, 187, 152, 208, 6, 114, 150, 224, 19, 224, 91, 98, 71, 211, 125, 184, 39, 237, 187,
            89, 211, 131, 139, 236, 160, 38, 19, 23, 191, 138, 216, 163, 222, 206, 226, 125, 249, 62, 35, 165, 232,
            110, 189, 68, 127, 226, 59, 11, 229, 71, 195, 119, 235, 119, 252, 109, 55, 161, 231, 196, 223, 130, 212,
            224, 123, 239, 202, 59, 120, 67, 81, 88, 75, 45, 13, 25, 18, 206, 13, 89, 13, 50, 66, 102, 5, 91, 35,
            89, 213, 83, 54, 240, 162, 243, 106, 16, 158, 193, 210, 136, 105, 184, 202, 172, 97, 221, 73, 177, 194,
            188, 120, 1, 42, 26, 91, 35, 113, 67, 35, 104, 207, 128, 224, 58, 78, 219, 32, 77, 222, 179, 250, 163,
            212, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6,
            101, 224, 60, 225, 5, 248, 160, 138, 36, 138, 124, 82, 53, 94, 245, 92, 14, 203, 220, 204, 20, 223, 11,
            86, 140, 188, 183, 165, 156, 193, 254, 6, 167, 213, 23, 25, 44, 92, 81, 33, 140, 201, 76, 61, 74, 241,
            127, 88, 218, 238, 8, 155, 161, 253, 68, 227, 219, 217, 138, 0, 0, 0, 0, 6, 221, 246, 225, 215, 101,
            161, 147, 217, 203, 225, 70, 206, 235, 121, 172, 28, 180, 133, 237, 95, 91, 55, 145, 58, 140, 245, 133,
            126, 255, 0, 169, 11, 112, 101, 177, 227, 209, 124, 69, 56, 157, 82, 127, 107, 4, 195, 205, 88, 184,
            108, 115, 26, 160, 253, 181, 73, 182, 209, 188, 3, 248, 41, 70, 78, 105, 0, 186, 223, 37, 238, 123, 248,
            244, 63, 132, 69, 96, 196, 53, 31, 39, 4, 139, 46, 43, 55, 219, 161, 131, 129, 158, 238, 134, 110, 241,
            140, 151, 37, 143, 78, 36, 137, 241, 187, 61, 16, 41, 20, 142, 13, 131, 11, 90, 19, 153, 218, 255, 16,
            132, 4, 142, 123, 216, 219, 233, 248, 89, 240, 43, 132, 95, 16, 240, 210, 26, 231, 103, 230, 214, 56,
            146, 38, 165, 229, 206, 185, 132, 108, 107, 5, 133, 203, 109, 140, 121, 238, 114, 140, 31, 245, 64, 164,
            50, 8, 172, 56, 194, 185, 38, 126, 211, 210, 189, 76, 5, 240, 248, 249, 25, 92, 67, 158, 215, 74, 23,
            149, 137, 86, 54, 134, 235, 92, 235, 31, 36, 159, 123, 208, 120, 64, 10, 68, 155, 82, 180, 203, 125,
            162, 78, 40, 129, 114, 14, 70, 28, 144, 71, 207, 219, 243, 142, 123, 179, 7, 6, 2, 0, 1, 52, 0, 0, 0, 0,
            96, 77, 22, 0, 0, 0, 0, 0, 82, 0, 0, 0, 0, 0, 0, 0, 6, 221, 246, 225, 215, 101, 161, 147, 217, 203, 225,
            70, 206, 235, 121, 172, 28, 180, 133, 237, 95, 91, 55, 145, 58, 140, 245, 133, 126, 255, 0, 169, 9, 2,
            1, 8, 67, 0, 0, 114, 150, 224, 19, 224, 91, 98, 71, 211, 125, 184, 39, 237, 187, 89, 211, 131, 139, 236,
            160, 38, 19, 23, 191, 138, 216, 163, 222, 206, 226, 125, 249, 1, 114, 150, 224, 19, 224, 91, 98, 71,
            211, 125, 184, 39, 237, 187, 89, 211, 131, 139, 236, 160, 38, 19, 23, 191, 138, 216, 163, 222, 206, 226,
            125, 249, 12, 7, 0, 4, 0, 1, 6, 9, 8, 0, 9, 3, 1, 4, 2, 9, 7, 1, 0, 0, 0, 0, 0, 0, 0, 10, 7, 5, 1, 2, 0,
            2, 6, 8, 133, 1, 33, 11, 0, 0, 0, 84, 114, 97, 115, 104, 32, 73, 116, 101, 109, 115, 5, 0, 0, 0, 84, 82,
            65, 83, 72, 59, 0, 0, 0, 104, 116, 116, 112, 115, 58, 47, 47, 109, 101, 116, 97, 46, 103, 97, 114, 98,
            108, 101, 115, 46, 102, 117, 110, 47, 105, 116, 101, 109, 47, 51, 110, 67, 57, 112, 101, 50, 89, 115,
            84, 75, 80, 106, 72, 81, 77, 51, 106, 115, 69, 65, 67, 118, 116, 89, 87, 55, 86, 99, 44, 1, 1, 1, 0, 0,
            0, 114, 150, 224, 19, 224, 91, 98, 71, 211, 125, 184, 39, 237, 187, 89, 211, 131, 139, 236, 160, 38, 19,
            23, 191, 138, 216, 163, 222, 206, 226, 125, 249, 1, 100, 0, 0, 1, 0, 10, 9, 3, 1, 2, 2, 0, 5, 9, 6, 8,
            10, 17, 1, 0, 0, 0, 0, 0, 0, 0, 0, 10, 8, 5, 2, 0, 2, 11, 14, 7, 13, 1, 25};
        
        #endregion
        
        [TestMethod]
        public void TestTransactionBuilderBuild()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);
            var fromAccount = wallet.GetAccount(0);
            var toAccount = wallet.GetAccount(1);
            var tx = new TransactionBuilder()
                .SetRecentBlockHash(Blockhash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.Transfer(fromAccount, toAccount.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(fromAccount, "Hello from Sol.Net :)"))
                .Build(fromAccount);

            Assert.AreEqual(ExpectedTransactionHashWithTransferAndMemo, Convert.ToBase64String(tx));
        }

        [TestMethod]
        public void TestTransactionDeserializeSerialize()
        {
            var transaction = Transaction.Deserialize(PartialMintTransaction);
            CollectionAssert.AreEqual(transaction.Serialize(), PartialMintTransaction);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestTransactionBuilderBuildNullBlockhashException()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);
            var fromAccount = wallet.GetAccount(0);
            var toAccount = wallet.GetAccount(1);
            _ = new TransactionBuilder().SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.Transfer(fromAccount, toAccount.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(fromAccount, "Hello from Sol.Net :)"))
                .Build(fromAccount);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestTransactionBuilderBuildNullFeePayerException()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);
            var fromAccount = wallet.GetAccount(0);
            var toAccount = wallet.GetAccount(1);
            _ = new TransactionBuilder()
                .SetRecentBlockHash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(fromAccount, toAccount.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(fromAccount, "Hello from Sol.Net :)"))
                .Build(fromAccount);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestTransactionBuilderBuildEmptySignersException()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);
            var fromAccount = wallet.GetAccount(0);
            var toAccount = wallet.GetAccount(1);
            _ = new TransactionBuilder()
                .SetRecentBlockHash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(fromAccount, toAccount.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(fromAccount, "Hello from Sol.Net :)"))
                .Build(new List<Account>());
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestTransactionBuilderBuildNullInstructionsException()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);
            var fromAccount = wallet.GetAccount(0);
            _ = new TransactionBuilder().SetRecentBlockHash(Blockhash)
                .Build(fromAccount);
        }

        [TestMethod]
        public void TestTransactionBuilderEmptyPrivateKey()
        {
            Account account = new Account(string.Empty, new PublicKey("2S1kjspXLPs6jpNVXQfNMqZzzSrKLbGdr9Fxap5h1DLN"));
            byte[] tx = new TransactionBuilder()
                .SetRecentBlockHash(Blockhash)
                .SetFeePayer(account)
                .AddInstruction(SystemProgram.Transfer(account, account.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(account, "Hello from Sol.Net :)"))
                .Build(account);
            Transaction transaction = Transaction.Deserialize(tx);
            Assert.IsTrue(transaction.Signatures.Count == 1);
            CollectionAssert.AreEqual(transaction.Signatures[0].Signature, new byte[64]);
        }

        [TestMethod]
        public void CreateInitializeAndMintToTest()
        {
            var wallet = new Wallet.Wallet(MnemonicWords);

            var blockHash = "G9JC6E7LfG6ayxARq5zDV5RdDr6P8NJEdzTUJ8ttrSKs";
            var minBalanceForAccount = 2039280UL;
            var minBalanceForMintAccount = 1461600UL;

            var mintAccount = wallet.GetAccount(17);
            var ownerAccount = wallet.GetAccount(10);
            var initialAccount = wallet.GetAccount(18);

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(ownerAccount)
                .AddInstruction(
                    SystemProgram.CreateAccount(
                    ownerAccount,
                    mintAccount,
                    minBalanceForMintAccount,
                    TokenProgram.MintAccountDataSize,
                    TokenProgram.ProgramIdKey))
                .AddInstruction(
                    TokenProgram.InitializeMint(
                    mintAccount.PublicKey,
                    2,
                    ownerAccount.PublicKey,
                    ownerAccount.PublicKey))
                .AddInstruction(
                    SystemProgram.CreateAccount(
                    ownerAccount,
                    initialAccount,
                    minBalanceForAccount,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey))
                .AddInstruction(
                    TokenProgram.InitializeAccount(
                    initialAccount.PublicKey,
                    mintAccount.PublicKey,
                    ownerAccount.PublicKey))
                .AddInstruction(
                    TokenProgram.MintTo(
                    mintAccount.PublicKey,
                    initialAccount.PublicKey,
                    25000,
                    ownerAccount))
                .AddInstruction(MemoProgram.NewMemo(initialAccount, "Hello from Sol.Net"))
                .Build(new List<Account> { ownerAccount, mintAccount, initialAccount });

            var tx2 = Transaction.Deserialize(tx);
            var msg = tx2.CompileMessage();

            Assert.IsTrue(tx2.Signatures[0].PublicKey.Verify(msg, tx2.Signatures[0].Signature));

            Assert.AreEqual(ExpectedTransactionHashCreateInitializeAndMintTo, Convert.ToBase64String(tx));
        }

        [TestMethod]
        public void CompileMessageTest()
        {
            Wallet.Wallet wallet = new(MnemonicWords);

            Account ownerAccount = wallet.GetAccount(10);
            Account nonceAccount = wallet.GetAccount(1119);
            Account toAccount = wallet.GetAccount(1);
            NonceInformation nonceInfo = new()
            {
                Nonce = Nonce,
                Instruction = SystemProgram.AdvanceNonceAccount(
                    nonceAccount.PublicKey,
                    ownerAccount
                )
            };

            byte[] txBytes = new TransactionBuilder()
                .SetFeePayer(ownerAccount)
                .SetNonceInformation(nonceInfo)
                .AddInstruction(
                    SystemProgram.Transfer(
                        ownerAccount,
                        toAccount,
                        1_000_000_000)
                )
                .CompileMessage();

            CollectionAssert.AreEqual(CompiledMessageBytes, txBytes);
        }


        [TestMethod]
        public void TestTransactionInstructionTest()
        {
            Wallet.Wallet wallet = new(MnemonicWords);

            Account ownerAccount = wallet.GetAccount(10);
            var memo = MemoProgram.NewMemo(ownerAccount, "Hello");
            var created = TransactionInstructionFactory.Create(new PublicKey(memo.ProgramId), memo.Keys, memo.Data);

            Assert.AreEqual(Convert.ToBase64String(memo.ProgramId), Convert.ToBase64String(created.ProgramId));
            Assert.AreSame(memo.Keys, created.Keys);
            Assert.AreEqual(Convert.ToBase64String(memo.Data), Convert.ToBase64String(created.Data));

        }

        [TestMethod]
        public void TransactionBuilderAddSignatureTest()
        {
            Wallet.Wallet wallet = new(MnemonicWords);

            Account fromAccount = wallet.GetAccount(10);
            Account toAccount = wallet.GetAccount(8);

            TransactionBuilder txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(AddSignatureBlockHash)
                .SetFeePayer(fromAccount)
                .AddInstruction(SystemProgram.Transfer(fromAccount.PublicKey, toAccount.PublicKey, 10000000))
                .AddInstruction(MemoProgram.NewMemo(fromAccount.PublicKey, "Hello from Sol.Net :)"));

            byte[] msgBytes = txBuilder.CompileMessage();
            byte[] signature = fromAccount.Sign(msgBytes);

            Assert.AreEqual(AddSignatureSignature, Encoders.Base58.EncodeData(signature));

            byte[] tx = txBuilder.AddSignature(signature)
                .Serialize();

            Assert.AreEqual(AddSignatureTransaction, Convert.ToBase64String(tx));
        }
        
    }
}