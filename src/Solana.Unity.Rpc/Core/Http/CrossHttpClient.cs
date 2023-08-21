using Solana.Unity.Rpc.Utilities;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Solana.Unity.Rpc.Core.Http;

/// <summary>
/// Provide a convenient way to make HTTP requests inside and outside Unity.
/// </summary>
public static class CrossHttpClient
{
    private static TaskCompletionSource<UnityWebRequest.Result> _currentRequestTask;

    /// <summary>
    /// Send an async request using HttpClient or UnityWebRequest if running on Unity
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="httpReq"></param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> SendAsyncRequest(HttpClient httpClient, HttpRequestMessage httpReq)
    {
        if (RuntimePlatforms.IsWebGL() || (RuntimePlatforms.IsAndroid() && RuntimePlatforms.IsMono()))
        {
            return await SendUnityWebRequest(httpClient.BaseAddress != null ? 
                httpClient.BaseAddress : httpReq.RequestUri, httpReq);
        }
        return await httpClient.SendAsync(httpReq).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Convert a httReq to a Unity Web request
    /// </summary>
    /// <param name="uri">RPC URI</param> 
    /// <param name="httpReq">The http request</param>
    /// <returns>Http response</returns>
    /// <exception cref="HttpRequestException"></exception>
    private static async Task<HttpResponseMessage> SendUnityWebRequest(Uri uri, HttpRequestMessage httpReq)
    {
        HttpResponseMessage response = new();
        try
        {
            using UnityWebRequest request = new(uri, httpReq.Method.ToString());
            if (httpReq.Content != null)
            {
                request.uploadHandler = new UploadHandlerRaw(await httpReq.Content.ReadAsByteArrayAsync());
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            if (_currentRequestTask != null)
            {
                await _currentRequestTask.Task;
            }
            UnityWebRequest.Result result = await SendRequest(request);
        
            if (result == UnityWebRequest.Result.Success)
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.downloadHandler.text));
            }
            else
            {
                response.StatusCode = HttpStatusCode.ExpectationFailed;
            }
        }
        catch (Exception e)
        {
            response.Content = new StringContent("Error: " + e.Message);
            response.StatusCode = HttpStatusCode.ExpectationFailed;
            _currentRequestTask?.TrySetException(e);
            _currentRequestTask = null;
        }
        return response;
    }

    private static Task<UnityWebRequest.Result> SendRequest(UnityWebRequest request)
    {
        TaskCompletionSource<UnityWebRequest.Result> sendRequestTask = new();
        try
        {
            _currentRequestTask = sendRequestTask;
            UnityWebRequestAsyncOperation op = request.SendWebRequest();

            if (request.isDone)
            {
                sendRequestTask.SetResult(request.result);
            }
            else
            {
                op.completed += asyncOp =>
                {
                    if (op.webRequest.error != null || request.error != null)
                    {
                        sendRequestTask.SetException(
                            new HttpRequestException(op.webRequest.error == null ? op.webRequest.error : request.error));
                    }
                    else
                    {
                        sendRequestTask.SetResult(((UnityWebRequestAsyncOperation)asyncOp).webRequest.result);
                    }
                };
            }
        }
        catch (Exception ex)
        {
            sendRequestTask.TrySetException(ex);
            _currentRequestTask.SetException(ex);
            _currentRequestTask = null;
        }
        return sendRequestTask.Task;
    }
}