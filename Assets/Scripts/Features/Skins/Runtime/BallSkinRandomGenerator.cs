using System.Collections.Generic;
using UnityEngine;

public class BallSkinRandomGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallSkinDatabase database;
    [SerializeField] private SkinRarityTable rarityTable;
    [SerializeField] private BallColorHarmonyProfile colorHarmonyProfile;

    [Header("Ranges")]
    [SerializeField] private Vector2 patternIntensityRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private Vector2 patternScaleRange = new Vector2(0.75f, 2f);

    [Header("Fallback")]
    [SerializeField] private bool fallbackToNearestAvailableRarity = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    public BallSkinData GenerateRandomSkin()
    {
        return GenerateRandomSkinWithMinimumRarity(null);
    }

    public BallSkinData GenerateRandomSkinWithMinimumRarity(SkinRarity? minimumGuaranteedRarity)
    {
        if (!ValidateReferences())
            return null;

        SkinRarity targetRarity = rarityTable.RollRarity(minimumGuaranteedRarity);
        return GenerateSkinOfTargetRarity(targetRarity);
    }

    public BallSkinData GenerateGuaranteedCommonSkin()
    {
        return GenerateSkinOfTargetRarity(SkinRarity.Common);
    }

    public BallSkinData GenerateGuaranteedRareSkin()
    {
        return GenerateSkinOfTargetRarity(SkinRarity.Rare);
    }

    public BallSkinData GenerateGuaranteedEpicSkin()
    {
        return GenerateSkinOfTargetRarity(SkinRarity.Epic);
    }

    public BallSkinData GenerateGuaranteedLegendarySkin()
    {
        return GenerateSkinOfTargetRarity(SkinRarity.Legendary);
    }

    public BallSkinData GenerateSkinOfTargetRarity(SkinRarity targetRarity)
    {
        if (!ValidateReferences())
            return null;

        BallColorLibrary.ColorEntry baseColor = GetRandomBaseColorForTargetRarity(targetRarity);
        if (baseColor == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Nessun base color disponibile per la rarità target: " + targetRarity, this);
            return null;
        }

        BallPatternLibrary.PatternEntry pattern = GetRandomPatternForTargetRarity(targetRarity);
        if (pattern == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Nessun pattern disponibile per la rarità target: " + targetRarity, this);
            return null;
        }

        BallColorLibrary.ColorEntry patternColor = BallColorHarmonyPicker.PickPatternColor(
            database.baseColorLibrary,
            database.patternColorLibrary,
            colorHarmonyProfile,
            baseColor.id,
            baseColor.rarity);

        if (patternColor == null)
        {
            Debug.LogWarning(
                "[BallSkinRandomGenerator] Pattern color armonico non trovato, fallback casuale attivato. BaseColor=" + baseColor.id,
                this
            );

            patternColor = GetFallbackPatternColor(baseColor.id, targetRarity);
        }

        if (patternColor == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Nessun pattern color disponibile.", this);
            return null;
        }

        float patternIntensity = Random.Range(patternIntensityRange.x, patternIntensityRange.y);
        float patternScale = Random.Range(patternScaleRange.x, patternScaleRange.y);

        BallSkinData newSkin = BallSkinFactory.CreateSkin(
            baseColor.id,
            pattern.id,
            patternColor.id,
            patternIntensity,
            patternScale,
            targetRarity
        );

        if (logDebug)
        {
            Debug.Log(
                "[BallSkinRandomGenerator] Generated skin -> " +
                "Rarity=" + targetRarity +
                " | Base=" + baseColor.id + " (" + baseColor.rarity + ")" +
                " | Pattern=" + pattern.id + " (" + pattern.rarity + ")" +
                " | PatternColor=" + patternColor.id + " (" + patternColor.rarity + ")" +
                " | SkinId=" + newSkin.skinUniqueId,
                this
            );
        }

        return newSkin;
    }

    private BallColorLibrary.ColorEntry GetRandomBaseColorForTargetRarity(SkinRarity targetRarity)
    {
        BallColorLibrary.ColorEntry exact = database.baseColorLibrary.GetRandomByRarity(targetRarity);
        if (exact != null)
            return exact;

        if (!fallbackToNearestAvailableRarity)
            return null;

        return GetRandomColorNearestRarity(database.baseColorLibrary, targetRarity);
    }

    private BallPatternLibrary.PatternEntry GetRandomPatternForTargetRarity(SkinRarity targetRarity)
    {
        BallPatternLibrary.PatternEntry exact = database.patternLibrary.GetRandomByRarity(targetRarity);
        if (exact != null)
            return exact;

        if (!fallbackToNearestAvailableRarity)
            return null;

        return GetRandomPatternNearestRarity(database.patternLibrary, targetRarity);
    }

    private BallColorLibrary.ColorEntry GetFallbackPatternColor(string baseColorId, SkinRarity targetRarity)
    {
        BallColorLibrary.ColorEntry exact = database.patternColorLibrary.GetRandomByRarity(targetRarity);
        if (exact != null && !IdsEqual(exact.id, baseColorId))
            return exact;

        IReadOnlyList<BallColorLibrary.ColorEntry> all = database.patternColorLibrary.Colors;
        if (all == null || all.Count == 0)
            return null;

        for (int i = 0; i < 32; i++)
        {
            BallColorLibrary.ColorEntry candidate = all[Random.Range(0, all.Count)];
            if (candidate != null && !IdsEqual(candidate.id, baseColorId))
                return candidate;
        }

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && !IdsEqual(all[i].id, baseColorId))
                return all[i];
        }

        return null;
    }

    private BallColorLibrary.ColorEntry GetRandomColorNearestRarity(BallColorLibrary library, SkinRarity targetRarity)
    {
        BallColorLibrary.ColorEntry entry = library.GetRandomByRarity(targetRarity);
        if (entry != null)
            return entry;

        for (int distance = 1; distance <= 3; distance++)
        {
            int lower = (int)targetRarity - distance;
            int upper = (int)targetRarity + distance;

            if (lower >= (int)SkinRarity.Common)
            {
                entry = library.GetRandomByRarity((SkinRarity)lower);
                if (entry != null)
                    return entry;
            }

            if (upper <= (int)SkinRarity.Legendary)
            {
                entry = library.GetRandomByRarity((SkinRarity)upper);
                if (entry != null)
                    return entry;
            }
        }

        return null;
    }

    private BallPatternLibrary.PatternEntry GetRandomPatternNearestRarity(BallPatternLibrary library, SkinRarity targetRarity)
    {
        BallPatternLibrary.PatternEntry entry = library.GetRandomByRarity(targetRarity);
        if (entry != null)
            return entry;

        for (int distance = 1; distance <= 3; distance++)
        {
            int lower = (int)targetRarity - distance;
            int upper = (int)targetRarity + distance;

            if (lower >= (int)SkinRarity.Common)
            {
                entry = library.GetRandomByRarity((SkinRarity)lower);
                if (entry != null)
                    return entry;
            }

            if (upper <= (int)SkinRarity.Legendary)
            {
                entry = library.GetRandomByRarity((SkinRarity)upper);
                if (entry != null)
                    return entry;
            }
        }

        return null;
    }

    private bool ValidateReferences()
    {
        if (database == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Database mancante.", this);
            return false;
        }

        if (database.baseColorLibrary == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] BaseColorLibrary mancante.", this);
            return false;
        }

        if (database.patternLibrary == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] PatternLibrary mancante.", this);
            return false;
        }

        if (database.patternColorLibrary == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] PatternColorLibrary mancante.", this);
            return false;
        }

        if (rarityTable == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] SkinRarityTable mancante.", this);
            return false;
        }

        if (colorHarmonyProfile == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] BallColorHarmonyProfile mancante.", this);
            return false;
        }

        return true;
    }

    private bool IdsEqual(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        return a.Trim().ToLowerInvariant() == b.Trim().ToLowerInvariant();
    }
}