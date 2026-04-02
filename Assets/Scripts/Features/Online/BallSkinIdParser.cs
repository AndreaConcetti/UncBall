using System;
using System.Collections.Generic;
using UnityEngine;

public static class BallSkinIdParser
{
    public static bool TryParse(string skinId, out BallSkinData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(skinId))
            return false;

        string[] parts = skinId.Split('_');
        if (parts.Length < 6)
            return false;

        try
        {
            SkinRarity rarity = ParseRarity(parts[0]);
            string baseColorId = parts[1];
            string patternId = parts[2];
            string patternColorId = parts[3];

            float intensity = ParseScaledFloat(parts[parts.Length - 2], 'i');
            float scale = ParseScaledFloat(parts[parts.Length - 1], 's');

            data = new BallSkinData
            {
                skinUniqueId = skinId,
                baseColorId = baseColorId,
                patternId = patternId,
                patternColorId = patternColorId,
                patternIntensity = intensity,
                patternScale = scale,
                rarity = rarity
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParse(string skinId, BallSkinDatabase database, out BallSkinData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(skinId))
            return false;

        if (database == null ||
            database.baseColorLibrary == null ||
            database.patternLibrary == null ||
            database.patternColorLibrary == null)
        {
            return TryParse(skinId, out data);
        }

        string normalizedSkinId = skinId.Trim();

        string[] parts = normalizedSkinId.Split('_');
        if (parts.Length < 6)
            return false;

        SkinRarity rarity = ParseRarity(parts[0]);
        float intensity = ParseScaledFloat(parts[parts.Length - 2], 'i');
        float scale = ParseScaledFloat(parts[parts.Length - 1], 's');

        string middle = ExtractMiddlePayload(parts);
        if (string.IsNullOrWhiteSpace(middle))
            return false;

        if (!TryResolveIdsFromLibraries(
                middle,
                database,
                out string baseColorId,
                out string patternId,
                out string patternColorId))
        {
            return false;
        }

        data = new BallSkinData
        {
            skinUniqueId = normalizedSkinId,
            baseColorId = baseColorId,
            patternId = patternId,
            patternColorId = patternColorId,
            patternIntensity = intensity,
            patternScale = scale,
            rarity = rarity
        };

        return true;
    }

    private static string ExtractMiddlePayload(string[] parts)
    {
        if (parts == null || parts.Length < 6)
            return string.Empty;

        return string.Join("_", parts, 1, parts.Length - 3);
    }

    private static bool TryResolveIdsFromLibraries(
        string payload,
        BallSkinDatabase database,
        out string baseColorId,
        out string patternId,
        out string patternColorId)
    {
        baseColorId = null;
        patternId = null;
        patternColorId = null;

        IReadOnlyList<BallColorLibrary.ColorEntry> baseEntries = database.baseColorLibrary.Colors;
        IReadOnlyList<BallPatternLibrary.PatternEntry> patternEntries = database.patternLibrary.Patterns;
        IReadOnlyList<BallColorLibrary.ColorEntry> patternColorEntries = database.patternColorLibrary.Colors;

        if (baseEntries == null || patternEntries == null || patternColorEntries == null)
            return false;

        string normalizedPayload = Normalize(payload);

        for (int b = 0; b < baseEntries.Count; b++)
        {
            BallColorLibrary.ColorEntry baseEntry = baseEntries[b];
            if (baseEntry == null || string.IsNullOrWhiteSpace(baseEntry.id))
                continue;

            string baseIdNorm = Normalize(baseEntry.id);
            string prefix = baseIdNorm + "_";

            if (!normalizedPayload.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string afterBase = normalizedPayload.Substring(prefix.Length);

            for (int p = 0; p < patternEntries.Count; p++)
            {
                BallPatternLibrary.PatternEntry patternEntry = patternEntries[p];
                if (patternEntry == null || string.IsNullOrWhiteSpace(patternEntry.id))
                    continue;

                string patternIdNorm = Normalize(patternEntry.id);
                string patternPrefix = patternIdNorm + "_";

                if (!afterBase.StartsWith(patternPrefix, StringComparison.Ordinal))
                    continue;

                string afterPattern = afterBase.Substring(patternPrefix.Length);

                for (int pc = 0; pc < patternColorEntries.Count; pc++)
                {
                    BallColorLibrary.ColorEntry patternColorEntry = patternColorEntries[pc];
                    if (patternColorEntry == null || string.IsNullOrWhiteSpace(patternColorEntry.id))
                        continue;

                    string patternColorNorm = Normalize(patternColorEntry.id);

                    if (!string.Equals(afterPattern, patternColorNorm, StringComparison.Ordinal))
                        continue;

                    baseColorId = baseEntry.id;
                    patternId = patternEntry.id;
                    patternColorId = patternColorEntry.id;
                    return true;
                }
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant();
    }

    private static SkinRarity ParseRarity(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "common": return SkinRarity.Common;
            case "rare": return SkinRarity.Rare;
            case "epic": return SkinRarity.Epic;
            case "legendary": return SkinRarity.Legendary;
            default: return SkinRarity.Common;
        }
    }

    private static float ParseScaledFloat(string token, char prefix)
    {
        if (string.IsNullOrWhiteSpace(token))
            return 1f;

        token = token.Trim().ToLowerInvariant();

        if (token.Length < 2 || token[0] != prefix)
            return 1f;

        string numeric = token.Substring(1);

        if (!float.TryParse(numeric, out float raw))
            return 1f;

        return raw / 100f;
    }
}