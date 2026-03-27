using System;

[Serializable]
public class BallSkinData
{
    public string skinUniqueId;

    public string baseColorId;
    public string patternId;
    public string patternColorId;

    public float patternIntensity = 1f;
    public float patternScale = 1f;

    public SkinRarity rarity = SkinRarity.Common;
}