using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSkinInventorySaveData
{
    public int saveVersion = 2;
    public string profileId = "";
    public long lastLocalSaveUnixTimeSeconds = 0;

    public List<BallSkinData> unlockedSkins = new List<BallSkinData>();
    public string equippedSkinId = "";
}