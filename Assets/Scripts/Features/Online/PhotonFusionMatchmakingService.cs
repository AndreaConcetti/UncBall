using Fusion;
using Fusion.Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class PhotonFusionMatchmakingService : IMatchmakingService
{
    private const string MatchStatePropertyKey = "match_state";
    private const int MatchStateLive = 0;
    private const int MatchStateEnded = 1;

    private readonly PhotonFusionRunnerManager runnerManager;
    private readonly OnlineQueueRulesConfig queueRulesConfig;
    private readonly bool logDebug;

    public bool IsSearching { get; private set; }

    public PhotonFusionMatchmakingService(
        PhotonFusionRunnerManager runnerManager,
        OnlineQueueRulesConfig queueRulesConfig,
        bool logDebug)
    {
        this.runnerManager = runnerManager;
        this.queueRulesConfig = queueRulesConfig;
        this.logDebug = logDebug;
    }

    public async Task<MatchAssignment> EnqueueAsync(
        QueueType queueType,
        OnlinePlayerIdentity localPlayer,
        CancellationToken cancellationToken)
    {
        if (runnerManager == null)
            throw new InvalidOperationException("PhotonFusionRunnerManager missing.");

        if (localPlayer == null)
            throw new InvalidOperationException("Local player identity missing.");

        if (queueRulesConfig == null)
            throw new InvalidOperationException("OnlineQueueRulesConfig missing.");

        if (IsSearching)
            throw new InvalidOperationException("Matchmaking already in progress.");

        IsSearching = true;

        try
        {
            QueueRuleSet rules = queueRulesConfig.GetRules(queueType);
            if (rules == null)
                throw new InvalidOperationException("Queue rules not found for queue type: " + queueType);

            bool isRanked = queueType == QueueType.Ranked;
            MatchMode matchMode = rules.matchMode;
            int pointsToWin = Mathf.Max(1, rules.pointsToWin);
            float matchDurationSeconds = Mathf.Max(1f, rules.matchDurationSeconds);
            float turnDurationSeconds = Mathf.Max(1f, rules.turnDurationSeconds);

            OnlinePlayerMatchStatsSnapshot localSnapshot =
                OnlinePlayerStatsSnapshotFactory.BuildFromLocalProfile(localPlayer);

            if (localSnapshot == null)
            {
                localSnapshot = OnlinePlayerMatchStatsSnapshot.CreateDefault(
                    localPlayer.displayName,
                    localPlayer.onlinePlayerId,
                    localPlayer.profileId);
            }

            localSnapshot.Normalize();

            if (logDebug)
            {
                Debug.Log(
                    "[PhotonFusionMatchmakingService] Enqueue -> " +
                    "Queue=" + queueType +
                    " | Name=" + localSnapshot.displayName +
                    " | Level=" + localSnapshot.level +
                    " | WL=" + localSnapshot.totalWins + "W-" + localSnapshot.totalLosses + "L" +
                    " | WR=" + localSnapshot.winRatePercent + "%" +
                    " | TokenBytes=" + (OnlinePlayerTokenCodec.Encode(localSnapshot)?.Length ?? 0)
                );
            }

            Dictionary<string, SessionProperty> properties = BuildQueueProperties(
                queueType,
                matchMode,
                pointsToWin,
                matchDurationSeconds,
                turnDurationSeconds,
                isRanked
            );

            byte[] token = OnlinePlayerTokenCodec.Encode(localSnapshot);

            bool ok = await runnerManager.StartPhotonQueueMatchmakingAsync(
                properties,
                2,
                runnerManager.MatchmakingLobbyName,
                token,
                MatchmakingMode.FillRoom
            );

            cancellationToken.ThrowIfCancellationRequested();

            if (!ok)
                throw new Exception("Photon queue start failed.");

            bool localIsHost = runnerManager.IsCurrentRunnerServer();

            MatchAssignment assignment = await WaitForMatchReadyAsync(
                queueType,
                localPlayer,
                localSnapshot,
                localIsHost,
                rules,
                cancellationToken
            );

            return assignment;
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task CancelAsync()
    {
        IsSearching = false;

        if (runnerManager == null)
            return;

        if (!runnerManager.HasActiveRunner)
            return;

        await runnerManager.ShutdownRunnerAsync();
    }

    private async Task<MatchAssignment> WaitForMatchReadyAsync(
        QueueType queueType,
        OnlinePlayerIdentity localPlayer,
        OnlinePlayerMatchStatsSnapshot localSnapshot,
        bool localIsHost,
        QueueRuleSet rules,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (runnerManager == null || !runnerManager.IsRunning)
                throw new Exception("Runner stopped during matchmaking.");

            int playerCount = runnerManager.GetCurrentPlayerCount();

            if (playerCount >= 2)
            {
                if (runnerManager.TryGetCurrentSessionProperty(MatchStatePropertyKey, out SessionProperty stateProperty))
                {
                    int stateValue = stateProperty;
                    if (stateValue == MatchStateEnded)
                    {
                        if (logDebug)
                            Debug.LogWarning("[PhotonFusionMatchmakingService] Joined an ended session. Aborting assignment.", runnerManager);

                        throw new Exception("Joined ended session.");
                    }
                }

                OnlinePlayerMatchStatsSnapshot remoteSnapshot = ResolveRemoteSnapshot(localIsHost);

                if (remoteSnapshot == null || !remoteSnapshot.IsValid)
                    remoteSnapshot = OnlinePlayerMatchStatsSnapshot.CreateDefault("Opponent");

                remoteSnapshot.Normalize();

                string remoteName = remoteSnapshot.GetDisplayNameOrFallback("Opponent");

                MatchAssignment assignment = new MatchAssignment
                {
                    matchId = runnerManager.GetCurrentSessionName(),
                    sessionName = runnerManager.GetCurrentSessionName(),
                    queueType = queueType,
                    matchMode = rules.matchMode,
                    pointsToWin = Mathf.Max(1, rules.pointsToWin),
                    matchDurationSeconds = Mathf.Max(1f, rules.matchDurationSeconds),
                    turnDurationSeconds = Mathf.Max(1f, rules.turnDurationSeconds),
                    isRanked = queueType == QueueType.Ranked,

                    allowChestRewards = rules.allowChestRewards,
                    allowXpRewards = rules.allowXpRewards,
                    allowStatsProgression = rules.allowStatsProgression,

                    localPlayer = new OnlinePlayerIdentity(
                        Safe(localPlayer.profileId, Safe(localSnapshot.profileId, "local_profile")),
                        Safe(localPlayer.onlinePlayerId, Safe(localSnapshot.onlinePlayerId, "local_player")),
                        Safe(localPlayer.displayName, Safe(localSnapshot.displayName, "Player"))
                    ),

                    remotePlayer = new OnlinePlayerIdentity(
                        Safe(remoteSnapshot.profileId, "remote_profile"),
                        Safe(remoteSnapshot.onlinePlayerId, "remote_player"),
                        remoteName
                    ),

                    localPlayerStats = CloneSnapshot(localSnapshot),
                    remotePlayerStats = CloneSnapshot(remoteSnapshot),

                    localIsHost = localIsHost,
                    player1SkinUniqueId = string.Empty,
                    player2SkinUniqueId = string.Empty
                };

                if (logDebug)
                {
                    Debug.Log(
                        "[PhotonFusionMatchmakingService] Match ready -> " +
                        "Session=" + assignment.sessionName +
                        " | LocalIsHost=" + assignment.localIsHost +
                        " | RemoteName=" + remoteName +
                        " | RemoteWL=" + remoteSnapshot.totalWins + "W-" + remoteSnapshot.totalLosses + "L" +
                        " | RemoteWR=" + remoteSnapshot.winRatePercent + "%" +
                        " | PlayerCount=" + playerCount,
                        runnerManager
                    );
                }

                return assignment;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private OnlinePlayerMatchStatsSnapshot ResolveRemoteSnapshot(bool localIsHost)
    {
        if (localIsHost)
        {
            if (FusionConnectionMetadataListener.Instance != null &&
                FusionConnectionMetadataListener.Instance.TryGetLatestJoinerSnapshot(out OnlinePlayerMatchStatsSnapshot joinerSnapshot) &&
                joinerSnapshot != null &&
                joinerSnapshot.IsValid)
            {
                joinerSnapshot.Normalize();
                return CloneSnapshot(joinerSnapshot);
            }

            return OnlinePlayerMatchStatsSnapshot.CreateDefault("Opponent");
        }

        if (FusionConnectionMetadataListener.Instance != null &&
            FusionConnectionMetadataListener.Instance.TryGetLatestHostSnapshot(out OnlinePlayerMatchStatsSnapshot hostSnapshot) &&
            hostSnapshot != null &&
            hostSnapshot.IsValid)
        {
            hostSnapshot.Normalize();
            return CloneSnapshot(hostSnapshot);
        }

        return OnlinePlayerMatchStatsSnapshot.CreateDefault("Opponent");
    }

    private Dictionary<string, SessionProperty> BuildQueueProperties(
        QueueType queueType,
        MatchMode matchMode,
        int pointsToWin,
        float matchDurationSeconds,
        float turnDurationSeconds,
        bool isRanked)
    {
        return new Dictionary<string, SessionProperty>
        {
            { OnlineSessionPropertyKeys.QueueId, queueType == QueueType.Ranked ? "ranked" : "normal" },
            { OnlineSessionPropertyKeys.MatchMode, (int)matchMode },
            { OnlineSessionPropertyKeys.PointsToWin, Mathf.Max(1, pointsToWin) },
            { OnlineSessionPropertyKeys.MatchDuration, Mathf.RoundToInt(Mathf.Max(1f, matchDurationSeconds)) },
            { OnlineSessionPropertyKeys.TurnDuration, Mathf.RoundToInt(Mathf.Max(1f, turnDurationSeconds)) },
            { OnlineSessionPropertyKeys.Ranked, isRanked ? 1 : 0 },
            { MatchStatePropertyKey, MatchStateLive }
        };
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

    private string Safe(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}
