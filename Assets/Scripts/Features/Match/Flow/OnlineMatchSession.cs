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

    public string hostPlayerId = "";
    public string hostDisplayName = "HostPlayer";
    public string joinPlayerId = "";
    public string joinDisplayName = "JoinPlayer";

    public bool isHost = false;
    public bool isRanked = false;
    public bool useLiveMultiplayerServices = false;

    public MatchRuntimeConfig.MatchSessionType sessionType = MatchRuntimeConfig.MatchSessionType.OnlinePrivate;
    public MatchRuntimeConfig.MatchAuthorityType authorityType = MatchRuntimeConfig.MatchAuthorityType.HostClient;
    public MatchRuntimeConfig.LocalParticipantSlot localParticipantSlot = MatchRuntimeConfig.LocalParticipantSlot.Player1;

    public MatchMode matchMode = MatchMode.ScoreTarget;
    public int pointsToWin = 16;
    public float matchDuration = 180f;

    public bool allowChestRewards = false;
    public bool allowXpRewards = false;
    public bool allowStatsProgression = false;

    public bool hasRemoteJoiner = false;
    public bool matchStarted = false;
    public bool startRequested = false;
    public string lobbyStatusText = "";

    public string matchmakingQueueId = "";
    public bool isMatchmakingSearch = false;

    public string player1SkinUniqueId = "";
    public string player2SkinUniqueId = "";
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

        ResolveDependencies();

        if (currentSession == null)
            currentSession = new OnlineMatchSessionData();

        SubscribeToLobbyService();
    }

    private void OnDestroy()
    {
        UnsubscribeFromLobbyService();
    }

    public void ClearPreparedSession()
    {
        currentSession = new OnlineMatchSessionData();
        NotifyUpdated();
    }

    public void CancelMatchmakingSearch()
    {
        ResolveDependencies();

        if (currentSession == null || !currentSession.isMatchmakingSearch)
            return;

        if (onlineLobbyService != null)
            onlineLobbyService.CancelMatchmaking(currentSession.localProfileId);

        ClearPreparedSession();
    }

    public void LeaveCurrentLobbyOrMatchmaking()
    {
        ResolveDependencies();

        if (!HasPreparedSession)
            return;

        if (onlineLobbyService != null)
            onlineLobbyService.LeaveLobby(currentSession.localProfileId);

        ClearPreparedSession();
    }

    public void PreparePrivateHostSession(
        string localProfileId,
        string localDisplayName,
        string remoteDisplayName,
        MatchMode matchMode,
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
        currentSession.roomCode = string.IsNullOrWhiteSpace(customRoomCode) ? string.Empty : customRoomCode.Trim().ToUpperInvariant();

        currentSession.localProfileId = Sanitize(localProfileId, "local_player_1");
        currentSession.localDisplayName = Sanitize(localDisplayName, "HostPlayer");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = Sanitize(remoteDisplayName, "Player 2");

        currentSession.hostPlayerId = currentSession.localProfileId;
        currentSession.hostDisplayName = currentSession.localDisplayName;
        currentSession.joinPlayerId = string.Empty;
        currentSession.joinDisplayName = Sanitize(remoteDisplayName, "JoinPlayer");

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
        currentSession.matchStarted = false;
        currentSession.startRequested = false;
        currentSession.lobbyStatusText = useLiveMultiplayerServices ? "Creating online lobby" : "Preparing host session";

        currentSession.matchmakingQueueId = string.Empty;
        currentSession.isMatchmakingSearch = false;

        currentSession.player1SkinUniqueId = ResolveLocalSkinUniqueId(MatchRuntimeConfig.LocalParticipantSlot.Player1);
        currentSession.player2SkinUniqueId = string.Empty;

        SyncPreparedSessionIntoRuntimeConfigIfPossible();
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
        MatchMode matchMode,
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

        currentSession.localProfileId = Sanitize(localProfileId, "local_player_2");
        currentSession.localDisplayName = Sanitize(localDisplayName, "JoinPlayer");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = Sanitize(remoteDisplayName, "HostPlayer");

        currentSession.hostPlayerId = string.Empty;
        currentSession.hostDisplayName = Sanitize(remoteDisplayName, "HostPlayer");
        currentSession.joinPlayerId = currentSession.localProfileId;
        currentSession.joinDisplayName = currentSession.localDisplayName;

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
        currentSession.matchStarted = false;
        currentSession.startRequested = false;
        currentSession.lobbyStatusText = useLiveMultiplayerServices ? "Joining online lobby" : "Preparing join session";

        currentSession.matchmakingQueueId = string.Empty;
        currentSession.isMatchmakingSearch = false;

        currentSession.player1SkinUniqueId = string.Empty;
        currentSession.player2SkinUniqueId = ResolveLocalSkinUniqueId(MatchRuntimeConfig.LocalParticipantSlot.Player2);

        SyncPreparedSessionIntoRuntimeConfigIfPossible();
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
        MatchMode matchMode,
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

        currentSession.localProfileId = Sanitize(localProfileId, "local_player_1");
        currentSession.localDisplayName = Sanitize(localDisplayName, "Player 1");
        currentSession.remotePlayerId = string.Empty;
        currentSession.remoteDisplayName = "Searching...";

        currentSession.hostPlayerId = string.Empty;
        currentSession.hostDisplayName = "Searching...";
        currentSession.joinPlayerId = string.Empty;
        currentSession.joinDisplayName = "Searching...";

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
        currentSession.matchStarted = false;
        currentSession.startRequested = false;
        currentSession.lobbyStatusText = "Searching matchmaking";

        currentSession.matchmakingQueueId = Sanitize(queueId, string.Empty);
        currentSession.isMatchmakingSearch = true;

        currentSession.player1SkinUniqueId = ResolveLocalSkinUniqueId(MatchRuntimeConfig.LocalParticipantSlot.Player1);
        currentSession.player2SkinUniqueId = string.Empty;

        SyncPreparedSessionIntoRuntimeConfigIfPossible();
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
        return HasPreparedSession &&
               currentSession.isHost &&
               currentSession.useLiveMultiplayerServices &&
               currentSession.hasRemoteJoiner &&
               !currentSession.matchStarted;
    }

    public bool RequestHostStartMatch()
    {
        ResolveDependencies();

        if (!CanHostStartMatch())
            return false;

        if (onlineLobbyService == null)
            return false;

        currentSession.startRequested = true;
        currentSession.lobbyStatusText = "Starting match";
        onlineLobbyService.RequestStartMatch(currentSession.localProfileId);
        NotifyUpdated();
        return true;
    }

    public bool PushPreparedSessionIntoMatchRuntimeConfig()
    {
        ResolveDependencies();

        if (!HasPreparedSession || matchRuntimeConfig == null)
            return false;

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

        matchRuntimeConfig.SetPlayerSkinUniqueIds(
            currentSession.player1SkinUniqueId,
            currentSession.player2SkinUniqueId
        );

        return true;
    }

    public bool LoadPreparedMatchScene(string gameplaySceneName)
    {
        if (!PushPreparedSessionIntoMatchRuntimeConfig())
            return false;

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
            return false;

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
        currentSession.matchStarted = lobbyState.matchStarted;
        currentSession.startRequested = lobbyState.startRequested;
        currentSession.lobbyStatusText = lobbyState.statusText;

        currentSession.matchMode = lobbyState.matchMode;
        currentSession.pointsToWin = Mathf.Max(1, lobbyState.pointsToWin);
        currentSession.matchDuration = Mathf.Max(1f, lobbyState.matchDuration);
        currentSession.isRanked = lobbyState.isRanked;

        currentSession.hostPlayerId = lobbyState.hostPlayer != null ? lobbyState.hostPlayer.playerId : string.Empty;
        currentSession.hostDisplayName = lobbyState.hostPlayer != null ? lobbyState.hostPlayer.displayName : "HostPlayer";

        currentSession.joinPlayerId = lobbyState.joinPlayer != null ? lobbyState.joinPlayer.playerId : string.Empty;
        currentSession.joinDisplayName = lobbyState.joinPlayer != null ? lobbyState.joinPlayer.displayName : "JoinPlayer";

        bool localIsHost =
            !string.IsNullOrWhiteSpace(currentSession.localProfileId) &&
            string.Equals(currentSession.hostPlayerId, currentSession.localProfileId, StringComparison.Ordinal);

        currentSession.isHost = localIsHost;
        currentSession.localParticipantSlot = localIsHost
            ? MatchRuntimeConfig.LocalParticipantSlot.Player1
            : MatchRuntimeConfig.LocalParticipantSlot.Player2;

        if (localIsHost)
        {
            currentSession.localDisplayName = currentSession.hostDisplayName;
            currentSession.remotePlayerId = currentSession.joinPlayerId;
            currentSession.remoteDisplayName = currentSession.joinDisplayName;
        }
        else
        {
            currentSession.localDisplayName = string.IsNullOrWhiteSpace(currentSession.joinDisplayName)
                ? currentSession.localDisplayName
                : currentSession.joinDisplayName;

            currentSession.remotePlayerId = currentSession.hostPlayerId;
            currentSession.remoteDisplayName = currentSession.hostDisplayName;
        }

        SyncPreparedSessionIntoRuntimeConfigIfPossible();
        NotifyUpdated();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchSession] HandleLobbyStateChanged -> " +
                "RemoteJoiner=" + currentSession.hasRemoteJoiner +
                " | MatchStarted=" + currentSession.matchStarted +
                " | Status=" + currentSession.lobbyStatusText,
                this
            );
        }
    }

    private void HandleLobbyError(string errorMessage)
    {
        if (currentSession == null)
            return;

        currentSession.lobbyStatusText = errorMessage;
        NotifyUpdated();
    }

    private void HandleLobbyInfo(string infoMessage)
    {
        if (currentSession == null)
            return;

        currentSession.lobbyStatusText = infoMessage;
        NotifyUpdated();
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
        if (onlineLobbyService == null)
            onlineLobbyService = FindFirstObjectByType<OnlineLobbyServiceBase>();
#else
        if (onlineLobbyService == null)
            onlineLobbyService = FindObjectOfType<OnlineLobbyServiceBase>();
#endif
    }

    private void EnsureRuntime()
    {
        if (currentSession == null)
            currentSession = new OnlineMatchSessionData();
    }

    private void SyncPreparedSessionIntoRuntimeConfigIfPossible()
    {
        if (HasPreparedSession && matchRuntimeConfig != null)
            PushPreparedSessionIntoMatchRuntimeConfig();
    }

    private void NotifyUpdated()
    {
        OnSessionUpdated?.Invoke();
    }

    private string ResolveLocalSkinUniqueId(MatchRuntimeConfig.LocalParticipantSlot slot)
    {
        if (PlayerSkinLoadout.Instance == null)
            return string.Empty;

        BallSkinData skin = slot == MatchRuntimeConfig.LocalParticipantSlot.Player1
            ? PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer1()
            : PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer2();

        if (skin == null || string.IsNullOrWhiteSpace(skin.skinUniqueId))
            return string.Empty;

        return skin.skinUniqueId.Trim();
    }

    private string SanitizeRoomCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "ROOM01";

        return value.Trim().ToUpperInvariant();
    }

    private string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}