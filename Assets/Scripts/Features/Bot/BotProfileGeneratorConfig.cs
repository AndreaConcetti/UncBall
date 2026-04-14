using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BotProfileGeneratorConfig",
    menuName = "Uncball Arena/Bots/Bot Profile Generator Config")]
public sealed class BotProfileGeneratorConfig : ScriptableObject
{
    [Header("General")]
    [SerializeField] private string botIdPrefix = "bot";
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool allowOnlineDisguiseByDefault = true;
    [SerializeField] private bool localBotsByDefault = true;

    [Header("Name Pools")]
    [SerializeField] private List<string> fixedNames = new List<string>();
    [SerializeField] private List<string> optionalPrefixes = new List<string>();
    [SerializeField] private List<string> optionalSuffixes = new List<string>();

    [Header("Future Cosmetics")]
    [SerializeField] private List<string> avatarPool = new List<string>();
    [SerializeField] private List<string> framePool = new List<string>();

    [Header("Difficulty Presets")]
    [SerializeField] private DifficultyGenerationSettings easySettings = DifficultyGenerationSettings.CreateDefaultEasy();
    [SerializeField] private DifficultyGenerationSettings mediumSettings = DifficultyGenerationSettings.CreateDefaultMedium();
    [SerializeField] private DifficultyGenerationSettings hardSettings = DifficultyGenerationSettings.CreateDefaultHard();
    [SerializeField] private DifficultyGenerationSettings unbeatableSettings = DifficultyGenerationSettings.CreateDefaultUnbeatable();

    public string BotIdPrefix => string.IsNullOrWhiteSpace(botIdPrefix) ? "bot" : botIdPrefix.Trim();
    public bool EnableDebugLogs => enableDebugLogs;
    public bool AllowOnlineDisguiseByDefault => allowOnlineDisguiseByDefault;
    public bool LocalBotsByDefault => localBotsByDefault;
    public IReadOnlyList<string> FixedNames => fixedNames;
    public IReadOnlyList<string> OptionalPrefixes => optionalPrefixes;
    public IReadOnlyList<string> OptionalSuffixes => optionalSuffixes;
    public IReadOnlyList<string> AvatarPool => avatarPool;
    public IReadOnlyList<string> FramePool => framePool;

    public DifficultyGenerationSettings GetSettings(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return easySettings;

            case BotDifficulty.Medium:
                return mediumSettings;

            case BotDifficulty.Hard:
                return hardSettings;

            case BotDifficulty.Unbeatable:
                return unbeatableSettings;

            default:
                return mediumSettings;
        }
    }

    private void OnValidate()
    {
        easySettings?.Validate();
        mediumSettings?.Validate();
        hardSettings?.Validate();
        unbeatableSettings?.Validate();
    }
}

[Serializable]
public sealed class DifficultyGenerationSettings
{
    [Header("Name Composition")]
    [Range(0f, 1f)] public float prefixChance = 0.10f;
    [Range(0f, 1f)] public float suffixChance = 0.15f;

    [Header("Skin Pool")]
    public List<string> allowedSkinIds = new List<string>();

    [Header("Archetypes")]
    public List<ArchetypeWeight> archetypeWeights = new List<ArchetypeWeight>();

    [Header("Displayed Stats")]
    public IntRange fakeLevelRange = new IntRange(1, 10);
    public IntRange fakeRankedLpRange = new IntRange(850, 1050);
    public IntRange fakeWinRateRange = new IntRange(38, 52);
    public IntRange fakeMatchesPlayedRange = new IntRange(20, 120);

    public void Validate()
    {
        fakeLevelRange.ValidateMinMax(1);
        fakeRankedLpRange.ValidateMinMax(0);
        fakeWinRateRange.ClampTo(0, 100);
        fakeMatchesPlayedRange.ValidateMinMax(0);

        if (archetypeWeights == null || archetypeWeights.Count == 0)
        {
            archetypeWeights = new List<ArchetypeWeight>
            {
                new ArchetypeWeight(BotArchetype.Balanced, 1f)
            };
        }

        for (int i = 0; i < archetypeWeights.Count; i++)
        {
            archetypeWeights[i].Validate();
        }

        if (allowedSkinIds == null)
        {
            allowedSkinIds = new List<string>();
        }
    }

    public static DifficultyGenerationSettings CreateDefaultEasy()
    {
        return new DifficultyGenerationSettings
        {
            prefixChance = 0.05f,
            suffixChance = 0.10f,
            fakeLevelRange = new IntRange(1, 8),
            fakeRankedLpRange = new IntRange(850, 1050),
            fakeWinRateRange = new IntRange(38, 52),
            fakeMatchesPlayedRange = new IntRange(20, 120),
            archetypeWeights = new List<ArchetypeWeight>
            {
                new ArchetypeWeight(BotArchetype.Balanced, 4f),
                new ArchetypeWeight(BotArchetype.Defensive, 3f),
                new ArchetypeWeight(BotArchetype.Trickster, 1.5f),
                new ArchetypeWeight(BotArchetype.Aggressive, 1f),
                new ArchetypeWeight(BotArchetype.Precision, 0.5f)
            }
        };
    }

    public static DifficultyGenerationSettings CreateDefaultMedium()
    {
        return new DifficultyGenerationSettings
        {
            prefixChance = 0.08f,
            suffixChance = 0.14f,
            fakeLevelRange = new IntRange(5, 18),
            fakeRankedLpRange = new IntRange(1050, 1350),
            fakeWinRateRange = new IntRange(48, 61),
            fakeMatchesPlayedRange = new IntRange(80, 350),
            archetypeWeights = new List<ArchetypeWeight>
            {
                new ArchetypeWeight(BotArchetype.Balanced, 3f),
                new ArchetypeWeight(BotArchetype.Aggressive, 2f),
                new ArchetypeWeight(BotArchetype.Defensive, 2f),
                new ArchetypeWeight(BotArchetype.Trickster, 1.5f),
                new ArchetypeWeight(BotArchetype.Precision, 1.5f)
            }
        };
    }

    public static DifficultyGenerationSettings CreateDefaultHard()
    {
        return new DifficultyGenerationSettings
        {
            prefixChance = 0.12f,
            suffixChance = 0.18f,
            fakeLevelRange = new IntRange(12, 30),
            fakeRankedLpRange = new IntRange(1350, 1800),
            fakeWinRateRange = new IntRange(58, 72),
            fakeMatchesPlayedRange = new IntRange(200, 1000),
            archetypeWeights = new List<ArchetypeWeight>
            {
                new ArchetypeWeight(BotArchetype.Precision, 3f),
                new ArchetypeWeight(BotArchetype.Aggressive, 2.5f),
                new ArchetypeWeight(BotArchetype.Balanced, 2f),
                new ArchetypeWeight(BotArchetype.Defensive, 1.5f),
                new ArchetypeWeight(BotArchetype.Trickster, 1f)
            }
        };
    }

    public static DifficultyGenerationSettings CreateDefaultUnbeatable()
    {
        return new DifficultyGenerationSettings
        {
            prefixChance = 0.14f,
            suffixChance = 0.20f,
            fakeLevelRange = new IntRange(22, 50),
            fakeRankedLpRange = new IntRange(1700, 2600),
            fakeWinRateRange = new IntRange(72, 92),
            fakeMatchesPlayedRange = new IntRange(500, 3000),
            archetypeWeights = new List<ArchetypeWeight>
            {
                new ArchetypeWeight(BotArchetype.Precision, 4f),
                new ArchetypeWeight(BotArchetype.Aggressive, 2.5f),
                new ArchetypeWeight(BotArchetype.Balanced, 1.5f),
                new ArchetypeWeight(BotArchetype.Defensive, 1f),
                new ArchetypeWeight(BotArchetype.Trickster, 0.5f)
            }
        };
    }
}

[Serializable]
public sealed class ArchetypeWeight
{
    public BotArchetype archetype;
    [Min(0.01f)] public float weight = 1f;

    public ArchetypeWeight()
    {
    }

    public ArchetypeWeight(BotArchetype archetype, float weight)
    {
        this.archetype = archetype;
        this.weight = Mathf.Max(0.01f, weight);
    }

    public void Validate()
    {
        weight = Mathf.Max(0.01f, weight);
    }
}

[Serializable]
public struct IntRange
{
    public int min;
    public int max;

    public IntRange(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    public int Random(System.Random random)
    {
        if (random == null)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        return random.Next(min, max + 1);
    }

    public void ValidateMinMax(int minimumAllowed)
    {
        min = Mathf.Max(minimumAllowed, min);
        max = Mathf.Max(min, max);
    }

    public void ClampTo(int absoluteMin, int absoluteMax)
    {
        min = Mathf.Clamp(min, absoluteMin, absoluteMax);
        max = Mathf.Clamp(max, min, absoluteMax);
    }
}