using UnityEngine;
using UncballArena.Core.Runtime;

public static class OnlinePlayerStatsSnapshotFactory
{
    public static OnlinePlayerMatchStatsSnapshot BuildFromLocalProfile(OnlinePlayerIdentity fallbackIdentity = null)
    {
        string fallbackDisplayName = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.displayName)
            ? fallbackIdentity.displayName
            : "Player";

        string fallbackOnlineId = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.onlinePlayerId)
            ? fallbackIdentity.onlinePlayerId
            : "local_player";

        string fallbackProfileId = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.profileId)
            ? fallbackIdentity.profileId
            : "local_profile";

        if (OnlineLocalPlayerContext.IsAvailable)
        {
            int level = Mathf.Max(1, OnlineLocalPlayerContext.Level);
            int totalMatches = Mathf.Max(0, OnlineLocalPlayerContext.TotalMatches);
            int totalWins = Mathf.Max(0, OnlineLocalPlayerContext.TotalWins);
            int totalLosses = Mathf.Max(0, totalMatches - totalWins);
            int winRatePercent = totalMatches > 0
                ? Mathf.Clamp(Mathf.RoundToInt((float)totalWins / totalMatches * 100f), 0, 100)
                : 0;

            OnlinePlayerMatchStatsSnapshot snapshot = new OnlinePlayerMatchStatsSnapshot
            {
                onlinePlayerId = !string.IsNullOrWhiteSpace(fallbackOnlineId)
                    ? fallbackOnlineId.Trim()
                    : OnlineLocalPlayerContext.PlayerId,

                profileId = !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId)
                    ? OnlineLocalPlayerContext.PlayerId.Trim()
                    : fallbackProfileId,

                displayName = !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.DisplayName)
                    ? OnlineLocalPlayerContext.DisplayName.Trim()
                    : fallbackDisplayName,

                level = level,
                totalMatches = totalMatches,
                totalWins = totalWins,
                totalLosses = totalLosses,
                winRatePercent = winRatePercent
            };

            snapshot.Normalize();
            return snapshot;
        }

        OnlinePlayerMatchStatsSnapshot fallbackSnapshot =
            OnlinePlayerMatchStatsSnapshot.CreateDefault(fallbackDisplayName, fallbackOnlineId, fallbackProfileId);

        fallbackSnapshot.Normalize();
        return fallbackSnapshot;
    }
}