using System;

[Serializable]
public class MatchAssignment
{
    public string matchId;
    public string sessionName;

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

    public OnlinePlayerMatchStatsSnapshot localPlayerStats;
    public OnlinePlayerMatchStatsSnapshot remotePlayerStats;

    public bool localIsHost;
    public bool localPlayerIsPlayer1 = true;
    public bool player1StartsOnLeft = true;
    public PlayerID initialTurnOwner = PlayerID.Player1;

    public string player1SkinUniqueId;
    public string player2SkinUniqueId;

    public bool isBotMatch;
    public bool isMaskedBotMatch;
    public bool useLocalBotGameplayAuthority;

    public string botDifficultyId;
    public string botProfileId;
    public float botFallbackDelaySeconds;
}