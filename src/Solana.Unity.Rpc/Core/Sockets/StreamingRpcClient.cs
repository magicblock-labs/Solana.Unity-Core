using Solana.Unity.Rpc.Types;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Solana.Unity.Rpc.Core.Sockets
{
    /// <summary>
    /// Base streaming Rpc client class that abstracts the websocket handling.
    /// </summary>
    internal abstract class StreamingRpcClient : IDisposable
    {
        private SemaphoreSlim _sem;

        /// <summary>
        /// The web socket client abstraction.
        /// </summary>
        protected IWebSocket ClientSocket;

        private bool disposedValue;

        /// <summary>
        /// The logger instance.
        /// </summary>
        protected readonly object _logger;

        /// <inheritdoc cref="IStreamingRpcClient.NodeAddress"/>
        public Uri NodeAddress { get; }

        /// <inheritdoc cref="IStreamingRpcClient.State"/>
        public WebSocketState State => ClientSocket.State;

        /// <inheritdoc cref="IStreamingRpcClient.ConnectionStateChangedEvent"/>
        public event EventHandler<WebSocketState> ConnectionStateChangedEvent;

        private ConnectionStats _connectionStats;

        /// <summary>
        /// Statistics of the current connection.
        /// </summary>
        public IConnectionStatistics Statistics => _connectionStats;

        /// <summary>
        /// The internal constructor that setups the client.
        /// </summary>
        /// <param name="url">The url of the streaming RPC server.</param>
        /// <param name="logger">The possible logger instance.</param>
        /// <param name="socket">The possible websocket instance. A new instance will be created if null.</param>
        /// <param name="clientWebSocket">The possible ClientWebSocket instance. A new instance will be created if null.</param>
        protected StreamingRpcClient(string url, object logger, IWebSocket socket = default, ClientWebSocket clientWebSocket = default)
        {
            NodeAddress = new Uri(url);
            ClientSocket = socket ?? new WebSocketWrapper();
            _logger = logger;
            _sem = new SemaphoreSlim(1, 1);
            _connectionStats = new ConnectionStats();
            ClientSocket.ConnectionStateChangedEvent += (sender, state) => ConnectionStateChangedEvent?.Invoke(sender, state);
        }
        
        /// <summary>
        /// Constructor that setups the client with a IWebSocket instance.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="socket"></param>
        protected StreamingRpcClient(string url, IWebSocket socket)
        {
            NodeAddress = new Uri(url);
            ClientSocket = socket ?? new WebSocketWrapper();
            _logger = null;
            _sem = new SemaphoreSlim(1, 1);
            _connectionStats = new ConnectionStats();
            ClientSocket.ConnectionStateChangedEvent += (sender, state) => ConnectionStateChangedEvent?.Invoke(sender, state);
        }

        /// <summary>
        /// Initializes the websocket connection and starts receiving messages asynchronously.
        /// </summary>
        /// <returns>Returns the task representing the asynchronous task.</returns>
        public async Task ConnectAsync()
        {
            if (ClientSocket.State == WebSocketState.Open) return;
            await _sem.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ClientSocket.State != WebSocketState.Open)
                {
                    await ClientSocket.ConnectAsync(NodeAddress, CancellationToken.None);
                    ClientSocket.OnMessage += DispatchMessage;
                    ConnectionStateChangedEvent?.Invoke(this, State);
                }
            }
            finally
            {
                _sem.Release();
            }
        }

        private void DispatchMessage(byte[] message)
        {
            HandleNewMessage(new Memory<byte>(message));
            _connectionStats.AddReceived((uint)message.Length);
            if (ClientSocket.State != WebSocketState.Open && ClientSocket.State != WebSocketState.Connecting)
            {
                ConnectionStateChangedEvent?.Invoke(this, State);
            }
        }

        /// <inheritdoc cref="IStreamingRpcClient.DisconnectAsync"/>
        public async Task DisconnectAsync()
        {
            if (ClientSocket.State == WebSocketState.Closed) return;
            await _sem.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ClientSocket.State == WebSocketState.Open)
                {
                    await ClientSocket.CloseAsync(CancellationToken.None);

                    //notify at the end of StartListening loop, given that it should end as soon as we terminate connection here
                    //and will also notify when there is a non-user triggered disconnection event

                    // handle disconnection cleanup
                    ClientSocket.OnMessage -= DispatchMessage;
                    ClientSocket.Dispose();
                    ClientSocket = new WebSocketWrapper();
                    CleanupSubscriptions();
                }
            }
            finally
            {
                _sem.Release();
            }
        }

        /// <summary>
        /// Handless a new message payload.
        /// </summary>
        /// <param name="messagePayload">The message payload.</param>
        protected abstract void HandleNewMessage(Memory<byte> messagePayload);

        /// <summary>
        /// Clean up subscription objects after disconnection.
        /// </summary>
        protected abstract void CleanupSubscriptions();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ClientSocket.Dispose();
                    _sem.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~StreamingRpcClient()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}