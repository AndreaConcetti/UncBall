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

    [Header("References")]
    public ScoreManagerNew scoreManager;
    public TurnManager turnManager;
    public BallTurnSpawner ballTurnSpawner;
    public SimpleCurrentScoreDisplay simpleCurrentScoreDisplay;
    public BottomBarOrderSwapper bottomBarOrderSwapper;
    public GameModeUIChanger gameModeUIChanger;
    public RewardManager rewardManager;
    public MatchRuntimeConfig matchRuntimeConfig;
    public PlayerXPRewardService xpRewardService;
    public PlayerProfileManager profileManager;

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
    [Tooltip("In TimeLimit, dopo che il reset di halftime è già avvenuto, termina subito se un player è matematicamente irraggiungibile.")]
    public bool enableMathematicalWinInTimeMode = true;

    [Tooltip("Se tutte le board sono piene: in prima metà della modalità TimeLimit avvia subito l'halftime; altrimenti chiude la partita.")]
    public bool endOrAdvanceWhenAllBoardsFull = true;

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

    public float CurrentMatchTimer => currentMatchTimer;

    void Start()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindFirstObjectByType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindFirstObjectByType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindObjectOfType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindObjectOfType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

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

        currentMatchTimer = matchDuration;
        RefreshModeUI();

        if (startMatchOnStart)
            StartMatch();
    }

    void OnEnable()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (scoreManager != null)
            scoreManager.onMatchEnd.AddListener(OnScoreManagerMatchEnd);
    }

    void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.onMatchEnd.RemoveListener(OnScoreManagerMatchEnd);
    }

    void Update()
    {
        if (!matchStarted || matchEnded || isPaused || scoreManager == null)
            return;

        if (matchMode == MatchMode.TimeLimit)
            UpdateTimeLimitMode();

        if (matchMode == MatchMode.ScoreTarget)
            UpdateScoreTargetMode();

        UpdateUniversalBoardAndMathRules();
    }

    void ApplyRuntimeConfig()
    {
        if (matchRuntimeConfig == null)
            return;

        matchMode = matchRuntimeConfig.SelectedMatchMode;
        targetScore = Mathf.Max(1, matchRuntimeConfig.SelectedPointsToWin);
        matchDuration = Mathf.Max(1f, matchRuntimeConfig.SelectedMatchDuration);
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
        {
            RequestEndMatch(mathematicalWinner);
        }
    }

    public void StartMatch()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindFirstObjectByType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindFirstObjectByType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (gameModeUIChanger == null)
            gameModeUIChanger = FindObjectOfType<GameModeUIChanger>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (xpRewardService == null)
            xpRewardService = FindObjectOfType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (scoreManager == null)
        {
            Debug.LogError("[StartEndController] ScoreManagerNew non trovato.", this);
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

        currentMatchTimer = matchDuration;
        RefreshModeUI();

        if (rewardManager != null)
            rewardManager.BeginNewMatchRewardCycle();

        scoreManager.StartMatch();

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);

        if (turnManager != null)
            turnManager.ResumeTimer();

        if (logDebug)
        {
            Debug.Log(
                "[StartEndController] StartMatch -> " +
                "GameMode=" + (matchRuntimeConfig != null ? matchRuntimeConfig.SelectedGameMode.ToString() : "null") +
                " | MatchMode=" + matchMode,
                this
            );
        }
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
        if (transitionCoroutineRunning)
            return;

        StartCoroutine(DelayedResolvedShotTransition());
    }

    public bool ShouldDelayProgressionAfterResolvedShot()
    {
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
        if (endMatchFinalizationStarted || matchEnded)
        {
            if (logDebug)
                Debug.Log("[StartEndController] RequestEndMatch ignored, finalization already started.", this);

            return;
        }

        endOfTimePending = false;
        resolvedWinner = winner;

        if (scoreManager != null && scoreManager.MatchActive)
        {
            if (logDebug)
                Debug.Log("[StartEndController] RequestEndMatch -> forwarding to ScoreManager.EndMatch", this);

            scoreManager.EndMatch(winner);
        }
        else
        {
            if (gameModeUIChanger != null)
                gameModeUIChanger.RefreshWinnerTexts(winner);

            FinalizeEndMatchUI();
        }
    }

    void OnScoreManagerMatchEnd(PlayerID winner)
    {
        if (endMatchFinalizationStarted || matchEnded)
        {
            if (logDebug)
                Debug.Log("[StartEndController] OnScoreManagerMatchEnd ignored, finalization already started.", this);

            return;
        }

        resolvedWinner = winner;

        if (gameModeUIChanger != null)
            gameModeUIChanger.RefreshWinnerTexts(winner);

        FinalizeEndMatchUI();
    }

    void FinalizeEndMatchUI()
    {
        if (endMatchFinalizationStarted || matchEnded)
        {
            if (logDebug)
                Debug.Log("[StartEndController] FinalizeEndMatchUI ignored, already processing.", this);

            return;
        }

        endMatchFinalizationStarted = true;

        if (logDebug)
        {
            Debug.Log(
                "[StartEndController] FinalizeEndMatchUI START -> Winner=" + resolvedWinner +
                " | GameMode=" + (matchRuntimeConfig != null ? matchRuntimeConfig.SelectedGameMode.ToString() : "null") +
                " | MatchMode=" + matchMode,
                this
            );
        }

        if (!matchRewardProcessed)
        {
            if (rewardManager == null)
                rewardManager = RewardManager.Instance;

            if (rewardManager != null)
                rewardManager.TryGrantMatchWinReward(resolvedWinner);

            matchRewardProcessed = true;
        }

#if UNITY_2023_1_OR_NEWER
        if (xpRewardService == null)
            xpRewardService = FindFirstObjectByType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (xpRewardService == null)
            xpRewardService = FindObjectOfType<PlayerXPRewardService>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif

        if (!matchXpProcessed && xpRewardService != null)
        {
            xpRewardService.TryGrantMatchCompletionRewards(resolvedWinner, PlayerID.Player1);
            matchXpProcessed = true;
        }

        if (!matchStatsProcessed && profileManager != null && matchRuntimeConfig != null)
        {
            bool localPlayerWon = resolvedWinner == PlayerID.Player1;

            profileManager.RegisterMatchResult(
                matchRuntimeConfig.SelectedGameMode,
                matchRuntimeConfig.SelectedMatchMode,
                localPlayerWon,
                treatCurrentMatchAsRanked && matchRuntimeConfig.SelectedGameMode == MatchRuntimeConfig.GameMode.Multiplayer
            );

            matchStatsProcessed = true;

            if (logDebug)
            {
                Debug.Log(
                    "[StartEndController] Match stats registered ONCE for local profile. " +
                    "LocalPlayer=Player1" +
                    " | LocalWin=" + localPlayerWon,
                    this
                );
            }
        }

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
        Time.timeScale = 1f;
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

    public bool IsMatchStarted() => matchStarted;
    public bool IsMatchEnded() => matchEnded;
    public bool IsPaused() => isPaused;
}