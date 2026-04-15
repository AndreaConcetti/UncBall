using System;

[Serializable]
public class MatchSessionContext
{
    public string matchId;
    public string sessionName;
    public string gameplaySceneName;

    public QueueType queueType;
    public MatchMode matchMode;
    public MatchRuntimeType runtimeType = MatchRuntimeType.OnlineHuman;

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
    public bool localPlayerIsPlayer1;
    public bool player1StartsOnLeft;
    public PlayerID initialTurnOwner;
    public bool isConnected;

    public string player1DisplayName;
    public string player2DisplayName;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;

    public bool isBotMatch;
    public bool isMaskedBotMatch;
    public bool useLocalBotGameplayAuthority;

    public string botDifficultyId;
    public string botProfileId;
    public float botFallbackDelaySeconds;

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
            runtimeType = assignment.runtimeType,

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
            localPlayerIsPlayer1 = assignment.localPlayerIsPlayer1,
            player1StartsOnLeft = assignment.player1StartsOnLeft,
            initialTurnOwner = assignment.initialTurnOwner,
            isConnected = assignment.runtimeType == MatchRuntimeType.OnlineHuman,

            player1DisplayName = assignment.localPlayerIsPlayer1
                ? SafeName(assignment.localPlayer)
                : SafeName(assignment.remotePlayer),

            player2DisplayName = assignment.localPlayerIsPlayer1
                ? SafeName(assignment.remotePlayer)
                : SafeName(assignment.localPlayer),

            player1SkinUniqueId = assignment.player1SkinUniqueId,
            player2SkinUniqueId = assignment.player2SkinUniqueId,

            isBotMatch = assignment.isBotMatch,
            isMaskedBotMatch = assignment.isMaskedBotMatch,
            useLocalBotGameplayAuthority = assignment.useLocalBotGameplayAuthority,

            botDifficultyId = assignment.botDifficultyId,
            botProfileId = assignment.botProfileId,
            botFallbackDelaySeconds = assignment.botFallbackDelaySeconds
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