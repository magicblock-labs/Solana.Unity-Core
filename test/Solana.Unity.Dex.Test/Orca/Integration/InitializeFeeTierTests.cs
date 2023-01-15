using NUnit.Framework;

using System.Threading.Tasks; 

using Solana.Unity.Wallet;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    //3 of 3 PASSED
    [TestFixture]
    public static class InitializeFeeTierTests
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
        [Description("successfully init a FeeRate stable account")]
        public static async Task SuccessfulInitFeeRateStable()
        {
            //init config 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);
            var initConfigResult = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);

            Assert.IsTrue(initConfigResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initConfigResult.Result, _defaultCommitment));

            //generate parameters
            var initFeeTierParams = FeeTierTestUtils.GenerateParams(
                _context, 
                initConfigParams, 
                defaultFeeRate: 800
            );
            
            //init fee tier 
            var initResult = await FeeTierTestUtils.InitializeFeeTierAsync(
                _context,
                initFeeTierParams
            );
            
            Assert.IsTrue(initResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initResult.Result, _defaultCommitment));

            //get the new fee tier address
            var generatedPda = PdaUtils.GetFeeTier(
                _context.ProgramId,
                initConfigParams.Accounts.Config, 
                initFeeTierParams.TickSpacing
            );

            //get the new fee tier 
            var getResult = await _context.WhirlpoolClient.GetFeeTierAsync(
                generatedPda.PublicKey, 
                _defaultCommitment); 
            
            //asserts 
            Assert.IsTrue(getResult.WasSuccessful);
            
            var feeTierAccount = getResult.ParsedResult;
            Assert.That(feeTierAccount.TickSpacing, Is.EqualTo(initFeeTierParams.TickSpacing));
            Assert.That(feeTierAccount.DefaultFeeRate, Is.EqualTo(initFeeTierParams.DefaultFeeRate));
        }

        [Test] 
        [Description("successfully init a FeeRate standard account")]
        public static async Task SuccessfulInitFeeRateStandard()
        {
            //init config 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);
            var initConfigResult = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);

            Assert.IsTrue(initConfigResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initConfigResult.Result, _defaultCommitment));
            
            //generate params 
            var initFeeTierParams = FeeTierTestUtils.GenerateParams(
                _context, 
                initConfigParams, 
                defaultFeeRate: 3000
            );
            
            //init fee tier 
            var initResult = await FeeTierTestUtils.InitializeFeeTierAsync(
                _context,
                initFeeTierParams
            );

            Assert.IsTrue(initResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initResult.Result, _defaultCommitment));


            //get the new fee tier 
            var getResult = await _context.WhirlpoolClient.GetFeeTierAsync(
                initFeeTierParams.FeeTierPda.PublicKey,
                _defaultCommitment);

            //asserts 
            Assert.IsTrue(getResult.WasSuccessful);

            var feeTierAccount = getResult.ParsedResult;
            Assert.That(feeTierAccount.TickSpacing, Is.EqualTo(initFeeTierParams.TickSpacing));
            Assert.That(feeTierAccount.DefaultFeeRate, Is.EqualTo(initFeeTierParams.DefaultFeeRate));
        }
        
        [Test] 
        [Description("successfully init a FeeRate with another funder wallet")]
        public static async Task SuccessfulInitFeeRateWithFunderWallet()
        {
            //init config 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);
            var initConfigResult = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);

            Assert.IsTrue(initConfigResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initConfigResult.Result, _defaultCommitment));

            //get params for init fee tier
            Account funderAccount = new();
            
            //do transfer of SOL to funder
            await SolUtils.FundTestAccountAsync(_context, funderAccount.PublicKey);

            //generate params 
            var initFeeTierParams = FeeTierTestUtils.GenerateParams(
                _context,
                initConfigParams,
                defaultFeeRate: 3000,
                funder: funderAccount
            );
            
            var initResult = await FeeTierTestUtils.InitializeFeeTierAsync(
                _context,
                initFeeTierParams,
                feePayer: funderAccount
            );

            Assert.IsTrue(initResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initResult.Result, _defaultCommitment));
        }
    }
}