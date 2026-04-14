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
    [SerializeField] private string unbeatableOfflineLabel = "BOT [UNBEATABLE]";

    [Header("Optional Runtime Skin Id Pool")]
    [SerializeField] private List<string> runtimeAvailableSkinIds = new List<string>();

    [Header("Gameplay Skin Integration")]
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;
    [SerializeField] private BallSkinRandomGenerator ballSkinRandomGenerator;
    [SerializeField] private bool assignGeneratedBotSkin = true;

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

        BallSkinData generatedBotSkin = null;
        if (assignGeneratedBotSkin)
            generatedBotSkin = GenerateBotSkinForDifficulty(difficulty);

        BotOfflineMatchRequest request = BuildOfflineMatchRequest(finalBot, generatedBotSkin, difficulty);
        if (request == null)
        {
            Debug.LogError("[BotMatchSetupController] Failed to build offline request.", this);
            return;
        }

        ApplyOfflineSkinOwnershipSnapshot(request, generatedBotSkin);
        botSessionRuntime.SetCurrentBot(finalBot);
        botOfflineMatchRuntime.SetRequest(request);

        if (enableDebugLogs)
            Debug.Log("[BotMatchSetupController] Offline bot match request created -> " + request, this);

        if (autoLoadGameplayScene && !string.IsNullOrWhiteSpace(gameplaySceneName))
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void InjectRuntimeSkinIds(IEnumerable<string> skinIds)
    {
        runtimeAvailableSkinIds.Clear();

        if (skinIds == null)
            return;

        foreach (string skinId in skinIds)
        {
            if (string.IsNullOrWhiteSpace(skinId))
                continue;

            string trimmed = skinId.Trim();
            if (!runtimeAvailableSkinIds.Contains(trimmed))
                runtimeAvailableSkinIds.Add(trimmed);
        }

        if (enableDebugLogs)
            Debug.Log($"[BotMatchSetupController] Injected runtime skin id pool. Count={runtimeAvailableSkinIds.Count}", this);
    }

    public void ClearBotSession()
    {
        if (botSessionRuntime != null)
            botSessionRuntime.ClearCurrentBot();

        if (botOfflineMatchRuntime != null)
            botOfflineMatchRuntime.ClearRequest();

        if (enableDebugLogs)
            Debug.Log("[BotMatchSetupController] Cleared bot session and offline request.", this);
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

            case BotDifficulty.Unbeatable:
                return string.IsNullOrWhiteSpace(unbeatableOfflineLabel) ? "BOT [UNBEATABLE]" : unbeatableOfflineLabel.Trim();

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

    private void ApplyOfflineSkinOwnershipSnapshot(BotOfflineMatchRequest request, BallSkinData generatedBotSkin)
    {
        if (playerSkinLoadout == null || request == null)
        {
            Debug.LogWarning("[BotMatchSetupController] Cannot apply offline skin snapshot. Missing loadout or request.", this);
            return;
        }

        BallSkinData localSkin = playerSkinLoadout.GetEquippedSkinForProfile(request.LocalProfileId);
        if (localSkin == null)
            localSkin = playerSkinLoadout.GetEquippedSkinForPlayer1();

        BallSkinData botSkin = generatedBotSkin;

        string player1ProfileId = request.LocalPlayerIsPlayer1 ? request.LocalProfileId : request.BotProfileId;
        string player2ProfileId = request.LocalPlayerIsPlayer1 ? request.BotProfileId : request.LocalProfileId;

        BallSkinData player1Skin = request.LocalPlayerIsPlayer1 ? localSkin : botSkin;
        BallSkinData player2Skin = request.LocalPlayerIsPlayer1 ? botSkin : localSkin;

        playerSkinLoadout.ApplyMatchSnapshot(
            player1ProfileId,
            player1Skin,
            player2ProfileId,
            player2Skin
        );

        if (enableDebugLogs)
        {
            Debug.Log(
                "[BotMatchSetupController] ApplyOfflineSkinOwnershipSnapshot -> " +
                "P1Profile=" + player1ProfileId +
                " | P1Skin=" + GetSafeSkinId(player1Skin) +
                " | P2Profile=" + player2ProfileId +
                " | P2Skin=" + GetSafeSkinId(player2Skin),
                this
            );
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

            case BotDifficulty.Unbeatable:
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

        BallSkinData localSkin = playerSkinLoadout.GetEquippedSkinForProfile(ResolveLocalProfileId());
        if (localSkin == null)
            localSkin = playerSkinLoadout.GetEquippedSkinForPlayer1();

        if (localSkin == null || string.IsNullOrWhiteSpace(localSkin.skinUniqueId))
            return string.Empty;

        return localSkin.skinUniqueId.Trim();
    }

    private string GetSafeSkinId(BallSkinData skin)
    {
        return skin != null ? skin.skinUniqueId : "none";
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

    [ContextMenu("Create Unbeatable Bot Match")]
    private void DebugCreateUnbeatable()
    {
        CreateBotMatch(BotDifficulty.Unbeatable);
    }
#endif
}