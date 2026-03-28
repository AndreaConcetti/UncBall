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
    public MatchRuntimeConfig.MatchAuthorityType authorityType = MatchRuntimeConfig.MatchAuthorityType.DedicatedServer;
    public MatchRuntimeConfig.LocalParticipantSlot localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

    public StartEndController.MatchMode matchMode = StartEndController.MatchMode.ScoreTarget;
    public int pointsToWin = 16;
    public float matchDuration = 180f;

    public bool allowChestRewards = false;
    public bool allowXpRewards = false;
    public bool allowStatsProgression = false;

    public bool hasRemoteJoiner = false;
    public string lobbyStatusText = "";
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

        if (logDebug)
            Debug.Log("[OnlineMatchSession] Cleared prepared session.", this);
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
        currentSession.localDisplayName = SanitizeName(localDisplayName, "Player 1");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = SanitizeName(remoteDisplayName, "Remote Player");

        currentSession.isHost = true;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
        currentSession.authorityType = useLiveMultiplayerServices
            ? MatchRuntimeConfig.MatchAuthorityType.DedicatedServer
            : MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = false;
        currentSession.lobbyStatusText = "Preparing host lobby";

        ResolveDependencies();

        if (onlineLobbyService != null)
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

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] PreparePrivateHostSession -> " +
                "LocalProfileId=" + currentSession.localProfileId +
                " | MatchMode=" + currentSession.matchMode +
                " | PointsToWin=" + currentSession.pointsToWin +
                " | Duration=" + currentSession.matchDuration,
                this
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

        currentSession.localProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        currentSession.localDisplayName = SanitizeName(localDisplayName, "Player 1");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = SanitizeName(remoteDisplayName, "Remote Player");

        currentSession.isHost = false;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
        currentSession.authorityType = useLiveMultiplayerServices
            ? MatchRuntimeConfig.MatchAuthorityType.DedicatedServer
            : MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player2;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = true;
        currentSession.lobbyStatusText = "Joining lobby";

        ResolveDependencies();

        if (onlineLobbyService != null)
        {
            onlineLobbyService.JoinLobby(
                currentSession.roomCode,
                currentSession.localProfileId,
                currentSession.localDisplayName
            );
        }

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] PreparePrivateJoinSession -> " +
                "RoomCode=" + currentSession.roomCode,
                this
            );
        }
    }

    public void PrepareMatchmakingSession(
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
        currentSession.roomCode = string.Empty;

        currentSession.localProfileId = SanitizeProfileId(localProfileId, "local_player_1");
        currentSession.localDisplayName = SanitizeName(localDisplayName, "Player 1");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = SanitizeName(remoteDisplayName, "Remote Player");

        currentSession.isHost = false;
        currentSession.isRanked = isRanked;
        currentSession.useLiveMultiplayerServices = useLiveMultiplayerServices;

        currentSession.sessionType = MatchRuntimeConfig.MatchSessionType.OnlineMatchmaking;
        currentSession.authorityType = useLiveMultiplayerServices
            ? MatchRuntimeConfig.MatchAuthorityType.DedicatedServer
            : MatchRuntimeConfig.MatchAuthorityType.HostClient;
        currentSession.localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

        currentSession.matchMode = matchMode;
        currentSession.pointsToWin = Mathf.Max(1, pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, matchDuration);

        currentSession.allowChestRewards = allowChestRewards;
        currentSession.allowXpRewards = allowXpRewards;
        currentSession.allowStatsProgression = allowStatsProgression;

        currentSession.hasRemoteJoiner = true;
        currentSession.lobbyStatusText = isRanked ? "Ranked matchmaking ready" : "Normal matchmaking ready";

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] PrepareMatchmakingSession -> " +
                "Ranked=" + currentSession.isRanked,
                this
            );
        }
    }

    public void MarkRemoteJoinerConnectedForDebug()
    {
        EnsureRuntime();

        currentSession.hasRemoteJoiner = true;
        currentSession.lobbyStatusText = "Opponent joined";

        if (logDebug)
            Debug.Log("[OnlineMatchSession] Remote joiner marked as connected.", this);
    }

    public bool CanHostStartMatch()
    {
        if (!HasPreparedSession)
            return false;

        if (!currentSession.isHost)
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

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] Prepared session pushed into MatchRuntimeConfig. " +
                "SessionId=" + currentSession.sessionId +
                " | RoomCode=" + currentSession.roomCode,
                this
            );
        }

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

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] Loading prepared gameplay scene -> " + gameplaySceneName,
                this
            );
        }

        return true;
    }

    private void HandleLobbyStateChanged(OnlineLobbyState lobbyState)
    {
        if (lobbyState == null)
            return;

        EnsureRuntime();

        currentSession.sessionId = lobbyState.sessionId;
        currentSession.roomCode = lobbyState.roomCode;
        currentSession.hasRemoteJoiner = lobbyState.HasJoinPlayer();
        currentSession.lobbyStatusText = lobbyState.statusText;

        if (currentSession.isHost)
        {
            if (lobbyState.joinPlayer != null && lobbyState.joinPlayer.isConnected)
            {
                currentSession.remotePlayerId = lobbyState.joinPlayer.playerId;
                currentSession.remoteDisplayName = lobbyState.joinPlayer.displayName;
            }
        }
        else
        {
            if (lobbyState.hostPlayer != null && lobbyState.hostPlayer.isConnected)
            {
                currentSession.remotePlayerId = lobbyState.hostPlayer.playerId;
                currentSession.remoteDisplayName = lobbyState.hostPlayer.displayName;
            }
        }

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] HandleLobbyStateChanged -> " +
                "RoomCode=" + currentSession.roomCode +
                " | HasRemoteJoiner=" + currentSession.hasRemoteJoiner +
                " | Status=" + currentSession.lobbyStatusText,
                this
            );
        }
    }

    private void HandleLobbyError(string errorMessage)
    {
        if (logDebug)
            Debug.LogWarning("[OnlineMatchSession] Lobby error -> " + errorMessage, this);

        if (currentSession != null)
            currentSession.lobbyStatusText = errorMessage;
    }

    private void HandleLobbyInfo(string infoMessage)
    {
        if (logDebug)
            Debug.Log("[OnlineMatchSession] Lobby info -> " + infoMessage, this);

        if (currentSession != null)
            currentSession.lobbyStatusText = infoMessage;
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