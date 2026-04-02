using System;

[Serializable]
public class MatchAssignment
{
    public string matchId;
    public string sessionName;

    public QueueType queueType;
    public MatchMode matchMode;

    public int pointsToWin;
    public float matchDurationSeconds;
    public float turnDurationSeconds;

    public bool isRanked;

    public bool allowChestRewards;
    public bool allowXpRewards;
    public bool allowStatsProgression;

    public OnlinePlayerIdentity localPlayer;
    public OnlinePlayerIdentity remotePlayer;

    public bool localIsHost;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;
}