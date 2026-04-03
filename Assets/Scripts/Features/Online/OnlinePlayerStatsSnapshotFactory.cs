using UnityEngine;

public static class OnlinePlayerStatsSnapshotFactory
{
    public static OnlinePlayerMatchStatsSnapshot BuildFromLocalProfile(OnlinePlayerIdentity fallbackIdentity = null)
    {
        PlayerProfileManager profileManager = PlayerProfileManager.Instance;
        if (profileManager == null)
            profileManager = Object.FindAnyObjectByType<PlayerProfileManager>();

        string fallbackDisplayName = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.displayName)
            ? fallbackIdentity.displayName
            : "Player";

        string fallbackOnlineId = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.onlinePlayerId)
            ? fallbackIdentity.onlinePlayerId
            : "local_player";

        string fallbackProfileId = fallbackIdentity != null && !string.IsNullOrWhiteSpace(fallbackIdentity.profileId)
            ? fallbackIdentity.profileId
            : "local_profile";

        if (profileManager == null || profileManager.ActiveProfile == null)
        {
            OnlinePlayerMatchStatsSnapshot fallbackSnapshot =
                OnlinePlayerMatchStatsSnapshot.CreateDefault(fallbackDisplayName, fallbackOnlineId, fallbackProfileId);

            fallbackSnapshot.Normalize();
            return fallbackSnapshot;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        int level = Mathf.Max(1, profile.level);
        int totalMatches = Mathf.Max(0, profile.totalMatchesPlayed);
        int totalWins = Mathf.Max(0, profile.totalWins);
        int totalLosses = Mathf.Max(0, totalMatches - totalWins);
        int winRatePercent = totalMatches > 0
            ? Mathf.Clamp(Mathf.RoundToInt((float)totalWins / totalMatches * 100f), 0, 100)
            : 0;

        OnlinePlayerMatchStatsSnapshot snapshot = new OnlinePlayerMatchStatsSnapshot
        {
            onlinePlayerId = !string.IsNullOrWhiteSpace(fallbackOnlineId)
                ? fallbackOnlineId.Trim()
                : "local_player",

            profileId = !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId)
                ? profileManager.ActiveProfileId.Trim()
                : fallbackProfileId,

            displayName = !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName)
                ? profileManager.ActiveDisplayName.Trim()
                : fallbackDisplayName,

            level = level,
            totalMatches = totalMatches,
            totalWins = totalWins,
            totalLosses = totalLosses,
            winRatePercent = winRatePercent
        };

        snapshot.Normalize();

        Debug.Log(
            "[OnlinePlayerStatsSnapshotFactory] Local snapshot built -> " +
            "Name=" + snapshot.displayName +
            " | Level=" + snapshot.level +
            " | Matches=" + snapshot.totalMatches +
            " | Wins=" + snapshot.totalWins +
            " | Losses=" + snapshot.totalLosses +
            " | WR=" + snapshot.winRatePercent + "%",
            profileManager);

        return snapshot;
    }
}