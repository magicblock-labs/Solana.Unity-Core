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
    /// <summary>
    /// Send an async request using HttpClient or UnityWebRequest if running on Unity
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="httpReq"></param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> SendAsyncRequest(HttpClient httpClient, HttpRequestMessage httpReq)
    {
        if (RuntimePlatform.IsUnityPlayer())
        {
            return await SendUnityWebRequest(httpClient.BaseAddress != null ? 
                httpClient.BaseAddress: httpReq.RequestUri, httpReq);
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