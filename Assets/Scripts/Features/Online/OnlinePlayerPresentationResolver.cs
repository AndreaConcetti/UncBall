using System;
using UnityEngine;

public sealed class OnlinePlayerPresentationResolver : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineFlowController onlineFlowController;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private void Awake()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;
    }

    private void Update()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;
    }

    public bool IsLocalHost()
    {
        MatchAssignment assignment = GetCurrentAssignment();
        return assignment != null && assignment.localIsHost;
    }

    public bool IsLocalPlayerPlayer1()
    {
        MatchSessionContext session = GetCurrentSession();
        if (session != null)
            return session.localPlayerIsPlayer1;

        MatchAssignment assignment = GetCurrentAssignment();
        if (assignment != null)
            return assignment.localPlayerIsPlayer1;

        return true;
    }

    public bool IsPlayer1OnLeft()
    {
        MatchSessionContext session = GetCurrentSession();
        if (session != null)
            return session.player1StartsOnLeft;

        MatchAssignment assignment = GetCurrentAssignment();
        if (assignment != null)
            return assignment.player1StartsOnLeft;

        return true;
    }

    public bool IsLocalOnLeft()
    {
        bool localIsPlayer1 = IsLocalPlayerPlayer1();
        bool player1OnLeft = IsPlayer1OnLeft();

        if (localIsPlayer1)
            return player1OnLeft;

        return !player1OnLeft;
    }

    public bool TryGetLocalSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        MatchAssignment assignment = GetCurrentAssignment();
        OnlinePlayerIdentity localIdentity = assignment != null ? assignment.localPlayer : null;

        snapshot = OnlinePlayerStatsSnapshotFactory.BuildFromLocalProfile(localIdentity);

        if (snapshot == null)
            return false;

        snapshot.Normalize();

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePlayerPresentationResolver] Local snapshot -> " +
                snapshot.displayName +
                " | Total=" + snapshot.totalWins + "W-" + snapshot.totalLosses + "L" +
                " | Normal=" + snapshot.normalWins + "W-" + snapshot.normalLosses + "L" +
                " | Ranked=" + snapshot.rankedWins + "W-" + snapshot.rankedLosses + "L",
                this
            );
        }

        return snapshot.IsValid;
    }

    public bool TryGetOpponentSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = null;

        MatchAssignment assignment = GetCurrentAssignment();

        if (assignment != null && HasUsableRemoteSnapshot(assignment.remotePlayerStats))
        {
            snapshot = CloneSnapshot(assignment.remotePlayerStats);
            snapshot.Normalize();
            return true;
        }

        OnlinePlayerMatchStatsSnapshot liveSnapshot = TryResolveLiveRemoteSnapshot(assignment);
        if (liveSnapshot != null && liveSnapshot.IsValid)
        {
            liveSnapshot.Normalize();
            ApplyResolvedRemoteSnapshotToRuntime(liveSnapshot);

            snapshot = CloneSnapshot(liveSnapshot);
            snapshot.Normalize();
            return true;
        }

        snapshot = OnlinePlayerMatchStatsSnapshot.CreateDefault("Opponent");
        snapshot.Normalize();
        return true;
    }

    private OnlinePlayerMatchStatsSnapshot TryResolveLiveRemoteSnapshot(MatchAssignment assignment)
    {
        if (FusionConnectionMetadataListener.Instance == null || assignment == null)
            return null;

        OnlinePlayerMatchStatsSnapshot snapshot = null;

        if (assignment.localIsHost)
        {
            if (FusionConnectionMetadataListener.Instance.TryGetLatestJoinerSnapshot(out OnlinePlayerMatchStatsSnapshot joiner) &&
                joiner != null &&
                joiner.IsValid)
            {
                snapshot = CloneSnapshot(joiner);
            }
        }
        else
        {
            if (FusionConnectionMetadataListener.Instance.TryGetLatestHostSnapshot(out OnlinePlayerMatchStatsSnapshot host) &&
                host != null &&
                host.IsValid)
            {
                snapshot = CloneSnapshot(host);
            }
        }

        if (snapshot != null)
            snapshot.Normalize();

        return snapshot;
    }

    private void ApplyResolvedRemoteSnapshotToRuntime(OnlinePlayerMatchStatsSnapshot resolvedSnapshot)
    {
        if (resolvedSnapshot == null || onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return;

        MatchAssignment assignment = onlineFlowController.RuntimeContext.currentAssignment;
        if (assignment == null)
            return;

        assignment.remotePlayerStats = CloneSnapshot(resolvedSnapshot);

        if (assignment.remotePlayer == null)
            assignment.remotePlayer = new OnlinePlayerIdentity();

        if (!string.IsNullOrWhiteSpace(resolvedSnapshot.onlinePlayerId))
            assignment.remotePlayer.onlinePlayerId = resolvedSnapshot.onlinePlayerId.Trim();

        if (!string.IsNullOrWhiteSpace(resolvedSnapshot.profileId))
            assignment.remotePlayer.profileId = resolvedSnapshot.profileId.Trim();

        if (!string.IsNullOrWhiteSpace(resolvedSnapshot.displayName))
            assignment.remotePlayer.displayName = resolvedSnapshot.displayName.Trim();
    }

    private bool HasUsableRemoteSnapshot(OnlinePlayerMatchStatsSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.IsValid)
            return false;

        bool hasIdentity = !string.IsNullOrWhiteSpace(snapshot.onlinePlayerId) ||
                           !string.IsNullOrWhiteSpace(snapshot.profileId);

        bool hasStats = snapshot.level > 1 ||
                        snapshot.totalMatches > 0 ||
                        snapshot.totalWins > 0 ||
                        snapshot.totalLosses > 0 ||
                        snapshot.winRatePercent > 0 ||
                        snapshot.normalMatches > 0 ||
                        snapshot.normalWins > 0 ||
                        snapshot.rankedMatches > 0 ||
                        snapshot.rankedWins > 0;

        bool hasNonPlaceholderName =
            !string.IsNullOrWhiteSpace(snapshot.displayName) &&
            !string.Equals(snapshot.displayName.Trim(), "Opponent", StringComparison.OrdinalIgnoreCase);

        return hasIdentity || hasStats || hasNonPlaceholderName;
    }

    private MatchAssignment GetCurrentAssignment()
    {
        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return null;

        return onlineFlowController.RuntimeContext.currentAssignment;
    }

    private MatchSessionContext GetCurrentSession()
    {
        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return null;

        return onlineFlowController.RuntimeContext.currentSession;
    }

    private OnlinePlayerMatchStatsSnapshot CloneSnapshot(OnlinePlayerMatchStatsSnapshot source)
    {
        if (source == null)
            return null;

        return new OnlinePlayerMatchStatsSnapshot
        {
            onlinePlayerId = source.onlinePlayerId,
            profileId = source.profileId,
            displayName = source.displayName,
            level = source.level,

            totalMatches = source.totalMatches,
            totalWins = source.totalWins,
            totalLosses = source.totalLosses,
            winRatePercent = source.winRatePercent,

            normalMatches = source.normalMatches,
            normalWins = source.normalWins,
            normalLosses = source.normalLosses,
            normalWinRatePercent = source.normalWinRatePercent,

            rankedMatches = source.rankedMatches,
            rankedWins = source.rankedWins,
            rankedLosses = source.rankedLosses,
            rankedWinRatePercent = source.rankedWinRatePercent
        };
    }
}