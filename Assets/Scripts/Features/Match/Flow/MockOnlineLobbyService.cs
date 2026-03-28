using System;
using UnityEngine;

public class MockOnlineLobbyService : OnlineLobbyServiceBase
{
    public static MockOnlineLobbyService Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [SerializeField] private OnlineLobbyState currentLobbyState = new OnlineLobbyState();

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

    public override void CreateLobby(
        string localPlayerId,
        string localDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked)
    {
        currentLobbyState.Clear();

        currentLobbyState.hasLobby = true;
        currentLobbyState.sessionId = Guid.NewGuid().ToString("N");
        currentLobbyState.roomCode = GenerateRoomCode();
        currentLobbyState.hostPlayerId = localPlayerId;

        currentLobbyState.hostPlayer.playerId = localPlayerId;
        currentLobbyState.hostPlayer.displayName = localDisplayName;
        currentLobbyState.hostPlayer.isHost = true;
        currentLobbyState.hostPlayer.isReady = true;
        currentLobbyState.hostPlayer.isConnected = true;

        currentLobbyState.matchMode = matchMode;
        currentLobbyState.pointsToWin = Mathf.Max(1, pointsToWin);
        currentLobbyState.matchDuration = Mathf.Max(1f, matchDuration);
        currentLobbyState.isRanked = isRanked;
        currentLobbyState.statusText = "Waiting for opponent";

        if (logDebug)
        {
            Debug.Log(
                "[MockOnlineLobbyService] CreateLobby -> " +
                "RoomCode=" + currentLobbyState.roomCode +
                " | Host=" + localDisplayName +
                " | MatchMode=" + currentLobbyState.matchMode,
                this
            );
        }

        RaiseLobbyInfo("Lobby created");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override void JoinLobby(
        string roomCode,
        string localPlayerId,
        string localDisplayName)
    {
        if (!HasActiveLobby)
        {
            RaiseLobbyError("No active lobby exists.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCode) || !string.Equals(currentLobbyState.roomCode, roomCode.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            RaiseLobbyError("Room code not found.");
            return;
        }

        if (currentLobbyState.HasJoinPlayer())
        {
            RaiseLobbyError("Lobby is already full.");
            return;
        }

        currentLobbyState.joinPlayer.playerId = localPlayerId;
        currentLobbyState.joinPlayer.displayName = localDisplayName;
        currentLobbyState.joinPlayer.isHost = false;
        currentLobbyState.joinPlayer.isReady = true;
        currentLobbyState.joinPlayer.isConnected = true;

        currentLobbyState.statusText = "Lobby full / ready";

        if (logDebug)
        {
            Debug.Log(
                "[MockOnlineLobbyService] JoinLobby -> " +
                "RoomCode=" + currentLobbyState.roomCode +
                " | JoinPlayer=" + localDisplayName,
                this
            );
        }

        RaiseLobbyInfo("Joined lobby");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    public override void LeaveLobby(string localPlayerId)
    {
        if (!HasActiveLobby)
            return;

        bool isHostLeaving = currentLobbyState.hostPlayer != null &&
                             currentLobbyState.hostPlayer.playerId == localPlayerId;

        bool isJoinLeaving = currentLobbyState.joinPlayer != null &&
                             currentLobbyState.joinPlayer.playerId == localPlayerId;

        if (!isHostLeaving && !isJoinLeaving)
            return;

        if (isHostLeaving)
        {
            currentLobbyState.Clear();

            if (logDebug)
                Debug.Log("[MockOnlineLobbyService] Host left. Lobby destroyed.", this);

            RaiseLobbyInfo("Host left. Lobby destroyed.");
            RaiseLobbyStateChanged(currentLobbyState);
            return;
        }

        currentLobbyState.joinPlayer = new OnlineLobbyPlayerData();
        currentLobbyState.startRequested = false;
        currentLobbyState.matchStarted = false;
        currentLobbyState.statusText = "Waiting for opponent";

        if (logDebug)
            Debug.Log("[MockOnlineLobbyService] Join player left lobby.", this);

        RaiseLobbyInfo("Join player left lobby");
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

        if (logDebug)
            Debug.Log("[MockOnlineLobbyService] RequestStartMatch accepted.", this);

        RaiseLobbyInfo("Match starting");
        RaiseLobbyStateChanged(currentLobbyState);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] result = new char[6];

        for (int i = 0; i < result.Length; i++)
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];

        return new string(result);
    }
}