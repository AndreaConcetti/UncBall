using System;
using System.Collections.Generic;

[Serializable]
public class PlayerChestSlotInventorySaveData
{
    public int saveVersion = 2;
    public string profileId = "";
    public long lastLocalSaveUnixTimeSeconds = 0;

    public ChestSlotSaveData[] slots;
    public List<ChestRuntimeData> queuedChests = new List<ChestRuntimeData>();
}