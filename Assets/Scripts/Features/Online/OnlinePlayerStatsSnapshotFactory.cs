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

        PlayerProfileManager profileManager = PlayerProfileManager.Instance;
        if (profileManager != null && profileManager.ActiveProfile != null)
        {
            PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

            int totalMatches = Mathf.Max(0, profile.totalMatchesPlayed);
            int totalWins = Mathf.Clamp(profile.totalWins, 0, totalMatches);
            int totalLosses = Mathf.Max(0, totalMatches - totalWins);

            int multiplayerMatches = Mathf.Max(0, profile.multiplayerMatchesPlayed);
            int multiplayerWins = Mathf.Clamp(profile.multiplayerWins, 0, multiplayerMatches);

            int rankedMatches = Mathf.Max(0, profile.rankedMatchesPlayed);
            int rankedWins = Mathf.Clamp(profile.rankedWins, 0, rankedMatches);
            int rankedLosses = Mathf.Max(0, rankedMatches - rankedWins);

            int normalMatches = Mathf.Max(0, multiplayerMatches - rankedMatches);
            int normalWins = Mathf.Max(0, multiplayerWins - rankedWins);
            normalWins = Mathf.Clamp(normalWins, 0, normalMatches);
            int normalLosses = Mathf.Max(0, normalMatches - normalWins);

            OnlinePlayerMatchStatsSnapshot snapshot = new OnlinePlayerMatchStatsSnapshot
            {
                onlinePlayerId = !string.IsNullOrWhiteSpace(fallbackOnlineId)
                    ? fallbackOnlineId.Trim()
                    : ResolveOnlineIdFallback(),

                profileId = !string.IsNullOrWhiteSpace(profile.profileId)
                    ? profile.profileId.Trim()
                    : fallbackProfileId,

                displayName = !string.IsNullOrWhiteSpace(profile.displayName)
                    ? profile.displayName.Trim()
                    : fallbackDisplayName,

                level = Mathf.Max(1, profile.level),

                totalMatches = totalMatches,
                totalWins = totalWins,
                totalLosses = totalLosses,

                normalMatches = normalMatches,
                normalWins = normalWins,
                normalLosses = normalLosses,

                rankedMatches = rankedMatches,
                rankedWins = rankedWins,
                rankedLosses = rankedLosses
            };

            snapshot.Normalize();
            return snapshot;
        }

        if (OnlineLocalPlayerContext.IsAvailable)
        {
            int level = Mathf.Max(1, OnlineLocalPlayerContext.Level);
            int totalMatches = Mathf.Max(0, OnlineLocalPlayerContext.TotalMatches);
            int totalWins = Mathf.Clamp(OnlineLocalPlayerContext.TotalWins, 0, totalMatches);
            int totalLosses = Mathf.Max(0, totalMatches - totalWins);

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

                normalMatches = totalMatches,
                normalWins = totalWins,
                normalLosses = totalLosses,

                rankedMatches = 0,
                rankedWins = 0,
                rankedLosses = 0
            };

            snapshot.Normalize();
            return snapshot;
        }

        OnlinePlayerMatchStatsSnapshot fallbackSnapshot =
            OnlinePlayerMatchStatsSnapshot.CreateDefault(fallbackDisplayName, fallbackOnlineId, fallbackProfileId);

        fallbackSnapshot.Normalize();
        return fallbackSnapshot;
    }

    private static string ResolveOnlineIdFallback()
    {
        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId))
            return OnlineLocalPlayerContext.PlayerId.Trim();

        return "local_player";
    }
}