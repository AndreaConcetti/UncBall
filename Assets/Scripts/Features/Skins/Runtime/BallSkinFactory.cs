using UnityEngine;

public static class BallSkinFactory
{
    public static BallSkinData CreateSkin(
        string baseColorId,
        string patternId,
        string patternColorId,
        float patternIntensity,
        float patternScale,
        SkinRarity rarity)
    {
        BallSkinData skin = new BallSkinData
        {
            baseColorId = SanitizeIdPart(baseColorId),
            patternId = SanitizeIdPart(patternId),
            patternColorId = SanitizeIdPart(patternColorId),
            patternIntensity = patternIntensity,
            patternScale = patternScale,
            rarity = rarity
        };

        skin.skinUniqueId = BuildSkinId(
            skin.baseColorId,
            skin.patternId,
            skin.patternColorId,
            skin.patternIntensity,
            skin.patternScale,
            skin.rarity
        );

        return skin;
    }

    public static string BuildSkinId(
        string baseColorId,
        string patternId,
        string patternColorId,
        float patternIntensity,
        float patternScale,
        SkinRarity rarity)
    {
        string safeBase = SanitizeIdPart(baseColorId);
        string safePattern = SanitizeIdPart(patternId);
        string safePatternColor = SanitizeIdPart(patternColorId);

        int intensityCode = Mathf.RoundToInt(patternIntensity * 100f);
        int scaleCode = Mathf.RoundToInt(patternScale * 100f);

        return $"{rarity.ToString().ToLowerInvariant()}_{safeBase}_{safePattern}_{safePatternColor}_i{intensityCode}_s{scaleCode}";
    }

    private static string SanitizeIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "undefined";

        value = value.Trim().ToLowerInvariant();
        value = value.Replace(" ", "_");
        value = value.Replace("-", "_");

        while (value.Contains("__"))
            value = value.Replace("__", "_");

        return value;
    }
}