using System;

namespace Solana.Unity.Rpc.Utilities;

/// <summary>
/// A class that contains the information about the runtime environment.
/// </summary>
public static class RuntimePlatform
{
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
}