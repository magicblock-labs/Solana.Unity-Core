using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WebSocket = NativeWebSocket.WebSocket;
using WebSocketState = System.Net.WebSockets.WebSocketState;

namespace Solana.Unity.Rpc.Core.Sockets
{
    internal class WebSocketWrapper : IWebSocket
    {
        private NativeWebSocket.IWebSocket webSocket;

        public WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;

        public string CloseStatusDescription => "Not implemented";
        
        private readonly ConcurrentQueue<byte[]> _mMessageQueue = new();
        private TaskCompletionSource<Tuple<byte[], WebSocketReceiveResult>> _receiveMessageTask;
        private TaskCompletionSource<bool> _webSocketConnectionTask = new();

        public WebSocketState State
        {
            get
            {
                if(webSocket == null)
                    return WebSocketState.None;
                return webSocket.State switch
                {
                    NativeWebSocket.WebSocketState.Open => WebSocketState.Open,
                    NativeWebSocket.WebSocketState.Closed => WebSocketState.Closed,
                    NativeWebSocket.WebSocketState.Connecting => WebSocketState.Connecting,
                    NativeWebSocket.WebSocketState.Closing => WebSocketState.CloseReceived,
                    _ => WebSocketState.None
                };
            }
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription,
            CancellationToken cancellationToken)
        {
            return webSocket.Close();
        }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            webSocket = WebSocket.Create(uri.AbsoluteUri);
            webSocket.OnOpen += () =>
            {
                _webSocketConnectionTask.TrySetResult(true);
                webSocket.OnMessage += MessageReceived;
                ConnectionStateChangedEvent?.Invoke(this, State);
            };
            webSocket.OnClose += _ =>
            {
                webSocket.OnMessage -= MessageReceived;
                ConnectionStateChangedEvent?.Invoke(this, State);
            };
            return webSocket.Connect();
        }

        private void MessageReceived(byte[] message)
        {
            OnMessage?.Invoke(message);
            _mMessageQueue.Enqueue(message);
            DispatchMessage(message);
        }

        public Task CloseAsync(CancellationToken cancellationToken)
            => webSocket.Close();
        
        private void DispatchMessage(byte[] message)
        {
            if(_receiveMessageTask == null) return;
            WebSocketReceiveResult webSocketReceiveResult = new(message.Length, WebSocketMessageType.Text, true);
            Tuple<byte[], WebSocketReceiveResult> messageTuple = new(message, webSocketReceiveResult);
            _receiveMessageTask.TrySetResult(messageTuple);
            _receiveMessageTask = null;
        }

        public event IWebSocket.WebSocketMessageEventHandler OnMessage;
        public event EventHandler<WebSocketState> ConnectionStateChangedEvent;

        public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (webSocket.State == NativeWebSocket.WebSocketState.Connecting)
            {
                return _webSocketConnectionTask.Task.ContinueWith(_ =>
                {
                    if (webSocket.State != NativeWebSocket.WebSocketState.Open)
                    {
                        throw new WebSocketException(WebSocketError.InvalidState, "WebSocket is not connected.");
                    }
                    return webSocket.Send(buffer.ToArray());
                }, cancellationToken).Unwrap();
            }
            if (webSocket.State != NativeWebSocket.WebSocketState.Open)
            {
                throw new WebSocketException(WebSocketError.InvalidState, "WebSocket is not connected.");
            }
            return webSocket.Send(buffer.ToArray());
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webSocket.Close();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}