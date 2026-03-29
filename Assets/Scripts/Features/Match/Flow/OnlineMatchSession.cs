using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class OnlineMatchSessionData
{
    public bool hasPreparedSession = false;

    public string sessionId = "";
    public string roomCode = "";

    public string localProfileId = "local_player_1";
    public string localDisplayName = "Player 1";
    public string remotePlayerId = "";
    public string remoteDisplayName = "Remote Player";

    public bool isHost = false;
    public bool isRanked = false;
    public bool useLiveMultiplayerServices = false;

    public MatchRuntimeConfig.MatchSessionType sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
    public MatchRuntimeConfig.MatchAuthorityType authorityType = MatchRuntimeConfig.MatchAuthorityType.HostClient;
    public MatchRuntimeConfig.LocalParticipantSlot localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

    public StartEndController.MatchMode matchMode = StartEndController.MatchMode.ScoreTarget;
    public int pointsToWin = 16;
    public float matchDuration = 180f;

    public bool allowChestRewards = false;
    public bool allowXpRewards = false;
    public bool allowStatsProgression = false;

    public bool hasRemoteJoiner = false;
    public string lobbyStatusText = "";

    public string matchmakingQueueId = "";
    public bool isMatchmakingSearch = false;
}

public class OnlineMatchSession : MonoBehaviour
{
    public static OnlineMatchSession Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Dependencies")]
    [SerializeField] private MatchRuntimeConfig matchRuntimeConfig;
    [SerializeField] private OnlineLobbyServiceBase onlineLobbyService;

    [Header("Runtime")]
    [SerializeField] private OnlineMatchSessionData currentSession = new OnlineMatchSessionData();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public OnlineMatchSessionData CurrentSession => currentSession;
    public bool HasPreparedSession => currentSession != null && currentSession.hasPreparedSession;
    public bool HasRemoteJoiner => currentSession != null && currentSession.hasRemoteJoiner;

    public event Action OnSessionUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();
        ResolveDependencies();

        if (currentSession == null)
            currentSession = new OnlineMatchSessionData();

        SubscribeToLobbyService();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] Initialized. " +
                "HasPreparedSession=" + HasPreparedSession +
                " | SessionId=" + currentSession.sessionId +
                " | RoomCode=" + currentSession.roomCode,
                this
            );
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromLobbyService();
    }

    public void ClearPreparedSession()
    {
        currentSession = new OnlineMatchSessionData();
        NotifyUpdated();

        if (logDebug)
            Debug.Log("[OnlineMatchSession] Cleared prepared session.", this);
    }

    public void CancelMatchmakingSearch()
    {
        ResolveDependencies();

        if (currentSession == null || !currentSession.isMatchmakingSearch)
        {
            if (logDebug)
                Debug.Log("[OnlineMatchSession] CancelMatchmakingSearch ignored: no active matchmaking search.", this);

            return;
        }

        if (onlineLobbyService != null)
            onlineLobbyService.CancelMatchmaking(currentSession.localProfileId);

        ClearPreparedSession();

        if (logDebug)
            Debug.Log("[OnlineMatchSession] Matchmaking search cancelled.", this);
    }

    public void LeaveCurrentLobbyOrMatchmaking()
    {
        ResolveDependencies();

        if (!HasPreparedSession)
        {
            if (logDebug)
                Debug.Log("[OnlineMatchSession] LeaveCurrentLobbyOrMatchmaking ignored: no prepared session.", this);

            return;
        }

        if (onlineLobbyService != null)
            onlineLobbyService.LeaveLobby(currentSession.localProfileId);

        ClearPreparedSession();

        if (logDebug)
            Debug.Log("[OnlineMatchSession] Left current lobby/matchmaking.", this);
    }

    public void PreparePrivateHostSession(
        string localProfileId,
        string localDisplayName,
        string remoteDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked,
        bool useLiveMultiplayerServices,
        bool allowChestRewards,
        bool allowXpRewards,
        bool allowStatsProgression,
        string customRoomCode = null)
    {
        EnsureRuntime();

        currentSession.hasPreparedSession = true;
        currentSession.sessionId = Guid.NewGuid().ToString("N");
        currentSession.roomCode = string.Empty;

        currentSession.localProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        currentSession.localDisplayName = SanitizeName(localDisplayName, "HostPlayer");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = SanitizeName(remoteDisplayName, "Player 2");

        currentSession.isHost = true;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
        currentSession.authorityType = MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = false;
        currentSession.lobbyStatusText = useLiveMultiplayerServices ? "Creating online lobby" : "Preparing offline placeholder lobby";

        currentSession.matchmakingQueueId = string.Empty;
        currentSession.isMatchmakingSearch = false;

        ResolveDependencies();
        NotifyUpdated();

        if (useLiveMultiplayerServices && onlineLobbyService != null)
        {
            onlineLobbyService.CreateLobby(
                currentSession.localProfileId,
                currentSession.localDisplayName,
                currentSession.matchMode,
                currentSession.pointsToWin,
                currentSession.matchDuration,
                currentSession.isRanked
            );
        }
    }

    public void PreparePrivateJoinSession(
        string roomCode,
        string localProfileId,
        string localDisplayName,
        string remoteDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked,
        bool useLiveMultiplayerServices,
        bool allowChestRewards,
        bool allowXpRewards,
        bool allowStatsProgression)
    {
        EnsureRuntime();

        currentSession.hasPreparedSession = true;
        currentSession.sessionId = Guid.NewGuid().ToString("N");
        currentSession.roomCode = SanitizeRoomCode(roomCode);

        currentSession.localProfileId = SanitizeProfileId(localProfileId, "local_player_2");
        currentSession.localDisplayName = SanitizeName(localDisplayName, "JoinPlayer");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = SanitizeName(remoteDisplayName, "HostPlayer");

        currentSession.isHost = false;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
        currentSession.authorityType = MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player2;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = false;
        currentSession.lobbyStatusText = useLiveMultiplayerServices ? "Joining online lobby" : "Preparing offline placeholder join";

        currentSession.matchmakingQueueId = string.Empty;
        currentSession.isMatchmakingSearch = false;

        ResolveDependencies();
        NotifyUpdated();

        if (useLiveMultiplayerServices && onlineLobbyService != null)
        {
            onlineLobbyService.JoinLobby(
                currentSession.roomCode,
                currentSession.localProfileId,
                currentSession.localDisplayName
            );
        }
    }

    public void PrepareMatchmakingSession(
        string queueId,
        string localProfileId,
        string localDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked,
        bool useLiveMultiplayerServices,
        bool allowChestRewards,
        bool allowXpRewards,
        bool allowStatsProgression)
    {
        EnsureRuntime();

        currentSession.hasPreparedSession = true;
        currentSession.sessionId = Guid.NewGuid().ToString("N");
        currentSession.roomCode = string.Empty;

        currentSession.localProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        currentSession.localDisplayName = SanitizeName(localDisplayName, "Player 1");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = "Searching...";

        currentSession.isHost = false;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlineMatchmaking;
        currentSession.authorityType = MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = false;
        currentSession.lobbyStatusText = "Searching matchmaking";

        currentSession.matchmakingQueueId = SanitizeOptionalValue(queueId);
        currentSession.isMatchmakingSearch = true;

        ResolveDependencies();
        NotifyUpdated();

        if (useLiveMultiplayerServices && onlineLobbyService != null)
        {
            onlineLobbyService.BeginMatchmaking(
                currentSession.matchmakingQueueId,
                currentSession.localProfileId,
                currentSession.localDisplayName,
                currentSession.matchMode,
                currentSession.pointsToWin,
                currentSession.matchDuration,
                currentSession.isRanked
            );
        }
    }

    public bool CanHostStartMatch()
    {
        if (!HasPreparedSession)
            return false;

        if (!currentSession.isHost)
            return false;

        if (!currentSession.useLiveMultiplayerServices)
            return false;

        if (!currentSession.hasRemoteJoiner)
            return false;

        return true;
    }

    public bool RequestHostStartMatch()
    {
        ResolveDependencies();

        if (!CanHostStartMatch())
            return false;

        if (onlineLobbyService != null)
        {
            onlineLobbyService.RequestStartMatch(currentSession.localProfileId);
            return true;
        }

        return false;
    }

    public bool PushPreparedSessionIntoMatchRuntimeConfig()
    {
        ResolveDependencies();

        if (!HasPreparedSession)
        {
            Debug.LogWarning("[OnlineMatchSession] No prepared session to push into MatchRuntimeConfig.", this);
            return false;
        }

        if (matchRuntimeConfig == null)
        {
            Debug.LogError("[OnlineMatchSession] MatchRuntimeConfig missing.", this);
            return false;
        }

        matchRuntimeConfig.ConfigureMultiplayerMode(
            currentSession.matchMode,
            currentSession.pointsToWin,
            currentSession.matchDuration,
            currentSession.localProfileId,
            currentSession.localDisplayName,
            currentSession.remoteDisplayName,
            currentSession.isRanked,
            currentSession.sessionType,
            currentSession.authorityType,
            currentSession.localParticipantSlot,
            currentSession.useLiveMultiplayerServices,
            currentSession.isHost,
            currentSession.sessionId,
            currentSession.roomCode,
            currentSession.remotePlayerId,
            currentSession.allowChestRewards,
            currentSession.allowXpRewards,
            currentSession.allowStatsProgression
        );

        return true;
    }

    public bool LoadPreparedMatchScene(string gameplaySceneName)
    {
        if (!PushPreparedSessionIntoMatchRuntimeConfig())
            return false;

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("[OnlineMatchSession] Gameplay scene name is empty.", this);
            return false;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
        return true;
    }

    private void HandleLobbyStateChanged(OnlineLobbyState lobbyState)
    {
        if (lobbyState == null)
            return;

        EnsureRuntime();

        currentSession.sessionId = string.IsNullOrWhiteSpace(lobbyState.sessionId) ? currentSession.sessionId : lobbyState.sessionId;
        currentSession.roomCode = string.IsNullOrWhiteSpace(lobbyState.roomCode) ? currentSession.roomCode : lobbyState.roomCode;

        currentSession.hasRemoteJoiner = lobbyState.HasJoinPlayer();
        currentSession.lobbyStatusText = lobbyState.statusText;

        currentSession.matchMode = lobbyState.matchMode;
        currentSession.pointsToWin = Mathf.Max(1, lobbyState.pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, lobbyState.matchDuration);
        currentSession.isRanked = lobbyState.isRanked;

        bool localIsHost = !string.IsNullOrWhiteSpace(currentSession.localProfileId) &&
                           string.Equals(lobbyState.hostPlayerId, currentSession.localProfileId, StringComparison.Ordinal);

        currentSession.isHost = localIsHost;
        currentSession.localParticipantSlot = localIsHost
            ? MatchRuntimeConfig.LocalParticipantSlot.Player1
            : MatchRuntimeConfig.LocalParticipantSlot.Player2;

        if (localIsHost)
        {
            if (lobbyState.joinPlayer != null && lobbyState.joinPlayer.isConnected)
            {
                currentSession.remotePlayerId = SanitizeOptionalValue(lobbyState.joinPlayer.playerId);
                currentSession.remoteDisplayName = SanitizeName(lobbyState.joinPlayer.displayName, "Player 2");
            }
        }
        else
        {
            if (lobbyState.hostPlayer != null && lobbyState.hostPlayer.isConnected)
            {
                currentSession.remotePlayerId = SanitizeOptionalValue(lobbyState.hostPlayer.playerId);
                currentSession.remoteDisplayName = SanitizeName(lobbyState.hostPlayer.displayName, "HostPlayer");
            }
        }

        NotifyUpdated();
    }

    private void HandleLobbyError(string errorMessage)
    {
        if (currentSession != null)
        {
            currentSession.lobbyStatusText = errorMessage;
            NotifyUpdated();
        }
    }

    private void HandleLobbyInfo(string infoMessage)
    {
        if (currentSession != null)
        {
            currentSession.lobbyStatusText = infoMessage;
            NotifyUpdated();
        }
    }

    private void SubscribeToLobbyService()
    {
        if (onlineLobbyService == null)
            return;

        onlineLobbyService.OnLobbyStateChanged -= HandleLobbyStateChanged;
        onlineLobbyService.OnLobbyStateChanged += HandleLobbyStateChanged;

        onlineLobbyService.OnLobbyError -= HandleLobbyError;
        onlineLobbyService.OnLobbyError += HandleLobbyError;

        onlineLobbyService.OnLobbyInfo -= HandleLobbyInfo;
        onlineLobbyService.OnLobbyInfo += HandleLobbyInfo;
    }

    private void UnsubscribeFromLobbyService()
    {
        if (onlineLobbyService == null)
            return;

        onlineLobbyService.OnLobbyStateChanged -= HandleLobbyStateChanged;
        onlineLobbyService.OnLobbyError -= HandleLobbyError;
        onlineLobbyService.OnLobbyInfo -= HandleLobbyInfo;
    }

    private void ResolveDependencies()
    {
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (onlineLobbyService == null)
            onlineLobbyService = FindFirstObjectByType<OnlineLobbyServiceBase>();
#else
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (onlineLobbyService == null)
            onlineLobbyService = FindObjectOfType<OnlineLobbyServiceBase>();
#endif
    }

    private void EnsureRuntime()
    {
        if (currentSession == null)
            currentSession = new OnlineMatchSessionData();
    }

    private void NotifyUpdated()
    {
        OnSessionUpdated?.Invoke();
    }

    private string SanitizeRoomCode(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return "ROOM01";

        return roomCode.Trim().ToUpperInvariant();
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