using TMPro;
using UnityEngine;

public class FusionOnlineMatchHUD : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text turnOwnerText;
    [SerializeField] private TMP_Text turnTimerText;
    [SerializeField] private TMP_Text matchTimerText;
    [SerializeField] private TMP_Text player1ScoreText;
    [SerializeField] private TMP_Text player2ScoreText;

    [Header("Fallback Names")]
    [SerializeField] private string player1FallbackName = "Player 1";
    [SerializeField] private string player2FallbackName = "Player 2";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private FusionOnlineMatchController cachedController;

    private void Update()
    {
        ResolveController();

        if (cachedController == null)
            return;

        ApplyState(
            cachedController.CurrentTurnOwner,
            cachedController.CurrentTurnTimeRemaining,
            cachedController.CurrentMatchTimeRemaining,
            cachedController.ScoreP1,
            cachedController.ScoreP2,
            cachedController.MatchStarted,
            cachedController.MatchEnded
        );
    }

    private void ResolveController()
    {
        if (cachedController != null)
            return;

        OnlineGameplayAuthority authority = OnlineGameplayAuthority.Instance;
        if (authority != null && authority.OnlineMatchController != null)
        {
            cachedController = authority.OnlineMatchController;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        cachedController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        cachedController = FindObjectOfType<FusionOnlineMatchController>();
#endif
    }

    public void ApplyState(
        PlayerID currentTurnOwner,
        float turnTimeRemaining,
        float matchTimeRemaining,
        int scoreP1,
        int scoreP2,
        bool matchStarted,
        bool matchEnded)
    {
        if (turnTimerText != null)
            turnTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, turnTimeRemaining)).ToString();

        if (matchTimerText != null)
            matchTimerText.text = FormatClock(matchTimeRemaining);

        if (player1ScoreText != null)
            player1ScoreText.text = scoreP1.ToString();

        if (player2ScoreText != null)
            player2ScoreText.text = scoreP2.ToString();

        if (turnOwnerText != null)
        {
            switch (currentTurnOwner)
            {
                case PlayerID.Player1:
                    turnOwnerText.text = ResolvePlayer1Name();
                    break;

                case PlayerID.Player2:
                    turnOwnerText.text = ResolvePlayer2Name();
                    break;

                default:
                    turnOwnerText.text = string.Empty;
                    break;
            }
        }

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchHUD] ApplyState -> " +
                "TurnOwner=" + currentTurnOwner +
                " | TurnTime=" + turnTimeRemaining +
                " | MatchTime=" + matchTimeRemaining +
                " | ScoreP1=" + scoreP1 +
                " | ScoreP2=" + scoreP2,
                this
            );
        }
    }

    private string ResolvePlayer1Name()
    {
        OnlineGameplayAuthority authority = OnlineGameplayAuthority.Instance;
        if (authority == null)
            return player1FallbackName;

        if (authority.LocalPlayerId == PlayerID.Player1)
            return string.IsNullOrWhiteSpace(authority.LocalPlayerName) ? player1FallbackName : authority.LocalPlayerName;

        return string.IsNullOrWhiteSpace(authority.RemotePlayerName) ? player1FallbackName : authority.RemotePlayerName;
    }

    private string ResolvePlayer2Name()
    {
        OnlineGameplayAuthority authority = OnlineGameplayAuthority.Instance;
        if (authority == null)
            return player2FallbackName;

        if (authority.LocalPlayerId == PlayerID.Player2)
            return string.IsNullOrWhiteSpace(authority.LocalPlayerName) ? player2FallbackName : authority.LocalPlayerName;

        return string.IsNullOrWhiteSpace(authority.RemotePlayerName) ? player2FallbackName : authority.RemotePlayerName;
    }

    private string FormatClock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return minutes.ToString("00") + ":" + secs.ToString("00");
    }
}