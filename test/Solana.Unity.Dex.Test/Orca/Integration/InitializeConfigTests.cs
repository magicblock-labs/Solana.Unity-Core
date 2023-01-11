using NUnit.Framework;

using System.Threading.Tasks; 

using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Types;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Wallet;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture] //3 of 3 passing 
    public class InitializeConfigTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }


        [Test] 
        [Description("Successfully init a WhirlpoolsConfig account")]
        public static async Task SuccessfulInitConfig()
        {
            //prepare config and client 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);

            //call the remote method
            RequestResult<string> result = await ConfigTestUtils.InitializeConfigAsync(
                _context, 
                initConfigParams
            );

            //make sure successful
            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            //get config to verify 
            var configAccount = await ConfigTestUtils.GetConfig(_context, initConfigParams);
            
            //test successful configuration
            Assert.IsTrue(configAccount.WasSuccessful);
            Assert.IsTrue(configAccount.WasDeserializationSuccessful);
            Assert.That(configAccount.ParsedResult.CollectProtocolFeesAuthority, 
                Is.EqualTo(initConfigParams.CollectProtocolFeesAuthority)
            );
            Assert.That(
                configAccount.ParsedResult.FeeAuthority, 
                Is.EqualTo(initConfigParams.FeeAuthorityKeypair.PublicKey)
            );
            Assert.That(
                configAccount.ParsedResult.RewardEmissionsSuperAuthority,
                Is.EqualTo(initConfigParams.RewardEmissionsSuperAuthority)
            );
            Assert.That(
                configAccount.ParsedResult.DefaultProtocolFeeRate,
                Is.EqualTo(initConfigParams.DefaultProtocolFeeRate)
            );
        }

        [Test]  
        [Description("Fail on passing in already initialized whirlpool account")]
        public static async Task FailInitConfigAlreadyInitialized()
        {
            //prepare config and client 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);

            //call the remote method
            RequestResult<string> result1 = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);

            //make sure successful
            Assert.IsTrue(result1.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result1.Result, _defaultCommitment));

            //try to config a second time 
            RequestResult<string> result2 = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);
            
            //should have failed
            Assert.IsFalse(result2.WasSuccessful);
        }
        
        [Test] 
        [Description("Succeeds when funder is different than account paying for transaction fee")]
        public static async Task FunderDifferentFromTransactionFeePayer()
        {
            //prepare config and client 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);
            
            //generate keypair for different funder 
            var funderKeypair = new Account();
            initConfigParams.Accounts.Funder = funderKeypair;

            TransactionInstruction transferInstr = SystemProgram.Transfer(
                _context.WalletPubKey,
                initConfigParams.Accounts.Funder,
                SolConstants.ONE_SOL
            );

            //get some sol to that new account 
            var transferResult = await SolUtils.FundTestAccountAsync(_context, initConfigParams.Accounts.Funder);
            
            Assert.IsTrue(transferResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(transferResult.Result, _defaultCommitment));
            
            //call the remote method
            RequestResult<string> result = await ConfigTestUtils.InitializeConfigAsync(
                _context, initConfigParams, feePayer:funderKeypair);
            
            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            //get config to verify 
            var configAccount = await ConfigTestUtils.GetConfig(_context, initConfigParams);

            //test successful configuration
            Assert.IsTrue(configAccount.WasSuccessful);
            Assert.IsTrue(configAccount.WasDeserializationSuccessful);
            Assert.That(configAccount.ParsedResult.CollectProtocolFeesAuthority,
                Is.EqualTo(initConfigParams.CollectProtocolFeesAuthority)
            );
            Assert.That(
                configAccount.ParsedResult.FeeAuthority,
                Is.EqualTo(initConfigParams.FeeAuthorityKeypair.PublicKey)
            );
            Assert.That(
                configAccount.ParsedResult.RewardEmissionsSuperAuthority,
                Is.EqualTo(initConfigParams.RewardEmissionsSuperAuthority)
            );
            Assert.That(
                configAccount.ParsedResult.DefaultProtocolFeeRate,
                Is.EqualTo(initConfigParams.DefaultProtocolFeeRate)
            );
        }
    }
}