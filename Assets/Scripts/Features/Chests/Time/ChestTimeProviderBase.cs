using UnityEngine;

public abstract class ChestTimeProviderBase : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] protected bool logDebug = false;

    public abstract long GetUnixTimeSeconds();

    public virtual bool IsUsingAuthoritativeServerTime()
    {
        return false;
    }

    public virtual string GetProviderDebugName()
    {
        return GetType().Name;
    }
}