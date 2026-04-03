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
            winRatePercent = 0
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
        totalMatches = Mathf.Max(0, totalMatches);
        totalWins = Mathf.Max(0, totalWins);
        totalLosses = Mathf.Max(0, totalLosses);

        if (totalLosses == 0 && totalMatches > totalWins)
            totalLosses = Mathf.Max(0, totalMatches - totalWins);

        int computedMatches = totalWins + totalLosses;
        if (computedMatches > totalMatches)
            totalMatches = computedMatches;

        if (totalMatches <= 0)
        {
            winRatePercent = 0;
        }
        else
        {
            float wr = (float)totalWins / totalMatches;
            winRatePercent = Mathf.Clamp(Mathf.RoundToInt(wr * 100f), 0, 100);
        }
    }
}