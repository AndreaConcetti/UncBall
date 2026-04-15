using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnlineFlowController : MonoBehaviour
{
    public static OnlineFlowController Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private OnlineQueueRulesConfig queueRulesConfig;
    [SerializeField] private OnlineMatchRewardsConfig rewardsConfig;

    [Header("Bot Runtime Bridge")]
    [SerializeField] private BotSessionRuntime botSessionRuntime;
    [SerializeField] private BotOfflineMatchRuntime botOfflineMatchRuntime;

    [Header("Ranked Masked Bot Queue")]
    [SerializeField] private bool allowRankedMaskedBots = true;
    [SerializeField] private Vector2 rankedBotQueueDelaySeconds = new Vector2(18f, 35f);
    [SerializeField] private BotDifficulty rankedBotDifficulty = BotDifficulty.Medium;

    [Header("Normal Masked Bot Queue")]
    [SerializeField] private bool allowNormalMaskedBots = true;
    [SerializeField] private Vector2 normalBotQueueDelaySeconds = new Vector2(8f, 16f);
    [SerializeField] private BotDifficulty normalBotDifficulty = BotDifficulty.Easy;

    [Header("Masked Bot Side Randomization")]
    [SerializeField] private bool randomizeMaskedBotPlayerSlots = true;
    [SerializeField] private bool randomizeMaskedBotStartingSide = true;

    [Header("Config")]
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float matchFoundUiDelaySeconds = 2.0f;
    [SerializeField] private bool logDebug = true;

    [Header("Runtime")]
    [SerializeField] private OnlineRuntimeContext runtimeContext = new OnlineRuntimeContext();

    private IMatchmakingService matchmakingService;
    private IMatchSessionService matchSessionService;

    private CancellationTokenSource currentFlowCts;
    private bool initialized;
    private bool runnerEventsSubscribed;
    private bool localShutdownRequested;

    public OnlineRuntimeContext RuntimeContext => runtimeContext;
    public OnlineFlowState CurrentState => runtimeContext != null ? runtimeContext.state : OnlineFlowState.Offline;
    public float MatchFoundUiDelaySeconds => Mathf.Max(0f, matchFoundUiDelaySeconds);

    public OnlineMatchRewardsConfig RewardsConfig => rewardsConfig;

    public bool IsBusy =>
        CurrentState == OnlineFlowState.Queueing ||
        CurrentState == OnlineFlowState.MatchAssigned;

    public bool IsQueueSearchActive => CurrentState == OnlineFlowState.Queueing;

    public event Action<OnlineRuntimeContext> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }

        ResolveDependencies();
        InitializeServices();
        EnsureRuntime();
        SubscribeRunnerEvents();

        runtimeContext.ResetToIdle();
        NotifyStateChanged();
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (botSessionRuntime == null)
            botSessionRuntime = BotSessionRuntime.Instance;

        if (botOfflineMatchRuntime == null)
            botOfflineMatchRuntime = BotOfflineMatchRuntime.Instance;
    }

    private void InitializeServices()
    {
        if (initialized)
            return;

        if (queueRulesConfig == null)
            Debug.LogError("[OnlineFlowController] OnlineQueueRulesConfig missing.", this);

        if (rewardsConfig == null)
            Debug.LogWarning("[OnlineFlowController] OnlineMatchRewardsConfig is not assigned in inspector. Masked bot ranked/normal rewards will not work.", this);

        matchmakingService = new PhotonFusionMatchmakingService(
            runnerManager,
            queueRulesConfig,
            logDebug,
            allowRankedMaskedBots,
            rankedBotQueueDelaySeconds,
            rankedBotDifficulty,
            allowNormalMaskedBots,
            normalBotQueueDelaySeconds,
            normalBotDifficulty);

        matchSessionService = new FusionMatchSessionService(runnerManager);
        initialized = true;
    }

    private void EnsureRuntime()
    {
        if (runtimeContext == null)
            runtimeContext = new OnlineRuntimeContext();
    }

    private void SubscribeRunnerEvents()
    {
        if (runnerEventsSubscribed || runnerManager == null)
            return;

        runnerManager.OnShutdownEvent -= HandleRunnerShutdown;
        runnerManager.OnDisconnectedFromServerEvent -= HandleDisconnectedFromServer;
        runnerManager.OnConnectedToServerEvent -= HandleConnectedToServer;

        runnerManager.OnShutdownEvent += HandleRunnerShutdown;
        runnerManager.OnDisconnectedFromServerEvent += HandleDisconnectedFromServer;
        runnerManager.OnConnectedToServerEvent += HandleConnectedToServer;

        runnerEventsSubscribed = true;
    }

    public async void EnterQueue(QueueType queueType)
    {
        if (IsBusy)
            return;

        EnsureRuntime();
        ResolveDependencies();
        SubscribeRunnerEvents();

        if (profileManager == null)
        {
            runtimeContext.SetError("PlayerProfileManager missing.");
            NotifyStateChanged();
            return;
        }

        if (queueRulesConfig == null)
        {
            runtimeContext.SetError("OnlineQueueRulesConfig missing.");
            NotifyStateChanged();
            return;
        }

        CancelCurrentFlowSilently();
        currentFlowCts = new CancellationTokenSource();
        localShutdownRequested = false;

        ClearPreparedMaskedBotRuntime();

        OnlinePlayerIdentity localPlayer = new OnlinePlayerIdentity(
            profileManager.ActiveProfileId,
            GetOrCreateLocalOnlinePlayerId(),
            profileManager.ActiveDisplayName
        );

        runtimeContext.ResetMatchLifecycleFlags();
        runtimeContext.queueType = queueType;
        runtimeContext.currentAssignment = null;
        runtimeContext.currentSession = null;
        runtimeContext.lastError = string.Empty;
        runtimeContext.state = OnlineFlowState.Queueing;
        runtimeContext.statusMessage = queueType == QueueType.Ranked
            ? "Searching ranked match..."
            : "Searching normal match...";
        NotifyStateChanged();

        try
        {
            MatchAssignment assignment = await matchmakingService.EnqueueAsync(
                queueType,
                localPlayer,
                currentFlowCts.Token
            );

            if (assignment == null)
            {
                runtimeContext.SetError("Match assignment is null.");
                NotifyStateChanged();
                return;
            }

            PrepareMaskedBotRuntimeIfNeeded(assignment);

            runtimeContext.currentAssignment = assignment;
            runtimeContext.state = OnlineFlowState.MatchAssigned;
            runtimeContext.statusMessage = BuildMatchAssignedStatusMessage(assignment);
            NotifyStateChanged();

            float delay = Mathf.Max(0f, matchFoundUiDelaySeconds);
            if (delay > 0f)
                await Task.Delay(TimeSpan.FromSeconds(delay), currentFlowCts.Token);

            MatchSessionContext sessionContext = MatchSessionContext.FromAssignment(
                assignment,
                gameplaySceneName
            );

            runtimeContext.currentSession = sessionContext;
            runtimeContext.state = OnlineFlowState.JoiningSession;
            runtimeContext.statusMessage = BuildJoiningStatusMessage(sessionContext);
            NotifyStateChanged();

            bool loaded = await LoadGameplayForAssignmentAsync(
                sessionContext,
                currentFlowCts.Token
            );

            if (!loaded)
            {
                runtimeContext.SetError("Failed to load gameplay scene.");
                NotifyStateChanged();
                return;
            }

            sessionContext.isConnected = sessionContext.runtimeType == MatchRuntimeType.OnlineHuman;

            runtimeContext.state = OnlineFlowState.InMatch;
            runtimeContext.statusMessage =
                sessionContext.runtimeType == MatchRuntimeType.RankedMaskedBot
                    ? "Gameplay scene loaded. Starting ranked masked bot match..."
                    : sessionContext.runtimeType == MatchRuntimeType.NormalMaskedBot
                        ? "Gameplay scene loaded. Starting normal masked bot match..."
                        : "Gameplay scene loaded. Waiting validation...";

            NotifyStateChanged();

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineFlowController] EnterQueue completed -> " +
                    "QueueType=" + queueType +
                    " | MatchId=" + assignment.matchId +
                    " | Session=" + assignment.sessionName +
                    " | RuntimeType=" + assignment.runtimeType +
                    " | LocalIsHost=" + assignment.localIsHost +
                    " | LocalIsP1=" + assignment.localPlayerIsPlayer1 +
                    " | P1OnLeft=" + assignment.player1StartsOnLeft +
                    " | InitialTurnOwner=" + assignment.initialTurnOwner,
                    this);
            }
        }
        catch (OperationCanceledException)
        {
            ClearPreparedMaskedBotRuntime();
            runtimeContext.ResetToIdle();
            runtimeContext.statusMessage = "Queue cancelled.";
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            ClearPreparedMaskedBotRuntime();
            runtimeContext.SetError("EnterQueue exception: " + ex.Message);
            NotifyStateChanged();
        }
    }

    public async void CancelQueue()
    {
        if (CurrentState != OnlineFlowState.Queueing &&
            CurrentState != OnlineFlowState.MatchAssigned &&
            CurrentState != OnlineFlowState.JoiningSession)
        {
            return;
        }

        localShutdownRequested = true;

        try
        {
            CancelCurrentFlowSilently();

            if (matchmakingService != null)
                await matchmakingService.CancelAsync();

            if (matchSessionService != null && matchSessionService.HasActiveSession)
                await matchSessionService.ShutdownSessionAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineFlowController] CancelQueue exception: " + ex.Message, this);
        }
        finally
        {
            ClearPreparedMaskedBotRuntime();
            runtimeContext.ResetToIdle();
            runtimeContext.statusMessage = "Queue cancelled.";
            NotifyStateChanged();
        }
    }

    public async void ReturnToMenuFromMatch(bool clearSession = true)
    {
        if (CurrentState == OnlineFlowState.ReturningToMenu)
            return;

        localShutdownRequested = true;
        runtimeContext.state = OnlineFlowState.ReturningToMenu;
        runtimeContext.statusMessage = "Returning to menu...";
        NotifyStateChanged();

        CancelCurrentFlowSilently();

        try
        {
            if (matchSessionService != null)
                await matchSessionService.ShutdownSessionAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlineFlowController] ReturnToMenuFromMatch shutdown exception: " + ex.Message, this);
        }

        if (clearSession)
        {
            runtimeContext.currentAssignment = null;
            runtimeContext.currentSession = null;
        }

        ClearPreparedMaskedBotRuntime();

        runtimeContext.ResetToIdle();
        NotifyStateChanged();

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void NotifyMatchStarted()
    {
        runtimeContext.MarkGameplayValidated();
        runtimeContext.state = OnlineFlowState.InMatch;
        runtimeContext.statusMessage = "In match.";
        NotifyStateChanged();
    }

    public void NotifyMatchEnded()
    {
        runtimeContext.state = OnlineFlowState.EndingMatch;
        runtimeContext.statusMessage = "Match ended.";
        NotifyStateChanged();
    }

    public string GetResolvedPlayer1Name()
    {
        if (runtimeContext != null &&
            runtimeContext.currentSession != null &&
            !string.IsNullOrWhiteSpace(runtimeContext.currentSession.player1DisplayName))
        {
            return runtimeContext.currentSession.player1DisplayName.Trim();
        }

        return "Player 1";
    }

    public string GetResolvedPlayer2Name()
    {
        if (runtimeContext != null &&
            runtimeContext.currentSession != null &&
            !string.IsNullOrWhiteSpace(runtimeContext.currentSession.player2DisplayName))
        {
            return runtimeContext.currentSession.player2DisplayName.Trim();
        }

        return "Player 2";
    }

    public string GetResolvedPlayer1SkinId()
    {
        if (runtimeContext != null &&
            runtimeContext.currentSession != null &&
            !string.IsNullOrWhiteSpace(runtimeContext.currentSession.player1SkinUniqueId))
        {
            return runtimeContext.currentSession.player1SkinUniqueId.Trim();
        }

        return string.Empty;
    }

    public string GetResolvedPlayer2SkinId()
    {
        if (runtimeContext != null &&
            runtimeContext.currentSession != null &&
            !string.IsNullOrWhiteSpace(runtimeContext.currentSession.player2SkinUniqueId))
        {
            return runtimeContext.currentSession.player2SkinUniqueId.Trim();
        }

        return string.Empty;
    }

    public string GetResolvedLocalDisplayName()
    {
        if (runtimeContext != null &&
            runtimeContext.currentSession != null &&
            runtimeContext.currentSession.localPlayer != null &&
            !string.IsNullOrWhiteSpace(runtimeContext.currentSession.localPlayer.displayName))
        {
            return runtimeContext.currentSession.localPlayer.displayName.Trim();
        }

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return profileManager.ActiveDisplayName.Trim();

        return "Player";
    }

    private async Task<bool> LoadGameplayForAssignmentAsync(
        MatchSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        if (sessionContext == null)
            return false;

        runtimeContext.state = OnlineFlowState.LoadingGameplay;
        runtimeContext.statusMessage =
            sessionContext.runtimeType == MatchRuntimeType.RankedMaskedBot
                ? "Loading ranked gameplay..."
                : sessionContext.runtimeType == MatchRuntimeType.NormalMaskedBot
                    ? "Loading normal gameplay..."
                    : "Loading gameplay...";

        NotifyStateChanged();

        if (sessionContext.runtimeType == MatchRuntimeType.OnlineHuman)
        {
            if (matchSessionService == null)
                return false;

            return await matchSessionService.LoadGameplaySceneAsync(
                sessionContext,
                cancellationToken
            );
        }

        return await LoadSceneLocallyAsync(
            sessionContext.gameplaySceneName,
            cancellationToken
        );
    }

    private async Task<bool> LoadSceneLocallyAsync(
        string sceneName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
            return false;

        while (!operation.isDone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        return true;
    }

    private string BuildMatchAssignedStatusMessage(MatchAssignment assignment)
    {
        if (assignment == null)
            return "Match found.";

        if (assignment.runtimeType == MatchRuntimeType.RankedMaskedBot)
            return "Ranked match found.";

        if (assignment.runtimeType == MatchRuntimeType.NormalMaskedBot)
            return "Normal match found.";

        return "Match found.";
    }

    private string BuildJoiningStatusMessage(MatchSessionContext sessionContext)
    {
        if (sessionContext == null)
            return "Joining match session...";

        if (sessionContext.runtimeType == MatchRuntimeType.RankedMaskedBot)
            return "Preparing ranked match...";

        if (sessionContext.runtimeType == MatchRuntimeType.NormalMaskedBot)
            return "Preparing normal match...";

        return "Joining match session...";
    }

    private void PrepareMaskedBotRuntimeIfNeeded(MatchAssignment assignment)
    {
        if (assignment == null)
            return;

        bool isMaskedBotRuntime =
            assignment.runtimeType == MatchRuntimeType.RankedMaskedBot ||
            assignment.runtimeType == MatchRuntimeType.NormalMaskedBot;

        if (!isMaskedBotRuntime)
            return;

        ResolveDependencies();

        if (botSessionRuntime == null)
        {
            Debug.LogError("[OnlineFlowController] Masked bot runtime requested but BotSessionRuntime is missing.", this);
            return;
        }

        if (botOfflineMatchRuntime == null)
        {
            Debug.LogError("[OnlineFlowController] Masked bot runtime requested but BotOfflineMatchRuntime is missing.", this);
            return;
        }

        BotDifficulty difficulty = ParseBotDifficulty(assignment.botDifficultyId);

        OnlinePlayerMatchStatsSnapshot remoteStats = assignment.remotePlayerStats;
        if (remoteStats == null)
        {
            remoteStats = OnlinePlayerMatchStatsSnapshot.CreateDefault(
                assignment.remotePlayer != null ? assignment.remotePlayer.displayName : "Opponent",
                assignment.remotePlayer != null ? assignment.remotePlayer.onlinePlayerId : "bot_online",
                assignment.remotePlayer != null ? assignment.remotePlayer.profileId : "bot_profile");
            remoteStats.Normalize();
        }

        string botDisplayName = assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.displayName)
            ? assignment.remotePlayer.displayName.Trim()
            : remoteStats.GetDisplayNameOrFallback("Opponent");

        string botProfileId = !string.IsNullOrWhiteSpace(assignment.botProfileId)
            ? assignment.botProfileId.Trim()
            : (assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.profileId)
                ? assignment.remotePlayer.profileId.Trim()
                : "masked_bot");

        string botOnlinePlayerId = assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.onlinePlayerId)
            ? assignment.remotePlayer.onlinePlayerId.Trim()
            : (!string.IsNullOrWhiteSpace(remoteStats.onlinePlayerId) ? remoteStats.onlinePlayerId.Trim() : "bot_online");

        int totalMatches = Mathf.Max(0, remoteStats.totalMatches);
        int totalWins = Mathf.Clamp(remoteStats.totalWins, 0, totalMatches);
        int winRate = totalMatches > 0
            ? Mathf.Clamp(Mathf.RoundToInt((float)totalWins / totalMatches * 100f), 0, 100)
            : Mathf.Clamp(remoteStats.winRatePercent, 0, 100);

        int fakeLp = 0;
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                fakeLp = UnityEngine.Random.Range(850, 1051);
                break;
            case BotDifficulty.Medium:
                fakeLp = UnityEngine.Random.Range(1050, 1351);
                break;
            case BotDifficulty.Hard:
                fakeLp = UnityEngine.Random.Range(1350, 1801);
                break;
            case BotDifficulty.Unbeatable:
                fakeLp = UnityEngine.Random.Range(1700, 2601);
                break;
        }

        bool localPlayerIsPlayer1 = randomizeMaskedBotPlayerSlots
            ? UnityEngine.Random.value >= 0.5f
            : true;

        bool player1StartsOnLeft = randomizeMaskedBotStartingSide
            ? UnityEngine.Random.value >= 0.5f
            : true;

        PlayerID initialTurnOwner = player1StartsOnLeft ? PlayerID.Player1 : PlayerID.Player2;

        assignment.localPlayerIsPlayer1 = localPlayerIsPlayer1;
        assignment.player1StartsOnLeft = player1StartsOnLeft;
        assignment.initialTurnOwner = initialTurnOwner;

        string localSkinId = !string.IsNullOrWhiteSpace(assignment.player1SkinUniqueId)
            ? assignment.player1SkinUniqueId.Trim()
            : string.Empty;

        string botSkinId = !string.IsNullOrWhiteSpace(assignment.player2SkinUniqueId)
            ? assignment.player2SkinUniqueId.Trim()
            : string.Empty;

        string player1Skin = localPlayerIsPlayer1 ? localSkinId : botSkinId;
        string player2Skin = localPlayerIsPlayer1 ? botSkinId : localSkinId;

        assignment.player1SkinUniqueId = player1Skin;
        assignment.player2SkinUniqueId = player2Skin;

        BotProfileRuntimeData botProfile = new BotProfileRuntimeData(
            botId: botProfileId,
            displayName: botDisplayName,
            difficulty: difficulty,
            botArchetype: BotArchetype.Balanced,
            equippedSkinId: botSkinId,
            fakeRankedLp: fakeLp,
            fakeWinRate: winRate,
            fakeMatchesPlayed: totalMatches,
            fakeWins: totalWins,
            fakeLevel: Mathf.Max(1, remoteStats.level),
            avatarId: string.Empty,
            frameId: string.Empty,
            isEligibleForOnlineDisguise: true,
            isLocalBot: true
        );

        botSessionRuntime.SetCurrentBot(botProfile);

        string localDisplayName = assignment.localPlayer != null && !string.IsNullOrWhiteSpace(assignment.localPlayer.displayName)
            ? assignment.localPlayer.displayName.Trim()
            : GetResolvedLocalDisplayName();

        string localProfileId = assignment.localPlayer != null && !string.IsNullOrWhiteSpace(assignment.localPlayer.profileId)
            ? assignment.localPlayer.profileId.Trim()
            : (profileManager != null ? profileManager.ActiveProfileId : "local_player_1");

        BotOfflineMatchRequest request = new BotOfflineMatchRequest(
            requestId: string.IsNullOrWhiteSpace(assignment.matchId) ? Guid.NewGuid().ToString("N") : assignment.matchId.Trim(),
            difficulty: difficulty,
            localDisplayName: localDisplayName,
            botDisplayName: botDisplayName,
            localProfileId: localProfileId,
            botProfileId: botOnlinePlayerId,
            matchMode: assignment.matchMode,
            pointsToWin: Mathf.Max(1, assignment.pointsToWin),
            matchDurationSeconds: Mathf.Max(1f, assignment.matchDurationSeconds),
            turnDurationSeconds: Mathf.Max(1f, assignment.turnDurationSeconds),
            localPlayerIsPlayer1: localPlayerIsPlayer1,
            player1StartsOnLeft: player1StartsOnLeft,
            initialTurnOwner: initialTurnOwner,
            player1SkinUniqueId: player1Skin,
            player2SkinUniqueId: player2Skin,
            useDisguisedBotIdentity: true,
            createdOfflineWithoutInternet: false,
            randomSeed: UnityEngine.Random.Range(int.MinValue, int.MaxValue)
        );

        botOfflineMatchRuntime.SetRequest(request);

        if (logDebug)
        {
            bool localOnLeft = localPlayerIsPlayer1 ? player1StartsOnLeft : !player1StartsOnLeft;

            Debug.Log(
                "[OnlineFlowController] PrepareMaskedBotRuntimeIfNeeded -> " +
                "QueueType=" + assignment.queueType +
                " | RuntimeType=" + assignment.runtimeType +
                " | BotName=" + botDisplayName +
                " | Difficulty=" + difficulty +
                " | LocalPlayerIsP1=" + localPlayerIsPlayer1 +
                " | Player1StartsOnLeft=" + player1StartsOnLeft +
                " | LocalOnLeft=" + localOnLeft +
                " | InitialTurnOwner=" + initialTurnOwner,
                this);
        }
    }

    private void ClearPreparedMaskedBotRuntime()
    {
        ResolveDependencies();

        if (botSessionRuntime != null)
            botSessionRuntime.ClearCurrentBot();

        if (botOfflineMatchRuntime != null)
            botOfflineMatchRuntime.ClearRequest();
    }

    private BotDifficulty ParseBotDifficulty(string botDifficultyId)
    {
        if (string.IsNullOrWhiteSpace(botDifficultyId))
            return BotDifficulty.Medium;

        string trimmed = botDifficultyId.Trim();

        if (Enum.TryParse(trimmed, true, out BotDifficulty parsed))
            return parsed;

        return BotDifficulty.Medium;
    }

    private void HandleRunnerShutdown(ShutdownReason reason)
    {
        TryResolvePrematchHostForfeitWin("Host disconnected before gameplay start.");
    }

    private void HandleDisconnectedFromServer()
    {
        TryResolvePrematchHostForfeitWin("Host disconnected before gameplay start.");
    }

    private void HandleConnectedToServer()
    {
        localShutdownRequested = false;
    }

    private void TryResolvePrematchHostForfeitWin(string message)
    {
        if (localShutdownRequested)
            return;

        if (runtimeContext == null)
            return;

        if (runtimeContext.IsAnyBotRuntime)
            return;

        if (runtimeContext.currentAssignment == null)
            return;

        if (runtimeContext.currentAssignment.localIsHost)
            return;

        if (runtimeContext.state != OnlineFlowState.MatchAssigned &&
            runtimeContext.state != OnlineFlowState.JoiningSession &&
            runtimeContext.state != OnlineFlowState.LoadingGameplay)
        {
            return;
        }

        if (!runtimeContext.TryResolvePrematchHostForfeitWin(message))
            return;

        ApplyPrematchHostForfeitRewardsIfNeeded();

        runtimeContext.state = OnlineFlowState.EndingMatch;
        runtimeContext.statusMessage = message;
        NotifyStateChanged();

        if (logDebug)
            Debug.LogWarning("[OnlineFlowController] Prematch host forfeit win resolved.", this);
    }

    private void ApplyPrematchHostForfeitRewardsIfNeeded()
    {
        if (runtimeContext == null || runtimeContext.prematchHostForfeitRewardsApplied)
            return;

        if (runtimeContext.IsAnyBotRuntime)
        {
            runtimeContext.MarkPrematchHostForfeitRewardsApplied();
            return;
        }

        if (profileManager == null || rewardsConfig == null)
        {
            runtimeContext.MarkPrematchHostForfeitRewardsApplied();
            return;
        }

        OnlineRewardRule rule = rewardsConfig.GetRule(
            OnlineRewardCategory.PrematchHostLeftWin,
            runtimeContext.queueType
        );

        if (runtimeContext.queueType == QueueType.Ranked && rule.rankedLpDelta != 0)
            profileManager.AddRankedLp(rule.rankedLpDelta);

        runtimeContext.MarkPrematchHostForfeitRewardsApplied();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineFlowController] Prematch host-left rewards applied -> LP=" + rule.rankedLpDelta,
                this);
        }
    }

    private void CancelCurrentFlowSilently()
    {
        if (currentFlowCts == null)
            return;

        try
        {
            if (!currentFlowCts.IsCancellationRequested)
                currentFlowCts.Cancel();
        }
        catch
        {
        }

        currentFlowCts.Dispose();
        currentFlowCts = null;
    }

    private string GetOrCreateLocalOnlinePlayerId()
    {
        const string prefsKey = "ONLINE_LOCAL_PLAYER_ID_V2";

        string existing = PlayerPrefs.GetString(prefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing.Trim();

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(prefsKey, created);
        PlayerPrefs.Save();
        return created;
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(runtimeContext);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        CancelCurrentFlowSilently();
    }
}