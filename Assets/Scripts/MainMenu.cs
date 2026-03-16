using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public static StartEndController.MatchMode selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
    public static int selectedPointsToWin = 16;
    public static float selectedMatchDuration = 180f;

    public static string selectedPlayer1Name = "Player 1";
    public static string selectedPlayer2Name = "Player 2";

    [Header("Scene")]
    public int gameplaySceneIndex = 1;

    [Header("Time Mode Inputs")]
    public TMP_InputField timeModeMatchDurationInputField;
    public TMP_InputField timeModePlayer1NameInputField;
    public TMP_InputField timeModePlayer2NameInputField;
    public float defaultMatchDuration = 180f;

    [Header("Score Mode Inputs")]
    public TMP_InputField scoreModePointsToWinInputField;
    public TMP_InputField scoreModePlayer1NameInputField;
    public TMP_InputField scoreModePlayer2NameInputField;
    public int defaultPointsToWin = 16;

    [Header("Default Player Names")]
    public string defaultPlayer1Name = "Player 1";
    public string defaultPlayer2Name = "Player 2";

    void Start()
    {
        if (timeModeMatchDurationInputField != null)
            timeModeMatchDurationInputField.text = Mathf.RoundToInt(defaultMatchDuration).ToString();

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
    }

    public void PlayTimeMode()
    {
        selectedMatchMode = StartEndController.MatchMode.TimeLimit;
        selectedMatchDuration = ReadTimeModeMatchDuration();
        selectedPointsToWin = defaultPointsToWin;

        selectedPlayer1Name = ReadPlayerName(timeModePlayer1NameInputField, defaultPlayer1Name);
        selectedPlayer2Name = ReadPlayerName(timeModePlayer2NameInputField, defaultPlayer2Name);

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneIndex);
    }

    public void PlayScoreMode()
    {
        selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
        selectedPointsToWin = ReadScoreModePointsToWin();
        selectedMatchDuration = defaultMatchDuration;

        selectedPlayer1Name = ReadPlayerName(scoreModePlayer1NameInputField, defaultPlayer1Name);
        selectedPlayer2Name = ReadPlayerName(scoreModePlayer2NameInputField, defaultPlayer2Name);

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneIndex);
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    float ReadTimeModeMatchDuration()
    {
        if (timeModeMatchDurationInputField == null)
            return defaultMatchDuration;

        string rawValue = timeModeMatchDurationInputField.text;

        if (float.TryParse(rawValue, out float parsedValue))
        {
            if (parsedValue < 1f)
                parsedValue = 1f;

            timeModeMatchDurationInputField.text = Mathf.RoundToInt(parsedValue).ToString();
            return parsedValue;
        }

        timeModeMatchDurationInputField.text = Mathf.RoundToInt(defaultMatchDuration).ToString();
        return defaultMatchDuration;
    }

    int ReadScoreModePointsToWin()
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

    string ReadPlayerName(TMP_InputField inputField, string fallbackValue)
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
}