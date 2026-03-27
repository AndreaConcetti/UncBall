using System;

[Serializable]
public class ChestRuntimeData
{
    public string chestInstanceId;
    public ChestType chestType;

    public string ownerProfileId;

    public long awardedUnixTimeSeconds;
    public long slotAssignedUnixTimeSeconds;
    public long unlockEndUnixTimeSeconds;

    public bool createdFromServer;
    public bool pendingServerClaim;
    public long lastServerSyncUnixTimeSeconds;

    public ChestRuntimeData()
    {
    }

    public ChestRuntimeData(ChestType chestType, long awardedUnixTimeSeconds, string ownerProfileId = "")
    {
        this.chestType = chestType;
        this.awardedUnixTimeSeconds = awardedUnixTimeSeconds;
        this.ownerProfileId = ownerProfileId;
        this.chestInstanceId = Guid.NewGuid().ToString("N");
        this.slotAssignedUnixTimeSeconds = 0;
        this.unlockEndUnixTimeSeconds = 0;
        this.createdFromServer = false;
        this.pendingServerClaim = false;
        this.lastServerSyncUnixTimeSeconds = 0;
    }
}