using NUnit.Framework;
using Orca;
using System.Numerics;
using System.Threading.Tasks;

using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;

using Solana.Unity.Dex.Orca;
using Solana.Unity.Dex.Orca.Core.Accounts;
using Solana.Unity.Dex.Orca.Address;
using Solana.Unity.Dex.Test.Orca.Utils;
using Solana.Unity.Rpc.Types;

namespace Solana.Unity.Dex.Test.Orca.Integration.TxApi
{
    [TestFixture]
    public class OpenPositionWithLiquidityTxApiTests
    {
        private static TestWhirlpoolContext _context;
        private static Commitment _defaultCommitment;

        [SetUp]
        public static void Setup()
        {
            _context = ContextFactory.CreateTestWhirlpoolContext(TestConfiguration.SolanaEnvironment);
            _defaultCommitment = _context.WhirlpoolClient.DefaultCommitment;
        }

        private static async Task<PublicKey> InitializeTestPool()
        {
            PoolInitResult poolInitResult = await PoolTestUtils.BuildPool(_context);
            Assert.IsTrue(poolInitResult.WasSuccessful);
            return poolInitResult.InitPoolParams.WhirlpoolPda;
        }

        private static async Task<IWhirlpoolContext> InitializeContext(Account walletAccount)
        {
            //fund the account 
            await SolUtils.FundTestAccountAsync(_context, walletAccount);

            return new WhirlpoolContext(
                _context.ProgramId,
                _context.RpcClient,
                _context.StreamingRpcClient,
                walletAccount.PublicKey
            );
        }

        [Test]
        [Description("all things for open position w/metadata in one transaction")]
        public static async Task OpenPositionWithMetadataSingleTransaction()
        {
            //create new account to be swapper, and a new context 
            Account walletAccount = new();

            //initialize everything 
            PublicKey whirlpoolAddr = await InitializeTestPool();
            IWhirlpoolContext newContext = await InitializeContext(walletAccount);
            IDex dex = new OrcaDex(walletAccount, newContext.RpcClient );

            int tickLowerIndex = 0;
            int tickUpperIndex = 128;

            //position mint account 
            Account positionMintAccount = new();
            Pda positionPda = PdaUtils.GetPosition(_context.ProgramId, positionMintAccount.PublicKey);

            //get the transaction to open the position 
            Transaction tx = await dex.OpenPosition(
                whirlpoolAddr,
                positionMintAccount,
                tickLowerIndex,
                tickUpperIndex,
                withMetadata: true,
                commitment: TestConfiguration.DefaultCommitment
            );
        
            //sign and execute the transaction 
            tx.Sign(walletAccount);
            tx.Sign(positionMintAccount);
            var openResult = await newContext.RpcClient.SendTransactionAsync(
                tx.Serialize(),
                commitment: TestConfiguration.DefaultCommitment
            );

            Assert.IsTrue(openResult.WasSuccessful);
            Assert.IsTrue(await _context.RpcClient.ConfirmTransaction(openResult.Result, _defaultCommitment));


            //retrieve the position
            var positionResult =
                await _context.WhirlpoolClient.GetPositionAsync(positionPda.PublicKey,
                _defaultCommitment
            );

            //asserts 
            Assert.IsTrue(positionResult.WasSuccessful);
            Position position = positionResult.ParsedResult;

            Assert.That(position.TickLowerIndex, Is.EqualTo(tickLowerIndex));
            Assert.That(position.TickUpperIndex, Is.EqualTo(tickUpperIndex));
            Assert.That(position.Whirlpool, Is.EqualTo(whirlpoolAddr));
            Assert.That(position.PositionMint, Is.EqualTo(positionMintAccount.PublicKey));
            Assert.That(position.Liquidity, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointA, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeGrowthCheckpointB, Is.EqualTo(BigInteger.Zero));
            Assert.That(position.FeeOwedA, Is.EqualTo(0));
            Assert.That(position.FeeOwedB, Is.EqualTo(0));

            Pda metadataPda = PdaUtils.GetPositionMetadata(positionMintAccount.PublicKey);
            Assert.IsNotNull(metadataPda); 
            
            var metadataResult = await _context.RpcClient.GetAccountInfoAsync(metadataPda.PublicKey, _defaultCommitment); 
            Assert.IsTrue(metadataResult.WasSuccessful);
            MetadataParser metadata = new(metadataResult.Result.Value.Data);

            Assert.That(metadata.UpdateAuthority, Is.EqualTo(AddressConstants.METADATA_UPDATE_AUTH_ID));
            Assert.IsTrue(metadata.Uri.Contains("https://arweave.net/"));
            Assert.That(metadata.Mint, Is.EqualTo(position.PositionMint.ToString()));
        }
    }
}