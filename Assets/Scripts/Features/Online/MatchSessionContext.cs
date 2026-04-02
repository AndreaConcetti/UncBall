using System;

[Serializable]
public sealed class MatchSessionContext
{
    public string matchId;
    public string sessionName;
    public string gameplaySceneName;

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
    public bool isConnected;

    public string player1DisplayName;
    public string player2DisplayName;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;

    public MatchSessionContext()
    {
        matchId = string.Empty;
        sessionName = string.Empty;
        gameplaySceneName = "Gameplay";

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
        isConnected = false;

        player1DisplayName = "Player 1";
        player2DisplayName = "Player 2";

        player1SkinUniqueId = string.Empty;
        player2SkinUniqueId = string.Empty;
    }

    public static MatchSessionContext FromAssignment(MatchAssignment assignment, string gameplaySceneName)
    {
        MatchSessionContext context = new MatchSessionContext();

        if (assignment == null)
            return context;

        context.matchId = string.IsNullOrWhiteSpace(assignment.matchId) ? string.Empty : assignment.matchId.Trim();
        context.sessionName = string.IsNullOrWhiteSpace(assignment.sessionName) ? string.Empty : assignment.sessionName.Trim();
        context.gameplaySceneName = string.IsNullOrWhiteSpace(gameplaySceneName) ? "Gameplay" : gameplaySceneName.Trim();

        context.queueType = assignment.queueType;
        context.matchMode = assignment.matchMode;
        context.pointsToWin = Math.Max(1, assignment.pointsToWin);
        context.matchDurationSeconds = Math.Max(1f, assignment.matchDurationSeconds);
        context.isRanked = assignment.isRanked;

        context.allowChestRewards = assignment.allowChestRewards;
        context.allowXpRewards = assignment.allowXpRewards;
        context.allowStatsProgression = assignment.allowStatsProgression;

        context.localPlayer = assignment.localPlayer ?? new OnlinePlayerIdentity();
        context.remotePlayer = assignment.remotePlayer ?? new OnlinePlayerIdentity();

        context.localIsHost = assignment.localIsHost;
        context.isConnected = false;

        context.player1DisplayName = context.localIsHost
            ? SafeName(context.localPlayer.displayName, "Player 1")
            : SafeName(context.remotePlayer.displayName, "Player 1");

        context.player2DisplayName = context.localIsHost
            ? SafeName(context.remotePlayer.displayName, "Player 2")
            : SafeName(context.localPlayer.displayName, "Player 2");

        context.player1SkinUniqueId = string.IsNullOrWhiteSpace(assignment.player1SkinUniqueId)
            ? string.Empty
            : assignment.player1SkinUniqueId.Trim();

        context.player2SkinUniqueId = string.IsNullOrWhiteSpace(assignment.player2SkinUniqueId)
            ? string.Empty
            : assignment.player2SkinUniqueId.Trim();

        return context;
    }

    private static string SafeName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}