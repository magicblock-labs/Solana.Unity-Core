using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Utilities;
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Solana.Unity.Rpc.Converters;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Solana.Unity.Rpc.Core.Http
{
    /// <summary>
    /// Base Rpc client class that abstracts the HttpClient handling.
    /// </summary>
    internal abstract class JsonRpcClient
    {
        
        /// <summary>
        /// The Json serializer options to be reused between calls.
        /// </summary>
        private readonly JsonSerializerSettings _serializerOptions;
        
        /// <summary>
        /// The HttpClient.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// The logger instance.
        /// </summary>
        private readonly object _logger;

        /// <summary>
        /// Rate limiting strategy
        /// </summary>
        private IRateLimiter _rateLimiter;

        /// <inheritdoc cref="IRpcClient.NodeAddress"/>
        public Uri NodeAddress { get; }

        /// <summary>
        /// The internal constructor that setups the client.
        /// </summary>
        /// <param name="url">The url of the RPC server.</param>
        /// <param name="logger">The possible logger instance.</param>
        /// <param name="httpClient">The possible HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null for no rate limiting.</param>
        protected JsonRpcClient(string url, object logger = default, HttpClient httpClient = default, IRateLimiter rateLimiter = null)
        {
            _logger = logger;
            NodeAddress = new Uri(url);
            _httpClient = httpClient ?? new HttpClient { BaseAddress = NodeAddress };
            _rateLimiter = rateLimiter;
            _serializerOptions = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters =
                {
                    new EncodingConverter(),
                    new StringEnumConverter(new CamelCaseNamingStrategy())
                }
            };
        }

        /// <summary>
        /// Sends a given message as a POST method and returns the deserialized message result based on the type parameter.
        /// </summary>
        /// <typeparam name="T">The type of the result to deserialize from json.</typeparam>
        /// <param name="req">The message request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        protected async Task<RequestResult<T>> SendRequest<T>(JsonRpcRequest req)
        {
            var requestJson = JsonConvert.SerializeObject(req, _serializerOptions);

            try
            {
                // pre-flight check with rate limiter if set
                _rateLimiter?.Fire(); 
                
                // logging
                if (_logger != null)
                {
                    Console.WriteLine($"{req.Id} {req.Method} Sending request: {requestJson}");
                }

                // create byte buffer to avoid charset=utf-8 in content-type header
                // as this is rejected by some RPC nodes
                var buffer = Encoding.UTF8.GetBytes(requestJson);
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, (string)null)
                {
                    Content = new ByteArrayContent(buffer)
                    {
                        Headers = {
                            { "Content-Type", "application/json"}
                        }
                    }
                };

                // execute POST
                using (var response = await SendAsyncRequest(_httpClient, httpReq))
                {
                    var result = await HandleResult<T>(req, response).ConfigureAwait(false);
                    result.RawRpcRequest = requestJson;
                    return result;
                }
            }
            catch (HttpRequestException e)
            {
                var result = new RequestResult<T>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestJson;
                if (_logger != null)
                {
                    Console.WriteLine($"{req.Id} {req.Method} Caught exception: {e.Message}");
                }
                return result;
            }
            catch (Exception e)
            {
                var result = new RequestResult<T>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestJson;
                if (_logger != null)
                {
                    Console.WriteLine($"{req.Id} {req.Method} Caught exception: {e.Message}");
                }
                return result;
            }
            
        }


        /// <summary>
        /// Handles the result after sending a request.
        /// </summary>
        /// <typeparam name="T">The type of the result to deserialize from json.</typeparam>
        /// <param name="req">The original message request.</param>
        /// <param name="response">The response obtained from the request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        private async Task<RequestResult<T>> HandleResult<T>(JsonRpcRequest req, HttpResponseMessage response)
        {
            RequestResult<T> result = new(response);
            try
            {
                result.RawRpcResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (_logger != null)
                {
                    Console.WriteLine($"{req.Id} {req.Method} Result: {result.RawRpcResponse}");
                }
                var res = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(result.RawRpcResponse, _serializerOptions);

                
                if (res.Result != null)
                {
                    result.Result = res.Result;
                    result.WasRequestSuccessfullyHandled = true;
                }
                else
                {
                    var errorRes = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(result.RawRpcResponse, _serializerOptions);
                    if (errorRes is { Error: { } })
                    {
                        result.Reason = errorRes.Error.Message;
                        result.ServerErrorCode = errorRes.Error.Code;
                        result.ErrorData = errorRes.Error.Data;
                    }
                    else if(errorRes is { ErrorMessage: { } })
                    {
                        result.Reason = errorRes.ErrorMessage;
                    }
                    else
                    {
                        result.Reason = "Something wrong happened.";
                    }
                }
            }
            catch (JsonException e)
            {
                if (_logger != null)
                {
                    Console.WriteLine($"{req.Id} {req.Method} Caught exception: {e.Message}");
                }
                result.WasRequestSuccessfullyHandled = false;
                result.Reason = "Unable to parse json.";
            }

            return result;
        }

        /// <summary>
        /// Sends a batch of messages as a POST method and returns a collection of responses.
        /// </summary>
        /// <param name="reqs">The message request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        public async Task<RequestResult<JsonRpcBatchResponse>> SendBatchRequestAsync(JsonRpcBatchRequest reqs)
        {
            if (reqs == null) throw new ArgumentNullException(nameof(reqs));
            if (reqs.Count == 0) throw new ArgumentException("Empty batch");
            var id_for_log = reqs.Min(x => x.Id);
            var requestsJson = JsonConvert.SerializeObject(reqs, _serializerOptions);
            try
            {
                // pre-flight check with rate limiter if set
                _rateLimiter?.Fire(); 
                
                if (_logger != null)
                {
                    Console.WriteLine($"{id_for_log} [batch of {reqs.Count}] Sending request: {requestsJson}");
                }

                // create byte buffer to avoid charset=utf-8 in content-type header
                // as this is rejected by some RPC nodes
                var buffer = Encoding.UTF8.GetBytes(requestsJson);
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, (string)null)
                {
                    Content = new ByteArrayContent(buffer)
                    {
                        Headers = {
                            { "Content-Type", "application/json"}
                        }
                    }
                };

                // execute POST
                using (var response = await SendAsyncRequest(_httpClient, httpReq))
                {
                    var result = await HandleBatchResult(reqs, response).ConfigureAwait(false);
                    result.RawRpcRequest = requestsJson;
                    return result;
                }

            }
            catch (HttpRequestException e)
            {
                var result = new RequestResult<JsonRpcBatchResponse>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestsJson;
                if (_logger != null)
                {
                    Console.WriteLine($"{id_for_log} [batch of {reqs.Count}] Caught exception: {e.Message}");
                }
                return result;
            }
            catch (Exception e)
            {
                var result = new RequestResult<JsonRpcBatchResponse>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestsJson;
                if (_logger != null)
                {
                    Console.WriteLine($"{id_for_log} [batch of {reqs.Count}] Caught exception: {e.Message}");
                }
                return result;
            }

        }

        /// <summary>
        /// Handles the result after sending a batch of requests.
        /// Outcome could be a collection of failures due to a single API issue or a mixed bag of 
        /// success and failure depending on the individual request outcomes.
        /// </summary>
        /// <param name="reqs">The original batch of request messages.</param>
        /// <param name="response">The batch of responses obtained from the HTTP request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        private async Task<RequestResult<JsonRpcBatchResponse>> HandleBatchResult(JsonRpcBatchRequest reqs, HttpResponseMessage response)
        {
            var id_for_log = reqs.Min(x => x.Id);
            RequestResult<JsonRpcBatchResponse> result = new RequestResult<JsonRpcBatchResponse>(response);
            try
            {
                result.RawRpcResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (_logger != null)
                {
                    Console.WriteLine($"{id_for_log} [batch of {reqs.Count}] Result: {result.RawRpcResponse}");
                }
                var res = JsonConvert.DeserializeObject<JsonRpcBatchResponse>(result.RawRpcResponse, _serializerOptions);

                if (res != null)
                {
                    result.Result = res;
                    result.WasRequestSuccessfullyHandled = true;
                }
                else
                {
                    var errorRes = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(result.RawRpcResponse, _serializerOptions);
                    if (errorRes is { Error: { } })
                    {
                        result.Reason = errorRes.Error.Message;
                        result.ServerErrorCode = errorRes.Error.Code;
                        result.ErrorData = errorRes.Error.Data;
                    }
                    else if (errorRes is { ErrorMessage: { } })
                    {
                        result.Reason = errorRes.ErrorMessage;
                    }
                    else
                    {
                        result.Reason = "Something wrong happened.";
                    }
                }
            }
            catch (JsonException e)
            {
                if (_logger != null)
                {
                    Console.WriteLine($"{id_for_log} [batch of {reqs.Count}] Caught exception: {e.Message}");
                }
                result.WasRequestSuccessfullyHandled = false;
                result.Reason = "Unable to parse json.";
            }

            return result;
        }
        
        /// <summary>
        /// Return True if running on Unity, False otherwise
        /// </summary>
        /// <returns>Return True if running on Unity, False otherwise</returns>
        private bool IsUnityPlayer()
        {
            #if NETSTANDARD2_0 && !DEBUG
            try
            {
                if (Application.platform != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            #endif
            return false;
        }
        
        /// <summary>
        /// Send an async request using HttpClient or UnityWebRequest if running on Unity
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="httpReq"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> SendAsyncRequest(HttpClient httpClient, HttpRequestMessage httpReq)
        {
            if (IsUnityPlayer())
            {
                return await SendUnityWebRequest(httpClient.BaseAddress, httpReq);
            }
            return await _httpClient.SendAsync(httpReq).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Convert a httReq to a Unity Web request
        /// </summary>
        /// <param name="uri">RPC URI</param> 
        /// <param name="httpReq">The http request</param>
        /// <returns>Http response</returns>
        /// <exception cref="HttpRequestException"></exception>
        private async Task<HttpResponseMessage> SendUnityWebRequest(Uri uri, HttpRequestMessage httpReq)
        {
            Byte[] buffer = await httpReq.Content.ReadAsByteArrayAsync();
            using (var request = new UnityWebRequest(uri, httpReq.Method.ToString()))
            {
                request.uploadHandler = new UploadHandlerRaw(buffer);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new HttpRequestException("Error While Sending: " + request.error);
                }
                while (!request.isDone)
                {
                    await Task.Yield();
                }
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.downloadHandler.text));
                return response;
            }
        }
    }

}