using Solana.Unity.Rpc.Utilities;
using System;
using System.Collections;
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
        if (RuntimePlatforms.IsWebGL())
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
        using (var request = new UnityWebRequest(uri, httpReq.Method.ToString()))
        {
            if (httpReq.Content != null)
            {
                request.uploadHandler = new UploadHandlerRaw(await httpReq.Content.ReadAsByteArrayAsync());
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            var response = new HttpResponseMessage();
            var e = SendRequest(request, response);
            while (e.MoveNext()) { }
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                throw new HttpRequestException("Error While Sending: " + request.error);
            }
            return response;
        }
    }

    /// <summary>
    /// Send a request using UnityWebRequest and wait for the response
    /// </summary>
    /// <param name="request"></param>
    /// <param name="res"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    private static IEnumerator SendRequest(UnityWebRequest request, HttpResponseMessage res)
    {
        yield return request.SendWebRequest();
        while (!request.isDone)
            yield return true;
        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            throw new HttpRequestException("Error While Sending: " + request.error);
        }
        res.StatusCode = HttpStatusCode.OK;
        res.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.downloadHandler.text));
    }
}