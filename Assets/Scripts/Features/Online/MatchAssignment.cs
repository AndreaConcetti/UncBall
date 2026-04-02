using System;

[Serializable]
public sealed class MatchAssignment
{
    public string matchId;
    public string sessionName;
    public QueueType queueType;
    public MatchMode matchMode;
    public int pointsToWin;
    public float matchDurationSeconds;
    public bool isRanked;

    public bool allowChestRewards;
    public bool allowXpRewards;
    public bool allowStatsProgression;

    public OnlinePlayerIdentity localPlayer;
    public OnlinePlayerIdentity remotePlayer;

    public bool localIsHost;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;

    public MatchAssignment()
    {
        matchId = string.Empty;
        sessionName = string.Empty;
        queueType = QueueType.Normal;
        matchMode = MatchMode.ScoreTarget;
        pointsToWin = 16;
        matchDurationSeconds = 180f;
        isRanked = false;

        allowChestRewards = false;
        allowXpRewards = false;
        allowStatsProgression = false;

        localPlayer = new OnlinePlayerIdentity();
        remotePlayer = new OnlinePlayerIdentity();

        localIsHost = false;

        player1SkinUniqueId = string.Empty;
        player2SkinUniqueId = string.Empty;
    }
}