using System;
using System.Reflection;
using UnityEngine;
using WebSocketSharp;

namespace Solana.Unity.Rpc.Utilities;

/// <summary>
/// A class that contains the information about the runtime environment.
/// </summary>
public static class RuntimePlatforms
{
    private const string WebGLPlayer = "WebGLPlayer";
    private const string Android = "Android";

    /// <summary>
    /// Return True if running on Unity, False otherwise
    /// </summary>
    /// <returns>Return True if running on Unity, False otherwise</returns>
    public static bool IsUnityPlayer()
    {
        try
        {
            if (Type.GetType("UnityEngine.GameObject, UnityEngine") != null)
            {
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }
    
    /// <summary>
    /// Return True if running on Unity, False otherwise
    /// </summary>
    /// <returns>Return True if running on Unity, False otherwise</returns>
    public static bool IsWebGL()
    {
        if (!IsUnityPlayer())
        {
            return false;
        }
        return RuntimeUtils.GetRuntimePlatform().Equals(WebGLPlayer);
    }
    
    /// <summary>
    /// Return True if running on Unity, False otherwise
    /// </summary>
    /// <returns>Return True if running on Unity, False otherwise</returns>
    public static bool IsAndroid()
    {
        if (!IsUnityPlayer())
        {
            return false;
        }
        return RuntimeUtils.GetRuntimePlatform().Equals(Android);
    }

    /// <summary>
    /// Return True if running on Mono, False otherwise
    /// </summary>
    /// <returns></returns>
    public static bool IsMono()
    {
        if (!IsUnityPlayer()) return false;
        Type type = Type.GetType("Mono.Runtime");
        if (type == null) return false;
        MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
        if (displayName == null) return false;
        var monoVersion = displayName.Invoke(null, null);
        Debug.Log(monoVersion?.ToString() );
        return monoVersion?.ToString() != null;
    }
}

[UnityEngine.Scripting.Preserve]
class RuntimeUtils
{
    [UnityEngine.Scripting.Preserve]
    public static string GetRuntimePlatform()
    {
        return Application.platform.ToString();
    }
}