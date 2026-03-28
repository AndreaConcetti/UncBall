#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BallColorLibraryPresetImporter
{
    private const int TargetCount = 200;

    private struct FamilyDefinition
    {
        public string name;
        public float hue;

        public FamilyDefinition(string name, float hue)
        {
            this.name = name;
            this.hue = hue;
        }
    }

    private struct VariantDefinition
    {
        public string suffix;
        public float saturation;
        public float value;
        public SkinRarity rarity;
        public float hueOffset;

        public VariantDefinition(string suffix, float saturation, float value, SkinRarity rarity, float hueOffset = 0f)
        {
            this.suffix = suffix;
            this.saturation = saturation;
            this.value = value;
            this.rarity = rarity;
            this.hueOffset = hueOffset;
        }
    }

    private struct PresetEntry
    {
        public string id;
        public Color color;
        public SkinRarity rarity;

        public PresetEntry(string id, Color color, SkinRarity rarity)
        {
            this.id = id;
            this.color = color;
            this.rarity = rarity;
        }
    }

    private static readonly FamilyDefinition[] Families =
    {
        new FamilyDefinition("red",      0.000f),
        new FamilyDefinition("crimson",  0.975f),
        new FamilyDefinition("coral",    0.030f),
        new FamilyDefinition("orange",   0.075f),
        new FamilyDefinition("gold",     0.140f),
        new FamilyDefinition("yellow",   0.160f),
        new FamilyDefinition("lime",     0.230f),
        new FamilyDefinition("green",    0.330f),
        new FamilyDefinition("emerald",  0.390f),
        new FamilyDefinition("cyan",     0.500f),
        new FamilyDefinition("azure",    0.555f),
        new FamilyDefinition("blue",     0.610f),
        new FamilyDefinition("navy",     0.655f),
        new FamilyDefinition("indigo",   0.705f),
        new FamilyDefinition("purple",   0.760f),
        new FamilyDefinition("magenta",  0.860f),
        new FamilyDefinition("pink",     0.910f)
    };

    private static readonly VariantDefinition[] BaseVariants =
    {
        new VariantDefinition("pastel",   0.22f, 1.00f, SkinRarity.Common),
        new VariantDefinition("soft",     0.38f, 0.96f, SkinRarity.Common),
        new VariantDefinition("light",    0.56f, 1.00f, SkinRarity.Common),
        new VariantDefinition("classic",  0.78f, 0.90f, SkinRarity.Common),

        new VariantDefinition("vivid",    0.92f, 0.98f, SkinRarity.Rare),
        new VariantDefinition("deep",     0.88f, 0.64f, SkinRarity.Rare),
        new VariantDefinition("ice",      0.18f, 1.00f, SkinRarity.Rare, 0.010f),

        new VariantDefinition("neon",     1.00f, 1.00f, SkinRarity.Epic),
        new VariantDefinition("royal",    0.82f, 0.78f, SkinRarity.Epic),

        new VariantDefinition("plasma",   1.00f, 1.00f, SkinRarity.Legendary, 0.015f),
        new VariantDefinition("inferno",  1.00f, 0.92f, SkinRarity.Legendary, -0.012f)
    };

    private static readonly VariantDefinition[] PatternVariants =
    {
        new VariantDefinition("soft",        0.30f, 0.96f, SkinRarity.Common),
        new VariantDefinition("clean",       0.52f, 0.98f, SkinRarity.Common),
        new VariantDefinition("classic",     0.74f, 0.92f, SkinRarity.Common),

        new VariantDefinition("vivid",       0.92f, 0.98f, SkinRarity.Rare),
        new VariantDefinition("dark",        0.78f, 0.42f, SkinRarity.Rare),
        new VariantDefinition("bright",      0.78f, 1.00f, SkinRarity.Rare),

        new VariantDefinition("neon",        1.00f, 1.00f, SkinRarity.Epic),
        new VariantDefinition("electric",    1.00f, 1.00f, SkinRarity.Epic, 0.008f),

        new VariantDefinition("plasma",      1.00f, 1.00f, SkinRarity.Legendary, 0.016f),
        new VariantDefinition("ultra_dark",  0.88f, 0.22f, SkinRarity.Legendary)
    };

    [MenuItem("Tools/Uncball/Skins/Populate Selected BallColorLibrary/Base Colors (200)")]
    public static void PopulateSelectedBaseLibrary()
    {
        PopulateSelectedLibrary(BuildBasePresetList(), "Base Colors");
    }

    [MenuItem("Tools/Uncball/Skins/Populate Selected BallColorLibrary/Pattern Colors (200)")]
    public static void PopulateSelectedPatternLibrary()
    {
        PopulateSelectedLibrary(BuildPatternPresetList(), "Pattern Colors");
    }

    [MenuItem("Tools/Uncball/Skins/Log Base Color IDs (200)")]
    public static void LogBaseIds()
    {
        LogPresetList(BuildBasePresetList(), "Base Colors");
    }

    [MenuItem("Tools/Uncball/Skins/Log Pattern Color IDs (200)")]
    public static void LogPatternIds()
    {
        LogPresetList(BuildPatternPresetList(), "Pattern Colors");
    }

    private static void PopulateSelectedLibrary(List<PresetEntry> presets, string label)
    {
        BallColorLibrary library = Selection.activeObject as BallColorLibrary;

        if (library == null)
        {
            EditorUtility.DisplayDialog(
                "BallColorLibrary non selezionata",
                "Seleziona un asset BallColorLibrary nel Project e rilancia il comando.",
                "OK");
            return;
        }

        if (presets == null || presets.Count != TargetCount)
        {
            Debug.LogError($"[BallColorLibraryPresetImporter] {label} invalid preset count: {(presets != null ? presets.Count : 0)}");
            return;
        }

        SerializedObject so = new SerializedObject(library);
        SerializedProperty colorsProp = so.FindProperty("colors");

        if (colorsProp == null || !colorsProp.isArray)
        {
            Debug.LogError("[BallColorLibraryPresetImporter] Proprietà 'colors' non trovata.");
            return;
        }

        colorsProp.ClearArray();
        colorsProp.arraySize = presets.Count;

        for (int i = 0; i < presets.Count; i++)
        {
            SerializedProperty entry = colorsProp.GetArrayElementAtIndex(i);

            SerializedProperty idProp = entry.FindPropertyRelative("id");
            SerializedProperty colorProp = entry.FindPropertyRelative("color");
            SerializedProperty rarityProp = entry.FindPropertyRelative("rarity");

            if (idProp != null)
                idProp.stringValue = presets[i].id;

            if (colorProp != null)
                colorProp.colorValue = presets[i].color;

            if (rarityProp != null && rarityProp.propertyType == SerializedPropertyType.Enum)
                rarityProp.enumValueIndex = (int)presets[i].rarity;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BallColorLibraryPresetImporter] Populated '{library.name}' as {label} with {presets.Count} entries.");
    }

    private static void LogPresetList(List<PresetEntry> presets, string label)
    {
        Debug.Log($"[BallColorLibraryPresetImporter] Logging {label} preset list. Count={presets.Count}");

        for (int i = 0; i < presets.Count; i++)
        {
            Color32 c = presets[i].color;
            Debug.Log($"{i + 1:000} | {presets[i].id} | {presets[i].rarity} | RGB({c.r},{c.g},{c.b})");
        }
    }

    private static List<PresetEntry> BuildBasePresetList()
    {
        List<PresetEntry> result = new List<PresetEntry>(TargetCount);
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddBaseNeutrals(result, ids);

        for (int i = 0; i < Families.Length; i++)
        {
            FamilyDefinition family = Families[i];

            for (int j = 0; j < BaseVariants.Length; j++)
            {
                VariantDefinition variant = BaseVariants[j];
                float hue = Repeat01(family.hue + variant.hueOffset);

                AddPreset(
                    result,
                    ids,
                    $"{family.name}_{variant.suffix}",
                    Color.HSVToRGB(hue, variant.saturation, variant.value),
                    variant.rarity);
            }
        }

        return TrimOrFail(result, "Base");
    }

    private static List<PresetEntry> BuildPatternPresetList()
    {
        List<PresetEntry> result = new List<PresetEntry>(TargetCount);
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddPatternNeutrals(result, ids);

        for (int i = 0; i < Families.Length; i++)
        {
            FamilyDefinition family = Families[i];

            for (int j = 0; j < PatternVariants.Length; j++)
            {
                VariantDefinition variant = PatternVariants[j];
                float hue = Repeat01(family.hue + variant.hueOffset);

                AddPreset(
                    result,
                    ids,
                    $"{family.name}_{variant.suffix}",
                    Color.HSVToRGB(hue, variant.saturation, variant.value),
                    variant.rarity);
            }
        }

        return TrimOrFail(result, "Pattern");
    }

    private static void AddBaseNeutrals(List<PresetEntry> result, HashSet<string> ids)
    {
        AddPreset(result, ids, "white", Hex("#FFFFFF"), SkinRarity.Common);
        AddPreset(result, ids, "ivory", Hex("#FFF8E7"), SkinRarity.Common);
        AddPreset(result, ids, "cream", Hex("#FFF1CC"), SkinRarity.Common);
        AddPreset(result, ids, "pearl", Hex("#F4F6F8"), SkinRarity.Common);
        AddPreset(result, ids, "gray", Hex("#808080"), SkinRarity.Common);
        AddPreset(result, ids, "slate", Hex("#708090"), SkinRarity.Common);

        AddPreset(result, ids, "silver", Hex("#C0C0C0"), SkinRarity.Rare);
        AddPreset(result, ids, "charcoal", Hex("#2F3640"), SkinRarity.Rare);
        AddPreset(result, ids, "graphite", Hex("#4A4F57"), SkinRarity.Rare);

        AddPreset(result, ids, "obsidian", Hex("#111111"), SkinRarity.Epic);
        AddPreset(result, ids, "frost_white", Hex("#F8FCFF"), SkinRarity.Epic);

        AddPreset(result, ids, "black", Hex("#000000"), SkinRarity.Legendary);
        AddPreset(result, ids, "champion_gold", Hex("#FFD700"), SkinRarity.Legendary);
    }

    private static void AddPatternNeutrals(List<PresetEntry> result, HashSet<string> ids)
    {
        AddPreset(result, ids, "white", Hex("#FFFFFF"), SkinRarity.Common);
        AddPreset(result, ids, "off_white", Hex("#F6F6F2"), SkinRarity.Common);
        AddPreset(result, ids, "light_gray", Hex("#D9D9D9"), SkinRarity.Common);
        AddPreset(result, ids, "gray", Hex("#808080"), SkinRarity.Common);
        AddPreset(result, ids, "slate", Hex("#708090"), SkinRarity.Common);

        AddPreset(result, ids, "silver", Hex("#C0C0C0"), SkinRarity.Rare);
        AddPreset(result, ids, "charcoal", Hex("#2F3640"), SkinRarity.Rare);
        AddPreset(result, ids, "graphite", Hex("#4A4F57"), SkinRarity.Rare);
        AddPreset(result, ids, "ink", Hex("#1F2430"), SkinRarity.Rare);

        AddPreset(result, ids, "obsidian", Hex("#111111"), SkinRarity.Epic);
        AddPreset(result, ids, "signal_white", Hex("#FCFCFC"), SkinRarity.Epic);

        AddPreset(result, ids, "black", Hex("#000000"), SkinRarity.Legendary);
        AddPreset(result, ids, "highlight_gold", Hex("#FFD700"), SkinRarity.Legendary);
        AddPreset(result, ids, "warning_red", Hex("#FF2A2A"), SkinRarity.Legendary);
        AddPreset(result, ids, "electric_cyan", Hex("#00F0FF"), SkinRarity.Legendary);
        AddPreset(result, ids, "toxic_lime", Hex("#A8FF00"), SkinRarity.Legendary);
        AddPreset(result, ids, "hot_magenta", Hex("#FF00CC"), SkinRarity.Legendary);
        AddPreset(result, ids, "royal_purple", Hex("#7A00FF"), SkinRarity.Legendary);
        AddPreset(result, ids, "deep_navy", Hex("#001A4D"), SkinRarity.Legendary);
        AddPreset(result, ids, "bright_yellow", Hex("#FFF000"), SkinRarity.Legendary);
        AddPreset(result, ids, "orange_burst", Hex("#FF7A00"), SkinRarity.Legendary);
        AddPreset(result, ids, "emerald_glow", Hex("#00D97E"), SkinRarity.Legendary);
        AddPreset(result, ids, "pink_flash", Hex("#FF5CCF"), SkinRarity.Legendary);
        AddPreset(result, ids, "ice_blue", Hex("#6FDBFF"), SkinRarity.Legendary);
        AddPreset(result, ids, "ultra_black", Hex("#050505"), SkinRarity.Legendary);
        AddPreset(result, ids, "pure_white", Hex("#FFFFFF"), SkinRarity.Legendary);
        AddPreset(result, ids, "high_contrast_red", Hex("#FF1E1E"), SkinRarity.Legendary);
        AddPreset(result, ids, "high_contrast_cyan", Hex("#00E5FF"), SkinRarity.Legendary);
        AddPreset(result, ids, "high_contrast_gold", Hex("#FFC400"), SkinRarity.Legendary);
        AddPreset(result, ids, "high_contrast_lime", Hex("#B7FF00"), SkinRarity.Legendary);
        AddPreset(result, ids, "high_contrast_purple", Hex("#8A2BFF"), SkinRarity.Legendary);
    }

    private static void AddPreset(List<PresetEntry> result, HashSet<string> ids, string id, Color color, SkinRarity rarity)
    {
        string normalized = NormalizeId(id);

        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (!ids.Add(normalized))
            return;

        result.Add(new PresetEntry(normalized, color, rarity));
    }

    private static List<PresetEntry> TrimOrFail(List<PresetEntry> result, string label)
    {
        if (result.Count < TargetCount)
        {
            Debug.LogError($"[BallColorLibraryPresetImporter] {label} preset count too low: {result.Count}/{TargetCount}");
            return result;
        }

        if (result.Count > TargetCount)
            result.RemoveRange(TargetCount, result.Count - TargetCount);

        return result;
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? string.Empty
            : id.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private static float Repeat01(float value)
    {
        value %= 1f;
        if (value < 0f)
            value += 1f;
        return value;
    }

    private static Color Hex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        return Color.white;
    }
}
#endif