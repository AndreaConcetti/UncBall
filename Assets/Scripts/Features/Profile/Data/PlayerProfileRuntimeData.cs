using System;

[Serializable]
public class PlayerProfileRuntimeData
{
    public int saveVersion = 3;

    public string profileId = "";
    public string displayName = "";

    public int xp = 0;
    public int level = 1;

    public int softCurrency = 0;
    public int premiumCurrency = 0;

    public int totalMatchesPlayed = 0;
    public int totalWins = 0;

    public int versusMatchesPlayed = 0;
    public int versusWins = 0;
    public int versusTimeMatchesPlayed = 0;
    public int versusScoreMatchesPlayed = 0;

    public int botMatchesPlayed = 0;
    public int botWins = 0;

    public int multiplayerMatchesPlayed = 0;
    public int multiplayerWins = 0;

    public int rankedMatchesPlayed = 0;
    public int rankedWins = 0;
    public int rankedLp = 1000;

    public string lastDailyLoginClaimDateUtc = "";
    public int consecutiveLoginDays = 0;

    public bool createdFromServer = false;
    public bool pendingServerSync = false;
    public long lastServerSyncUnixTimeSeconds = 0;
    public long lastLocalSaveUnixTimeSeconds = 0;
}