using System;

[Serializable]
public class ChestRuntimeData
{
    public string chestInstanceId;
    public ChestType chestType;

    public long awardedUnixTimeSeconds;
    public long slotAssignedUnixTimeSeconds;
    public long unlockEndUnixTimeSeconds;

    public ChestRuntimeData()
    {
    }

    public ChestRuntimeData(ChestType chestType, long awardedUnixTimeSeconds)
    {
        this.chestType = chestType;
        this.awardedUnixTimeSeconds = awardedUnixTimeSeconds;
        this.chestInstanceId = Guid.NewGuid().ToString("N");
        this.slotAssignedUnixTimeSeconds = 0;
        this.unlockEndUnixTimeSeconds = 0;
    }
}