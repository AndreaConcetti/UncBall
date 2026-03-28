using UnityEngine;

[CreateAssetMenu(fileName = "BallColorHarmonyProfile", menuName = "Uncball/Skins/Ball Color Harmony Profile")]
public class BallColorHarmonyProfile : ScriptableObject
{
    [Header("Hue Harmony Weights")]
    [Min(0f)] public float complementaryWeight = 1.35f;
    [Min(0f)] public float splitComplementaryWeight = 1.10f;
    [Min(0f)] public float analogousWeight = 0.70f;
    [Min(0f)] public float triadicWeight = 0.95f;
    [Min(0f)] public float neutralWeight = 0.55f;

    [Header("Contrast Weights")]
    [Min(0f)] public float valueContrastWeight = 0.90f;
    [Min(0f)] public float saturationContrastWeight = 0.40f;
    [Range(0f, 1f)] public float sameHuePenalty = 0.70f;
    [Range(0f, 1f)] public float tooSimilarPenalty = 0.45f;

    [Header("Rarity Multipliers - Base Common")]
    [Min(0f)] public float baseCommonToCommon = 0.90f;
    [Min(0f)] public float baseCommonToRare = 1.20f;
    [Min(0f)] public float baseCommonToEpic = 1.10f;
    [Min(0f)] public float baseCommonToLegendary = 0.65f;

    [Header("Rarity Multipliers - Base Rare")]
    [Min(0f)] public float baseRareToCommon = 1.00f;
    [Min(0f)] public float baseRareToRare = 0.95f;
    [Min(0f)] public float baseRareToEpic = 1.10f;
    [Min(0f)] public float baseRareToLegendary = 0.80f;

    [Header("Rarity Multipliers - Base Epic")]
    [Min(0f)] public float baseEpicToCommon = 1.25f;
    [Min(0f)] public float baseEpicToRare = 1.10f;
    [Min(0f)] public float baseEpicToEpic = 0.85f;
    [Min(0f)] public float baseEpicToLegendary = 0.55f;

    [Header("Rarity Multipliers - Base Legendary")]
    [Min(0f)] public float baseLegendaryToCommon = 1.45f;
    [Min(0f)] public float baseLegendaryToRare = 1.20f;
    [Min(0f)] public float baseLegendaryToEpic = 0.75f;
    [Min(0f)] public float baseLegendaryToLegendary = 0.35f;

    [Header("Safety")]
    [Min(0f)] public float minimumCandidateWeight = 0.0001f;

    public float GetRarityMultiplier(SkinRarity baseRarity, SkinRarity candidateRarity)
    {
        switch (baseRarity)
        {
            case SkinRarity.Common:
                switch (candidateRarity)
                {
                    case SkinRarity.Common: return baseCommonToCommon;
                    case SkinRarity.Rare: return baseCommonToRare;
                    case SkinRarity.Epic: return baseCommonToEpic;
                    case SkinRarity.Legendary: return baseCommonToLegendary;
                }
                break;

            case SkinRarity.Rare:
                switch (candidateRarity)
                {
                    case SkinRarity.Common: return baseRareToCommon;
                    case SkinRarity.Rare: return baseRareToRare;
                    case SkinRarity.Epic: return baseRareToEpic;
                    case SkinRarity.Legendary: return baseRareToLegendary;
                }
                break;

            case SkinRarity.Epic:
                switch (candidateRarity)
                {
                    case SkinRarity.Common: return baseEpicToCommon;
                    case SkinRarity.Rare: return baseEpicToRare;
                    case SkinRarity.Epic: return baseEpicToEpic;
                    case SkinRarity.Legendary: return baseEpicToLegendary;
                }
                break;

            case SkinRarity.Legendary:
                switch (candidateRarity)
                {
                    case SkinRarity.Common: return baseLegendaryToCommon;
                    case SkinRarity.Rare: return baseLegendaryToRare;
                    case SkinRarity.Epic: return baseLegendaryToEpic;
                    case SkinRarity.Legendary: return baseLegendaryToLegendary;
                }
                break;
        }

        return 1f;
    }
}