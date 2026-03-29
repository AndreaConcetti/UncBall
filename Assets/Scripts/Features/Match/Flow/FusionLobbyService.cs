using Fusion;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class FusionLobbyService : OnlineLobbyServiceBase
{
    public static FusionLobbyService Instance { get; private set; }

    private const string HostNamePropertyKey = "hn";
    private const string JoinNamePropertyKey = "jn";
    private const string QueueIdPropertyKey = "qid";
    private const string MatchModePropertyKey = "mm";
    private const string PointsToWinPropertyKey = "pt";
    private const string MatchDurationPropertyKey = "md";
    private const string RankedPropertyKey = "rk";

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;

    [Header("Lobby Config")]
    [SerializeField] private string customLobbyName = "uncball_private_lobby";
    [SerializeField] private string matchmakingLobbyName = "uncball_matchmaking_lobby";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private float sessionPropertyPollInterval = 0.25f;

    [SerializeField] private OnlineLobbyState currentLobbyState = new OnlineLobbyState();

    private string pendingLocalPlayerId = string.Empty;
    private string pendingLocalDisplayName = string.Empty;
    private bool pendingLocalIsHost = false;
    private bool operationInProgress = false;
    private float nextPropertyPollTime = 0f;

    public override bool HasActiveLobby => currentLobbyState != null && currentLobbyState.hasLobby;
    public override OnlineLobbyState CurrentLobbyState => currentLobbyState;

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

        if (currentLobbyState == null)
            currentLobbyState = new OnlineLobbyState();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (runnerManager != null)
        {
            runnerManager.OnPlayerJoinedEvent += HandlePlayerJoined;
            runnerManager.OnPlayerLeftEvent += HandlePlayerLeft;
            runnerManager.OnShutdownEvent += HandleShutdown;
        }
    }

    private void OnDisable()
    {
        if (runnerManager != null)
        {
            runnerManager.OnPlayerJoinedEvent -= HandlePlayerJoined;
            runnerManager.OnPlayerLeftEvent -= HandlePlayerLeft;
            runnerManager.OnShutdownEvent -= HandleShutdown;
        }
    }

    private void Update()
    {
        if (!HasActiveLobby || runnerManager == null || !runnerManager.IsRunning)
            return;

        if (Time.unscaledTime < nextPropertyPollTime)
            return;

        nextPropertyPollTime = Time.unscaledTime + Mathf.Max(0.1f, sessionPropertyPollInterval);
        SyncNamesAndMetaFromSessionProperties();
    }

    public override async void CreateLobby(
        string localPlayerId,
        string localDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        ResolveDependencies();

        if (runnerManager == null)
        {
            RaiseLobbyError("Fusion runner manager missing.");
            return;
        }

        if (operationInProgress)
        {
            RaiseLobbyError("Lobby operation already in progress.");
            return;
        }

        operationInProgress = true;
        pendingLocalPlayerId = Sanitize(localPlayerId, "local_player_1");
        pendingLocalDisplayName = Sanitize(localDisplayName, "HostPlayer");
        pendingLocalIsHost = true;

        string roomCode = GenerateRoomCode();

        currentLobbyState.Clear();
        currentLobbyState.hasLobby = false;
        currentLobbyState.sessionId = Guid.NewGuid().ToString("N");
        currentLobbyState.roomCode = roomCode;
        currentLobbyState.hostPlayerId = pendingLocalPlayerId;

        currentLobbyState.hostPlayer.playerId = pendingLocalPlayerId;
        currentLobbyState.hostPlayer.displayName = pendingLocalDisplayName;
        currentLobbyState.hostPlayer.isHost = true;
        currentLobbyState.hostPlayer.isReady = true;
        currentLobbyState.hostPlayer.isConnected = false;

        currentLobbyState.joinPlayer = new OnlineLobbyPlayerData();

        currentLobbyState.matchMode = matchMode;
        currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);
        currentLobbyState.matchDuration = Mathf.Max(1f, matchDuration);
        currentLobbyState.isRanked = isRanked;
        currentLobbyState.startRequested = false;
        currentLobbyState.matchStarted = false;
        currentLobbyState.statusText = "Creating lobby";

        Dictionary<string, SessionProperty> sessionProps = BuildSessionProperties(
            pendingLocalDisplayName,
            string.Empty,
            string.Empty,
            matchMode,
            pointsToWin,
            matchDuration,
            isRanked
        );

        bool ok = await runnerManager.StartHostLobbyAsync(
            roomCode,
            maxPlayers,
            sessionProps,
            customLobbyName
        );

        operationInProgress = false;

        if (!ok)
        {
            currentLobbyState.statusText = "Create lobby failed";
            RaiseLobbyError("Failed to create Fusion lobby.");
            RaiseLobbyStateChanged(currentLobbyState);
            return;
        }

        currentLobbyState.hasLobby = true;
        currentLobbyState.roomCode = GetResolvedRoomCode(roomCode);
        currentLobbyState.statusText = "Waiting for opponent";
        currentLobbyState.hostPlayer.isConnected = true;

        SyncNamesAndMetaFromSessionProperties();
        RaiseLobbyInfo("Lobby created");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override async void JoinLobby(
        string roomCode,
        string localPlayerId,
        string localDisplayName)
    {
        ResolveDependencies();

        if (runnerManager == null)
        {
            RaiseLobbyError("Fusion runner manager missing.");
            return;
        }

        if (operationInProgress)
        {
            RaiseLobbyError("Lobby operation already in progress.");
            return;
        }

        operationInProgress = true;
        pendingLocalPlayerId = Sanitize(localPlayerId, "local_player_2");
        pendingLocalDisplayName = Sanitize(localDisplayName, "JoinPlayer");
        pendingLocalIsHost = false;

        string sanitizedRoomCode = SanitizeRoomCode(roomCode);
        byte[] token = BuildConnectionToken(pendingLocalPlayerId, pendingLocalDisplayName);

        currentLobbyState.Clear();
        currentLobbyState.hasLobby = false;
        currentLobbyState.sessionId = Guid.NewGuid().ToString("N");
        currentLobbyState.roomCode = sanitizedRoomCode;
        currentLobbyState.hostPlayerId = string.Empty;

        currentLobbyState.hostPlayer.displayName = "HostPlayer";
        currentLobbyState.hostPlayer.isHost = true;
        currentLobbyState.hostPlayer.isReady = true;
        currentLobbyState.hostPlayer.isConnected = false;

        currentLobbyState.joinPlayer.playerId = pendingLocalPlayerId;
        currentLobbyState.joinPlayer.displayName = pendingLocalDisplayName;
        currentLobbyState.joinPlayer.isHost = false;
        currentLobbyState.joinPlayer.isReady = true;
        currentLobbyState.joinPlayer.isConnected = false;

        currentLobbyState.statusText = "Joining lobby";

        bool ok = await runnerManager.StartClientLobbyAsync(
            sanitizedRoomCode,
            maxPlayers,
            customLobbyName,
            token
        );

        operationInProgress = false;

        if (!ok)
        {
            currentLobbyState.statusText = "Join lobby failed";
            RaiseLobbyError("Failed to join Fusion lobby.");
            RaiseLobbyStateChanged(currentLobbyState);
            return;
        }

        currentLobbyState.hasLobby = true;
        currentLobbyState.roomCode = GetResolvedRoomCode(sanitizedRoomCode);
        currentLobbyState.hostPlayer.isConnected = true;
        currentLobbyState.joinPlayer.isConnected = true;
        currentLobbyState.statusText = "Joined lobby / waiting host";

        SyncNamesAndMetaFromSessionProperties();
        RaiseLobbyInfo("Joined lobby");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override async void BeginMatchmaking(
        string queueId,
        string localPlayerId,
        string localDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        ResolveDependencies();

        if (runnerManager == null)
        {
            RaiseLobbyError("Fusion runner manager missing.");
            return;
        }

        if (operationInProgress)
        {
            RaiseLobbyError("Lobby operation already in progress.");
            return;
        }

        operationInProgress = true;
        pendingLocalPlayerId = Sanitize(localPlayerId, "local_player_1");
        pendingLocalDisplayName = Sanitize(localDisplayName, "Player 1");
        pendingLocalIsHost = false;

        byte[] token = BuildConnectionToken(pendingLocalPlayerId, pendingLocalDisplayName);

        currentLobbyState.Clear();
        currentLobbyState.hasLobby = false;
        currentLobbyState.sessionId = Guid.NewGuid().ToString("N");
        currentLobbyState.roomCode = string.Empty;
        currentLobbyState.hostPlayerId = string.Empty;
        currentLobbyState.hostPlayer.displayName = "Searching...";
        currentLobbyState.joinPlayer.displayName = pendingLocalDisplayName;
        currentLobbyState.matchMode = matchMode;
        currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);
        currentLobbyState.matchDuration = Mathf.Max(1f, matchDuration);
        currentLobbyState.isRanked = isRanked;
        currentLobbyState.statusText = "Searching matchmaking";

        Dictionary<string, SessionProperty> filters = BuildSessionProperties(
            hostName: string.Empty,
            joinName: string.Empty,
            queueId: Sanitize(queueId, "normal"),
            matchMode,
            pointsToWin,
            matchDuration,
            isRanked
        );

        bool ok = await runnerManager.StartMatchmakingAsync(
            filters,
            maxPlayers,
            matchmakingLobbyName,
            token
        );

        operationInProgress = false;

        if (!ok)
        {
            currentLobbyState.statusText = "Matchmaking failed";
            RaiseLobbyError("Failed to start matchmaking.");
            RaiseLobbyStateChanged(currentLobbyState);
            return;
        }

        currentLobbyState.hasLobby = true;
        currentLobbyState.roomCode = GetResolvedRoomCode("MATCH");
        currentLobbyState.hostPlayerId = runnerManager.IsCurrentRunnerServer() ? pendingLocalPlayerId : currentLobbyState.hostPlayerId;

        if (runnerManager.IsCurrentRunnerServer())
        {
            pendingLocalIsHost = true;

            currentLobbyState.hostPlayer.playerId = pendingLocalPlayerId;
            currentLobbyState.hostPlayer.displayName = pendingLocalDisplayName;
            currentLobbyState.hostPlayer.isHost = true;
            currentLobbyState.hostPlayer.isReady = true;
            currentLobbyState.hostPlayer.isConnected = true;

            currentLobbyState.joinPlayer = new OnlineLobbyPlayerData();
            currentLobbyState.statusText = "Waiting for opponent";
        }
        else
        {
            pendingLocalIsHost = false;

            currentLobbyState.joinPlayer.playerId = pendingLocalPlayerId;
            currentLobbyState.joinPlayer.displayName = pendingLocalDisplayName;
            currentLobbyState.joinPlayer.isHost = false;
            currentLobbyState.joinPlayer.isReady = true;
            currentLobbyState.joinPlayer.isConnected = true;
            currentLobbyState.statusText = "Joined matchmaking lobby";
        }

        SyncNamesAndMetaFromSessionProperties();
        RaiseLobbyInfo("Matchmaking session ready");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override async void CancelMatchmaking(string localPlayerId)
    {
        await LeaveInternal();
    }

    public override async void LeaveLobby(string localPlayerId)
    {
        await LeaveInternal();
    }

    private async System.Threading.Tasks.Task LeaveInternal()
    {
        ResolveDependencies();

        if (runnerManager != null && runnerManager.HasActiveRunner)
            await runnerManager.ShutdownRunnerAsync();

        currentLobbyState.Clear();
        operationInProgress = false;
        RaiseLobbyInfo("Lobby closed");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override void SetLocalReady(string localPlayerId, bool isReady)
    {
        if (!HasActiveLobby)
            return;

        if (currentLobbyState.hostPlayer != null && currentLobbyState.hostPlayer.playerId == localPlayerId)
            currentLobbyState.hostPlayer.isReady = isReady;

        if (currentLobbyState.joinPlayer != null && currentLobbyState.joinPlayer.playerId == localPlayerId)
            currentLobbyState.joinPlayer.isReady = isReady;

        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override void RequestStartMatch(string localPlayerId)
    {
        if (!HasActiveLobby)
        {
            RaiseLobbyError("No active lobby.");
            return;
        }

        if (!currentLobbyState.HasHost() || !currentLobbyState.HasJoinPlayer())
        {
            RaiseLobbyError("Cannot start: lobby is not full.");
            return;
        }

        if (currentLobbyState.hostPlayer.playerId != localPlayerId)
        {
            RaiseLobbyError("Only host can start the match.");
            return;
        }

        currentLobbyState.startRequested = true;
        currentLobbyState.matchStarted = true;
        currentLobbyState.statusText = "Match starting";

        RaiseLobbyInfo("Match starting");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    private void HandlePlayerJoined(PlayerRef player)
    {
        if (!HasActiveLobby || runnerManager == null || !runnerManager.IsRunning)
            return;

        int count = runnerManager.GetCurrentPlayerCount();

        if (pendingLocalIsHost)
        {
            currentLobbyState.hostPlayer.isConnected = true;

            if (count >= 2)
            {
                string joinPlayerName = TryReadJoinNameFromConnectionToken(player);
                if (!string.IsNullOrWhiteSpace(joinPlayerName))
                    currentLobbyState.joinPlayer.displayName = joinPlayerName;

                currentLobbyState.joinPlayer.playerId = string.IsNullOrWhiteSpace(currentLobbyState.joinPlayer.playerId)
                    ? "remote_player"
                    : currentLobbyState.joinPlayer.playerId;

                currentLobbyState.joinPlayer.displayName = Sanitize(currentLobbyState.joinPlayer.displayName, "JoinPlayer");
                currentLobbyState.joinPlayer.isHost = false;
                currentLobbyState.joinPlayer.isReady = true;
                currentLobbyState.joinPlayer.isConnected = true;
                currentLobbyState.statusText = "Lobby full / ready";

                runnerManager.TryUpdateSessionProperties(new Dictionary<string, SessionProperty>
                {
                    { JoinNamePropertyKey, currentLobbyState.joinPlayer.displayName }
                });
            }
            else
            {
                currentLobbyState.statusText = "Waiting for opponent";
            }
        }
        else
        {
            currentLobbyState.hostPlayer.isConnected = true;
            currentLobbyState.joinPlayer.playerId = pendingLocalPlayerId;
            currentLobbyState.joinPlayer.displayName = pendingLocalDisplayName;
            currentLobbyState.joinPlayer.isHost = false;
            currentLobbyState.joinPlayer.isReady = true;
            currentLobbyState.joinPlayer.isConnected = true;
            currentLobbyState.statusText = count >= 2 ? "Joined lobby / ready" : "Joined lobby / waiting";
        }

        SyncNamesAndMetaFromSessionProperties();
        RaiseLobbyStateChanged(currentLobbyState);
    }

    private void HandlePlayerLeft(PlayerRef player)
    {
        if (!HasActiveLobby)
            return;

        if (pendingLocalIsHost)
        {
            currentLobbyState.joinPlayer = new OnlineLobbyPlayerData();
            currentLobbyState.startRequested = false;
            currentLobbyState.matchStarted = false;
            currentLobbyState.statusText = "Waiting for opponent";

            if (runnerManager != null && runnerManager.IsRunning)
            {
                runnerManager.TryUpdateSessionProperties(new Dictionary<string, SessionProperty>
                {
                    { JoinNamePropertyKey, string.Empty }
                });
            }
        }
        else
        {
            currentLobbyState.statusText = "Disconnected from lobby";
        }

        RaiseLobbyStateChanged(currentLobbyState);
    }

    private void HandleShutdown(ShutdownReason reason)
    {
        if (!HasActiveLobby)
            return;

        currentLobbyState.statusText = "Lobby shutdown: " + reason;
        RaiseLobbyStateChanged(currentLobbyState);
    }

    private void SyncNamesAndMetaFromSessionProperties()
    {
        if (runnerManager == null || !runnerManager.IsRunning)
            return;

        if (TryReadSessionPropertyString(HostNamePropertyKey, out string hostName))
        {
            currentLobbyState.hostPlayer.displayName = Sanitize(hostName, "HostPlayer");
        }

        if (TryReadSessionPropertyString(JoinNamePropertyKey, out string joinName))
        {
            if (!string.IsNullOrWhiteSpace(joinName))
            {
                currentLobbyState.joinPlayer.displayName = Sanitize(joinName, "JoinPlayer");
                currentLobbyState.joinPlayer.isConnected = true;
            }
        }

        if (TryReadSessionPropertyInt(MatchModePropertyKey, out int matchModeValue))
            currentLobbyState.matchMode = (StartEndController.MatchMode)Mathf.Clamp(matchModeValue, 0, 1);

        if (TryReadSessionPropertyInt(PointsToWinPropertyKey, out int pointsToWin))
            currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);

        if (TryReadSessionPropertyInt(MatchDurationPropertyKey, out int matchDuration))
            currentLobbyState.matchDuration = Mathf.Max(1, matchDuration);

        if (TryReadSessionPropertyInt(RankedPropertyKey, out int ranked))
            currentLobbyState.isRanked = ranked == 1;
    }

    private bool TryReadSessionPropertyString(string key, out string value)
    {
        value = string.Empty;

        if (!runnerManager.TryGetCurrentSessionProperty(key, out SessionProperty prop))
            return false;

        string raw = prop.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        Match m = Regex.Match(raw, @"^\[SessionProperty:\s*(.*?),\s*Type=.*\]$");
        if (m.Success)
        {
            value = m.Groups[1].Value?.Trim() ?? string.Empty;
            return true;
        }

        value = raw.Trim();
        return true;
    }

    private bool TryReadSessionPropertyInt(string key, out int value)
    {
        value = 0;

        if (!runnerManager.TryGetCurrentSessionProperty(key, out SessionProperty prop))
            return false;

        string raw = prop.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        Match m = Regex.Match(raw, @"^\[SessionProperty:\s*(.*?),\s*Type=.*\]$");
        if (m.Success)
            raw = m.Groups[1].Value?.Trim() ?? string.Empty;

        return int.TryParse(raw, out value);
    }

    private string TryReadJoinNameFromConnectionToken(PlayerRef player)
    {
        byte[] token = runnerManager.GetPlayerConnectionToken(player);
        if (token == null || token.Length == 0)
            return string.Empty;

        try
        {
            string raw = Encoding.UTF8.GetString(token);
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string[] parts = raw.Split('|');
            if (parts.Length < 2)
                return string.Empty;

            return Sanitize(parts[1], "JoinPlayer");
        }
        catch
        {
            return string.Empty;
        }
    }

    private byte[] BuildConnectionToken(string playerId, string displayName)
    {
        string tokenString = Sanitize(playerId, "local_player_2") + "|" + Sanitize(displayName, "JoinPlayer");
        return Encoding.UTF8.GetBytes(tokenString);
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();
#else
        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();
#endif
    }

    private Dictionary<string, SessionProperty> BuildSessionProperties(
        string hostName,
        string joinName,
        string queueId,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        return new Dictionary<string, SessionProperty>
        {
            { HostNamePropertyKey, Sanitize(hostName, string.Empty) },
            { JoinNamePropertyKey, Sanitize(joinName, string.Empty) },
            { QueueIdPropertyKey, Sanitize(queueId, string.Empty) },
            { MatchModePropertyKey, (int)matchMode },
            { PointsToWinPropertyKey, Mathf.Max(1, pointsToWin) },
            { MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, matchDuration)) },
            { RankedPropertyKey, isRanked ? 1 : 0 }
        };
    }

    private string GetResolvedRoomCode(string fallback)
    {
        if (runnerManager != null)
        {
            string sessionName = runnerManager.GetCurrentSessionName();
            if (!string.IsNullOrWhiteSpace(sessionName))
                return sessionName.Trim().ToUpperInvariant();
        }

        return fallback;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] result = new char[6];

        for (int i = 0; i < result.Length; i++)
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];

        return new string(result);
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