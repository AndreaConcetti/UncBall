using TMPro;
using UnityEngine;

public class FusionOnlineMatchHUD : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineFlowController onlineFlowController;

    [Header("Top HUD")]
    [SerializeField] private TMP_Text turnOwnerText;
    [SerializeField] private TMP_Text turnTimerText;

    [Header("Mode Info - Separate Layout")]
    [SerializeField] private GameObject timeModeInfoRoot;
    [SerializeField] private GameObject scoreModeInfoRoot;
    [SerializeField] private TMP_Text timeModeCountdownText;
    [SerializeField] private TMP_Text scoreModeTargetText;

    [Header("Mode Info - Shared Single Text")]
    [SerializeField] private TMP_Text sharedModeValueText;

    [Header("Bottom Score HUD")]
    [SerializeField] private TMP_Text player1ScoreText;
    [SerializeField] private TMP_Text player2ScoreText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;

    [Header("Panels")]
    [SerializeField] private GameObject gameUIPanel;
    [SerializeField] private GameObject halfTimePanel;
    [SerializeField] private GameObject halfPointPanel;
    [SerializeField] private GameObject postGamePanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Post Game Delay")]
    [SerializeField] private float postGamePanelShowDelaySeconds = 0f;

    [Header("Reconnect Overlay")]
    [SerializeField] private GameObject reconnectPanel;
    [SerializeField] private TMP_Text reconnectTitleText;
    [SerializeField] private TMP_Text reconnectMessageText;
    [SerializeField] private TMP_Text reconnectCountdownText;

    [Header("Reconnect Overlay Text")]
    [SerializeField] private string authorityDisconnectedSuffix = "DISCONNECTED";
    [SerializeField] private string authorityDisconnectedMessage = "WAITING FOR MATCH RESOLUTION...";
    [SerializeField] private string localDisconnectedTitle = "YOU DISCONNECTED FROM SERVER";
    [SerializeField] private string localDisconnectedMessage = "WAITING FOR MATCH RESOLUTION...";
    [SerializeField] private bool showReconnectTimerAsCountUp = true;

    [Header("Half Time Panel UI")]
    [SerializeField] private TMP_Text halfTimeCountdownText;
    [SerializeField] private TMP_Text halfTimePlayer1NameText;
    [SerializeField] private TMP_Text halfTimePlayer2NameText;
    [SerializeField] private TMP_Text halfTimePlayer1ScoreText;
    [SerializeField] private TMP_Text halfTimePlayer2ScoreText;

    [Header("Half Point Panel UI")]
    [SerializeField] private TMP_Text halfPointCountdownText;
    [SerializeField] private TMP_Text halfPointPlayer1NameText;
    [SerializeField] private TMP_Text halfPointPlayer2NameText;
    [SerializeField] private TMP_Text halfPointPlayer1ScoreText;
    [SerializeField] private TMP_Text halfPointPlayer2ScoreText;

    [Header("Endgame UI")]
    [SerializeField] private TMP_Text endgamePlayer1NameText;
    [SerializeField] private TMP_Text endgamePlayer2NameText;
    [SerializeField] private TMP_Text endgamePlayer1ScoreText;
    [SerializeField] private TMP_Text endgamePlayer2ScoreText;

    [Header("Game Result Texts")]
    [SerializeField] private TMP_Text gameResultWinnerText;
    [SerializeField] private TMP_Text gameResultLoserText;
    [SerializeField] private TMP_Text drawText;
    [SerializeField] private TMP_Text opponentDisconnectedText;

    [Header("Layout")]
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;

    [Header("Fallback Names")]
    [SerializeField] private string player1FallbackName = "Player 1";
    [SerializeField] private string player2FallbackName = "Player 2";

    [Header("Preview")]
    [SerializeField] private bool applySessionPreviewBeforeNetworkState = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool hasAppliedInitialLayout;
    private bool hasReceivedNetworkState;
    private bool previousMatchEnded;
    private bool postGameDelayActive;
    private float postGameDelayRemaining;
    private bool forcedPostGameRequested;
    private bool forcedShowOpponentDisconnectedText;
    private string forcedDisconnectedPlayerName = string.Empty;

    private void Awake()
    {
        ResolveDependencies();
        ResetPostGameDelayState();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (applySessionPreviewBeforeNetworkState)
            ApplySessionPreviewIfPossible();
    }

    private void Update()
    {
        if (postGameDelayActive)
        {
            postGameDelayRemaining -= Time.unscaledDeltaTime;
            if (postGameDelayRemaining <= 0f)
            {
                postGameDelayRemaining = 0f;
                postGameDelayActive = false;

                if (postGamePanel != null)
                    postGamePanel.SetActive(true);

                if (logDebug)
                    Debug.Log("[FusionOnlineMatchHUD] Post game panel shown after delay.", this);
            }
        }

        if (!applySessionPreviewBeforeNetworkState)
            return;

        if (hasReceivedNetworkState)
            return;

        ApplySessionPreviewIfPossible();
    }

    public void ApplyState(
        MatchMode matchMode,
        int pointsToWin,
        float configuredMatchDuration,
        PlayerID currentTurnOwner,
        float turnTimeRemaining,
        float matchTimeRemaining,
        int scoreP1,
        int scoreP2,
        bool matchStarted,
        bool matchEnded,
        bool midBreakActive,
        bool isTimeHalftime,
        bool isHalfPoint,
        float breakTimeRemaining,
        string player1Name,
        string player2Name,
        PlayerID winner,
        bool player1OnLeft,
        bool reconnectPending,
        PlayerID reconnectMissingPlayer,
        float reconnectTimeRemaining,
        OnlineMatchEndReason endReason)
    {
        hasReceivedNetworkState = true;

        string safeP1 = string.IsNullOrWhiteSpace(player1Name) ? player1FallbackName : player1Name;
        string safeP2 = string.IsNullOrWhiteSpace(player2Name) ? player2FallbackName : player2Name;

        if (bottomBarOrderSwapper != null)
        {
            if (!hasAppliedInitialLayout || bottomBarOrderSwapper.IsPlayer1OnLeft() != player1OnLeft)
                bottomBarOrderSwapper.SetOrder(player1OnLeft);

            hasAppliedInitialLayout = true;
        }

        bool isTimeMode = matchMode == MatchMode.TimeLimit;
        bool isScoreMode = matchMode == MatchMode.ScoreTarget;

        if (timeModeInfoRoot != null)
            timeModeInfoRoot.SetActive(isTimeMode);

        if (scoreModeInfoRoot != null)
            scoreModeInfoRoot.SetActive(isScoreMode);

        if (timeModeCountdownText != null)
            timeModeCountdownText.text = FormatClock(matchTimeRemaining);

        if (scoreModeTargetText != null)
            scoreModeTargetText.text = pointsToWin.ToString();

        if (sharedModeValueText != null)
            sharedModeValueText.text = isTimeMode ? FormatClock(matchTimeRemaining) : pointsToWin.ToString();

        bool showReconnectOverlay = reconnectPending && !forcedPostGameRequested && !matchEnded;
        bool showGameplayHud = matchStarted && !matchEnded && !midBreakActive && !showReconnectOverlay && !forcedPostGameRequested;

        if (gameUIPanel != null)
            gameUIPanel.SetActive(showGameplayHud);

        if (halfTimePanel != null)
            halfTimePanel.SetActive(isTimeHalftime && !matchEnded && !showReconnectOverlay && !forcedPostGameRequested);

        if (halfPointPanel != null)
            halfPointPanel.SetActive(isHalfPoint && !matchEnded && !showReconnectOverlay && !forcedPostGameRequested);

        if (!forcedPostGameRequested)
            HandlePostGamePanelVisibility(matchEnded);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        ApplyReconnectOverlay(
            showReconnectOverlay,
            reconnectMissingPlayer,
            reconnectTimeRemaining,
            safeP1,
            safeP2,
            endReason);

        if (turnOwnerText != null)
            turnOwnerText.text = currentTurnOwner == PlayerID.Player1 ? safeP1 : safeP2;

        if (turnTimerText != null)
            turnTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, turnTimeRemaining)).ToString();

        string bottomP1Score = FormatScoreForMode(matchMode, scoreP1, pointsToWin);
        string bottomP2Score = FormatScoreForMode(matchMode, scoreP2, pointsToWin);

        if (player1ScoreText != null)
            player1ScoreText.text = bottomP1Score;

        if (player2ScoreText != null)
            player2ScoreText.text = bottomP2Score;

        if (player1NameText != null)
            player1NameText.text = safeP1;

        if (player2NameText != null)
            player2NameText.text = safeP2;

        if (halfTimeCountdownText != null)
            halfTimeCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, breakTimeRemaining)).ToString();

        if (halfPointCountdownText != null)
            halfPointCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, breakTimeRemaining)).ToString();

        if (halfTimePlayer1NameText != null)
            halfTimePlayer1NameText.text = safeP1;

        if (halfTimePlayer2NameText != null)
            halfTimePlayer2NameText.text = safeP2;

        if (halfTimePlayer1ScoreText != null)
            halfTimePlayer1ScoreText.text = FormatScoreForMode(matchMode, scoreP1, pointsToWin);

        if (halfTimePlayer2ScoreText != null)
            halfTimePlayer2ScoreText.text = FormatScoreForMode(matchMode, scoreP2, pointsToWin);

        if (halfPointPlayer1NameText != null)
            halfPointPlayer1NameText.text = safeP1;

        if (halfPointPlayer2NameText != null)
            halfPointPlayer2NameText.text = safeP2;

        if (halfPointPlayer1ScoreText != null)
            halfPointPlayer1ScoreText.text = FormatScoreForMode(matchMode, scoreP1, pointsToWin);

        if (halfPointPlayer2ScoreText != null)
            halfPointPlayer2ScoreText.text = FormatScoreForMode(matchMode, scoreP2, pointsToWin);

        if (endgamePlayer1NameText != null)
            endgamePlayer1NameText.text = safeP1;

        if (endgamePlayer2NameText != null)
            endgamePlayer2NameText.text = safeP2;

        if (endgamePlayer1ScoreText != null)
            endgamePlayer1ScoreText.text = FormatScoreForMode(matchMode, scoreP1, pointsToWin);

        if (endgamePlayer2ScoreText != null)
            endgamePlayer2ScoreText.text = FormatScoreForMode(matchMode, scoreP2, pointsToWin);

        ApplyGameResultTexts(winner, safeP1, safeP2, endReason);

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchHUD] ApplyState -> " +
                "Mode=" + matchMode +
                " | P1=" + safeP1 +
                " | P2=" + safeP2 +
                " | MatchEnded=" + matchEnded +
                " | ReconnectPending=" + reconnectPending +
                " | ReconnectMissingPlayer=" + reconnectMissingPlayer +
                " | ReconnectTime=" + reconnectTimeRemaining.ToString("F2") +
                " | EndReason=" + endReason,
                this
            );
        }
    }

    public void ForceShowPostGame(
        MatchMode matchMode,
        int pointsToWin,
        string player1Name,
        string player2Name,
        int scoreP1,
        int scoreP2,
        PlayerID winner,
        OnlineMatchEndReason endReason,
        bool showOpponentDisconnectedExplicitly,
        string disconnectedPlayerName)
    {
        forcedPostGameRequested = true;
        forcedShowOpponentDisconnectedText = showOpponentDisconnectedExplicitly;
        forcedDisconnectedPlayerName = disconnectedPlayerName ?? string.Empty;
        hasReceivedNetworkState = true;

        string safeP1 = string.IsNullOrWhiteSpace(player1Name) ? player1FallbackName : player1Name;
        string safeP2 = string.IsNullOrWhiteSpace(player2Name) ? player2FallbackName : player2Name;

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);

        if (halfTimePanel != null)
            halfTimePanel.SetActive(false);

        if (halfPointPanel != null)
            halfPointPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (reconnectPanel != null)
            reconnectPanel.SetActive(false);

        if (endgamePlayer1NameText != null)
            endgamePlayer1NameText.text = safeP1;

        if (endgamePlayer2NameText != null)
            endgamePlayer2NameText.text = safeP2;

        if (endgamePlayer1ScoreText != null)
            endgamePlayer1ScoreText.text = FormatScoreForMode(matchMode, scoreP1, pointsToWin);

        if (endgamePlayer2ScoreText != null)
            endgamePlayer2ScoreText.text = FormatScoreForMode(matchMode, scoreP2, pointsToWin);

        ApplyGameResultTexts(winner, safeP1, safeP2, endReason);
        TriggerPostGamePanelVisibility();
    }

    private void ApplyReconnectOverlay(
        bool visible,
        PlayerID reconnectMissingPlayer,
        float reconnectTimeRemaining,
        string player1Name,
        string player2Name,
        OnlineMatchEndReason endReason)
    {
        if (reconnectPanel != null)
            reconnectPanel.SetActive(visible);

        if (!visible)
            return;

        string title;
        string message;

        if (reconnectMissingPlayer == PlayerID.None || endReason == OnlineMatchEndReason.DisconnectLoss)
        {
            title = localDisconnectedTitle;
            message = localDisconnectedMessage;
        }
        else
        {
            string missingName = ResolveReconnectMissingPlayerName(reconnectMissingPlayer, player1Name, player2Name);
            title = missingName.ToUpperInvariant() + " " + authorityDisconnectedSuffix;
            message = authorityDisconnectedMessage;
        }

        if (reconnectTitleText != null)
            reconnectTitleText.text = title;

        if (reconnectMessageText != null)
            reconnectMessageText.text = message;

        if (reconnectCountdownText != null)
        {
            float displayedValue = Mathf.Max(0f, reconnectTimeRemaining);
            reconnectCountdownText.text = displayedValue.ToString("F1") + "s";
        }
    }

    private string ResolveReconnectMissingPlayerName(PlayerID reconnectMissingPlayer, string player1Name, string player2Name)
    {
        if (reconnectMissingPlayer == PlayerID.Player1)
            return string.IsNullOrWhiteSpace(player1Name) ? player1FallbackName : player1Name;

        if (reconnectMissingPlayer == PlayerID.Player2)
            return string.IsNullOrWhiteSpace(player2Name) ? player2FallbackName : player2Name;

        return "OPPONENT";
    }

    private void TriggerPostGamePanelVisibility()
    {
        previousMatchEnded = true;

        float delay = Mathf.Max(0f, postGamePanelShowDelaySeconds);
        if (delay <= 0f)
        {
            postGameDelayActive = false;
            postGameDelayRemaining = 0f;

            if (postGamePanel != null)
                postGamePanel.SetActive(true);

            return;
        }

        postGameDelayActive = true;
        postGameDelayRemaining = delay;

        if (postGamePanel != null)
            postGamePanel.SetActive(false);
    }

    private void HandlePostGamePanelVisibility(bool matchEnded)
    {
        if (!matchEnded)
        {
            previousMatchEnded = false;
            postGameDelayActive = false;
            postGameDelayRemaining = 0f;

            if (postGamePanel != null)
                postGamePanel.SetActive(false);

            return;
        }

        if (!previousMatchEnded)
        {
            previousMatchEnded = true;

            float delay = Mathf.Max(0f, postGamePanelShowDelaySeconds);
            if (delay <= 0f)
            {
                postGameDelayActive = false;
                postGameDelayRemaining = 0f;

                if (postGamePanel != null)
                    postGamePanel.SetActive(true);
            }
            else
            {
                postGameDelayActive = true;
                postGameDelayRemaining = delay;

                if (postGamePanel != null)
                    postGamePanel.SetActive(false);
            }

            return;
        }

        if (!postGameDelayActive && postGamePanel != null)
            postGamePanel.SetActive(true);
    }

    private void ApplyGameResultTexts(
        PlayerID winner,
        string player1Name,
        string player2Name,
        OnlineMatchEndReason endReason)
    {
        bool isDraw = winner == PlayerID.None;

        if (isDraw)
        {
            SetTextVisible(gameResultWinnerText, false);
            SetTextVisible(gameResultLoserText, false);
            SetTextVisible(drawText, true);

            if (drawText != null)
                drawText.text = "DRAW";
        }
        else
        {
            string winnerName = winner == PlayerID.Player1 ? player1Name : player2Name;
            string loserName = winner == PlayerID.Player1 ? player2Name : player1Name;

            SetTextVisible(gameResultWinnerText, true);
            SetTextVisible(gameResultLoserText, true);
            SetTextVisible(drawText, false);

            if (gameResultWinnerText != null)
                gameResultWinnerText.text = winnerName.ToUpperInvariant() + " WIN";

            if (gameResultLoserText != null)
                gameResultLoserText.text = loserName.ToUpperInvariant() + " LOST";
        }

        bool showOpponentDisconnected =
            forcedPostGameRequested
                ? forcedShowOpponentDisconnectedText
                : endReason == OnlineMatchEndReason.DisconnectWin;

        SetTextVisible(opponentDisconnectedText, showOpponentDisconnected);

        if (showOpponentDisconnected && opponentDisconnectedText != null)
        {
            if (!string.IsNullOrWhiteSpace(forcedDisconnectedPlayerName))
                opponentDisconnectedText.text = forcedDisconnectedPlayerName.ToUpperInvariant() + " DISCONNECTED";
            else
                opponentDisconnectedText.text = "OPPONENT DISCONNECTED";
        }
    }

    private void SetTextVisible(TMP_Text textComponent, bool visible)
    {
        if (textComponent == null)
            return;

        textComponent.gameObject.SetActive(visible);
    }

    private void ApplySessionPreviewIfPossible()
    {
        ResolveDependencies();

        if (onlineFlowController == null)
            return;

        OnlineRuntimeContext runtime = onlineFlowController.RuntimeContext;
        if (runtime == null || runtime.currentSession == null)
            return;

        MatchSessionContext session = runtime.currentSession;

        string safeP1 = string.IsNullOrWhiteSpace(session.player1DisplayName) ? player1FallbackName : session.player1DisplayName.Trim();
        string safeP2 = string.IsNullOrWhiteSpace(session.player2DisplayName) ? player2FallbackName : session.player2DisplayName.Trim();

        MatchMode mode = session.matchMode;
        int pointsToWin = Mathf.Max(1, session.pointsToWin);
        float matchDuration = Mathf.Max(1f, session.matchDurationSeconds);
        float turnDuration = Mathf.Max(1f, session.turnDurationSeconds);

        bool isTimeMode = mode == MatchMode.TimeLimit;
        bool isScoreMode = mode == MatchMode.ScoreTarget;

        if (timeModeInfoRoot != null)
            timeModeInfoRoot.SetActive(isTimeMode);

        if (scoreModeInfoRoot != null)
            scoreModeInfoRoot.SetActive(isScoreMode);

        if (timeModeCountdownText != null)
            timeModeCountdownText.text = FormatClock(matchDuration);

        if (scoreModeTargetText != null)
            scoreModeTargetText.text = pointsToWin.ToString();

        if (sharedModeValueText != null)
            sharedModeValueText.text = isTimeMode ? FormatClock(matchDuration) : pointsToWin.ToString();

        if (turnOwnerText != null)
            turnOwnerText.text = safeP1;

        if (turnTimerText != null)
            turnTimerText.text = Mathf.CeilToInt(turnDuration).ToString();

        string previewP1Score = FormatScoreForMode(mode, 0, pointsToWin);
        string previewP2Score = FormatScoreForMode(mode, 0, pointsToWin);

        if (player1ScoreText != null)
            player1ScoreText.text = previewP1Score;

        if (player2ScoreText != null)
            player2ScoreText.text = previewP2Score;

        if (player1NameText != null)
            player1NameText.text = safeP1;

        if (player2NameText != null)
            player2NameText.text = safeP2;

        if (halfTimePlayer1NameText != null)
            halfTimePlayer1NameText.text = safeP1;

        if (halfTimePlayer2NameText != null)
            halfTimePlayer2NameText.text = safeP2;

        if (halfTimePlayer1ScoreText != null)
            halfTimePlayer1ScoreText.text = previewP1Score;

        if (halfTimePlayer2ScoreText != null)
            halfTimePlayer2ScoreText.text = previewP2Score;

        if (halfPointPlayer1NameText != null)
            halfPointPlayer1NameText.text = safeP1;

        if (halfPointPlayer2NameText != null)
            halfPointPlayer2NameText.text = safeP2;

        if (halfPointPlayer1ScoreText != null)
            halfPointPlayer1ScoreText.text = previewP1Score;

        if (halfPointPlayer2ScoreText != null)
            halfPointPlayer2ScoreText.text = previewP2Score;

        if (endgamePlayer1NameText != null)
            endgamePlayer1NameText.text = safeP1;

        if (endgamePlayer2NameText != null)
            endgamePlayer2NameText.text = safeP2;

        if (endgamePlayer1ScoreText != null)
            endgamePlayer1ScoreText.text = previewP1Score;

        if (endgamePlayer2ScoreText != null)
            endgamePlayer2ScoreText.text = previewP2Score;

        SetTextVisible(gameResultWinnerText, false);
        SetTextVisible(gameResultLoserText, false);
        SetTextVisible(drawText, false);
        SetTextVisible(opponentDisconnectedText, false);

        if (reconnectPanel != null)
            reconnectPanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(false);
    }

    private void ResetPostGameDelayState()
    {
        previousMatchEnded = false;
        postGameDelayActive = false;
        postGameDelayRemaining = 0f;
        forcedPostGameRequested = false;
        forcedShowOpponentDisconnectedText = false;
        forcedDisconnectedPlayerName = string.Empty;
    }

    private void ResolveDependencies()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

#if UNITY_2023_1_OR_NEWER
        if (onlineFlowController == null)
            onlineFlowController = FindFirstObjectByType<OnlineFlowController>();
#else
        if (onlineFlowController == null)
            onlineFlowController = FindObjectOfType<OnlineFlowController>();
#endif
    }

    private string FormatClock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return minutes.ToString("00") + ":" + secs.ToString("00");
    }

    private string FormatScoreForMode(MatchMode matchMode, int currentScore, int pointsToWin)
    {
        if (matchMode == MatchMode.ScoreTarget)
            return currentScore + " / " + Mathf.Max(1, pointsToWin);

        return currentScore.ToString();
    }
}