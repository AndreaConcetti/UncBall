using System;

[Serializable]
public class MatchSessionContext
{
    public string matchId;
    public string sessionName;
    public string gameplaySceneName;

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
    public bool isConnected;

    public string player1DisplayName;
    public string player2DisplayName;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;

    public static MatchSessionContext FromAssignment(
        MatchAssignment assignment,
        string gameplaySceneName)
    {
        if (assignment == null)
            return null;

        MatchSessionContext context = new MatchSessionContext
        {
            matchId = assignment.matchId,
            sessionName = assignment.sessionName,
            gameplaySceneName = gameplaySceneName,

            queueType = assignment.queueType,
            matchMode = assignment.matchMode,

            pointsToWin = assignment.pointsToWin,
            matchDurationSeconds = assignment.matchDurationSeconds,
            turnDurationSeconds = assignment.turnDurationSeconds,

            isRanked = assignment.isRanked,

            allowChestRewards = assignment.allowChestRewards,
            allowXpRewards = assignment.allowXpRewards,
            allowStatsProgression = assignment.allowStatsProgression,

            localPlayer = assignment.localPlayer,
            remotePlayer = assignment.remotePlayer,

            localIsHost = assignment.localIsHost,
            isConnected = false,

            player1DisplayName = assignment.localIsHost
                ? SafeName(assignment.localPlayer)
                : SafeName(assignment.remotePlayer),

            player2DisplayName = assignment.localIsHost
                ? SafeName(assignment.remotePlayer)
                : SafeName(assignment.localPlayer),

            player1SkinUniqueId = assignment.player1SkinUniqueId,
            player2SkinUniqueId = assignment.player2SkinUniqueId
        };

        return context;
    }

    private static string SafeName(OnlinePlayerIdentity identity)
    {
        if (identity == null || string.IsNullOrWhiteSpace(identity.displayName))
            return "Player";

        return identity.displayName.Trim();
    }
}