using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Gestisce il flow globale del match:
/// start, pausa, halftime, overtime, end match e timer globale.
/// </summary>
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
    public TMP_Text matchTimerText;
    public bool useMinutesSecondsFormat = true;

    [Header("Halftime")]
    public bool enableHalftime = true;
    public bool pauseAtHalftime = true;

    [Header("Overtime")]
    public bool enableOvertime = true;
    public float overtimeDuration = 180f;

    [Header("Transition Delay")]
    [Tooltip("Attesa dopo la risoluzione dell'ultimo tiro prima di entrare in halftime o finire il match")]
    public float postShotTransitionDelay = 1f;

    [Header("Start Options")]
    public bool startMatchOnStart = true;

    [Header("Time Options")]
    public bool stopTimeOnPause = true;
    public bool stopTimeOnEnd = true;

    private float currentMatchTimer;

    private bool matchStarted = false;
    private bool matchEnded = false;
    private bool isPaused = false;
    private bool halftimeTriggered = false;
    private bool transitionCoroutineRunning = false;

    private bool halftimePending = false;
    private bool endOfTimePending = false;

    public float CurrentMatchTimer => currentMatchTimer;

    void Start()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

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

        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

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
    }

    void UpdateTimeLimitMode()
    {
        if (!halftimePending && !endOfTimePending)
        {
            currentMatchTimer -= Time.deltaTime;

            if (currentMatchTimer < 0f)
                currentMatchTimer = 0f;

            UpdateMatchTimerUI();
        }

        if (enableHalftime && !halftimeTriggered && !halftimePending && !scoreManager.IsOvertime)
        {
            float halftimeThreshold = matchDuration * 0.5f;
            if (currentMatchTimer <= halftimeThreshold)
            {
                halftimePending = true;
                currentMatchTimer = halftimeThreshold;
                UpdateMatchTimerUI();
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
            UpdateMatchTimerUI();
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
        }
    }

    public void StartMatch()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        if (scoreManager == null)
        {
            Debug.LogError("[StartEndController] ScoreManagerNew non trovato.", this);
            return;
        }

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

        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

        scoreManager.StartMatch();

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);
    }

    bool ShouldEnterHalftimeNow()
    {
        if (!enableHalftime)
            return false;

        if (halftimeTriggered)
            return false;

        if (scoreManager == null)
            return false;

        if (scoreManager.IsOvertime)
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

        yield return new WaitForSecondsRealtime(postShotTransitionDelay);

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

        if (scoreManager.IsOvertime)
        {
            RequestEndMatch(GetWinnerOrNone());
            return;
        }

        if (enableOvertime && scoreManager.ScoreP1 == scoreManager.ScoreP2)
        {
            StartOvertime();
            return;
        }

        RequestEndMatch(GetWinnerOrNone());
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
            turnManager.StartSpecificTurn(turnManager.player2);
    }

    public void StartOvertime()
    {
        if (scoreManager == null)
            return;

        currentMatchTimer = overtimeDuration;
        UpdateMatchTimerUI();

        halftimePending = false;
        endOfTimePending = false;

        scoreManager.BeginOvertime();
    }

    public void PauseMatch()
    {
        if (!matchStarted || matchEnded || isPaused)
            return;

        isPaused = true;

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
        endOfTimePending = false;

        if (scoreManager != null && scoreManager.MatchActive)
            scoreManager.EndMatch(winner);
        else
            FinalizeEndMatchUI();
    }

    void OnScoreManagerMatchEnd(PlayerID winner)
    {
        FinalizeEndMatchUI();
    }

    void FinalizeEndMatchUI()
    {
        if (matchEnded)
            return;

        matchEnded = true;
        isPaused = false;
        halftimePending = false;
        endOfTimePending = false;

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

        if (pausePanel != null) pausePanel.SetActive(false);
        if (halftimePanel != null) halftimePanel.SetActive(false);
        if (postGamePanel != null) postGamePanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

        Time.timeScale = 1f;

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);
    }

    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    void UpdateMatchTimerUI()
    {
        if (matchTimerText == null)
            return;

        if (useMinutesSecondsFormat)
        {
            int totalSeconds = Mathf.CeilToInt(currentMatchTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            matchTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
        else
        {
            matchTimerText.text = Mathf.CeilToInt(currentMatchTimer).ToString();
        }
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