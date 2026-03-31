using UnityEngine;

public class MatchRuntimeConfig : MonoBehaviour
{
    public enum GameMode
    {
        Versus = 0,
        Bot = 1,
        Multiplayer = 2
    }

    public enum OpponentType
    {
        LocalGuest = 0,
        Bot = 1,
        RemotePlayer = 2
    }

    public enum MatchSessionType
    {
        OfflineLocal = 0,
        OfflineBot = 1,
        OnlinePrivate = 2,
        OnlineMatchmaking = 3
    }

    public enum MatchAuthorityType
    {
        LocalDevice = 0,
        HostClient = 1,
        DedicatedServer = 2
    }

    public enum LocalParticipantSlot
    {
        Player1 = 0,
        Player2 = 1
    }

    public static MatchRuntimeConfig Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Runtime Config")]
    [SerializeField] private GameMode selectedGameMode = GameMode.Multiplayer;
    [SerializeField] private MatchMode selectedMatchMode = MatchMode.ScoreTarget;
    [SerializeField] private OpponentType selectedOpponentType = OpponentType.RemotePlayer;
    [SerializeField] private MatchSessionType selectedSessionType = MatchSessionType.OnlinePrivate;
    [SerializeField] private MatchAuthorityType selectedAuthorityType = MatchAuthorityType.HostClient;
    [SerializeField] private LocalParticipantSlot selectedLocalParticipantSlot = LocalParticipantSlot.Player1;

    [SerializeField] private int selectedPointsToWin = 16;
    [SerializeField] private float selectedMatchDuration = 180f;
    [SerializeField] private bool selectedIsRanked = false;

    [SerializeField] private string selectedLocalProfileId = "local_player_1";
    [SerializeField] private string selectedLocalDisplayName = "Player 1";
    [SerializeField] private string selectedOpponentDisplayName = "Player 2";

    [SerializeField] private string selectedPlayer1Name = "Player 1";
    [SerializeField] private string selectedPlayer2Name = "Player 2";

    [Header("Online Session Meta")]
    [SerializeField] private bool selectedUseLiveMultiplayerServices = false;
    [SerializeField] private bool selectedIsHost = false;
    [SerializeField] private string selectedSessionId = "";
    [SerializeField] private string selectedRoomCode = "";
    [SerializeField] private string selectedRemotePlayerId = "";

    [Header("Player Skins")]
    [SerializeField] private string player1SkinUniqueId = "";
    [SerializeField] private string player2SkinUniqueId = "";

    [Header("Progression Policy")]
    [SerializeField] private bool selectedAllowChestRewards = false;
    [SerializeField] private bool selectedAllowXpRewards = false;
    [SerializeField] private bool selectedAllowStatsProgression = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public GameMode SelectedGameMode => selectedGameMode;
    public MatchMode SelectedMatchMode => selectedMatchMode;
    public OpponentType SelectedOpponentType => selectedOpponentType;
    public MatchSessionType SelectedSessionType => selectedSessionType;
    public MatchAuthorityType SelectedAuthorityType => selectedAuthorityType;
    public LocalParticipantSlot SelectedLocalParticipantSlot => selectedLocalParticipantSlot;

    public int SelectedPointsToWin => selectedPointsToWin;
    public float SelectedMatchDuration => selectedMatchDuration;
    public bool SelectedIsRanked => selectedIsRanked;

    public string SelectedLocalProfileId => selectedLocalProfileId;
    public string SelectedLocalDisplayName => selectedLocalDisplayName;
    public string SelectedOpponentDisplayName => selectedOpponentDisplayName;

    public string SelectedPlayer1Name => selectedPlayer1Name;
    public string SelectedPlayer2Name => selectedPlayer2Name;

    public bool SelectedUseLiveMultiplayerServices => selectedUseLiveMultiplayerServices;
    public bool SelectedIsHost => selectedIsHost;
    public string SelectedSessionId => selectedSessionId;
    public string SelectedRoomCode => selectedRoomCode;
    public string SelectedRemotePlayerId => selectedRemotePlayerId;

    public string Player1SkinUniqueId => player1SkinUniqueId;
    public string Player2SkinUniqueId => player2SkinUniqueId;

    public bool SelectedAllowChestRewards => selectedAllowChestRewards;
    public bool SelectedAllowXpRewards => selectedAllowXpRewards;
    public bool SelectedAllowStatsProgression => selectedAllowStatsProgression;

    public bool IsOnlineMatch =>
        selectedSessionType == MatchSessionType.OnlinePrivate ||
        selectedSessionType == MatchSessionType.OnlineMatchmaking;

    public bool LocalProfileOwnsPlayer1 => selectedLocalParticipantSlot == LocalParticipantSlot.Player1;
    public bool LocalProfileOwnsPlayer2 => selectedLocalParticipantSlot == LocalParticipantSlot.Player2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }
    }

    public void ConfigureMultiplayerMode(
        MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        string localProfileId,
        string localDisplayName,
        string remoteDisplayName,
        bool isRanked,
        MatchSessionType sessionType,
        MatchAuthorityType authorityType,
        LocalParticipantSlot localParticipantSlot,
        bool useLiveMultiplayerServices,
        bool isHost,
        string sessionId,
        string roomCode,
        string remotePlayerId,
        bool allowChestRewards,
        bool allowXpRewards,
        bool allowStatsProgression)
    {
        selectedGameMode = GameMode.Multiplayer;
        selectedMatchMode = matchMode;
        selectedOpponentType = OpponentType.RemotePlayer;
        selectedSessionType = sessionType;
        selectedAuthorityType = authorityType;
        selectedLocalParticipantSlot = localParticipantSlot;
        selectedPointsToWin = Mathf.Max(1, pointsToWin);
        selectedMatchDuration = Mathf.Max(1f, matchDuration);
        selectedIsRanked = isRanked;

        selectedLocalProfileId = Sanitize(localProfileId, "local_player_1");
        selectedLocalDisplayName = Sanitize(localDisplayName, "Player 1");
        selectedOpponentDisplayName = Sanitize(remoteDisplayName, "Player 2");

        selectedUseLiveMultiplayerServices = useLiveMultiplayerServices;
        selectedIsHost = isHost;
        selectedSessionId = Sanitize(sessionId, string.Empty);
        selectedRoomCode = Sanitize(roomCode, string.Empty);
        selectedRemotePlayerId = Sanitize(remotePlayerId, string.Empty);

        selectedAllowChestRewards = allowChestRewards;
        selectedAllowXpRewards = allowXpRewards;
        selectedAllowStatsProgression = allowStatsProgression;

        ApplyDisplayedPlayerNamesFromLocalSlot();

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureMultiplayerMode -> " +
                "MatchMode=" + selectedMatchMode +
                " | SessionType=" + selectedSessionType +
                " | LocalSlot=" + selectedLocalParticipantSlot +
                " | LocalName=" + selectedLocalDisplayName +
                " | OpponentName=" + selectedOpponentDisplayName,
                this
            );
        }
    }

    public void SetPlayerSkinUniqueIds(string p1SkinUniqueId, string p2SkinUniqueId)
    {
        player1SkinUniqueId = string.IsNullOrWhiteSpace(p1SkinUniqueId) ? string.Empty : p1SkinUniqueId.Trim();
        player2SkinUniqueId = string.IsNullOrWhiteSpace(p2SkinUniqueId) ? string.Empty : p2SkinUniqueId.Trim();
    }

    public PlayerID GetResolvedLocalPlayerId()
    {
        return selectedLocalParticipantSlot == LocalParticipantSlot.Player1
            ? PlayerID.Player1
            : PlayerID.Player2;
    }

    public PlayerID GetResolvedOpponentPlayerId()
    {
        return selectedLocalParticipantSlot == LocalParticipantSlot.Player1
            ? PlayerID.Player2
            : PlayerID.Player1;
    }

    private void ApplyDisplayedPlayerNamesFromLocalSlot()
    {
        if (selectedLocalParticipantSlot == LocalParticipantSlot.Player1)
        {
            selectedPlayer1Name = selectedLocalDisplayName;
            selectedPlayer2Name = selectedOpponentDisplayName;
        }
        else
        {
            selectedPlayer1Name = selectedOpponentDisplayName;
            selectedPlayer2Name = selectedLocalDisplayName;
        }
    }

    private string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}