using System;
using System.Threading;
using System.Threading.Tasks;
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

    [Header("Config")]
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool logDebug = true;

    [Header("Runtime")]
    [SerializeField] private OnlineRuntimeContext runtimeContext = new OnlineRuntimeContext();

    private IMatchmakingService matchmakingService;
    private IMatchSessionService matchSessionService;

    private CancellationTokenSource currentFlowCts;
    private bool initialized;

    public OnlineRuntimeContext RuntimeContext => runtimeContext;
    public OnlineFlowState CurrentState => runtimeContext != null ? runtimeContext.state : OnlineFlowState.Offline;

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

        matchmakingService = new LocalDevMatchmakingService();
        matchSessionService = new FusionMatchSessionService(runnerManager);

        initialized = true;
    }

    private void EnsureRuntime()
    {
        if (runtimeContext == null)
            runtimeContext = new OnlineRuntimeContext();
    }

    public async void EnterQueue(QueueType queueType)
    {
        if (IsBusy)
            return;

        EnsureRuntime();
        ResolveDependencies();

        if (profileManager == null)
        {
            runtimeContext.SetError("PlayerProfileManager missing.");
            NotifyStateChanged();
            return;
        }

        CancelCurrentFlowSilently();
        currentFlowCts = new CancellationTokenSource();

        OnlinePlayerIdentity localPlayer = new OnlinePlayerIdentity(
            profileManager.ActiveProfileId,
            GetOrCreateLocalOnlinePlayerId(),
            profileManager.ActiveDisplayName
        );

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

            MatchSessionContext sessionContext = MatchSessionContext.FromAssignment(
                assignment,
                gameplaySceneName
            );

            runtimeContext.currentSession = sessionContext;
            runtimeContext.state = OnlineFlowState.JoiningSession;
            runtimeContext.statusMessage = "Joining match session...";
            NotifyStateChanged();

            bool joined = await matchSessionService.JoinAssignedMatchAsync(
                sessionContext,
                currentFlowCts.Token
            );

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
            runtimeContext.statusMessage = "In match.";
            NotifyStateChanged();

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineFlowController] EnterQueue completed -> " +
                    "QueueType=" + queueType +
                    " | MatchId=" + assignment.matchId +
                    " | Session=" + assignment.sessionName +
                    " | LocalIsHost=" + assignment.localIsHost,
                    this
                );
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