using System;
using System.Collections.Generic;

[Serializable]
public class PlayerChestSlotInventorySaveData
{
    public ChestSlotSaveData[] slots;
    public List<ChestRuntimeData> queuedChests = new List<ChestRuntimeData>();
}