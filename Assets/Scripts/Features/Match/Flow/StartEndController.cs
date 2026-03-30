using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartEndController : MonoBehaviour
{
    public enum MatchMode
    {
        ScoreTarget,
        TimeLimit
    }

    public static StartEndController Instance { get; private set; }

    [Header("References")]
    public ScoreManager scoreManager;
    public TurnManager turnManager;
    public BallTurnSpawner ballTurnSpawner;
    public SimpleCurrentScoreDisplay simpleCurrentScoreDisplay;
    public BottomBarOrderSwapper bottomBarOrderSwapper;
    public GameModeUIChanger gameModeUIChanger;
    public RewardManager rewardManager;
    public MatchRuntimeConfig matchRuntimeConfig;
    public PlayerXPRewardService xpRewardService;
    public PlayerProfileManager profileManager;
    public OnlineGameplayAuthority onlineGameplayAuthority;
    public PhotonFusionSessionController fusionSessionController;
    public PhotonFusionRunnerManager runnerManager;
    public OnlineMatchSession onlineMatchSession;
    public FusionMatchState fusionMatchState;

    [Header("UI Panels")]
    public GameObject gameUIPanel;
    public GameObject pausePanel;
    public GameObject halftimePanel;
    public GameObject postGamePanel;

    [Header("Match Mode")]
    public MatchMode matchMode = MatchMode.ScoreTarget;

    [Header("Score Target Mode")]
    public int targetScore = 16;

    [Header("Time Limit Mode")]
    public float matchDuration = 180f;

    [Header("Future Multiplayer Flags")]
    [SerializeField] private bool treatCurrentMatchAsRanked = false;

    [Header("Halftime")]
    public bool enableHalftime = true;
    public bool pauseAtHalftime = true;

    [Header("Transition Delay")]
    public float postShotTransitionDelay = 1f;

    [Header("Start Options")]
    public bool startMatchOnStart = true;

    [Header("Time Options")]
    public bool stopTimeOnPause = true;
    public bool stopTimeOnEnd = true;

    [Header("Early Finish Rules")]
    public bool enableMathematicalWinInTimeMode = true;
    public bool endOrAdvanceWhenAllBoardsFull = true;

    [Header("Online Exit")]
    [SerializeField] private bool shutdownFusionRunnerBeforeMainMenu = true;
    [SerializeField] private bool clearPreparedOnlineSessionOnMainMenuReturn = true;

    [Header("Safe Reward Execution")]
    [SerializeField] private bool protectEndMatchFlowFromRewardExceptions = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private float currentMatchTimer;

    private bool matchStarted = false;
    private bool matchEnded = false;
    private bool isPaused = false;
    private bool halftimeTriggered = false;
    private bool transitionCoroutineRunning = false;

    private bool halftimePending = false;
    private bool endOfTimePending = false;

    private PlayerID resolvedWinner = PlayerID.None;
    private bool matchRewardProcessed = false;
    private bool matchXpProcessed = false;
    private bool matchStatsProcessed = false;
    private bool endMatchFinalizationStarted = false;
    private bool returnToMenuRequested = false;

    public float CurrentMatchTimer => currentMatchTimer;

    public static StartEndController InstanceOrFind()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<StartEndController>();
#else
        return FindObjectOfType<StartEndController>();
#endif
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResolveDependencies();
        ApplyRuntimeConfig();

        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        matchStarted = false;
        matchEnded = false;
        isPaused = false;
        halftimeTriggered = false;
        transitionCoroutineRunning = false;
        halftimePending = false;
        endOfTimePending = false;
        resolvedWinner = PlayerID.None;
        matchRewardProcessed = false;
        matchXpProcessed = false;
        matchStatsProcessed = false;
        endMatchFinalizationStarted = false;
        returnToMenuRequested = false;

        currentMatchTimer = matchDuration;
        RefreshModeUI();

        if (startMatchOnStart && !IsRuntimeOnlineMode())
            StartMatch();
    }

    void OnEnable()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (scoreManager != null)
            scoreManager.onMatchEnd.AddListener(OnScoreManagerMatchEnd);
    }

    void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.onMatchEnd.RemoveListener(OnScoreManagerMatchEnd);
    }

    void ResolveDependencies()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (fusionSessionController == null)
            fusionSessionController = PhotonFusionSessionController.Instance;

        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

#if UNITY_2023_1_OR_NEWER
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindFirstObjectByType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindFirstObjectByType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();

        if (fusionSessionController == null)
            fusionSessionController = FindFirstObjectByType<PhotonFusionSessionController>();

        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (fusionMatchState == null)
            fusionMatchState = FindFirstObjectByType<FusionMatchState>();
#else
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindObjectOfType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindObjectOfType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindObjectOfType<OnlineGameplayAuthority>();

        if (fusionSessionController == null)
            fusionSessionController = FindObjectOfType<PhotonFusionSessionController>();

        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (fusionMatchState == null)
            fusionMatchState = FindObjectOfType<FusionMatchState>();
#endif
    }

    void ApplyRuntimeConfig()
    {
        if (matchRuntimeConfig == null)
            return;

        matchMode = matchRuntimeConfig.SelectedMatchMode;
        targetScore = Mathf.Max(1, matchRuntimeConfig.SelectedPointsToWin);
        matchDuration = Mathf.Max(1f, matchRuntimeConfig.SelectedMatchDuration);
    }

    bool IsRuntimeOnlineMode()
    {
        if (onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession)
            return true;

        if (matchRuntimeConfig != null && matchRuntimeConfig.IsOnlineMatch)
            return true;

        if (runnerManager != null && runnerManager.IsRunning)
            return true;

        return false;
    }

    bool ShouldRunAuthoritativeMatchFlow()
    {
        if (IsRuntimeOnlineMode())
            return false;

        return true;
    }

    void RefreshModeUI()
    {
        if (gameModeUIChanger == null)
            return;

        gameModeUIChanger.ApplyCurrentMode();
        gameModeUIChanger.RefreshPlayerNameTexts();
        gameModeUIChanger.RefreshTargetScoreTexts();
        gameModeUIChanger.RefreshTimerText();
    }

    void Update()
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (!matchStarted || matchEnded || isPaused || scoreManager == null)
            return;

        if (matchMode == MatchMode.TimeLimit)
            UpdateTimeLimitMode();

        if (matchMode == MatchMode.ScoreTarget)
            UpdateScoreTargetMode();

        UpdateUniversalBoardAndMathRules();
    }

    void UpdateTimeLimitMode()
    {
        if (!halftimePending && !endOfTimePending)
        {
            currentMatchTimer -= Time.deltaTime;

            if (currentMatchTimer < 0f)
                currentMatchTimer = 0f;

            if (gameModeUIChanger != null)
                gameModeUIChanger.RefreshTimerText();
        }

        if (enableHalftime && !halftimeTriggered && !halftimePending)
        {
            float halftimeThreshold = matchDuration * 0.5f;
            if (currentMatchTimer <= halftimeThreshold)
            {
                halftimePending = true;
                currentMatchTimer = halftimeThreshold;

                if (gameModeUIChanger != null)
                    gameModeUIChanger.RefreshTimerText();
            }
        }

        if (halftimePending)
        {
            if (turnManager != null && turnManager.IsLaunchInProgress())
                return;

            StartHalftime();
            return;
        }

        if (!endOfTimePending && currentMatchTimer <= 0f)
        {
            endOfTimePending = true;
            currentMatchTimer = 0f;

            if (gameModeUIChanger != null)
                gameModeUIChanger.RefreshTimerText();
        }

        if (endOfTimePending)
        {
            if (turnManager != null && turnManager.IsLaunchInProgress())
                return;

            ExecuteEndOfTimeRule();
        }
    }

    void UpdateScoreTargetMode()
    {
        if (scoreManager.ScoreP1 >= targetScore || scoreManager.ScoreP2 >= targetScore)
        {
            if (turnManager != null && turnManager.IsLaunchInProgress())
                return;

            RequestEndMatch(GetWinnerOrNone());
            return;
        }

        if (endOrAdvanceWhenAllBoardsFull && scoreManager.AreAllBoardsFull())
        {
            if (turnManager != null && turnManager.IsLaunchInProgress())
                return;

            RequestEndMatch(GetWinnerOrNone());
        }
    }

    void UpdateUniversalBoardAndMathRules()
    {
        if (scoreManager == null || matchEnded || isPaused)
            return;

        if (turnManager != null && turnManager.IsLaunchInProgress())
            return;

        if (endOrAdvanceWhenAllBoardsFull && scoreManager.AreAllBoardsFull())
        {
            ResolveAllBoardsFullCondition();
            return;
        }

        if (TryGetMathematicalWinnerInTimeMode(out PlayerID mathematicalWinner))
            RequestEndMatch(mathematicalWinner);
    }

    public void StartMatch()
    {
        ResolveDependencies();

        if (IsRuntimeOnlineMode())
        {
            if (logDebug)
                Debug.Log("[StartEndController] StartMatch ignored because online runtime is active.", this);

            return;
        }

        if (scoreManager == null)
        {
            Debug.LogError("[StartEndController] ScoreManager non trovato.", this);
            return;
        }

        ApplyRuntimeConfig();

        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        isPaused = false;
        matchEnded = false;
        matchStarted = true;
        halftimeTriggered = false;
        transitionCoroutineRunning = false;
        halftimePending = false;
        endOfTimePending = false;
        resolvedWinner = PlayerID.None;
        matchRewardProcessed = false;
        matchXpProcessed = false;
        matchStatsProcessed = false;
        endMatchFinalizationStarted = false;
        returnToMenuRequested = false;

        currentMatchTimer = matchDuration;
        RefreshModeUI();

        if (rewardManager != null)
            rewardManager.BeginNewMatchRewardCycle();

        scoreManager.StartMatch();

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);

        if (turnManager != null)
            turnManager.ResumeTimer();
    }

    bool ShouldEnterHalftimeNow()
    {
        if (!enableHalftime)
            return false;

        if (halftimeTriggered)
            return false;

        if (scoreManager == null)
            return false;

        if (matchMode != MatchMode.TimeLimit)
            return false;

        float halftimeThreshold = matchDuration * 0.5f;
        return currentMatchTimer <= halftimeThreshold;
    }

    public void HandleResolvedShotTransition()
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (transitionCoroutineRunning)
            return;

        StartCoroutine(DelayedResolvedShotTransition());
    }

    public bool ShouldDelayProgressionAfterResolvedShot()
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return false;

        if (transitionCoroutineRunning)
            return true;

        if (halftimePending || endOfTimePending)
            return true;

        if (scoreManager != null)
        {
            if (endOrAdvanceWhenAllBoardsFull && scoreManager.AreAllBoardsFull())
                return true;

            if (TryGetMathematicalWinnerInTimeMode(out _))
                return true;
        }

        if (ShouldEnterHalftimeNow())
            return true;

        if (matchMode == MatchMode.TimeLimit && currentMatchTimer <= 0f)
            return true;

        if (matchMode == MatchMode.ScoreTarget &&
            scoreManager != null &&
            (scoreManager.ScoreP1 >= targetScore || scoreManager.ScoreP2 >= targetScore))
            return true;

        return false;
    }

    IEnumerator DelayedResolvedShotTransition()
    {
        transitionCoroutineRunning = true;

        float elapsed = 0f;

        while (elapsed < postShotTransitionDelay)
        {
            if (!isPaused && !matchEnded)
                elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        if (scoreManager != null && endOrAdvanceWhenAllBoardsFull && scoreManager.AreAllBoardsFull())
        {
            ResolveAllBoardsFullCondition();
            transitionCoroutineRunning = false;
            yield break;
        }

        if (TryGetMathematicalWinnerInTimeMode(out PlayerID mathematicalWinner))
        {
            RequestEndMatch(mathematicalWinner);
            transitionCoroutineRunning = false;
            yield break;
        }

        if (halftimePending)
        {
            StartHalftime();
            transitionCoroutineRunning = false;
            yield break;
        }

        if (endOfTimePending)
        {
            ExecuteEndOfTimeRule();
            transitionCoroutineRunning = false;
            yield break;
        }

        if (ShouldEnterHalftimeNow())
        {
            StartHalftime();
            transitionCoroutineRunning = false;
            yield break;
        }

        if (matchMode == MatchMode.TimeLimit && currentMatchTimer <= 0f)
        {
            ExecuteEndOfTimeRule();
            transitionCoroutineRunning = false;
            yield break;
        }

        if (matchMode == MatchMode.ScoreTarget &&
            scoreManager != null &&
            (scoreManager.ScoreP1 >= targetScore || scoreManager.ScoreP2 >= targetScore))
        {
            RequestEndMatch(GetWinnerOrNone());
            transitionCoroutineRunning = false;
            yield break;
        }

        transitionCoroutineRunning = false;
    }

    void ExecuteEndOfTimeRule()
    {
        endOfTimePending = false;
        RequestEndMatch(GetWinnerOrNone());
    }

    void ResolveAllBoardsFullCondition()
    {
        if (scoreManager == null)
            return;

        bool shouldForceImmediateHalftime =
            matchMode == MatchMode.TimeLimit &&
            enableHalftime &&
            !halftimeTriggered &&
            !scoreManager.IsHalftime;

        if (shouldForceImmediateHalftime)
        {
            halftimePending = false;
            StartHalftime();
            return;
        }

        RequestEndMatch(GetWinnerOrNone());
    }

    bool TryGetMathematicalWinnerInTimeMode(out PlayerID winner)
    {
        winner = PlayerID.None;

        if (!enableMathematicalWinInTimeMode)
            return false;

        if (scoreManager == null)
            return false;

        if (matchMode != MatchMode.TimeLimit)
            return false;

        if (!matchStarted || matchEnded || isPaused)
            return false;

        if (scoreManager.IsHalftime)
            return false;

        if (enableHalftime && !halftimeTriggered)
            return false;

        int maxAdditionalP1 = scoreManager.GetMaxAdditionalPointsAvailable(PlayerID.Player1);
        int maxAdditionalP2 = scoreManager.GetMaxAdditionalPointsAvailable(PlayerID.Player2);

        int absoluteMaxP1 = scoreManager.ScoreP1 + maxAdditionalP1;
        int absoluteMaxP2 = scoreManager.ScoreP2 + maxAdditionalP2;

        if (scoreManager.ScoreP1 > absoluteMaxP2)
        {
            winner = PlayerID.Player1;
            return true;
        }

        if (scoreManager.ScoreP2 > absoluteMaxP1)
        {
            winner = PlayerID.Player2;
            return true;
        }

        return false;
    }

    public void StartHalftime()
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (!matchStarted || matchEnded || halftimeTriggered || scoreManager == null)
            return;

        halftimeTriggered = true;
        halftimePending = false;

        scoreManager.BeginHalftime();

        if (turnManager != null)
            turnManager.SuspendTurnForHalftime();

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.SwapPlayerSides();
            ballTurnSpawner.ClearAllBallsInScene();
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SwapOrder();

        isPaused = true;

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);

        if (halftimePanel != null)
            halftimePanel.SetActive(true);

        if (simpleCurrentScoreDisplay != null)
            simpleCurrentScoreDisplay.RefreshScores();

        if (pauseAtHalftime && stopTimeOnPause)
            Time.timeScale = 0f;
    }

    public void EndHalftimeAndResume()
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (scoreManager == null || !scoreManager.IsHalftime)
            return;

        scoreManager.EndHalftime();

        if (halftimePanel != null)
            halftimePanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);

        isPaused = false;
        Time.timeScale = 1f;

        if (turnManager != null)
        {
            turnManager.ResumeTimer();
            turnManager.StartSpecificTurn(turnManager.player2);
        }

        RefreshModeUI();
    }

    public void PauseMatch()
    {
        if (!matchStarted || matchEnded || isPaused)
            return;

        if (scoreManager != null && scoreManager.IsHalftime)
            return;

        isPaused = true;

        if (turnManager != null)
            turnManager.PauseTimer();

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (stopTimeOnPause)
            Time.timeScale = 0f;
    }

    public void ResumeMatch()
    {
        if (!matchStarted || matchEnded || !isPaused)
            return;

        if (scoreManager != null && scoreManager.IsHalftime)
            return;

        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;

        if (turnManager != null)
            turnManager.ResumeTimer();
    }

    public void TogglePause()
    {
        if (isPaused)
            ResumeMatch();
        else
            PauseMatch();
    }

    public void RequestEndMatch(PlayerID winner)
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (endMatchFinalizationStarted || matchEnded)
            return;

        endOfTimePending = false;
        resolvedWinner = winner;

        if (scoreManager != null && scoreManager.MatchActive)
            scoreManager.EndMatch(winner);
        else
            FinalizeEndMatchUI();
    }

    void OnScoreManagerMatchEnd(PlayerID winner)
    {
        if (!ShouldRunAuthoritativeMatchFlow())
            return;

        if (endMatchFinalizationStarted || matchEnded)
            return;

        resolvedWinner = winner;

        if (gameModeUIChanger != null)
            gameModeUIChanger.RefreshWinnerTexts(winner);

        FinalizeEndMatchUI();
    }

    void FinalizeEndMatchUI()
    {
        if (endMatchFinalizationStarted || matchEnded)
            return;

        endMatchFinalizationStarted = true;

        matchEnded = true;
        isPaused = false;
        halftimePending = false;
        endOfTimePending = false;

        if (turnManager != null)
            turnManager.PauseTimer();

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(true);

        if (simpleCurrentScoreDisplay != null)
            simpleCurrentScoreDisplay.RefreshScores();

        if (stopTimeOnEnd)
            Time.timeScale = 0f;

        TryGrantChestRewardsSafe();
        TryGrantXpRewardsSafe();
        TryRegisterStatsSafe();
    }

    void TryGrantChestRewardsSafe()
    {
        if (matchRewardProcessed || !ShouldGrantChestRewards())
            return;

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (rewardManager == null)
            return;

        try
        {
            rewardManager.TryGrantMatchWinReward(resolvedWinner);
            matchRewardProcessed = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[StartEndController] Chest reward exception during end match: " + ex, this);
        }
    }

    void TryGrantXpRewardsSafe()
    {
        if (matchXpProcessed || !ShouldGrantXpRewards())
            return;

        ResolveDependencies();

        if (xpRewardService == null)
            return;

        try
        {
            xpRewardService.TryGrantMatchCompletionRewards(resolvedWinner, GetResolvedLocalPlayerId());
            matchXpProcessed = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[StartEndController] XP reward exception during end match: " + ex, this);
        }
    }

    void TryRegisterStatsSafe()
    {
        if (matchStatsProcessed || !ShouldGrantStatsProgression())
            return;

        ResolveDependencies();

        if (profileManager == null || matchRuntimeConfig == null)
            return;

        bool localPlayerWon = resolvedWinner == GetResolvedLocalPlayerId();

        try
        {
            profileManager.RegisterMatchResult(
                matchRuntimeConfig.SelectedGameMode,
                matchRuntimeConfig.SelectedMatchMode,
                localPlayerWon,
                IsCurrentMatchRanked()
            );

            matchStatsProcessed = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[StartEndController] Stats registration exception during end match: " + ex, this);
        }
    }

    public void ResetUIState()
    {
        matchStarted = false;
        matchEnded = false;
        isPaused = false;
        halftimeTriggered = false;
        transitionCoroutineRunning = false;
        halftimePending = false;
        endOfTimePending = false;
        resolvedWinner = PlayerID.None;
        matchRewardProcessed = false;
        matchXpProcessed = false;
        matchStatsProcessed = false;
        endMatchFinalizationStarted = false;
        returnToMenuRequested = false;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        currentMatchTimer = matchDuration;
        RefreshModeUI();

        Time.timeScale = 1f;

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);

        if (turnManager != null)
            turnManager.ResumeTimer();

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (rewardManager != null)
            rewardManager.BeginNewMatchRewardCycle();
    }

    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        if (returnToMenuRequested)
            return;

        returnToMenuRequested = true;
        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        ResolveDependencies();

        Time.timeScale = 1f;

        bool shouldShutdownFusion =
            shutdownFusionRunnerBeforeMainMenu &&
            matchRuntimeConfig != null &&
            matchRuntimeConfig.IsOnlineMatch &&
            runnerManager != null &&
            runnerManager.HasActiveRunner;

        if (shouldShutdownFusion)
        {
            if (fusionSessionController != null)
            {
                fusionSessionController.ShutdownSession();

                float timeout = 3f;
                float elapsed = 0f;

                while (runnerManager != null && runnerManager.HasActiveRunner && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        if (clearPreparedOnlineSessionOnMainMenuReturn && onlineMatchSession != null)
            onlineMatchSession.ClearPreparedSession();

        SceneManager.LoadScene("MainMenu");
    }

    PlayerID GetWinnerOrNone()
    {
        if (scoreManager == null)
            return PlayerID.None;

        if (scoreManager.ScoreP1 > scoreManager.ScoreP2)
            return PlayerID.Player1;

        if (scoreManager.ScoreP2 > scoreManager.ScoreP1)
            return PlayerID.Player2;

        return PlayerID.None;
    }

    private PlayerID GetResolvedLocalPlayerId()
    {
        if (matchRuntimeConfig == null)
            return PlayerID.Player1;

        return matchRuntimeConfig.GetResolvedLocalPlayerId();
    }

    private bool IsCurrentMatchRanked()
    {
        if (matchRuntimeConfig != null)
            return matchRuntimeConfig.SelectedIsRanked;

        return treatCurrentMatchAsRanked;
    }

    private bool ShouldGrantChestRewards()
    {
        if (matchRuntimeConfig == null)
            return true;

        return matchRuntimeConfig.SelectedAllowChestRewards;
    }

    private bool ShouldGrantXpRewards()
    {
        if (matchRuntimeConfig == null)
            return true;

        return matchRuntimeConfig.SelectedAllowXpRewards;
    }

    private bool ShouldGrantStatsProgression()
    {
        if (matchRuntimeConfig == null)
            return true;

        return matchRuntimeConfig.SelectedAllowStatsProgression;
    }

    private string GetSafeGameMode()
    {
        return matchRuntimeConfig != null ? matchRuntimeConfig.SelectedGameMode.ToString() : "null";
    }

    public void StartMatchOnlineReplicaSafe()
    {
        ResolveDependencies();
        ApplyRuntimeConfig();

        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        matchStarted = true;
        matchEnded = false;
        isPaused = false;
        halftimeTriggered = false;
        transitionCoroutineRunning = false;
        halftimePending = false;
        endOfTimePending = false;
        resolvedWinner = PlayerID.None;
        matchRewardProcessed = false;
        matchXpProcessed = false;
        matchStatsProcessed = false;
        endMatchFinalizationStarted = false;
        returnToMenuRequested = false;

        currentMatchTimer = matchDuration;
        RefreshModeUI();
    }

    public void ApplyOnlineReplicaState(bool replicatedStarted, bool replicatedEnded)
    {
        if (!IsRuntimeOnlineMode())
            return;

        matchStarted = replicatedStarted;
        matchEnded = replicatedEnded;

        if (replicatedEnded)
        {
            isPaused = false;

            if (pausePanel != null) pausePanel.SetActive(false);
            if (halftimePanel != null) halftimePanel.SetActive(false);
            if (gameUIPanel != null) gameUIPanel.SetActive(false);
            if (postGamePanel != null) postGamePanel.SetActive(true);
        }
        else
        {
            if (postGamePanel != null) postGamePanel.SetActive(false);
            if (gameUIPanel != null) gameUIPanel.SetActive(true);
        }
    }

    public bool IsMatchStarted() => matchStarted;
    public bool IsMatchEnded() => matchEnded;
    public bool IsPaused() => isPaused;
}