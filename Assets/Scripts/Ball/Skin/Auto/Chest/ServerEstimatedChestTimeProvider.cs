using System;
using UnityEngine;

public class ServerEstimatedChestTimeProvider : ChestTimeProviderBase
{
    [Header("Server Time State")]
    [SerializeField] private bool hasServerSync = false;
    [SerializeField] private long lastKnownServerUnixTime = 0;
    [SerializeField] private long lastKnownDeviceUnixTimeAtSync = 0;

    [Header("Fallback")]
    [SerializeField] private bool fallbackToDeviceTimeIfNoSync = true;

    public override long GetUnixTimeSeconds()
    {
        if (hasServerSync)
        {
            long currentDeviceUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long delta = currentDeviceUnix - lastKnownDeviceUnixTimeAtSync;
            long estimatedServerNow = lastKnownServerUnixTime + delta;

            if (logDebug)
            {
                Debug.Log(
                    "[ServerEstimatedChestTimeProvider] EstimatedServerNow=" + estimatedServerNow +
                    " | LastKnownServer=" + lastKnownServerUnixTime +
                    " | DeviceAtSync=" + lastKnownDeviceUnixTimeAtSync +
                    " | CurrentDevice=" + currentDeviceUnix,
                    this
                );
            }

            return estimatedServerNow;
        }

        if (fallbackToDeviceTimeIfNoSync)
        {
            long fallback = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (logDebug)
                Debug.Log("[ServerEstimatedChestTimeProvider] No sync available. Fallback device time=" + fallback, this);

            return fallback;
        }

        if (logDebug)
            Debug.LogWarning("[ServerEstimatedChestTimeProvider] No sync available and no fallback enabled. Returning 0.", this);

        return 0;
    }

    public override bool IsUsingAuthoritativeServerTime()
    {
        return hasServerSync;
    }

    public void ApplyServerTimeSync(long serverUnixTime)
    {
        hasServerSync = true;
        lastKnownServerUnixTime = serverUnixTime;
        lastKnownDeviceUnixTimeAtSync = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (logDebug)
        {
            Debug.Log(
                "[ServerEstimatedChestTimeProvider] Applied server sync. ServerUnix=" + serverUnixTime +
                " | DeviceUnixAtSync=" + lastKnownDeviceUnixTimeAtSync,
                this
            );
        }
    }

    public void ClearServerTimeSync()
    {
        hasServerSync = false;
        lastKnownServerUnixTime = 0;
        lastKnownDeviceUnixTimeAtSync = 0;

        if (logDebug)
            Debug.Log("[ServerEstimatedChestTimeProvider] Cleared server time sync.", this);
    }
}