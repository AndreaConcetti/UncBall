using System;
using System.Collections.Generic;

[Serializable]
public class Player1SkinInventorySaveData
{
    public List<BallSkinData> unlockedSkins = new List<BallSkinData>();
    public string equippedSkinId;
}