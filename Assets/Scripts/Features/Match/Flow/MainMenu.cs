using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public enum GameMode
    {
        Versus = 0,
        Bot = 1,
        Multiplayer = 2
    }

    public static GameMode selectedGameMode = GameMode.Versus;
    public static StartEndController.MatchMode selectedMatchMode = StartEndController.MatchMode.ScoreTarget;

    public static int selectedPointsToWin = 16;
    public static float selectedMatchDuration = 180f;

    public static string selectedPlayer1Name = "Player 1";
    public static string selectedPlayer2Name = "Player 2";

    [Header("Scene References")]
    [Tooltip("Scena gameplay della modalità Versus, usata sia per TimeLimit che per ScoreTarget")]
    public string versusSceneName = "Gameplay";

    [Tooltip("Scena placeholder futura per la modalità BOT")]
    public string botSceneName = "BotMenu";

    [Tooltip("Scena placeholder futura per la modalità Multiplayer")]
    public string multiplayerSceneName = "MultiplayerMenu";

    [Header("Time Mode Presets")]
    [Tooltip("Durate selezionabili per la modalità a tempo, in secondi")]
    public int[] timeModeDurationPresets = new int[] { 180, 300, 360 };

    [Tooltip("Indice preset iniziale selezionato")]
    public int defaultTimePresetIndex = 0;

    [Tooltip("Testo che mostra la durata attualmente selezionata")]
    public TMP_Text timeModeDurationText;

    [Header("Time Mode Inputs")]
    public TMP_InputField timeModePlayer1NameInputField;
    public TMP_InputField timeModePlayer2NameInputField;

    [Header("Score Mode Inputs")]
    public TMP_InputField scoreModePointsToWinInputField;
    public TMP_InputField scoreModePlayer1NameInputField;
    public TMP_InputField scoreModePlayer2NameInputField;
    public int defaultPointsToWin = 16;

    [Header("Default Player Names")]
    public string defaultPlayer1Name = "Player 1";
    public string defaultPlayer2Name = "Player 2";

    [Header("Chest UI Refresh")]
    [SerializeField] private ChestSlotUI[] chestSlotUIs;
    [SerializeField] private bool refreshChestSlotsOnStart = true;
    [SerializeField] private bool refreshChestSlotsOnEnable = true;
    [SerializeField] private bool refreshChestSlotsNextFrameToo = true;
    [SerializeField] private bool refreshChestSlotsAfterShortDelay = true;
    [SerializeField] private float delayedChestRefreshSeconds = 0.15f;
    [SerializeField] private int extraRefreshFrames = 3;
    [SerializeField] private bool logChestRefreshDebug = false;

    private int currentTimePresetIndex;
    private Coroutine chestRefreshRoutine;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (refreshChestSlotsOnEnable)
            StartChestRefreshRoutine();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (chestRefreshRoutine != null)
        {
            StopCoroutine(chestRefreshRoutine);
            chestRefreshRoutine = null;
        }
    }

    private void Start()
    {
        ValidateTimePresets();

        currentTimePresetIndex = Mathf.Clamp(defaultTimePresetIndex, 0, timeModeDurationPresets.Length - 1);
        selectedMatchDuration = timeModeDurationPresets[currentTimePresetIndex];

        if (scoreModePointsToWinInputField != null)
            scoreModePointsToWinInputField.text = defaultPointsToWin.ToString();

        if (timeModePlayer1NameInputField != null)
            timeModePlayer1NameInputField.text = defaultPlayer1Name;

        if (timeModePlayer2NameInputField != null)
            timeModePlayer2NameInputField.text = defaultPlayer2Name;

        if (scoreModePlayer1NameInputField != null)
            scoreModePlayer1NameInputField.text = defaultPlayer1Name;

        if (scoreModePlayer2NameInputField != null)
            scoreModePlayer2NameInputField.text = defaultPlayer2Name;

        RefreshTimeDurationUI();

        if (refreshChestSlotsOnStart)
            StartChestRefreshRoutine();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        if (!isActiveAndEnabled)
            return;

        StartChestRefreshRoutine();
    }

    private void ValidateTimePresets()
    {
        if (timeModeDurationPresets == null || timeModeDurationPresets.Length == 0)
            timeModeDurationPresets = new int[] { 180, 300, 360 };

        for (int i = 0; i < timeModeDurationPresets.Length; i++)
        {
            if (timeModeDurationPresets[i] < 1)
                timeModeDurationPresets[i] = 1;
        }
    }

    public void NextTimeDurationPreset()
    {
        ValidateTimePresets();

        currentTimePresetIndex++;

        if (currentTimePresetIndex >= timeModeDurationPresets.Length)
            currentTimePresetIndex = 0;

        selectedMatchDuration = timeModeDurationPresets[currentTimePresetIndex];
        RefreshTimeDurationUI();
    }

    public void PreviousTimeDurationPreset()
    {
        ValidateTimePresets();

        currentTimePresetIndex--;

        if (currentTimePresetIndex < 0)
            currentTimePresetIndex = timeModeDurationPresets.Length - 1;

        selectedMatchDuration = timeModeDurationPresets[currentTimePresetIndex];
        RefreshTimeDurationUI();
    }

    public void SetTimeDurationPresetByIndex(int presetIndex)
    {
        ValidateTimePresets();

        currentTimePresetIndex = Mathf.Clamp(presetIndex, 0, timeModeDurationPresets.Length - 1);
        selectedMatchDuration = timeModeDurationPresets[currentTimePresetIndex];
        RefreshTimeDurationUI();
    }

    public void PlayVersusTimeMode()
    {
        ValidateTimePresets();

        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.TimeLimit;

        selectedMatchDuration = timeModeDurationPresets[currentTimePresetIndex];
        selectedPointsToWin = defaultPointsToWin;

        selectedPlayer1Name = ReadPlayerName(timeModePlayer1NameInputField, defaultPlayer1Name);
        selectedPlayer2Name = ReadPlayerName(timeModePlayer2NameInputField, defaultPlayer2Name);

        LoadSceneSafe(versusSceneName);
    }

    public void PlayVersusScoreMode()
    {
        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.ScoreTarget;

        selectedPointsToWin = ReadScoreModePointsToWin();

        selectedPlayer1Name = ReadPlayerName(scoreModePlayer1NameInputField, defaultPlayer1Name);
        selectedPlayer2Name = ReadPlayerName(scoreModePlayer2NameInputField, defaultPlayer2Name);

        LoadSceneSafe(versusSceneName);
    }

    public void OpenBotMode()
    {
        selectedGameMode = GameMode.Bot;
        LoadSceneSafe(botSceneName);
    }

    public void OpenMultiplayerMode()
    {
        selectedGameMode = GameMode.Multiplayer;
        LoadSceneSafe(multiplayerSceneName);
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[MainMenu] Scene name is empty.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void RefreshTimeDurationUI()
    {
        if (timeModeDurationText != null)
            timeModeDurationText.text = timeModeDurationPresets[currentTimePresetIndex].ToString();
    }

    private int ReadScoreModePointsToWin()
    {
        if (scoreModePointsToWinInputField == null)
            return defaultPointsToWin;

        string rawValue = scoreModePointsToWinInputField.text;

        if (int.TryParse(rawValue, out int parsedValue))
        {
            if (parsedValue < 1)
                parsedValue = 1;

            scoreModePointsToWinInputField.text = parsedValue.ToString();
            return parsedValue;
        }

        scoreModePointsToWinInputField.text = defaultPointsToWin.ToString();
        return defaultPointsToWin;
    }

    private string ReadPlayerName(TMP_InputField inputField, string fallbackValue)
    {
        if (inputField == null)
            return fallbackValue;

        string value = inputField.text.Trim();

        if (string.IsNullOrEmpty(value))
        {
            inputField.text = fallbackValue;
            return fallbackValue;
        }

        return value;
    }

    public int GetCurrentTimePresetIndex()
    {
        return currentTimePresetIndex;
    }

    public int GetCurrentTimePresetValue()
    {
        ValidateTimePresets();
        return timeModeDurationPresets[currentTimePresetIndex];
    }

    public void ForceRefreshChestSlotsUI()
    {
        StartChestRefreshRoutine();
    }

    public void RefreshChestSlotsUI()
    {
        if (chestSlotUIs == null || chestSlotUIs.Length == 0)
        {
            if (logChestRefreshDebug)
                Debug.Log("[MainMenu] No ChestSlotUI references assigned.", this);

            return;
        }

        for (int i = 0; i < chestSlotUIs.Length; i++)
        {
            if (chestSlotUIs[i] == null)
                continue;

            chestSlotUIs[i].RefreshUI();
        }

        if (logChestRefreshDebug)
            Debug.Log("[MainMenu] Chest slots refreshed.", this);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isActiveAndEnabled)
            return;

        if (scene.name != "MainMenu")
            return;

        StartChestRefreshRoutine();
    }

    private void StartChestRefreshRoutine()
    {
        if (!isActiveAndEnabled)
            return;

        if (chestRefreshRoutine != null)
            StopCoroutine(chestRefreshRoutine);

        chestRefreshRoutine = StartCoroutine(RefreshChestSlotsRoutine());
    }

    private IEnumerator RefreshChestSlotsRoutine()
    {
        RefreshChestSlotsUI();

        if (refreshChestSlotsNextFrameToo)
        {
            yield return null;
            RefreshChestSlotsUI();
        }

        int frameCount = Mathf.Max(0, extraRefreshFrames);
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
            RefreshChestSlotsUI();
        }

        if (refreshChestSlotsAfterShortDelay && delayedChestRefreshSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delayedChestRefreshSeconds);
            RefreshChestSlotsUI();
        }

        chestRefreshRoutine = null;
    }
}