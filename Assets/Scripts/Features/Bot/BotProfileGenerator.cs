using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BotProfileGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotProfileGeneratorConfig config;

    [Header("Runtime")]
    [SerializeField] private bool useDeterministicSeed = false;
    [SerializeField] private int deterministicSeed = 12345;

    [Header("Injected Runtime Data")]
    [Tooltip("Optional runtime pool. If populated, bot skin selection will prefer this list over config skin pools.")]
    [SerializeField] private List<string> injectedRuntimeSkinIds = new List<string>();

    private System.Random random;

    public BotProfileGeneratorConfig Config => config;

    private void Awake()
    {
        InitializeRandom();
    }

    private void InitializeRandom()
    {
        if (useDeterministicSeed)
        {
            random = new System.Random(deterministicSeed);
            Debug.Log($"[BotProfileGenerator] Initialized with deterministic seed={deterministicSeed}.", this);
            return;
        }

        random = new System.Random(Environment.TickCount);
        Debug.Log("[BotProfileGenerator] Initialized with non-deterministic seed.", this);
    }

    public void InjectRuntimeSkinIds(IEnumerable<string> skinIds)
    {
        injectedRuntimeSkinIds.Clear();

        if (skinIds == null)
        {
            Debug.Log("[BotProfileGenerator] InjectRuntimeSkinIds called with null. Runtime skin pool cleared.", this);
            return;
        }

        foreach (string skinId in skinIds)
        {
            if (string.IsNullOrWhiteSpace(skinId))
                continue;

            string trimmed = skinId.Trim();
            if (!injectedRuntimeSkinIds.Contains(trimmed))
                injectedRuntimeSkinIds.Add(trimmed);
        }

        Debug.Log($"[BotProfileGenerator] Runtime skin pool injected. Count={injectedRuntimeSkinIds.Count}.", this);
    }

    public void ClearInjectedRuntimeSkinIds()
    {
        injectedRuntimeSkinIds.Clear();
        Debug.Log("[BotProfileGenerator] Runtime skin pool cleared.", this);
    }

    public BotProfileRuntimeData Generate(BotDifficulty difficulty)
    {
        if (config == null)
        {
            Debug.LogError("[BotProfileGenerator] Missing BotProfileGeneratorConfig reference.", this);
            return null;
        }

        DifficultyGenerationSettings settings = config.GetSettings(difficulty);
        if (settings == null)
        {
            Debug.LogError($"[BotProfileGenerator] Missing settings for difficulty={difficulty}.", this);
            return null;
        }

        string generatedName = GenerateDisplayName(settings);
        BotArchetype archetype = PickArchetype(settings);
        string skinId = PickSkinId(settings);
        string avatarId = PickFromList(config.AvatarPool);
        string frameId = PickFromList(config.FramePool);

        int fakeLevel = settings.fakeLevelRange.Random(random);
        int fakeLp = settings.fakeRankedLpRange.Random(random);
        int fakeWinRate = settings.fakeWinRateRange.Random(random);
        int fakeMatchesPlayed = settings.fakeMatchesPlayedRange.Random(random);

        int calculatedWins = CalculateWinsFromWinRate(fakeMatchesPlayed, fakeWinRate);
        string botId = BuildBotId(difficulty, archetype, generatedName);

        BotProfileRuntimeData result = new BotProfileRuntimeData(
            botId: botId,
            displayName: generatedName,
            difficulty: difficulty,
            botArchetype: archetype,
            equippedSkinId: skinId,
            fakeRankedLp: fakeLp,
            fakeWinRate: fakeWinRate,
            fakeMatchesPlayed: fakeMatchesPlayed,
            fakeWins: calculatedWins,
            fakeLevel: fakeLevel,
            avatarId: avatarId,
            frameId: frameId,
            isEligibleForOnlineDisguise: config.AllowOnlineDisguiseByDefault,
            isLocalBot: config.LocalBotsByDefault);

        if (config.EnableDebugLogs)
            Debug.Log($"[BotProfileGenerator] Generated bot -> {result}", this);

        return result;
    }

    public OpponentPresentationProfile GeneratePresentationProfile(BotDifficulty difficulty)
    {
        BotProfileRuntimeData bot = Generate(difficulty);
        if (bot == null)
            return null;

        OpponentPresentationProfile profile = OpponentPresentationProfile.FromBot(bot);

        if (config != null && config.EnableDebugLogs && profile != null)
            Debug.Log($"[BotProfileGenerator] Generated opponent presentation -> {profile}", this);

        return profile;
    }

    private string GenerateDisplayName(DifficultyGenerationSettings settings)
    {
        string coreName = GenerateCoreName();
        string prefix = ShouldApply(settings.prefixChance) ? PickFromList(config.OptionalPrefixes) : string.Empty;
        string suffix = ShouldApply(settings.suffixChance) ? PickFromList(config.OptionalSuffixes) : string.Empty;

        string result = $"{prefix}{coreName}{suffix}".Trim();

        if (string.IsNullOrWhiteSpace(result))
            result = $"Bot{random.Next(100, 999)}";

        return SanitizeDisplayName(result);
    }

    private string GenerateCoreName()
    {
        // 1) Override esplicito da config
        if (config.FixedNames != null && config.FixedNames.Count >= 20)
            return PickFromList(config.FixedNames);

        // 2) Fallback automatico sulla libreria grande 500+
        IReadOnlyList<string> generatedPool = BotNameLibrary.GetDefaultGeneratedPool(500);
        if (generatedPool != null && generatedPool.Count > 0)
            return PickFromList(generatedPool);

        // 3) Fallback di sicurezza
        return $"Bot{random.Next(100, 999)}";
    }

    private BotArchetype PickArchetype(DifficultyGenerationSettings settings)
    {
        if (settings == null || settings.archetypeWeights == null || settings.archetypeWeights.Count == 0)
            return BotArchetype.Balanced;

        float totalWeight = 0f;
        for (int i = 0; i < settings.archetypeWeights.Count; i++)
            totalWeight += Mathf.Max(0.01f, settings.archetypeWeights[i].weight);

        float pick = (float)(random.NextDouble() * totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < settings.archetypeWeights.Count; i++)
        {
            cumulative += Mathf.Max(0.01f, settings.archetypeWeights[i].weight);
            if (pick <= cumulative)
                return settings.archetypeWeights[i].archetype;
        }

        return settings.archetypeWeights[settings.archetypeWeights.Count - 1].archetype;
    }

    private string PickSkinId(DifficultyGenerationSettings settings)
    {
        if (injectedRuntimeSkinIds != null && injectedRuntimeSkinIds.Count > 0)
        {
            string runtimeSkin = PickFromList(injectedRuntimeSkinIds);
            if (!string.IsNullOrWhiteSpace(runtimeSkin))
                return runtimeSkin;
        }

        if (settings != null && settings.allowedSkinIds != null && settings.allowedSkinIds.Count > 0)
        {
            string configSkin = PickFromList(settings.allowedSkinIds);
            if (!string.IsNullOrWhiteSpace(configSkin))
                return configSkin;
        }

        return string.Empty;
    }

    private string BuildBotId(BotDifficulty difficulty, BotArchetype archetype, string displayName)
    {
        string safeName = string.IsNullOrWhiteSpace(displayName)
            ? "bot"
            : displayName.Replace(" ", string.Empty).ToLowerInvariant();

        int randomChunk = random.Next(100000, 999999);
        return $"{config.BotIdPrefix}_{difficulty.ToString().ToLowerInvariant()}_{archetype.ToString().ToLowerInvariant()}_{safeName}_{randomChunk}";
    }

    private int CalculateWinsFromWinRate(int matches, int winRatePercent)
    {
        if (matches <= 0 || winRatePercent <= 0)
            return 0;

        float exact = matches * (winRatePercent / 100f);
        int wins = Mathf.RoundToInt(exact);
        return Mathf.Clamp(wins, 0, matches);
    }

    private bool ShouldApply(float chance)
    {
        chance = Mathf.Clamp01(chance);
        return random.NextDouble() <= chance;
    }

    private string PickFromList(IReadOnlyList<string> list)
    {
        if (list == null || list.Count == 0)
            return string.Empty;

        int index = random.Next(0, list.Count);
        string result = list[index];
        return string.IsNullOrWhiteSpace(result) ? string.Empty : result.Trim();
    }

    private string SanitizeDisplayName(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return "Bot";

        string trimmed = rawValue.Trim();

        if (trimmed.Length > 16)
            trimmed = trimmed.Substring(0, 16);

        return trimmed;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Easy Bot (Log)")]
    private void DebugGenerateEasy()
    {
        Generate(BotDifficulty.Easy);
    }

    [ContextMenu("Generate Medium Bot (Log)")]
    private void DebugGenerateMedium()
    {
        Generate(BotDifficulty.Medium);
    }

    [ContextMenu("Generate Hard Bot (Log)")]
    private void DebugGenerateHard()
    {
        Generate(BotDifficulty.Hard);
    }
#endif
}