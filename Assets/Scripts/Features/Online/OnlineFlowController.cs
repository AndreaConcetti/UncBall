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

    public bool IsBusy =>
        CurrentState == OnlineFlowState.Queueing ||
        CurrentState == OnlineFlowState.JoiningSession ||
        CurrentState == OnlineFlowState.LoadingGameplay ||
        CurrentState == OnlineFlowState.ReturningToMenu;

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
    }

    private void InitializeServices()
    {
        if (initialized)
            return;

        if (queueRulesConfig == null)
            Debug.LogError("[OnlineFlowController] OnlineQueueRulesConfig missing.", this);

        matchmakingService = new PhotonFusionMatchmakingService(runnerManager, queueRulesConfig, logDebug);
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

            runtimeContext.currentAssignment = assignment;
            runtimeContext.state = OnlineFlowState.MatchAssigned;
            runtimeContext.statusMessage = "Match found.";
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
            runtimeContext.statusMessage = "Joining match session...";
            NotifyStateChanged();

            bool joined = true;

            if (!joined)
            {
                runtimeContext.SetError("Failed to join assigned match session.");
                NotifyStateChanged();
                return;
            }

            sessionContext.isConnected = true;

            runtimeContext.state = OnlineFlowState.LoadingGameplay;
            runtimeContext.statusMessage = "Loading gameplay...";
            NotifyStateChanged();

            bool loaded = await matchSessionService.LoadGameplaySceneAsync(
                sessionContext,
                currentFlowCts.Token
            );

            if (!loaded)
            {
                runtimeContext.SetError("Failed to load gameplay scene.");
                NotifyStateChanged();
                return;
            }

            runtimeContext.state = OnlineFlowState.InMatch;
            runtimeContext.statusMessage = "Gameplay scene loaded. Waiting validation...";
            NotifyStateChanged();

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineFlowController] EnterQueue completed -> " +
                    "QueueType=" + queueType +
                    " | MatchId=" + assignment.matchId +
                    " | Session=" + assignment.sessionName +
                    " | LocalIsHost=" + assignment.localIsHost,
                    this);
            }
        }
        catch (OperationCanceledException)
        {
            runtimeContext.ResetToIdle();
            runtimeContext.statusMessage = "Queue cancelled.";
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
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