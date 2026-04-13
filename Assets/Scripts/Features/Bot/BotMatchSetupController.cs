using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BotMatchSetupController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private BotProfileGenerator botProfileGenerator;
    [SerializeField] private BotSessionRuntime botSessionRuntime;
    [SerializeField] private BotOfflineMatchRuntime botOfflineMatchRuntime;

    [Header("Rules")]
    [SerializeField] private OfflineBotMatchRulesConfig offlineBotMatchRulesConfig;

    [Header("Identity Mode")]
    [Tooltip("False = offline visible bot label like BOT [EASY]. True = future disguised/random identity for online masked bots.")]
    [SerializeField] private bool useDisguisedIdentity = false;

    [Header("Offline Visible Labels")]
    [SerializeField] private string easyOfflineLabel = "BOT [EASY]";
    [SerializeField] private string mediumOfflineLabel = "BOT [MEDIUM]";
    [SerializeField] private string hardOfflineLabel = "BOT [HARD]";

    [Header("Optional Runtime Skin Id Pool")]
    [SerializeField] private List<string> runtimeAvailableSkinIds = new List<string>();

    [Header("Gameplay Skin Integration")]
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;
    [SerializeField] private BallSkinRandomGenerator ballSkinRandomGenerator;
    [SerializeField] private bool assignGeneratedBotSkinToPlayer2 = true;

    [Header("Offline Side Policy")]
    [SerializeField] private bool randomizeSidesForOfflineBot = true;

    [Header("Scene Flow")]
    [SerializeField] private bool autoLoadGameplayScene = true;
    [SerializeField] private string gameplaySceneName = "Gameplay";

    [Header("Default Generation")]
    [SerializeField] private BotDifficulty defaultDifficulty = BotDifficulty.Medium;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public BotProfileRuntimeData CurrentBotProfile =>
        botSessionRuntime != null ? botSessionRuntime.CurrentBotProfile : null;

    public OpponentPresentationProfile CurrentOpponentPresentation =>
        botSessionRuntime != null ? botSessionRuntime.CurrentOpponentPresentation : null;

    private void Awake()
    {
        ResolveOptionalDependencies();
    }

    public void CreateBotMatch()
    {
        CreateBotMatch(defaultDifficulty);
    }

    public void CreateBotMatch(BotDifficulty difficulty)
    {
        ResolveOptionalDependencies();

        if (botProfileGenerator == null)
        {
            Debug.LogError("[BotMatchSetupController] Missing BotProfileGenerator reference.", this);
            return;
        }

        if (botSessionRuntime == null)
        {
            Debug.LogError("[BotMatchSetupController] Missing BotSessionRuntime reference.", this);
            return;
        }

        if (botOfflineMatchRuntime == null)
        {
            Debug.LogError("[BotMatchSetupController] Missing BotOfflineMatchRuntime reference.", this);
            return;
        }

        if (offlineBotMatchRulesConfig == null)
        {
            Debug.LogError("[BotMatchSetupController] Missing OfflineBotMatchRulesConfig reference.", this);
            return;
        }

        botProfileGenerator.InjectRuntimeSkinIds(runtimeAvailableSkinIds);

        BotProfileRuntimeData generatedBot = botProfileGenerator.Generate(difficulty);
        if (generatedBot == null)
        {
            Debug.LogError("[BotMatchSetupController] Bot generation failed.", this);
            return;
        }

        BotProfileRuntimeData finalBot = useDisguisedIdentity
            ? generatedBot
            : CreateOfflineVisibleBot(generatedBot, difficulty);

        BallSkinData botSkin = null;
        if (assignGeneratedBotSkinToPlayer2)
        {
            botSkin = GenerateBotSkinForDifficulty(difficulty);
            AssignBotSkinToPlayer2(finalBot, botSkin);
        }

        botSessionRuntime.SetCurrentBot(finalBot);

        BotOfflineMatchRequest request = BuildOfflineMatchRequest(finalBot, botSkin, difficulty);
        botOfflineMatchRuntime.SetRequest(request);

        if (enableDebugLogs)
        {
            Debug.Log("[BotMatchSetupController] Offline bot match request created -> " + request, this);
        }

        if (autoLoadGameplayScene && !string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    public void InjectRuntimeSkinIds(IEnumerable<string> skinIds)
    {
        runtimeAvailableSkinIds.Clear();

        if (skinIds == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[BotMatchSetupController] InjectRuntimeSkinIds called with null. Runtime skin pool cleared.", this);
            }
            return;
        }

        foreach (string skinId in skinIds)
        {
            if (string.IsNullOrWhiteSpace(skinId))
                continue;

            string trimmed = skinId.Trim();
            if (!runtimeAvailableSkinIds.Contains(trimmed))
                runtimeAvailableSkinIds.Add(trimmed);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[BotMatchSetupController] Injected runtime skin id pool. Count={runtimeAvailableSkinIds.Count}", this);
        }
    }

    public void ClearBotSession()
    {
        if (botSessionRuntime != null)
            botSessionRuntime.ClearCurrentBot();

        if (botOfflineMatchRuntime != null)
            botOfflineMatchRuntime.ClearRequest();

        if (enableDebugLogs)
        {
            Debug.Log("[BotMatchSetupController] Cleared bot session and offline request.", this);
        }
    }

    private void ResolveOptionalDependencies()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        if (ballSkinRandomGenerator == null)
            ballSkinRandomGenerator = FindFirstObjectByType<BallSkinRandomGenerator>();

        if (botOfflineMatchRuntime == null)
            botOfflineMatchRuntime = BotOfflineMatchRuntime.Instance;
    }

    private BotProfileRuntimeData CreateOfflineVisibleBot(BotProfileRuntimeData source, BotDifficulty difficulty)
    {
        if (source == null)
            return null;

        return new BotProfileRuntimeData(
            botId: source.BotId,
            displayName: GetOfflineVisibleName(difficulty),
            difficulty: source.Difficulty,
            botArchetype: source.Archetype,
            equippedSkinId: source.EquippedSkinId,
            fakeRankedLp: source.FakeRankedLp,
            fakeWinRate: source.FakeWinRate,
            fakeMatchesPlayed: source.FakeMatchesPlayed,
            fakeWins: source.FakeWins,
            fakeLevel: source.FakeLevel,
            avatarId: source.AvatarId,
            frameId: source.FrameId,
            isEligibleForOnlineDisguise: source.IsEligibleForOnlineDisguise,
            isLocalBot: source.IsLocalBot);
    }

    private string GetOfflineVisibleName(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return string.IsNullOrWhiteSpace(easyOfflineLabel) ? "BOT [EASY]" : easyOfflineLabel.Trim();
            case BotDifficulty.Medium:
                return string.IsNullOrWhiteSpace(mediumOfflineLabel) ? "BOT [MEDIUM]" : mediumOfflineLabel.Trim();
            case BotDifficulty.Hard:
                return string.IsNullOrWhiteSpace(hardOfflineLabel) ? "BOT [HARD]" : hardOfflineLabel.Trim();
            default:
                return $"BOT [{difficulty.ToString().ToUpperInvariant()}]";
        }
    }

    private BotOfflineMatchRequest BuildOfflineMatchRequest(BotProfileRuntimeData bot, BallSkinData botSkin, BotDifficulty difficulty)
    {
        OfflineBotRuleSet rules = offlineBotMatchRulesConfig.GetRules(difficulty);
        if (rules == null)
        {
            Debug.LogError("[BotMatchSetupController] No offline rules found for difficulty=" + difficulty, this);
            return null;
        }

        string localDisplayName = ResolveLocalDisplayName();
        string localProfileId = ResolveLocalProfileId();
        string botDisplayName = bot != null ? bot.DisplayName : GetOfflineVisibleName(difficulty);
        string botProfileId = bot != null ? bot.BotId : "offline_bot";

        bool localPlayerIsPlayer1 = randomizeSidesForOfflineBot ? UnityEngine.Random.value >= 0.5f : true;
        bool player1StartsOnLeft = UnityEngine.Random.value >= 0.5f;

        // Offline bot rule:
        // - player or bot can be on the left randomly
        // - the LEFT side always starts
        PlayerID initialTurnOwner = player1StartsOnLeft ? PlayerID.Player1 : PlayerID.Player2;

        string localSkinId = ResolveLocalEquippedSkinId();
        string botSkinId = botSkin != null && !string.IsNullOrWhiteSpace(botSkin.skinUniqueId)
            ? botSkin.skinUniqueId.Trim()
            : string.Empty;

        string player1Skin = localPlayerIsPlayer1 ? localSkinId : botSkinId;
        string player2Skin = localPlayerIsPlayer1 ? botSkinId : localSkinId;

        return new BotOfflineMatchRequest(
            requestId: Guid.NewGuid().ToString("N"),
            difficulty: difficulty,
            localDisplayName: localDisplayName,
            botDisplayName: botDisplayName,
            localProfileId: localProfileId,
            botProfileId: botProfileId,
            matchMode: rules.matchMode,
            pointsToWin: rules.pointsToWin,
            matchDurationSeconds: rules.matchDurationSeconds,
            turnDurationSeconds: rules.turnDurationSeconds,
            localPlayerIsPlayer1: localPlayerIsPlayer1,
            player1StartsOnLeft: player1StartsOnLeft,
            initialTurnOwner: initialTurnOwner,
            player1SkinUniqueId: player1Skin,
            player2SkinUniqueId: player2Skin,
            useDisguisedBotIdentity: useDisguisedIdentity,
            createdOfflineWithoutInternet: Application.internetReachability == NetworkReachability.NotReachable,
            randomSeed: UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    }

    private void AssignBotSkinToPlayer2(BotProfileRuntimeData bot, BallSkinData generatedSkin)
    {
        if (playerSkinLoadout == null)
        {
            Debug.LogWarning("[BotMatchSetupController] PlayerSkinLoadout not found. Bot skin will not be assigned.", this);
            return;
        }

        if (generatedSkin == null)
        {
            Debug.LogWarning("[BotMatchSetupController] Generated bot skin is null.", this);
            return;
        }

        playerSkinLoadout.SetPlayer2ProfileId(bot != null ? bot.BotId : "bot_player_2");
        playerSkinLoadout.EquipSkinForPlayer2(generatedSkin);

        if (enableDebugLogs)
        {
            Debug.Log(
                "[BotMatchSetupController] Assigned generated bot skin to Player2 -> " +
                generatedSkin.skinUniqueId,
                this);
        }
    }

    private BallSkinData GenerateBotSkinForDifficulty(BotDifficulty difficulty)
    {
        if (ballSkinRandomGenerator == null)
        {
            Debug.LogWarning("[BotMatchSetupController] BallSkinRandomGenerator not found.", this);
            return null;
        }

        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return ballSkinRandomGenerator.GenerateGuaranteedCommonSkin();
            case BotDifficulty.Medium:
                return ballSkinRandomGenerator.GenerateGuaranteedRareSkin();
            case BotDifficulty.Hard:
                return ballSkinRandomGenerator.GenerateGuaranteedEpicSkin();
            default:
                return ballSkinRandomGenerator.GenerateRandomSkin();
        }
    }

    private string ResolveLocalDisplayName()
    {
        if (PlayerProfileManager.Instance != null && !string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.ActiveDisplayName))
            return PlayerProfileManager.Instance.ActiveDisplayName.Trim();

        return "PLAYER";
    }

    private string ResolveLocalProfileId()
    {
        if (PlayerProfileManager.Instance != null && !string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.ActiveProfileId))
            return PlayerProfileManager.Instance.ActiveProfileId.Trim();

        return "local_player_1";
    }

    private string ResolveLocalEquippedSkinId()
    {
        if (playerSkinLoadout == null)
            return string.Empty;

        BallSkinData localSkin = playerSkinLoadout.GetEquippedSkinForPlayer1();
        if (localSkin == null || string.IsNullOrWhiteSpace(localSkin.skinUniqueId))
            return string.Empty;

        return localSkin.skinUniqueId.Trim();
    }

#if UNITY_EDITOR
    [ContextMenu("Create Easy Bot Match")]
    private void DebugCreateEasy()
    {
        CreateBotMatch(BotDifficulty.Easy);
    }

    [ContextMenu("Create Medium Bot Match")]
    private void DebugCreateMedium()
    {
        CreateBotMatch(BotDifficulty.Medium);
    }

    [ContextMenu("Create Hard Bot Match")]
    private void DebugCreateHard()
    {
        CreateBotMatch(BotDifficulty.Hard);
    }
#endif
}
