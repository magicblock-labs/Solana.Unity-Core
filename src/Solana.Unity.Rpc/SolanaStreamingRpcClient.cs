using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Solana.Unity.Rpc.Converters;
using Solana.Unity.Rpc.Core;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Solana.Unity.Rpc
{
    /// <summary>
    /// Implementation of the Solana streaming RPC API abstraction client.
    /// </summary>
    [DebuggerDisplay("Cluster = {" + nameof(NodeAddress) + "}")]
    internal class SolanaStreamingRpcClient : StreamingRpcClient, IStreamingRpcClient
    {
        /// <summary>
        /// Message Id generator.
        /// </summary>
        private readonly IdGenerator _idGenerator = new();

        /// <summary>
        /// Maps the internal ids to the unconfirmed subscription state objects.
        /// </summary>
        private readonly Dictionary<int, SubscriptionState> _unconfirmedRequests = new();

        /// <summary>
        /// Maps the server ids to the confirmed subscription state objects.
        /// </summary>
        private readonly Dictionary<int, SubscriptionState> _confirmedSubscriptions = new();

        private Commitment _defaultCommitment = Commitment.Processed;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="url">The url of the server to connect to.</param>
        /// <param name="logger">The possible ILogger instance.</param>
        /// <param name="websocket">The possible IWebSocket instance.</param>
        /// <param name="clientWebSocket">The possible ClientWebSocket instance.</param>
        internal SolanaStreamingRpcClient(string url, object logger = null, IWebSocket websocket = default, ClientWebSocket clientWebSocket = default) : base(url, logger, websocket, clientWebSocket)
        {
            ConnectionStateChangedEvent += (_, state) =>
            {
                Debug.Log("ConnectionStateChangedEvent: " + state);
                Debug.Log(ClientSocket.CloseStatus);
                Debug.Log(ClientSocket.CloseStatusDescription);
                Debug.Log(_confirmedSubscriptions.Count);
                if (state == WebSocketState.Closed) TryReconnect();
            };
        }

        /// <summary>
        /// Try Reconnect to the server and reopening the confirmed subscription.
        /// </summary>
        private async void TryReconnect()
        {
            Debug.Log("Reconnecting...");
            ClientSocket.Dispose();
            var confirmedSubscriptions = CloneObject(_confirmedSubscriptions);
            _unconfirmedRequests.Clear();
            _confirmedSubscriptions.Clear();
            foreach (var sub in confirmedSubscriptions)
            {
                SubscriptionState subState = sub.Value;
                JsonRpcRequest req = subState.Request;
                subState.ChangeState(SubscriptionStatus.Unsubscribed);
                await Subscribe(subState, req).ConfigureAwait(false);
            }
        }

        /// <inheritdoc cref="IStreamingRpcClient.ConnectAsync"/>
        public void SetDefaultCommitment(Commitment commitment)
        {
            _defaultCommitment = commitment;
        }
        
        /// <summary>
        /// Return commitment if not None, otherwise return the default.
        /// </summary>
        /// <param name="commitment"></param>
        /// <returns></returns>
        private Commitment CommitmentOrDefault(Commitment commitment)
        {
            return commitment == Commitment.None ? _defaultCommitment : commitment;
        }

        /// <inheritdoc cref="StreamingRpcClient.CleanupSubscriptions"/>
        protected override void CleanupSubscriptions()
        {
            foreach (var sub in _confirmedSubscriptions)
            {
                sub.Value.ChangeState(SubscriptionStatus.Unsubscribed, "Connection terminated");
            }

            foreach (var sub in _unconfirmedRequests)
            {
                sub.Value.ChangeState(SubscriptionStatus.Unsubscribed, "Connection terminated");
            }
            _unconfirmedRequests.Clear();
            _confirmedSubscriptions.Clear();
        }


        /// <inheritdoc cref="StreamingRpcClient.HandleNewMessage(Memory{byte})"/>
        protected override void HandleNewMessage(Memory<byte> messagePayload)
        {
            bool parsedValue;
            
            var jToken = JToken.Parse(Encoding.UTF8.GetString(messagePayload.Span.ToArray()));
            
            if (_logger != null)
            {
                var str = jToken.ToString();
                Console.WriteLine($"[Received]{str}");
            }

            if (jToken["error"] != null)
            {
                HandleError(jToken);
            }else if(jToken["method"] != null && jToken["params"] != null && jToken["params"]["subscription"] != null)
            {
                var id = Convert.ToInt32(jToken["params"]["subscription"]);
                var method = jToken["method"].ToString();
                HandleDataMessage(jToken["params"], method, id);
            }else if (jToken["id"] != null && jToken["result"] != null 
                      && Boolean.TryParse(jToken["result"].ToString(), out parsedValue) && parsedValue)
            {
                RemoveSubscription(Convert.ToInt32(jToken["id"]), true);
            }
            else if (jToken["id"] != null && jToken["result"] != null )
            {
                ConfirmSubscription(Convert.ToInt32(jToken["id"]), Convert.ToInt32(jToken["result"]));
            }
        }

        /// <summary>
        /// Handles and finishes parsing the contents of an error message.
        /// </summary>
        /// <param name="jToken">The jtoken that read the message so far.</param>
        private void HandleError(JToken jToken)
        {
            JsonSerializerSettings opts = new() { MaxDepth = 64,  ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var err = jToken["error"].ToObject<ErrorContent>(JsonSerializer.Create(opts));

            var id = Convert.ToInt32(jToken["id"]);

            var sub = RemoveUnconfirmedSubscription(id);

            sub?.ChangeState(SubscriptionStatus.ErrorSubscribing, err.Message, err.Code.ToString());
        }


        #region SubscriptionMapHandling
        /// <summary>
        /// Removes an unconfirmed subscription.
        /// </summary>
        /// <param name="id">The subscription id.</param>
        /// <returns>Returns the subscription object if it was found.</returns>
        private SubscriptionState RemoveUnconfirmedSubscription(int id)
        {
            SubscriptionState sub;
            lock (this)
            {
                _unconfirmedRequests.TryGetValue(id, out sub);
                if (!_unconfirmedRequests.Remove(id))
                {
                    if (_logger != null)
                    {
                        Console.WriteLine($"No unconfirmed subscription found with ID:{id}");
                    }
                }
            }
            return sub;
        }

        /// <summary>
        /// Removes a given subscription object from the map and notifies the object of the unsubscription.
        /// </summary>
        /// <param name="id">The subscription id.</param>
        /// <param name="shouldNotify">Whether or not to notify that the subscription was removed.</param>
        private void RemoveSubscription(int id, bool shouldNotify)
        {
            SubscriptionState sub;
            lock (this)
            {
                _confirmedSubscriptions.TryGetValue(id, out sub);
                if (!_confirmedSubscriptions.Remove(id))
                {
                    if (_logger != null)
                    {
                        Console.WriteLine($"No subscription found with ID:{id}");
                    }
                }
            }
            if (shouldNotify)
            {
                sub?.ChangeState(SubscriptionStatus.Unsubscribed);
            }
        }

        /// <summary>
        /// Confirms a given subcription based on the internal subscription id and the newly received external id.
        /// Moves the subcription state object from the unconfirmed map to the confirmed map.
        /// </summary>
        /// <param name="internalId"></param>
        /// <param name="resultId"></param>
        private void ConfirmSubscription(int internalId, int resultId)
        {
            SubscriptionState sub;
            lock (this)
            {
                _unconfirmedRequests.TryGetValue(internalId, out sub);
                if (_unconfirmedRequests.Remove(internalId))
                {
                    sub.SubscriptionId = resultId;
                    _confirmedSubscriptions.Add(resultId, sub);
                }
            }

            sub?.ChangeState(SubscriptionStatus.Subscribed);
        }

        /// <summary>
        /// Adds a new subscription state object into the unconfirmed subscriptions map.
        /// </summary>
        /// <param name="subscription">The subcription to add.</param>
        /// <param name="internalId">The internally generated id of the subscription.</param>
        private void AddSubscription(SubscriptionState subscription, int internalId)
        {
            lock (this)
            {
                _unconfirmedRequests.Add(internalId, subscription);
            }
        }

        /// <summary>
        /// Safely retrieves a subscription state object from a given subscription id.
        /// </summary>
        /// <param name="subscriptionId">The subscription id.</param>
        /// <returns>The subscription state object.</returns>
        private SubscriptionState RetrieveSubscription(int subscriptionId)
        {
            lock (this)
            {
                return _confirmedSubscriptions[subscriptionId];
            }
        }
        #endregion
        /// <summary>
        /// Handles a notification message and finishes parsing the contents.
        /// </summary>
        /// <param name="jToken">The current JToken being used to parse the message.</param>
        /// <param name="method">The method parameter already parsed within the message.</param>
        /// <param name="subscriptionId">The subscriptionId for this message.</param>
        private void HandleDataMessage(JToken jToken, string method, int subscriptionId)
        {
            JsonSerializerSettings opts = new() { MaxDepth = 64,  ContractResolver = new CamelCasePropertyNamesContractResolver() };

            var sub = RetrieveSubscription(subscriptionId);

            object result = null;

            switch (method)
            {
                case "accountNotification":
                    {
                        if (sub.Channel == SubscriptionChannel.TokenAccount)
                        {
                            var tokenAccNotification = jToken.ToObject<JsonRpcStreamResponse<ResponseValue<TokenAccountInfo>>>(JsonSerializer.Create(opts));
                            result = tokenAccNotification.Result;
                        }
                        else
                        {
                            var accNotification = jToken.ToObject<JsonRpcStreamResponse<ResponseValue<AccountInfo>>>(JsonSerializer.Create(opts));
                            result = accNotification.Result;
                        }
                        break;
                    }
                case "logsNotification":
                    var logsNotification = jToken.ToObject<JsonRpcStreamResponse<ResponseValue<LogInfo>>>(JsonSerializer.Create(opts));
                    result = logsNotification.Result;
                    break;
                case "programNotification":
                    var programNotification = jToken.ToObject<JsonRpcStreamResponse<ResponseValue<AccountKeyPair>>>(JsonSerializer.Create(opts));
                    result = programNotification.Result; 
                    break;
                case "signatureNotification":
                    var signatureNotification = jToken.ToObject<JsonRpcStreamResponse<ResponseValue<ErrorResult>>>(JsonSerializer.Create(opts));
                    result = signatureNotification.Result;
                    RemoveSubscription(signatureNotification.Subscription, true);
                    break;
                case "slotNotification":
                    var slotNotification = jToken.ToObject<JsonRpcStreamResponse<SlotInfo>>(JsonSerializer.Create(opts));
                    result = slotNotification.Result;
                    break;
                case "rootNotification":
                    var rootNotification = jToken.ToObject<JsonRpcStreamResponse<int>>(JsonSerializer.Create(opts));
                    result = rootNotification.Result;
                    break;
            }

            sub.HandleData(result);
        }

        #region AccountInfo
        

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeAccountInfoAsync(string, Action{SubscriptionState, ResponseValue{AccountInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeAccountInfoAsync(string pubkey, Action<SubscriptionState, ResponseValue<AccountInfo>> callback, Commitment commitment = default)
        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { pubkey };
            var configParams = new Dictionary<string, object> { { "encoding", "base64" } };

            if (commitment != Commitment.Finalized)
            {
                configParams.Add("commitment", commitment);
            }

            parameters.Add(configParams);

            var sub = new SubscriptionState<ResponseValue<AccountInfo>>(this, SubscriptionChannel.Account, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "accountSubscribe", parameters);

            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeAccountInfo(string, Action{SubscriptionState, ResponseValue{AccountInfo}}, Commitment)"/>
        public SubscriptionState SubscribeAccountInfo(string pubkey, Action<SubscriptionState, ResponseValue<AccountInfo>> callback, Commitment commitment = default)
            => SubscribeAccountInfoAsync(pubkey, callback, commitment).Result;
        #endregion

        #region TokenAccount
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeTokenAccountAsync(string, Action{SubscriptionState, ResponseValue{TokenAccountInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeTokenAccountAsync(string pubkey, Action<SubscriptionState, ResponseValue<TokenAccountInfo>> callback, Commitment commitment = default)

        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { pubkey };
            var configParams = new Dictionary<string, object> { { "encoding", "jsonParsed" } };

            if (commitment != Commitment.Finalized)
            {
                configParams.Add("commitment", commitment);
            }

            parameters.Add(configParams);

            var sub = new SubscriptionState<ResponseValue<TokenAccountInfo>>(this, SubscriptionChannel.TokenAccount, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "accountSubscribe", parameters);

            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeTokenAccount(string, Action{SubscriptionState, ResponseValue{TokenAccountInfo}}, Commitment)"/>
        public SubscriptionState SubscribeTokenAccount(string pubkey, Action<SubscriptionState, ResponseValue<TokenAccountInfo>> callback, Commitment commitment = default)
            => SubscribeTokenAccountAsync(pubkey, callback, commitment).Result;
        #endregion

        #region Logs
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfoAsync(string, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeLogInfoAsync(string pubkey, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = default)
        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { new Dictionary<string, object> { { "mentions", new List<string> { pubkey } } } };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<LogInfo>>(this, SubscriptionChannel.Logs, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "logsSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfo(string, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public SubscriptionState SubscribeLogInfo(string pubkey, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = default)
            => SubscribeLogInfoAsync(pubkey, callback, commitment).Result;

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfoAsync(LogsSubscriptionType, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeLogInfoAsync(LogsSubscriptionType subscriptionType, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = default)
        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { subscriptionType };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<LogInfo>>(this, SubscriptionChannel.Logs, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "logsSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfo(LogsSubscriptionType, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public SubscriptionState SubscribeLogInfo(LogsSubscriptionType subscriptionType, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = default)
            => SubscribeLogInfoAsync(subscriptionType, callback, commitment).Result;
        #endregion

        #region Signature
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSignatureAsync(string, Action{SubscriptionState, ResponseValue{ErrorResult}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeSignatureAsync(string transactionSignature, Action<SubscriptionState, ResponseValue<ErrorResult>> callback, Commitment commitment = default)
        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { transactionSignature };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<ErrorResult>>(this, SubscriptionChannel.Signature, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "signatureSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSignature(string, Action{SubscriptionState, ResponseValue{ErrorResult}}, Commitment)"/>
        public SubscriptionState SubscribeSignature(string transactionSignature, Action<SubscriptionState, ResponseValue<ErrorResult>> callback, Commitment commitment = default)
            => SubscribeSignatureAsync(transactionSignature, callback, commitment).Result;
        #endregion

        #region Program
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeProgramAsync(string, Action{SubscriptionState, ResponseValue{AccountKeyPair}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeProgramAsync(string programPubkey, Action<SubscriptionState, ResponseValue<AccountKeyPair>> callback, Commitment commitment = default)
        {
            commitment = CommitmentOrDefault(commitment);
            var parameters = new List<object> { programPubkey };
            var configParams = new Dictionary<string, object> { { "encoding", "base64" } };

            if (commitment != Commitment.Finalized)
            {
                configParams.Add("commitment", commitment);
            }

            parameters.Add(configParams);

            var sub = new SubscriptionState<ResponseValue<AccountKeyPair>>(this, SubscriptionChannel.Program, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "programSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeProgram(string, Action{SubscriptionState, ResponseValue{AccountKeyPair}}, Commitment)"/>
        public SubscriptionState SubscribeProgram(string programPubkey, Action<SubscriptionState, ResponseValue<AccountKeyPair>> callback, Commitment commitment = default)
            => SubscribeProgramAsync(programPubkey, callback, commitment).Result;
        #endregion

        #region SlotInfo
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSlotInfoAsync(Action{SubscriptionState, SlotInfo})"/>
        public async Task<SubscriptionState> SubscribeSlotInfoAsync(Action<SubscriptionState, SlotInfo> callback)
        {
            var sub = new SubscriptionState<SlotInfo>(this, SubscriptionChannel.Slot, callback);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "slotSubscribe", null);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSlotInfo(Action{SubscriptionState, SlotInfo})"/>
        public SubscriptionState SubscribeSlotInfo(Action<SubscriptionState, SlotInfo> callback)
            => SubscribeSlotInfoAsync(callback).Result;
        #endregion

        #region Root
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeRootAsync(Action{SubscriptionState, int})"/>
        public async Task<SubscriptionState> SubscribeRootAsync(Action<SubscriptionState, int> callback)
        {
            var sub = new SubscriptionState<int>(this, SubscriptionChannel.Root, callback);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "rootSubscribe", null);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeRoot(Action{SubscriptionState, int})"/>
        public SubscriptionState SubscribeRoot(Action<SubscriptionState, int> callback)
            => SubscribeRootAsync(callback).Result;
        #endregion

        /// <summary>
        /// Internal subscribe function, finishes the serialization and sends the message payload.
        /// </summary>
        /// <param name="sub">The subscription state object.</param>
        /// <param name="msg">The message to be serialized and sent.</param>
        /// <returns>A task representing the state of the asynchronous operation-</returns>
        private async Task<SubscriptionState> Subscribe(SubscriptionState sub, JsonRpcRequest msg)
        {
            var opts = new JsonSerializerSettings()
            {
                Formatting = Formatting.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters =
                {
                    new EncodingConverter(),
                    new StringEnumConverter(new CamelCaseNamingStrategy())
                }
            };
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msg, opts));

            if (_logger != null)
            {
                var jsonString = Encoding.UTF8.GetString(json);
                Console.WriteLine($"{msg.Id} {msg.Method} [Sending]{jsonString}");
            }

            ReadOnlyMemory<byte> mem = new(json);

            try
            {
                await ClientSocket.SendAsync(mem, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                sub.SetRequest(msg);
                AddSubscription(sub, msg.Id);
            }
            catch (Exception e)
            {
                sub.ChangeState(SubscriptionStatus.ErrorSubscribing, e.Message);
                if (_logger != null)
                {
                    Console.WriteLine($"{msg.Id} {msg.Method} Unable to send message, {e.Message}");
                }
                
            }

            return sub;
        }

        private string GetUnsubscribeMethodName(SubscriptionChannel channel) => channel switch
        {
            SubscriptionChannel.Account => "accountUnsubscribe",
            SubscriptionChannel.Logs => "logsUnsubscribe",
            SubscriptionChannel.Program => "programUnsubscribe",
            SubscriptionChannel.Root => "rootUnsubscribe",
            SubscriptionChannel.Signature => "signatureUnsubscribe",
            SubscriptionChannel.Slot => "slotUnsubscribe",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "invalid message type")
        };

        /// <inheritdoc cref="IStreamingRpcClient.UnsubscribeAsync(SubscriptionState)"/>
        public async Task UnsubscribeAsync(SubscriptionState subscription)
        {
            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), GetUnsubscribeMethodName(subscription.Channel), new List<object> { subscription.SubscriptionId });

            await Subscribe(subscription, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.Unsubscribe(SubscriptionState)"/>
        public void Unsubscribe(SubscriptionState subscription) => UnsubscribeAsync(subscription).Wait();
        
        /// <summary>
        /// Clones a object via shallow copy
        /// </summary>
        /// <typeparam name="T">Object Type to Clone</typeparam>
        /// <param name="obj">Object to Clone</param>
        /// <returns>New Object reference</returns>
        public static T CloneObject<T>(T obj) where T : class
        {
            if (obj == null) return null;
            System.Reflection.MethodInfo inst = obj.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (inst != null)
                return (T)inst.Invoke(obj, null);
            return null;
        }
    }
}