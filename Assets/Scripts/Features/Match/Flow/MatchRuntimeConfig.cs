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
    [SerializeField] private GameMode selectedGameMode = GameMode.Versus;
    [SerializeField] private StartEndController.MatchMode selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
    [SerializeField] private OpponentType selectedOpponentType = OpponentType.LocalGuest;
    [SerializeField] private MatchSessionType selectedSessionType = MatchSessionType.OfflineLocal;
    [SerializeField] private MatchAuthorityType selectedAuthorityType = MatchAuthorityType.LocalDevice;
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

    [Header("Progression Policy")]
    [SerializeField] private bool selectedAllowChestRewards = true;
    [SerializeField] private bool selectedAllowXpRewards = true;
    [SerializeField] private bool selectedAllowStatsProgression = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public GameMode SelectedGameMode => selectedGameMode;
    public StartEndController.MatchMode SelectedMatchMode => selectedMatchMode;
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

    public bool SelectedAllowChestRewards => selectedAllowChestRewards;
    public bool SelectedAllowXpRewards => selectedAllowXpRewards;
    public bool SelectedAllowStatsProgression => selectedAllowStatsProgression;

    public bool IsOnlineMatch =>
        selectedSessionType == MatchSessionType.OnlinePrivate ||
        selectedSessionType == MatchSessionType.OnlineMatchmaking;

    public bool IsOfflineMatch =>
        selectedSessionType == MatchSessionType.OfflineLocal ||
        selectedSessionType == MatchSessionType.OfflineBot;

    public bool LocalProfileOwnsPlayer1 => selectedLocalParticipantSlot == LocalParticipantSlot.Player1;
    public bool LocalProfileOwnsPlayer2 => selectedLocalParticipantSlot == LocalParticipantSlot.Player2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] Initialized. " +
                "GameMode=" + selectedGameMode +
                " | MatchMode=" + selectedMatchMode +
                " | OpponentType=" + selectedOpponentType +
                " | SessionType=" + selectedSessionType +
                " | AuthorityType=" + selectedAuthorityType +
                " | LocalSlot=" + selectedLocalParticipantSlot +
                " | PointsToWin=" + selectedPointsToWin +
                " | Duration=" + selectedMatchDuration +
                " | Ranked=" + selectedIsRanked +
                " | LiveServices=" + selectedUseLiveMultiplayerServices +
                " | SessionId=" + selectedSessionId +
                " | RoomCode=" + selectedRoomCode +
                " | LocalProfileId=" + selectedLocalProfileId +
                " | P1=" + selectedPlayer1Name +
                " | P2=" + selectedPlayer2Name,
                this
            );
        }
    }

    public void ConfigureLocalVersusScoreMode(
        int pointsToWin,
        string localProfileId,
        string localDisplayName,
        string guestDisplayName)
    {
        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.ScoreTarget;
        selectedOpponentType = OpponentType.LocalGuest;
        selectedSessionType = MatchSessionType.OfflineLocal;
        selectedAuthorityType = MatchAuthorityType.LocalDevice;
        selectedLocalParticipantSlot = LocalParticipantSlot.Player1;
        selectedPointsToWin = Mathf.Max(1, pointsToWin);
        selectedMatchDuration = Mathf.Max(1f, selectedMatchDuration);
        selectedIsRanked = false;

        selectedLocalProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        selectedLocalDisplayName = SanitizeName(localDisplayName, "Player 1");
        selectedOpponentDisplayName = SanitizeName(guestDisplayName, "Player 2");

        selectedUseLiveMultiplayerServices = false;
        selectedIsHost = false;
        selectedSessionId = string.Empty;
        selectedRoomCode = string.Empty;
        selectedRemotePlayerId = string.Empty;

        selectedAllowChestRewards = true;
        selectedAllowXpRewards = true;
        selectedAllowStatsProgression = true;

        ApplyDisplayedPlayerNamesFromLocalSlot();

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureLocalVersusScoreMode -> " +
                "PointsToWin=" + selectedPointsToWin +
                " | LocalProfileId=" + selectedLocalProfileId +
                " | LocalName=" + selectedLocalDisplayName +
                " | GuestName=" + selectedOpponentDisplayName,
                this
            );
        }
    }

    public void ConfigureLocalVersusTimeMode(
        float matchDuration,
        int fallbackPointsToWin,
        string localProfileId,
        string localDisplayName,
        string guestDisplayName)
    {
        selectedGameMode = GameMode.Versus;
        selectedMatchMode = StartEndController.MatchMode.TimeLimit;
        selectedOpponentType = OpponentType.LocalGuest;
        selectedSessionType = MatchSessionType.OfflineLocal;
        selectedAuthorityType = MatchAuthorityType.LocalDevice;
        selectedLocalParticipantSlot = LocalParticipantSlot.Player1;
        selectedMatchDuration = Mathf.Max(1f, matchDuration);
        selectedPointsToWin = Mathf.Max(1, fallbackPointsToWin);
        selectedIsRanked = false;

        selectedLocalProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        selectedLocalDisplayName = SanitizeName(localDisplayName, "Player 1");
        selectedOpponentDisplayName = SanitizeName(guestDisplayName, "Player 2");

        selectedUseLiveMultiplayerServices = false;
        selectedIsHost = false;
        selectedSessionId = string.Empty;
        selectedRoomCode = string.Empty;
        selectedRemotePlayerId = string.Empty;

        selectedAllowChestRewards = true;
        selectedAllowXpRewards = true;
        selectedAllowStatsProgression = true;

        ApplyDisplayedPlayerNamesFromLocalSlot();

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureLocalVersusTimeMode -> " +
                "Duration=" + selectedMatchDuration +
                " | LocalProfileId=" + selectedLocalProfileId +
                " | LocalName=" + selectedLocalDisplayName +
                " | GuestName=" + selectedOpponentDisplayName,
                this
            );
        }
    }

    public void ConfigureBotMode(
        string localProfileId,
        string localDisplayName,
        string botDisplayName = "BOT",
        int pointsToWin = 16,
        float matchDuration = 180f,
        StartEndController.MatchMode matchMode = StartEndController.MatchMode.ScoreTarget)
    {
        selectedGameMode = GameMode.Bot;
        selectedMatchMode = matchMode;
        selectedOpponentType = OpponentType.Bot;
        selectedSessionType = MatchSessionType.OfflineBot;
        selectedAuthorityType = MatchAuthorityType.LocalDevice;
        selectedLocalParticipantSlot = LocalParticipantSlot.Player1;
        selectedPointsToWin = Mathf.Max(1, pointsToWin);
        selectedMatchDuration = Mathf.Max(1f, matchDuration);
        selectedIsRanked = false;

        selectedLocalProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        selectedLocalDisplayName = SanitizeName(localDisplayName, "Player 1");
        selectedOpponentDisplayName = SanitizeName(botDisplayName, "BOT");

        selectedUseLiveMultiplayerServices = false;
        selectedIsHost = false;
        selectedSessionId = string.Empty;
        selectedRoomCode = string.Empty;
        selectedRemotePlayerId = string.Empty;

        selectedAllowChestRewards = true;
        selectedAllowXpRewards = true;
        selectedAllowStatsProgression = true;

        ApplyDisplayedPlayerNamesFromLocalSlot();

        if (logDebug)
        {
            Debug.Log(
                "[MatchRuntimeConfig] ConfigureBotMode -> " +
                "MatchMode=" + selectedMatchMode +
                " | LocalProfileId=" + selectedLocalProfileId +
                " | LocalName=" + selectedLocalDisplayName +
                " | BotName=" + selectedOpponentDisplayName,
                this
            );
        }
    }

    public void ConfigureMultiplayerMode(
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        string localProfileId,
        string localDisplayName,
        string remoteDisplayName,
        bool isRanked,
        MatchSessionType sessionType = MatchSessionType.OnlinePrivate,
        MatchAuthorityType authorityType = MatchAuthorityType.DedicatedServer,
        LocalParticipantSlot localParticipantSlot = LocalParticipantSlot.Player1)
    {
        ConfigureMultiplayerMode(
            matchMode,
            pointsToWin,
            matchDuration,
            localProfileId,
            localDisplayName,
            remoteDisplayName,
            isRanked,
            sessionType,
            authorityType,
            localParticipantSlot,
            useLiveMultiplayerServices: false,
            isHost: false,
            sessionId: string.Empty,
            roomCode: string.Empty,
            remotePlayerId: string.Empty,
            allowChestRewards: false,
            allowXpRewards: false,
            allowStatsProgression: false
        );
    }

    public void ConfigureMultiplayerMode(
        StartEndController.MatchMode matchMode,
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

        selectedLocalProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        selectedLocalDisplayName = SanitizeName(localDisplayName, "Player 1");
        selectedOpponentDisplayName = SanitizeName(remoteDisplayName, "Remote Player");

        selectedUseLiveMultiplayerServices = useLiveMultiplayerServices;
        selectedIsHost = isHost;
        selectedSessionId = SanitizeOptionalValue(sessionId);
        selectedRoomCode = SanitizeOptionalValue(roomCode);
        selectedRemotePlayerId = SanitizeOptionalValue(remotePlayerId);

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
                " | AuthorityType=" + selectedAuthorityType +
                " | LocalSlot=" + selectedLocalParticipantSlot +
                " | Ranked=" + selectedIsRanked +
                " | LiveServices=" + selectedUseLiveMultiplayerServices +
                " | IsHost=" + selectedIsHost +
                " | SessionId=" + selectedSessionId +
                " | RoomCode=" + selectedRoomCode +
                " | LocalProfileId=" + selectedLocalProfileId +
                " | LocalName=" + selectedLocalDisplayName +
                " | RemoteName=" + selectedOpponentDisplayName +
                " | AllowChestRewards=" + selectedAllowChestRewards +
                " | AllowXpRewards=" + selectedAllowXpRewards +
                " | AllowStatsProgression=" + selectedAllowStatsProgression,
                this
            );
        }
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

    private string SanitizeName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    private string SanitizeProfileId(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    private string SanitizeOptionalValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim();
    }

    private void MarkRuntimeRootPersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
            return;

        GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(runtimeRoot);
    }

    private void DestroyDuplicateRuntimeRoot()
    {
        GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
        Destroy(duplicateRoot);
    }
}