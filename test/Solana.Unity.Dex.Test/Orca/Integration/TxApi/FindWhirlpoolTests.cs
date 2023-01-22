using NUnit.Framework;
using Orca;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Programs.Models;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Math;
using Solana.Unity.Dex.Orca.SolanaApi;
using Solana.Unity.Dex.Orca.Ticks;
using Solana.Unity.Dex.Orca.Quotes.Swap;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Test.Orca.Params;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Dex.Ticks;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class FindWhirlpoolTests
    {
        private static TestWhirlpoolContext _context;
        private static List<WhirlpoolMiniData> _testWhirlpools; 
        private static PublicKey _whirlpoolConfigAddress; 
        private static List<Tuple<PublicKey, PublicKey>> _tokenPairs;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static async Task Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _testWhirlpools = await CreateABunchOfPools(3);
            _tokenPairs = _testWhirlpools.Select(wp =>
                Tuple.Create(wp.TokenMintA, wp.TokenMintB)
            ).ToList();
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private class WhirlpoolMiniData 
        {
            public PublicKey WhirlpoolAddress; 
            public PublicKey TokenMintA; 
            public PublicKey TokenMintB; 
        }
        
        private static async Task<List<WhirlpoolMiniData>> CreateABunchOfPools(int numPools = 3) 
        {
            var initConfigParams = ConfigTestUtils.GenerateParams(_context); 
            var feeTierParams = FeeTierTestUtils.GenerateParams(_context, initConfigParams);
            
            var initResult = await ConfigTestUtils.InitializeConfigAsync(_context, initConfigParams); 
            Assert.IsTrue(initResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initResult.Result, _defaultCommitment));
            
            var initFreeTier = await FeeTierTestUtils.InitializeFeeTierAsync(_context, feeTierParams);
            Assert.IsTrue(initFreeTier.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(initFreeTier.Result, _defaultCommitment));
            
            _whirlpoolConfigAddress = initConfigParams.Accounts.Config;

            var initPromises = new List<Task<PoolInitResult>>();
            for (int n=0; n< numPools; n++) 
            {
                initPromises.Add(
                    PoolTestUtils.BuildPoolWithTokens(
                        _context, initConfigParams, skipInitConfig: true, initFeeTierParams: feeTierParams
                    )
                );
            }
            
            await Task.WhenAll(initPromises);
            
            return initPromises.Select(p => new WhirlpoolMiniData
            {
                WhirlpoolAddress = PdaUtils.GetWhirlpool(
                    _context.ProgramId, 
                    _whirlpoolConfigAddress,
                    p.Result.InitPoolParams.Accounts.TokenMintA,
                    p.Result.InitPoolParams.Accounts.TokenMintB, 
                    128
                ).PublicKey, 
                TokenMintA = p.Result.InitPoolParams.Accounts.TokenMintA,
                TokenMintB = p.Result.InitPoolParams.Accounts.TokenMintB
            }).ToList();
        }
        
        [Test]
        [Description("finds whirlpool with tokens specified in correct order, with correct tickspacing")]
        public static async Task FindWhirlpoolsCorrectOrderCorrectTickSpacing()
        {
            OrcaDex dex = new OrcaDex(_context);

            var (address, wp) = await dex.FindWhirlpool(
                _tokenPairs[0].Item1,
                _tokenPairs[0].Item2,
                TickSpacing.Standard,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNotNull(wp);
            Assert.That(wp.TokenMintA, Is.EqualTo(_tokenPairs[0].Item1));
            Assert.That(wp.TokenMintB, Is.EqualTo(_tokenPairs[0].Item2));
            Assert.That(wp.TickSpacing, Is.EqualTo(TickSpacing.Standard));
            Assert.That(wp.Address, Is.EqualTo(address));
        }
        
        [Test]
        [Description("finds whirlpool with tokens specified in correct order, with correct tickspacing")]
        public static async Task FindWhirlpoolsAddressCorrectOrderCorrectTickSpacing()
        {
            IDex dex = new OrcaDex(_context);

            var address = await dex.FindWhirlpoolAddress(
                _tokenPairs[0].Item1,
                _tokenPairs[0].Item2,
                TickSpacing.Standard,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNotNull(address);
        }

        [Test]
        [Description("finds whirlpool with tokens specified in correct order, with wrong tickspacing")]
        public static async Task FindWhirlpoolsCorrectOrderWrongTickSpacing()
        {
            OrcaDex dex = new(_context);
            
            var (address, wp) = await dex.FindWhirlpool(
                _tokenPairs[0].Item1,
                _tokenPairs[0].Item2,
                TickSpacing.Stable,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNotNull(wp);
            Assert.That(wp.TokenMintA, Is.EqualTo(_tokenPairs[0].Item1));
            Assert.That(wp.TokenMintB, Is.EqualTo(_tokenPairs[0].Item2));
            Assert.That(wp.TickSpacing, Is.EqualTo(TickSpacing.Standard));
            Assert.That(wp.Address, Is.EqualTo(address));
        }

        [Test]
        [Description("finds whirlpool with tokens specified in the wrong order, with correct tickspacing")]
        public static async Task FindWhirlpoolsWrongOrderCorrectTickSpacing()
        {
            OrcaDex dex = new (_context);

            var (address, wp) = await dex.FindWhirlpool(
                _tokenPairs[0].Item2,
                _tokenPairs[0].Item1,
                TickSpacing.Standard,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNotNull(wp);
            Assert.That(wp.TokenMintA, Is.EqualTo(_tokenPairs[0].Item1));
            Assert.That(wp.TokenMintB, Is.EqualTo(_tokenPairs[0].Item2));
            Assert.That(wp.TickSpacing, Is.EqualTo(TickSpacing.Standard));
            Assert.That(wp.Address, Is.EqualTo(address));
        }

        [Test]
        [Description("finds whirlpool with tokens specified in wrong order, with wrong tickspacing")]
        public static async Task FindWhirlpoolsWrongOrderWrongTickSpacing()
        {
            OrcaDex dex = new(_context);

            var (address, wp) = await dex.FindWhirlpool(
                _tokenPairs[0].Item2,
                _tokenPairs[0].Item1,
                TickSpacing.Stable,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNotNull(wp);
            Assert.That(wp.TokenMintA, Is.EqualTo(_tokenPairs[0].Item1));
            Assert.That(wp.TokenMintB, Is.EqualTo(_tokenPairs[0].Item2));
            Assert.That(wp.TickSpacing, Is.EqualTo(TickSpacing.Standard));
            Assert.That(wp.Address, Is.EqualTo(address));
        }

        [Test]
        [Description("fails to find whirlpool when one of the mints is incorrect")]
        public static async Task FindWhirlpoolsWrongMint()
        {
            OrcaDex dex = new(_context);
            
            PublicKey fakeToken = new Account().PublicKey;

            var (address, wp) = await dex.FindWhirlpool(
                fakeToken,
                _tokenPairs[0].Item2,
                TickSpacing.Standard,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNull(wp);

            (address, wp) = await dex.FindWhirlpool(
                _tokenPairs[0].Item1,
                fakeToken,
                TickSpacing.Standard,
                _whirlpoolConfigAddress,
                TestConfiguration.DefaultCommitment
            );
            Assert.IsNull(wp);
        }
    }
}