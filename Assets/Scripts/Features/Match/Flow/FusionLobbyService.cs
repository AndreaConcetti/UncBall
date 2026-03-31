using Fusion;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class FusionLobbyService : OnlineLobbyServiceBase
{
    public static FusionLobbyService Instance { get; private set; }

    private const string HostIdPropertyKey = "hid";
    private const string HostNamePropertyKey = "hn";
    private const string JoinIdPropertyKey = "jid";
    private const string JoinNamePropertyKey = "jn";
    private const string QueueIdPropertyKey = "qid";
    private const string MatchModePropertyKey = "mm";
    private const string PointsToWinPropertyKey = "pt";
    private const string MatchDurationPropertyKey = "md";
    private const string RankedPropertyKey = "rk";

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private string customLobbyName = "uncball_private_lobby";
    [SerializeField] private string matchmakingLobbyName = "uncball_matchmaking_lobby";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private float sessionPropertyPollInterval = 0.25f;
    [SerializeField] private OnlineLobbyState currentLobbyState = new OnlineLobbyState();

    private string pendingLocalPlayerId = string.Empty;
    private string pendingLocalDisplayName = string.Empty;
    private bool pendingLocalIsHost = false;
    private float nextPropertyPollTime = 0f;
    private bool operationInProgress = false;

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
        MatchMode matchMode,
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
        currentLobbyState.sessionId = roomCode;
        currentLobbyState.roomCode = roomCode;
        currentLobbyState.hostPlayerId = pendingLocalPlayerId;
        currentLobbyState.matchMode = matchMode;
        currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);
        currentLobbyState.matchDuration = Mathf.Max(1f, matchDuration);
        currentLobbyState.isRanked = isRanked;
        currentLobbyState.statusText = "Creating lobby";

        currentLobbyState.hostPlayer.playerId = pendingLocalPlayerId;
        currentLobbyState.hostPlayer.displayName = pendingLocalDisplayName;
        currentLobbyState.hostPlayer.isHost = true;
        currentLobbyState.hostPlayer.isReady = true;

        Dictionary<string, SessionProperty> sessionProps = BuildSessionProperties(
            pendingLocalPlayerId,
            pendingLocalDisplayName,
            string.Empty,
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
        currentLobbyState.hostPlayer.isConnected = true;
        currentLobbyState.statusText = "Waiting for opponent";

        PublishHostProperties();
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
        currentLobbyState.roomCode = sanitizedRoomCode;
        currentLobbyState.sessionId = sanitizedRoomCode;
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
        currentLobbyState.joinPlayer.playerId = pendingLocalPlayerId;
        currentLobbyState.joinPlayer.displayName = pendingLocalDisplayName;
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
        MatchMode matchMode,
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

        string bucketSessionName = BuildDeterministicMatchmakingBucket(
            queueId,
            matchMode,
            pointsToWin,
            matchDuration,
            isRanked
        );

        byte[] token = BuildConnectionToken(pendingLocalPlayerId, pendingLocalDisplayName);

        currentLobbyState.Clear();
        currentLobbyState.sessionId = bucketSessionName;
        currentLobbyState.roomCode = bucketSessionName;
        currentLobbyState.matchMode = matchMode;
        currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);
        currentLobbyState.matchDuration = Mathf.Max(1f, matchDuration);
        currentLobbyState.isRanked = isRanked;
        currentLobbyState.statusText = "Searching matchmaking";

        Dictionary<string, SessionProperty> properties = BuildSessionProperties(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Sanitize(queueId, "normal"),
            matchMode,
            pointsToWin,
            matchDuration,
            isRanked
        );

        bool ok = await runnerManager.StartMatchmakingAsync(
            bucketSessionName,
            properties,
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

        if (runnerManager.IsCurrentRunnerServer())
        {
            pendingLocalIsHost = true;

            currentLobbyState.hostPlayerId = pendingLocalPlayerId;
            currentLobbyState.hostPlayer.playerId = pendingLocalPlayerId;
            currentLobbyState.hostPlayer.displayName = pendingLocalDisplayName;
            currentLobbyState.hostPlayer.isHost = true;
            currentLobbyState.hostPlayer.isReady = true;
            currentLobbyState.hostPlayer.isConnected = true;
            currentLobbyState.statusText = "Waiting for opponent";

            PublishHostProperties();
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
                ParsePlayerToken(player, out string joinPlayerId, out string joinPlayerName);

                currentLobbyState.joinPlayer.playerId = Sanitize(joinPlayerId, "remote_player");
                currentLobbyState.joinPlayer.displayName = Sanitize(joinPlayerName, "JoinPlayer");
                currentLobbyState.joinPlayer.isHost = false;
                currentLobbyState.joinPlayer.isReady = true;
                currentLobbyState.joinPlayer.isConnected = true;
                currentLobbyState.statusText = "Lobby full / ready";

                PublishJoinProperties();
            }
            else
            {
                currentLobbyState.statusText = "Waiting for opponent";
                PublishHostProperties();
            }
        }
        else
        {
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
                    { JoinIdPropertyKey, string.Empty },
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

        if (TryReadSessionPropertyString(HostIdPropertyKey, out string hostId))
        {
            currentLobbyState.hostPlayerId = Sanitize(hostId, currentLobbyState.hostPlayerId);
            currentLobbyState.hostPlayer.playerId = Sanitize(hostId, currentLobbyState.hostPlayer.playerId);
        }

        if (TryReadSessionPropertyString(HostNamePropertyKey, out string hostName))
            currentLobbyState.hostPlayer.displayName = Sanitize(hostName, "HostPlayer");

        if (TryReadSessionPropertyString(JoinIdPropertyKey, out string joinId) && !string.IsNullOrWhiteSpace(joinId))
        {
            currentLobbyState.joinPlayer.playerId = Sanitize(joinId, currentLobbyState.joinPlayer.playerId);
            currentLobbyState.joinPlayer.isConnected = true;
        }

        if (TryReadSessionPropertyString(JoinNamePropertyKey, out string joinName) && !string.IsNullOrWhiteSpace(joinName))
        {
            currentLobbyState.joinPlayer.displayName = Sanitize(joinName, "JoinPlayer");
            currentLobbyState.joinPlayer.isConnected = true;
        }

        if (TryReadSessionPropertyInt(MatchModePropertyKey, out int matchModeValue))
            currentLobbyState.matchMode = (MatchMode)Mathf.Clamp(matchModeValue, 0, 1);

        if (TryReadSessionPropertyInt(PointsToWinPropertyKey, out int pointsToWin))
            currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);

        if (TryReadSessionPropertyInt(MatchDurationPropertyKey, out int matchDuration))
            currentLobbyState.matchDuration = Mathf.Max(1, matchDuration);

        if (TryReadSessionPropertyInt(RankedPropertyKey, out int ranked))
            currentLobbyState.isRanked = ranked == 1;
    }

    private void PublishHostProperties()
    {
        if (runnerManager == null || !runnerManager.IsRunning)
            return;

        runnerManager.TryUpdateSessionProperties(new Dictionary<string, SessionProperty>
        {
            { HostIdPropertyKey, Sanitize(currentLobbyState.hostPlayer.playerId, pendingLocalPlayerId) },
            { HostNamePropertyKey, Sanitize(currentLobbyState.hostPlayer.displayName, pendingLocalDisplayName) },
            { MatchModePropertyKey, (int)currentLobbyState.matchMode },
            { PointsToWinPropertyKey, Mathf.Max(1, currentLobbyState.pointsToWin) },
            { MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, currentLobbyState.matchDuration)) },
            { RankedPropertyKey, currentLobbyState.isRanked ? 1 : 0 }
        });
    }

    private void PublishJoinProperties()
    {
        if (runnerManager == null || !runnerManager.IsRunning)
            return;

        runnerManager.TryUpdateSessionProperties(new Dictionary<string, SessionProperty>
        {
            { JoinIdPropertyKey, Sanitize(currentLobbyState.joinPlayer.playerId, string.Empty) },
            { JoinNamePropertyKey, Sanitize(currentLobbyState.joinPlayer.displayName, string.Empty) }
        });
    }

    private void ParsePlayerToken(PlayerRef player, out string playerId, out string displayName)
    {
        playerId = string.Empty;
        displayName = string.Empty;

        byte[] token = runnerManager.GetPlayerConnectionToken(player);
        if (token == null || token.Length == 0)
            return;

        try
        {
            string raw = Encoding.UTF8.GetString(token);
            if (string.IsNullOrWhiteSpace(raw))
                return;

            string[] parts = raw.Split('|');
            if (parts.Length >= 1)
                playerId = Sanitize(parts[0], string.Empty);

            if (parts.Length >= 2)
                displayName = Sanitize(parts[1], string.Empty);
        }
        catch
        {
        }
    }

    private byte[] BuildConnectionToken(string playerId, string displayName)
    {
        string tokenString = Sanitize(playerId, "local_player_2") + "|" + Sanitize(displayName, "JoinPlayer");
        return Encoding.UTF8.GetBytes(tokenString);
    }

    private string BuildDeterministicMatchmakingBucket(
        string queueId,
        MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        string q = Sanitize(queueId, "normal").ToLowerInvariant();
        int mm = (int)matchMode;
        int pt = Mathf.Max(1, pointsToWin);
        int md = Mathf.RoundToInt(Mathf.Max(1f, matchDuration));
        int rk = isRanked ? 1 : 0;

        return $"MM_{q}_{mm}_{pt}_{md}_{rk}";
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

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;
    }

    private Dictionary<string, SessionProperty> BuildSessionProperties(
        string hostId,
        string hostName,
        string joinId,
        string joinName,
        string queueId,
        MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        return new Dictionary<string, SessionProperty>
        {
            { HostIdPropertyKey, Sanitize(hostId, string.Empty) },
            { HostNamePropertyKey, Sanitize(hostName, string.Empty) },
            { JoinIdPropertyKey, Sanitize(joinId, string.Empty) },
            { JoinNamePropertyKey, Sanitize(joinName, string.Empty) },
            { QueueIdPropertyKey, Sanitize(queueId, string.Empty) },
            { MatchModePropertyKey, (int)matchMode },
            { PointsToWinPropertyKey, Mathf.Max(1, pointsToWin) },
            { MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, matchDuration)) },
            { RankedPropertyKey, isRanked ? 1 : 0 }
        };
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