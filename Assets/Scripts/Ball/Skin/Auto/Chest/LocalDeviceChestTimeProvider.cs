using System;
using UnityEngine;

public class LocalDeviceChestTimeProvider : ChestTimeProviderBase
{
    public override long GetUnixTimeSeconds()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (logDebug)
            Debug.Log("[LocalDeviceChestTimeProvider] UnixTimeSeconds=" + now, this);

        return now;
    }

    public override bool IsUsingAuthoritativeServerTime()
    {
        return false;
    }
}