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
                "[OnlinePlayerPresentationResolver] Local from local profile -> " +
                snapshot.displayName +
                " | " + snapshot.totalWins + "W-" + snapshot.totalLosses + "L" +
                " | WR=" + snapshot.winRatePercent + "%",
                this
            );
        }

        return snapshot.IsValid;
    }

    public bool TryGetOpponentSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = null;

        MatchAssignment assignment = GetCurrentAssignment();
        if (assignment != null && assignment.remotePlayerStats != null && assignment.remotePlayerStats.IsValid)
        {
            snapshot = CloneSnapshot(assignment.remotePlayerStats);
            snapshot.Normalize();

            if (logDebug)
            {
                Debug.Log(
                    "[OnlinePlayerPresentationResolver] Opponent from match assignment -> " +
                    snapshot.displayName +
                    " | " + snapshot.totalWins + "W-" + snapshot.totalLosses + "L" +
                    " | WR=" + snapshot.winRatePercent + "%",
                    this
                );
            }

            return true;
        }

        snapshot = OnlinePlayerMatchStatsSnapshot.CreateDefault("Opponent");
        snapshot.Normalize();

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePlayerPresentationResolver] Opponent fallback -> " +
                snapshot.displayName +
                " | " + snapshot.totalWins + "W-" + snapshot.totalLosses + "L" +
                " | WR=" + snapshot.winRatePercent + "%",
                this
            );
        }

        return true;
    }

    private MatchAssignment GetCurrentAssignment()
    {
        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return null;

        return onlineFlowController.RuntimeContext.currentAssignment;
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
            winRatePercent = source.winRatePercent
        };
    }
}