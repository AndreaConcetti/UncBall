using System;
using UnityEngine;

[Serializable]
public class OnlineLobbyPlayerData
{
    public string playerId = "";
    public string displayName = "";
    public bool isHost = false;
    public bool isReady = false;
    public bool isConnected = false;
}

[Serializable]
public class OnlineLobbyState
{
    public bool hasLobby = false;

    public string sessionId = "";
    public string roomCode = "";
    public string hostPlayerId = "";

    public OnlineLobbyPlayerData hostPlayer = new OnlineLobbyPlayerData();
    public OnlineLobbyPlayerData joinPlayer = new OnlineLobbyPlayerData();

    public MatchMode matchMode = MatchMode.TimeLimit;
    public int pointsToWin = 16;
    public float matchDuration = 180f;
    public bool isRanked = false;

    public bool startRequested = false;
    public bool matchStarted = false;

    public string statusText = "";

    public bool HasHost()
    {
        return hostPlayer != null && hostPlayer.isConnected;
    }

    public bool HasJoinPlayer()
    {
        return joinPlayer != null && joinPlayer.isConnected;
    }

    public bool IsFull()
    {
        return HasHost() && HasJoinPlayer();
    }

    public void Clear()
    {
        hasLobby = false;
        sessionId = "";
        roomCode = "";
        hostPlayerId = "";

        hostPlayer = new OnlineLobbyPlayerData();
        joinPlayer = new OnlineLobbyPlayerData();

        matchMode = MatchMode.TimeLimit;
        pointsToWin = 16;
        matchDuration = 180f;
        isRanked = false;

        startRequested = false;
        matchStarted = false;
        statusText = "";
    }
}