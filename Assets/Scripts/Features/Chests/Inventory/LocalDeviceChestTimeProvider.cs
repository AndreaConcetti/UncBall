using System;
using UnityEngine;

public class LocalDeviceChestTimeProvider : ChestTimeProviderBase
{
    public override long GetUnixTimeSeconds()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (logDebug)
        {
            Debug.Log(
                "[LocalDeviceChestTimeProvider] Returning device UTC unix time: " + now,
                this
            );
        }

        return now;
    }

    public override bool IsUsingAuthoritativeServerTime()
    {
        return false;
    }

    public override string GetProviderDebugName()
    {
        return "Local_Device_UtcNow";
    }
}