using TMPro;
using UnityEngine;

public class GameModeUIChanger : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController onlineMatchController;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private BallLauncher launcher;

    [Header("Mode Objects")]
    [SerializeField] private GameObject[] timeModeObjects;
    [SerializeField] private GameObject[] scoreModeObjects;

    [Header("Texts")]
    [SerializeField] private TMP_Text[] targetScoreTexts;
    [SerializeField] private TMP_Text[] player1NameTexts;
    [SerializeField] private TMP_Text[] player2NameTexts;
    [SerializeField] private TMP_Text matchTimerText;
    [SerializeField] private TMP_Text[] winnerTexts;

    [Header("Turn Objects")]
    [SerializeField] private GameObject player1TurnActiveObject;
    [SerializeField] private GameObject player2TurnActiveObject;

    [Header("Launcher Phase")]
    [SerializeField] private GameObject placementPhaseObject;
    [SerializeField] private GameObject aimReadyPhaseObject;

    [Header("Fallback Config")]
    [SerializeField] private MatchMode fallbackMatchMode = MatchMode.ScoreTarget;
    [SerializeField] private int fallbackPointsToWin = 16;
    [SerializeField] private float fallbackMatchDurationSeconds = 180f;

    [Header("Config")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private string player1FallbackName = "Player 1";
    [SerializeField] private string player2FallbackName = "Player 2";
    [SerializeField] private string winnerSuffix = " Wins!";

    private void Start()
    {
        ResolveDependencies();

        if (applyOnStart)
            ApplyCurrentMode();

        RefreshAll();
    }

    private void Update()
    {
        RefreshAll();
    }

    public void ApplyCurrentMode()
    {
        ResolveDependencies();

        MatchMode mode = ResolveCurrentMatchMode();

        bool isTimeMode = mode == MatchMode.TimeLimit;
        bool isScoreMode = mode == MatchMode.ScoreTarget;

        SetObjectsActive(timeModeObjects, isTimeMode);
        SetObjectsActive(scoreModeObjects, isScoreMode);

        RefreshPlayerNameTexts();
        RefreshTargetScoreTexts();
    }

    public void RefreshAll()
    {
        RefreshPlayerNameTexts();
        RefreshTargetScoreTexts();
        RefreshTimerText();
        RefreshTurnActiveUI();
        RefreshLauncherPhaseUI();
    }

    public void RefreshPlayerNameTexts()
    {
        string p1 = GetPlayer1Name();
        string p2 = GetPlayer2Name();

        SetTextArray(player1NameTexts, p1);
        SetTextArray(player2NameTexts, p2);
    }

    public void RefreshTargetScoreTexts()
    {
        SetTextArray(targetScoreTexts, ResolvePointsToWin().ToString());
    }

    public void RefreshTimerText()
    {
        if (matchTimerText == null)
            return;

        ResolveDependencies();

        float seconds = 0f;

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
            seconds = onlineMatchController.CurrentMatchTimeRemaining;
        else
            seconds = ResolveConfiguredMatchDurationSeconds();

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;

        matchTimerText.text = minutes.ToString("00") + ":" + secs.ToString("00");
    }

    public void RefreshTurnActiveUI()
    {
        ResolveDependencies();

        PlayerID currentTurnOwner = PlayerID.None;

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
            currentTurnOwner = onlineMatchController.CurrentTurnOwner;
        else if (onlineGameplayAuthority != null)
            currentTurnOwner = onlineGameplayAuthority.CurrentTurnOwner;

        if (player1TurnActiveObject != null)
            player1TurnActiveObject.SetActive(currentTurnOwner == PlayerID.Player1);

        if (player2TurnActiveObject != null)
            player2TurnActiveObject.SetActive(currentTurnOwner == PlayerID.Player2);
    }

    public void RefreshLauncherPhaseUI()
    {
        if (launcher == null)
        {
            if (placementPhaseObject != null)
                placementPhaseObject.SetActive(false);

            if (aimReadyPhaseObject != null)
                aimReadyPhaseObject.SetActive(false);

            return;
        }

        if (placementPhaseObject != null)
            placementPhaseObject.SetActive(launcher.CurrentPhase == BallLauncher.LaunchPhase.Placement);

        if (aimReadyPhaseObject != null)
            aimReadyPhaseObject.SetActive(launcher.CurrentPhase == BallLauncher.LaunchPhase.AimReady);
    }

    public void RefreshWinnerTexts(PlayerID winner)
    {
        string winnerName;

        switch (winner)
        {
            case PlayerID.Player1:
                winnerName = GetPlayer1Name();
                break;
            case PlayerID.Player2:
                winnerName = GetPlayer2Name();
                break;
            default:
                winnerName = "Draw";
                break;
        }

        string finalText = winner == PlayerID.None
            ? winnerName
            : winnerName + winnerSuffix;

        SetTextArray(winnerTexts, finalText);
    }

    private string GetPlayer1Name()
    {
        ResolveDependencies();

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
        {
            return string.IsNullOrWhiteSpace(onlineMatchController.Player1DisplayName)
                ? player1FallbackName
                : onlineMatchController.Player1DisplayName;
        }

        if (onlineFlowController != null)
        {
            string resolved = onlineFlowController.GetResolvedPlayer1Name();
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return player1FallbackName;
    }

    private string GetPlayer2Name()
    {
        ResolveDependencies();

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
        {
            return string.IsNullOrWhiteSpace(onlineMatchController.Player2DisplayName)
                ? player2FallbackName
                : onlineMatchController.Player2DisplayName;
        }

        if (onlineFlowController != null)
        {
            string resolved = onlineFlowController.GetResolvedPlayer2Name();
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return player2FallbackName;
    }

    private MatchMode ResolveCurrentMatchMode()
    {
        ResolveDependencies();

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
            return onlineMatchController.CurrentMatchMode;

        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return onlineFlowController.RuntimeContext.currentSession.matchMode;
        }

        return fallbackMatchMode;
    }

    private int ResolvePointsToWin()
    {
        ResolveDependencies();

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
            return Mathf.Max(1, onlineMatchController.PointsToWin);

        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return Mathf.Max(1, onlineFlowController.RuntimeContext.currentSession.pointsToWin);
        }

        return Mathf.Max(1, fallbackPointsToWin);
    }

    private float ResolveConfiguredMatchDurationSeconds()
    {
        ResolveDependencies();

        if (onlineMatchController != null && onlineMatchController.IsNetworkStateReadable)
            return Mathf.Max(1f, onlineMatchController.ConfiguredMatchDuration);

        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return Mathf.Max(1f, onlineFlowController.RuntimeContext.currentSession.matchDurationSeconds);
        }

        return Mathf.Max(1f, fallbackMatchDurationSeconds);
    }

    private void ResolveDependencies()
    {
        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (onlineMatchController == null && onlineGameplayAuthority != null)
            onlineMatchController = onlineGameplayAuthority.OnlineMatchController;

#if UNITY_2023_1_OR_NEWER
        if (onlineMatchController == null)
            onlineMatchController = FindFirstObjectByType<FusionOnlineMatchController>();

        if (launcher == null)
            launcher = FindFirstObjectByType<BallLauncher>();
#else
        if (onlineMatchController == null)
            onlineMatchController = FindObjectOfType<FusionOnlineMatchController>();

        if (launcher == null)
            launcher = FindObjectOfType<BallLauncher>();
#endif
    }

    private void SetObjectsActive(GameObject[] objectsToSet, bool activeState)
    {
        if (objectsToSet == null)
            return;

        for (int i = 0; i < objectsToSet.Length; i++)
        {
            if (objectsToSet[i] != null)
                objectsToSet[i].SetActive(activeState);
        }
    }

    private void SetTextArray(TMP_Text[] texts, string value)
    {
        if (texts == null)
            return;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].text = value;
        }
    }
}