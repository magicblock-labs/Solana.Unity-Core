using System;
using UnityEngine;

namespace Solana.Unity.Rpc.Utilities;

/// <summary>
/// A class that contains the information about the runtime environment.
/// </summary>
public static class RuntimePlatforms
{
    private const string WebGLPlayer = "WebGLPlayer";

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