using UnityEngine;

public class MatchRuntimeConfig : MonoBehaviour
{
    public enum GameMode
    {
        Versus = 0,
        Bot = 1,
        Multiplayer = 2
    }

    public static MatchRuntimeConfig Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Runtime Config")]
    [SerializeField] private GameMode selectedGameMode = GameMode.Versus;
    [SerializeField] private StartEndController.MatchMode selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
    [SerializeField] private int selectedPointsToWin = 16;
    [SerializeField] private float selectedMatchDuration = 180f;
    [SerializeField] private string selectedPlayer1Name = "Player 1";
    [SerializeField] private string selectedPlayer2Name = "Player 2";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public GameMode SelectedGameMode => selectedGameMode;
    public StartEndController.MatchMode SelectedMatchMode => selectedMatchMode;
    public int SelectedPointsToWin => selectedPointsToWin;
    public float SelectedMatchDuration => selectedMatchDuration;
    public string SelectedPlayer1Name => selectedPlayer1Name;
    public string SelectedPlayer2Name => selectedPlayer2Name;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] Initialized. " +
                "GameMode=" + selectedGameMode +
                " | MatchMode=" + selectedMatchMode +
                " | PointsToWin=" + selectedPointsToWin +
                " | Duration=" + selectedMatchDuration +
                " | P1=" + selectedPlayer1Name +
                " | P2=" + selectedPlayer2Name,
                this
            );
        }
    }

    public void ConfigureVersusScoreMode(int pointsToWin, string player1Name, string player2Name)
    {
        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
        selectedPointsToWin = Mathf.Max(1, pointsToWin);
        selectedPlayer1Name = SanitizeName(player1Name, "Player 1");
        selectedPlayer2Name = SanitizeName(player2Name, "Player 2");

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureVersusScoreMode -> " +
                "PointsToWin=" + selectedPointsToWin +
                " | P1=" + selectedPlayer1Name +
                " | P2=" + selectedPlayer2Name,
                this
            );
        }
    }

    public void ConfigureVersusTimeMode(float matchDuration, int fallbackPointsToWin, string player1Name, string player2Name)
    {
        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.TimeLimit;
        selectedMatchDuration = Mathf.Max(1f, matchDuration);
        selectedPointsToWin = Mathf.Max(1, fallbackPointsToWin);
        selectedPlayer1Name = SanitizeName(player1Name, "Player 1");
        selectedPlayer2Name = SanitizeName(player2Name, "Player 2");

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureVersusTimeMode -> " +
                "Duration=" + selectedMatchDuration +
                " | P1=" + selectedPlayer1Name +
                " | P2=" + selectedPlayer2Name,
                this
            );
        }
    }

    public void ConfigureBotMode()
    {
        selectedGameMode = GameMode.Bot;

        if (logDebug)
            Debug.Log("[MatchRuntimeConfig] ConfigureBotMode", this);
    }

    public void ConfigureMultiplayerMode()
    {
        selectedGameMode = GameMode.Multiplayer;

        if (logDebug)
            Debug.Log("[MatchRuntimeConfig] ConfigureMultiplayerMode", this);
    }

    private string SanitizeName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}