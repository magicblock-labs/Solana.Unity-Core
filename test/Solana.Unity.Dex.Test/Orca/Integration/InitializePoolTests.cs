using NUnit.Framework;

using System.Threading.Tasks; 

using Solana.Unity.Wallet;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration
{
    [TestFixture]
    public class InitializePoolTests
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
        [Description("successfully init a Standard account")]
        public static async Task SuccessfulInitStandardAccount()
        {
            bool tokenAIsNative = false;

            //init config 
            InitializeConfigParams initConfigParams = ConfigTestUtils.GenerateParams(_context);
            var initConfigResult = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams);
            
            Assert.IsTrue(initConfigResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initConfigResult.Result, _defaultCommitment));

            //init fee tier 
            var initFeeTierParams = FeeTierTestUtils.GenerateParams(
                _context,
                initConfigParams,
                tickSpacing: TickSpacing.Standard
            );
            var initFeeTierResult = await FeeTierTestUtils.InitializeFeeTierAsync(
                _context,
                initFeeTierParams
            );

            Assert.IsTrue(initFeeTierResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initFeeTierResult.Result, _defaultCommitment));

            //create some mints 
            var tokenMints = await MintUtils.CreateInOrderMints(_context, _context.WalletAccount, tokenAIsNative);
            PublicKey tokenAMintAddr = tokenMints.Item1;
            PublicKey tokenBMintAddr = tokenMints.Item2;

            //generate params 
            InitializePoolParams initPoolParams = PoolTestUtils.GenerateParams(
                _context,
                tokenAMintAddr,
                tokenBMintAddr,
                initConfigParams,
                initFeeTierParams,
                tokenAIsNative: tokenAIsNative
            );

            RequestResult<string> result = await PoolTestUtils.InitializePoolAsync(_context, initPoolParams);

            Assert.IsTrue(result.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(result.Result, _defaultCommitment));

            Pda expectedWhirlpoolPda = PdaUtils.GetWhirlpool(
                _context.ProgramId,
                initConfigParams.Accounts.Config,
                tokenAMintAddr,
                tokenBMintAddr,
                TickSpacing.Standard
            );

            Whirlpool whirlpool = (await _context.WhirlpoolClient.GetWhirlpoolAsync(
                initPoolParams.WhirlpoolPda.PublicKey
            )).ParsedResult;

            Assert.That(initPoolParams.WhirlpoolPda.PublicKey, Is.EqualTo(expectedWhirlpoolPda.PublicKey));
            
            Assert.That(whirlpool.WhirlpoolBump[0], Is.EqualTo(expectedWhirlpoolPda.Bump));

            Assert.That(whirlpool.WhirlpoolsConfig, Is.EqualTo(initPoolParams.Accounts.WhirlpoolsConfig));
            Assert.That(whirlpool.TokenMintA, Is.EqualTo(tokenAMintAddr));
            Assert.That(whirlpool.TokenMintB, Is.EqualTo(tokenBMintAddr));
            Assert.That(whirlpool.TokenVaultA, Is.EqualTo(initPoolParams.TokenVaultAKeyPair.PublicKey));
            Assert.That(whirlpool.TokenVaultB, Is.EqualTo(initPoolParams.TokenVaultBKeyPair.PublicKey));
            Assert.That(whirlpool.FeeRate, Is.EqualTo(initFeeTierParams.DefaultFeeRate));
            Assert.That(whirlpool.ProtocolFeeRate, Is.EqualTo(initConfigParams.DefaultProtocolFeeRate));
        }
    }
}