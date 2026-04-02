using Fusion;
using Fusion.Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class PhotonFusionMatchmakingService : IMatchmakingService
{
    private const string HostIdPropertyKey = "hid";
    private const string HostNamePropertyKey = "hn";
    private const string JoinIdPropertyKey = "jid";
    private const string JoinNamePropertyKey = "jn";
    private const string QueueIdPropertyKey = "qid";
    private const string MatchModePropertyKey = "mm";
    private const string PointsToWinPropertyKey = "pt";
    private const string MatchDurationPropertyKey = "md";
    private const string TurnDurationPropertyKey = "td";
    private const string RankedPropertyKey = "rk";

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

            Dictionary<string, SessionProperty> properties = BuildQueueProperties(
                queueType,
                matchMode,
                pointsToWin,
                matchDurationSeconds,
                turnDurationSeconds,
                isRanked
            );

            byte[] token = BuildConnectionToken(
                Safe(localPlayer.onlinePlayerId, "local_online_player"),
                Safe(localPlayer.profileId, "local_profile"),
                Safe(localPlayer.displayName, "Player")
            );

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

            PublishLocalIdentityToSession(
                localIsHost,
                localPlayer,
                queueType,
                matchMode,
                pointsToWin,
                matchDurationSeconds,
                turnDurationSeconds,
                isRanked
            );

            MatchAssignment assignment = await WaitForMatchReadyAsync(
                queueType,
                localPlayer,
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

            string hostId = ReadSessionString(HostIdPropertyKey);
            string hostName = ReadSessionString(HostNamePropertyKey);
            string joinId = ReadSessionString(JoinIdPropertyKey);
            string joinName = ReadSessionString(JoinNamePropertyKey);

            int modeRaw = ReadSessionInt(MatchModePropertyKey, (int)rules.matchMode);
            int points = ReadSessionInt(PointsToWinPropertyKey, Mathf.Max(1, rules.pointsToWin));
            int duration = ReadSessionInt(MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, rules.matchDurationSeconds)));
            int turnDuration = ReadSessionInt(TurnDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, rules.turnDurationSeconds)));
            int rankedRaw = ReadSessionInt(RankedPropertyKey, queueType == QueueType.Ranked ? 1 : 0);

            if (playerCount >= 2)
            {
                string resolvedHostId = Safe(
                    hostId,
                    localIsHost ? localPlayer.onlinePlayerId : "remote_host");

                string resolvedHostName = Safe(
                    hostName,
                    localIsHost ? localPlayer.displayName : "Opponent");

                string resolvedJoinId = Safe(
                    joinId,
                    localIsHost ? "remote_join" : localPlayer.onlinePlayerId);

                string resolvedJoinName = Safe(
                    joinName,
                    localIsHost ? "Opponent" : localPlayer.displayName);

                MatchAssignment assignment = new MatchAssignment
                {
                    matchId = runnerManager.GetCurrentSessionName(),
                    sessionName = runnerManager.GetCurrentSessionName(),
                    queueType = queueType,
                    matchMode = (MatchMode)Mathf.Clamp(modeRaw, 0, 1),
                    pointsToWin = Mathf.Max(1, points),
                    matchDurationSeconds = Mathf.Max(1f, duration),
                    turnDurationSeconds = Mathf.Max(1f, turnDuration),
                    isRanked = rankedRaw == 1,

                    allowChestRewards = rules.allowChestRewards,
                    allowXpRewards = rules.allowXpRewards,
                    allowStatsProgression = rules.allowStatsProgression,

                    localPlayer = localPlayer,
                    remotePlayer = localIsHost
                        ? new OnlinePlayerIdentity(resolvedJoinId, resolvedJoinId, resolvedJoinName)
                        : new OnlinePlayerIdentity(resolvedHostId, resolvedHostId, resolvedHostName),

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
                        " | Host=" + resolvedHostName +
                        " | Join=" + resolvedJoinName +
                        " | PlayerCount=" + playerCount
                    );
                }

                return assignment;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private void PublishLocalIdentityToSession(
        bool localIsHost,
        OnlinePlayerIdentity localPlayer,
        QueueType queueType,
        MatchMode matchMode,
        int pointsToWin,
        float matchDurationSeconds,
        float turnDurationSeconds,
        bool isRanked)
    {
        if (runnerManager == null || !runnerManager.IsRunning)
            return;

        Dictionary<string, SessionProperty> props = new Dictionary<string, SessionProperty>
        {
            { QueueIdPropertyKey, queueType == QueueType.Ranked ? "ranked" : "normal" },
            { MatchModePropertyKey, (int)matchMode },
            { PointsToWinPropertyKey, Mathf.Max(1, pointsToWin) },
            { MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, matchDurationSeconds)) },
            { TurnDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, turnDurationSeconds)) },
            { RankedPropertyKey, isRanked ? 1 : 0 }
        };

        if (localIsHost)
        {
            props[HostIdPropertyKey] = Safe(localPlayer.onlinePlayerId, "host_player");
            props[HostNamePropertyKey] = Safe(localPlayer.displayName, "Host");
        }
        else
        {
            props[JoinIdPropertyKey] = Safe(localPlayer.onlinePlayerId, "join_player");
            props[JoinNamePropertyKey] = Safe(localPlayer.displayName, "Join");
        }

        bool updated = runnerManager.TryUpdateSessionProperties(props);

        if (logDebug)
        {
            Debug.Log(
                "[PhotonFusionMatchmakingService] PublishLocalIdentityToSession -> " +
                "LocalIsHost=" + localIsHost +
                " | Updated=" + updated +
                " | DisplayName=" + localPlayer.displayName
            );
        }
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
            { QueueIdPropertyKey, queueType == QueueType.Ranked ? "ranked" : "normal" },
            { MatchModePropertyKey, (int)matchMode },
            { PointsToWinPropertyKey, Mathf.Max(1, pointsToWin) },
            { MatchDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, matchDurationSeconds)) },
            { TurnDurationPropertyKey, Mathf.RoundToInt(Mathf.Max(1f, turnDurationSeconds)) },
            { RankedPropertyKey, isRanked ? 1 : 0 }
        };
    }

    private byte[] BuildConnectionToken(string onlinePlayerId, string profileId, string displayName)
    {
        string raw =
            Safe(onlinePlayerId, "player") + "|" +
            Safe(profileId, "profile") + "|" +
            Safe(displayName, "Player");

        return Encoding.UTF8.GetBytes(raw);
    }

    private string ReadSessionString(string key)
    {
        if (runnerManager == null)
            return string.Empty;

        if (!runnerManager.TryGetCurrentSessionProperty(key, out SessionProperty prop))
            return string.Empty;

        string raw = prop.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        Match match = Regex.Match(raw, @"^\[SessionProperty:\s*(.*?),\s*Type=.*\]$");
        if (match.Success)
            return Safe(match.Groups[1].Value, string.Empty);

        return Safe(raw, string.Empty);
    }

    private int ReadSessionInt(string key, int fallback)
    {
        if (runnerManager == null)
            return fallback;

        if (!runnerManager.TryGetCurrentSessionProperty(key, out SessionProperty prop))
            return fallback;

        string raw = prop.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        Match match = Regex.Match(raw, @"^\[SessionProperty:\s*(.*?),\s*Type=.*\]$");
        if (match.Success)
            raw = Safe(match.Groups[1].Value, string.Empty);

        return int.TryParse(raw, out int parsed) ? parsed : fallback;
    }

    private string Safe(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}