using NativeWebSocket;
using Solana.Unity.Rpc.Utilities;
using System.Net.Http;
using System.Net.WebSockets;

namespace Solana.Unity.Rpc
{
    /// <summary>
    /// Implements a client factory for Solana RPC and Streaming RPC APIs.
    /// </summary>
    public static class ClientFactory
    {
        /// <summary>
        /// The local net cluster.
        /// </summary>
        private const string RpcLocalNet = "http://127.0.0.1:8899";
        /// <summary>
        /// The dev net cluster.
        /// </summary>
        private const string RpcDevNet = "https://api.devnet.solana.com";

        /// <summary>
        /// The test net cluster.
        /// </summary>
        private const string RpcTestNet = "https://api.testnet.solana.com";

        /// <summary>
        /// The main net cluster.
        /// </summary>
        private const string RpcMainNet = "https://api.mainnet-beta.solana.com";

        /// <summary>
        /// The localnet net cluster.
        /// </summary>
        private const string StreamingRpcLocalNet = "ws://127.0.0.1:8900";

        /// <summary>
        /// The dev net cluster.
        /// </summary>
        private const string StreamingRpcDevNet = "wss://api.devnet.solana.com";

        /// <summary>
        /// The test net cluster.
        /// </summary>
        private const string StreamingRpcTestNet = "wss://api.testnet.solana.com";

        /// <summary>
        /// The main net cluster.
        /// </summary>
        private const string StreamingRpcMainNet = "wss://api.mainnet-beta.solana.com";

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(Cluster cluster, object logger)
        {
            return GetClient(cluster, logger, null);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(Cluster cluster)
        {
            return GetClient(cluster, logger: null, rateLimiter: null);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(
            Cluster cluster,
            object logger = null,
            IRateLimiter rateLimiter = null)
        {
            return GetClient(cluster, logger, httpClient: null, rateLimiter: rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">A HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(Cluster cluster, object logger = null,
                HttpClient httpClient = null, IRateLimiter rateLimiter = null)
        {
            var url = cluster switch
            {
                Cluster.DevNet => RpcDevNet,
                Cluster.TestNet => RpcTestNet,
                Cluster.LocalNet => RpcLocalNet,
                _ => RpcMainNet,
            };
            
            return GetClient(url, logger, httpClient, rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url, object logger)
        {
            return GetClient(url, logger, null);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url, object logger, IRateLimiter rateLimiter)
        {
            return GetClient(url, logger, httpClient: null, rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">A HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url, object logger = null, HttpClient httpClient = null, IRateLimiter rateLimiter = null)
        {
            return new SolanaRpcClient(url, logger, httpClient, rateLimiter);
        }

        /// <summary>
        /// Instantiate a streaming client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The streaming client.</returns>
        public static IStreamingRpcClient GetStreamingClient(
            Cluster cluster,
            object logger = null)
        {
            var url = cluster switch
            {
                Cluster.DevNet => StreamingRpcDevNet,
                Cluster.TestNet => StreamingRpcTestNet,
                Cluster.LocalNet => StreamingRpcLocalNet,
                _ => StreamingRpcMainNet,
            };
            return GetStreamingClient(url, logger);
        }
        
        public static IStreamingRpcClient GetStreamingClient(
            Cluster cluster,
            IWebSocket socket)
        {
            var url = cluster switch
            {
                Cluster.DevNet => StreamingRpcDevNet,
                Cluster.TestNet => StreamingRpcTestNet,
                Cluster.LocalNet => StreamingRpcLocalNet,
                _ => StreamingRpcMainNet,
            };
            return GetStreamingClient(url, null, socket, null);
        }

        /// <summary>
        /// Instantiate a streaming client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="clientWebSocket">A ClientWebSocket instance. If null, a new instance will be created.</param>
        /// <returns>The streaming client.</returns>
        public static IStreamingRpcClient GetStreamingClient(string url, object logger = null, IWebSocket socket = null, ClientWebSocket clientWebSocket = null)
        {
            return new SolanaStreamingRpcClient(url, logger, null, clientWebSocket);
        }
    }
}