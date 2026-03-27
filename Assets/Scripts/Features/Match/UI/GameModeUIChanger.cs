using UnityEngine;
using TMPro;

public class GameModeUIChanger : MonoBehaviour
{
    [Header("References")]
    public StartEndController startEndController;
    public TurnManager turnManager;
    public BallLauncher launcher;
    public MatchRuntimeConfig matchRuntimeConfig;
    public TutorialPromptSettings tutorialPromptSettings;

    [Header("Auto Apply")]
    public bool applyOnStart = true;

    [Header("Objects for Time Mode")]
    public GameObject[] timeModeObjects;

    [Header("Objects for Score Mode")]
    public GameObject[] scoreModeObjects;

    [Header("Score Target Texts")]
    public TMP_Text[] targetScoreTexts;

    [Header("Timer Text")]
    public TMP_Text matchTimerText;
    public bool useMinutesSecondsFormat = true;

    [Header("Player Name Texts")]
    public TMP_Text[] player1NameTexts;
    public TMP_Text[] player2NameTexts;

    [Header("Turn Active UI")]
    public GameObject player1TurnActiveObject;
    public GameObject player2TurnActiveObject;

    [Header("Launcher Phase UI")]
    [Tooltip("Attivo solo durante la fase di placement della ball")]
    public GameObject placementPhaseObject;

    [Tooltip("Attivo solo durante la fase di aim dopo il lock del placement")]
    public GameObject aimReadyPhaseObject;

    [Header("Tutorial Prompt Toggle UI")]
    [Tooltip("Oggetto visibile quando il tutorial prompt è ON")]
    public GameObject tutorialPromptOnButton;

    [Tooltip("Oggetto visibile quando il tutorial prompt è OFF")]
    public GameObject tutorialPromptOffButton;

    [Header("End Game Winner Texts")]
    public TMP_Text[] winnerTexts;
    public string player1FallbackName = "Player 1";
    public string player2FallbackName = "Player 2";
    public string winnerSuffix = " Wins!";

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        if (startEndController == null)
            startEndController = FindFirstObjectByType<StartEndController>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (launcher == null)
            launcher = FindFirstObjectByType<BallLauncher>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (tutorialPromptSettings == null)
            tutorialPromptSettings = FindFirstObjectByType<TutorialPromptSettings>();
#else
        if (startEndController == null)
            startEndController = FindObjectOfType<StartEndController>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (launcher == null)
            launcher = FindObjectOfType<BallLauncher>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (tutorialPromptSettings == null)
            tutorialPromptSettings = FindObjectOfType<TutorialPromptSettings>();
#endif

        if (applyOnStart)
            ApplyCurrentMode();

        RefreshTurnActiveUI();
        RefreshLauncherPhaseUI();
        RefreshTutorialPromptButtons();
        RefreshTimerText();
    }

    void Update()
    {
        RefreshTurnActiveUI();
        RefreshLauncherPhaseUI();
        RefreshTimerText();
    }

    public void ApplyCurrentMode()
    {
        StartEndController.MatchMode currentMode = GetCurrentMatchMode();

        bool isTimeMode = currentMode == StartEndController.MatchMode.TimeLimit;
        bool isScoreMode = currentMode == StartEndController.MatchMode.ScoreTarget;

        SetObjectsActive(timeModeObjects, isTimeMode);
        SetObjectsActive(scoreModeObjects, isScoreMode);

        RefreshPlayerNameTexts();
        RefreshTargetScoreTexts();
        RefreshTimerText();
        RefreshTurnActiveUI();
        RefreshLauncherPhaseUI();
        RefreshTutorialPromptButtons();
    }

    public void RefreshPlayerNameTexts()
    {
        string player1Name = GetPlayer1Name();
        string player2Name = GetPlayer2Name();

        SetTextArray(player1NameTexts, player1Name);
        SetTextArray(player2NameTexts, player2Name);

        if (turnManager != null)
            turnManager.ApplyPlayerNames(player1Name, player2Name);
    }

    public void RefreshTargetScoreTexts()
    {
        if (startEndController == null)
            return;

        string textValue = startEndController.targetScore.ToString();
        SetTextArray(targetScoreTexts, textValue);
    }

    public void RefreshTimerText()
    {
        if (matchTimerText == null || startEndController == null)
            return;

        float currentTime = startEndController.CurrentMatchTimer;

        if (useMinutesSecondsFormat)
        {
            int totalSeconds = Mathf.CeilToInt(currentTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            matchTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
        else
        {
            matchTimerText.text = Mathf.CeilToInt(currentTime).ToString();
        }
    }

    public void RefreshTurnActiveUI()
    {
        if (turnManager == null)
            return;

        bool isPlayer1Turn = turnManager.IsPlayer1Turn;
        bool isPlayer2Turn = turnManager.IsPlayer2Turn;

        if (player1TurnActiveObject != null)
            player1TurnActiveObject.SetActive(isPlayer1Turn);

        if (player2TurnActiveObject != null)
            player2TurnActiveObject.SetActive(isPlayer2Turn);
    }

    public void RefreshLauncherPhaseUI()
    {
        bool promptsEnabled = AreTutorialPromptsEnabled();

        if (!promptsEnabled)
        {
            if (placementPhaseObject != null)
                placementPhaseObject.SetActive(false);

            if (aimReadyPhaseObject != null)
                aimReadyPhaseObject.SetActive(false);

            return;
        }

        if (launcher == null)
        {
            if (placementPhaseObject != null)
                placementPhaseObject.SetActive(false);

            if (aimReadyPhaseObject != null)
                aimReadyPhaseObject.SetActive(false);

            return;
        }

        bool showPlacement = launcher.CurrentPhase == BallLauncher.LaunchPhase.Placement;
        bool showAimReady = launcher.CurrentPhase == BallLauncher.LaunchPhase.AimReady;

        if (placementPhaseObject != null)
            placementPhaseObject.SetActive(showPlacement);

        if (aimReadyPhaseObject != null)
            aimReadyPhaseObject.SetActive(showAimReady);
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

    public void ApplyTimeModePreview()
    {
        SetObjectsActive(timeModeObjects, true);
        SetObjectsActive(scoreModeObjects, false);
    }

    public void ApplyScoreModePreview()
    {
        SetObjectsActive(timeModeObjects, false);
        SetObjectsActive(scoreModeObjects, true);
    }

    public void ToggleTutorialPrompts()
    {
        if (tutorialPromptSettings == null)
            return;

        tutorialPromptSettings.Toggle();
        RefreshTutorialPromptButtons();
        RefreshLauncherPhaseUI();
    }

    public void EnableTutorialPrompts()
    {
        if (tutorialPromptSettings == null)
            return;

        tutorialPromptSettings.SetEnabled(true);
        RefreshTutorialPromptButtons();
        RefreshLauncherPhaseUI();
    }

    public void DisableTutorialPrompts()
    {
        if (tutorialPromptSettings == null)
            return;

        tutorialPromptSettings.SetEnabled(false);
        RefreshTutorialPromptButtons();
        RefreshLauncherPhaseUI();
    }

    public bool AreTutorialPromptsEnabled()
    {
        if (tutorialPromptSettings == null)
            return true;

        return tutorialPromptSettings.TutorialPromptsEnabled;
    }

    public void RefreshTutorialPromptButtons()
    {
        bool promptsEnabled = AreTutorialPromptsEnabled();

        if (tutorialPromptOnButton != null)
            tutorialPromptOnButton.SetActive(promptsEnabled);

        if (tutorialPromptOffButton != null)
            tutorialPromptOffButton.SetActive(!promptsEnabled);
    }

    string GetPlayer1Name()
    {
        if (matchRuntimeConfig == null || string.IsNullOrWhiteSpace(matchRuntimeConfig.SelectedPlayer1Name))
            return player1FallbackName;

        return matchRuntimeConfig.SelectedPlayer1Name;
    }

    string GetPlayer2Name()
    {
        if (matchRuntimeConfig == null || string.IsNullOrWhiteSpace(matchRuntimeConfig.SelectedPlayer2Name))
            return player2FallbackName;

        return matchRuntimeConfig.SelectedPlayer2Name;
    }

    StartEndController.MatchMode GetCurrentMatchMode()
    {
        if (matchRuntimeConfig != null)
            return matchRuntimeConfig.SelectedMatchMode;

        return StartEndController.MatchMode.ScoreTarget;
    }

    void SetObjectsActive(GameObject[] objectsToSet, bool activeState)
    {
        if (objectsToSet == null)
            return;

        for (int i = 0; i < objectsToSet.Length; i++)
        {
            if (objectsToSet[i] != null)
                objectsToSet[i].SetActive(activeState);
        }
    }

    void SetTextArray(TMP_Text[] texts, string value)
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