using TMPro;
using UnityEngine;

public class FusionOnlineMatchHUD : MonoBehaviour
{
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
    [SerializeField] private TMP_Text winnerNameText;

    [Header("Layout")]
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;

    [Header("Fallback Names")]
    [SerializeField] private string player1FallbackName = "Player 1";
    [SerializeField] private string player2FallbackName = "Player 2";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool hasAppliedInitialLayout;

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
        bool player1OnLeft)
    {
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

        if (gameUIPanel != null)
            gameUIPanel.SetActive(matchStarted && !matchEnded && !midBreakActive);

        if (halfTimePanel != null)
            halfTimePanel.SetActive(isTimeHalftime && !matchEnded);

        if (halfPointPanel != null)
            halfPointPanel.SetActive(isHalfPoint && !matchEnded);

        if (postGamePanel != null)
            postGamePanel.SetActive(matchEnded);

        if (pausePanel != null)
            pausePanel.SetActive(false);

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

        if (winnerNameText != null)
        {
            switch (winner)
            {
                case PlayerID.Player1:
                    winnerNameText.text = safeP1;
                    break;
                case PlayerID.Player2:
                    winnerNameText.text = safeP2;
                    break;
                default:
                    winnerNameText.text = "DRAW";
                    break;
            }
        }

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchHUD] ApplyState -> " +
                "Mode=" + matchMode +
                " | SharedModeText=" + (sharedModeValueText != null ? sharedModeValueText.text : "NULL") +
                " | ScoreP1=" + bottomP1Score +
                " | ScoreP2=" + bottomP2Score,
                this
            );
        }
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