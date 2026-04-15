using System;
using UnityEngine;

[Serializable]
public sealed class OnlinePlayerMatchStatsSnapshot
{
    public string onlinePlayerId;
    public string profileId;
    public string displayName;

    public int level;

    public int totalMatches;
    public int totalWins;
    public int totalLosses;
    public int winRatePercent;

    public int normalMatches;
    public int normalWins;
    public int normalLosses;
    public int normalWinRatePercent;

    public int rankedMatches;
    public int rankedWins;
    public int rankedLosses;
    public int rankedWinRatePercent;

    public bool IsValid => !string.IsNullOrWhiteSpace(displayName);

    public string GetDisplayNameOrFallback(string fallback = "Opponent")
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
    }

    public string GetWinLoseText()
    {
        return totalWins + "W - " + totalLosses + "L";
    }

    public string GetWinRateText()
    {
        return winRatePercent + "%";
    }

    public int GetMatchesForQueue(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? rankedMatches : normalMatches;
    }

    public int GetWinsForQueue(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? rankedWins : normalWins;
    }

    public int GetLossesForQueue(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? rankedLosses : normalLosses;
    }

    public int GetWinRatePercentForQueue(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? rankedWinRatePercent : normalWinRatePercent;
    }

    public static OnlinePlayerMatchStatsSnapshot CreateDefault(
        string displayNameFallback,
        string onlinePlayerIdFallback = "",
        string profileIdFallback = "")
    {
        return new OnlinePlayerMatchStatsSnapshot
        {
            onlinePlayerId = onlinePlayerIdFallback ?? string.Empty,
            profileId = profileIdFallback ?? string.Empty,
            displayName = string.IsNullOrWhiteSpace(displayNameFallback) ? "Opponent" : displayNameFallback,
            level = 1,

            totalMatches = 0,
            totalWins = 0,
            totalLosses = 0,
            winRatePercent = 0,

            normalMatches = 0,
            normalWins = 0,
            normalLosses = 0,
            normalWinRatePercent = 0,

            rankedMatches = 0,
            rankedWins = 0,
            rankedLosses = 0,
            rankedWinRatePercent = 0
        };
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(onlinePlayerId))
            onlinePlayerId = string.Empty;

        if (string.IsNullOrWhiteSpace(profileId))
            profileId = string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Opponent";

        level = Mathf.Max(1, level);

        NormalizeBucket(ref totalMatches, ref totalWins, ref totalLosses, ref winRatePercent);
        NormalizeBucket(ref normalMatches, ref normalWins, ref normalLosses, ref normalWinRatePercent);
        NormalizeBucket(ref rankedMatches, ref rankedWins, ref rankedLosses, ref rankedWinRatePercent);

        int recomposedMatches = Mathf.Max(totalMatches, normalMatches + rankedMatches);
        int recomposedWins = Mathf.Max(totalWins, normalWins + rankedWins);
        int recomposedLosses = Mathf.Max(totalLosses, normalLosses + rankedLosses);

        totalMatches = Mathf.Max(0, recomposedMatches);
        totalWins = Mathf.Clamp(recomposedWins, 0, totalMatches);
        totalLosses = Mathf.Clamp(recomposedLosses, 0, totalMatches);

        if (totalWins + totalLosses > totalMatches)
            totalMatches = totalWins + totalLosses;

        winRatePercent = totalMatches > 0
            ? Mathf.Clamp(Mathf.RoundToInt((float)totalWins / totalMatches * 100f), 0, 100)
            : 0;
    }

    private static void NormalizeBucket(ref int matches, ref int wins, ref int losses, ref int winRate)
    {
        matches = Mathf.Max(0, matches);
        wins = Mathf.Max(0, wins);
        losses = Mathf.Max(0, losses);

        if (losses == 0 && matches > wins)
            losses = Mathf.Max(0, matches - wins);

        int computedMatches = wins + losses;
        if (computedMatches > matches)
            matches = computedMatches;

        wins = Mathf.Clamp(wins, 0, matches);
        losses = Mathf.Clamp(losses, 0, matches);

        if (wins + losses > matches)
            matches = wins + losses;

        if (matches <= 0)
        {
            winRate = 0;
        }
        else
        {
            float wr = (float)wins / matches;
            winRate = Mathf.Clamp(Mathf.RoundToInt(wr * 100f), 0, 100);
        }
    }
}