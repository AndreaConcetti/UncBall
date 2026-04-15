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

    private readonly bool allowRankedMaskedBots;
    private readonly Vector2 rankedBotQueueDelaySeconds;
    private readonly BotDifficulty rankedBotDifficulty;

    private readonly bool allowNormalMaskedBots;
    private readonly Vector2 normalBotQueueDelaySeconds;
    private readonly BotDifficulty normalBotDifficulty;

    public bool IsSearching { get; private set; }

    public PhotonFusionMatchmakingService(
        PhotonFusionRunnerManager runnerManager,
        OnlineQueueRulesConfig queueRulesConfig,
        bool logDebug,
        bool allowRankedMaskedBots,
        Vector2 rankedBotQueueDelaySeconds,
        BotDifficulty rankedBotDifficulty,
        bool allowNormalMaskedBots,
        Vector2 normalBotQueueDelaySeconds,
        BotDifficulty normalBotDifficulty)
    {
        this.runnerManager = runnerManager;
        this.queueRulesConfig = queueRulesConfig;
        this.logDebug = logDebug;

        this.allowRankedMaskedBots = allowRankedMaskedBots;
        this.rankedBotQueueDelaySeconds = NormalizeDelayRange(rankedBotQueueDelaySeconds, 18f, 35f);
        this.rankedBotDifficulty = rankedBotDifficulty;

        this.allowNormalMaskedBots = allowNormalMaskedBots;
        this.normalBotQueueDelaySeconds = NormalizeDelayRange(normalBotQueueDelaySeconds, 8f, 16f);
        this.normalBotDifficulty = normalBotDifficulty;
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

            bool queueAllowsMaskedBot = IsMaskedBotAllowedForQueue(queueType);
            float maskedBotFallbackDelaySeconds = queueAllowsMaskedBot
                ? SampleFallbackDelaySeconds(queueType)
                : -1f;

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
                    " | TokenBytes=" + (OnlinePlayerTokenCodec.Encode(localSnapshot)?.Length ?? 0) +
                    " | AllowMaskedBot=" + queueAllowsMaskedBot +
                    " | MaskedBotDelay=" + maskedBotFallbackDelaySeconds +
                    " | BotDifficulty=" + GetQueueBotDifficulty(queueType),
                    runnerManager
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
                maskedBotFallbackDelaySeconds,
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
        float maskedBotFallbackDelaySeconds,
        CancellationToken cancellationToken)
    {
        DateTime searchStartUtc = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (runnerManager == null || !runnerManager.IsRunning)
                throw new Exception("Runner stopped during matchmaking.");

            double elapsedSeconds = (DateTime.UtcNow - searchStartUtc).TotalSeconds;
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
                    runtimeType = MatchRuntimeType.OnlineHuman,
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
                    localPlayerIsPlayer1 = localIsHost,
                    player1StartsOnLeft = true,
                    initialTurnOwner = PlayerID.Player1,

                    player1SkinUniqueId = string.Empty,
                    player2SkinUniqueId = string.Empty,

                    isBotMatch = false,
                    isMaskedBotMatch = false,
                    useLocalBotGameplayAuthority = false,
                    botDifficultyId = string.Empty,
                    botProfileId = string.Empty,
                    botFallbackDelaySeconds = -1f
                };

                if (logDebug)
                {
                    Debug.Log(
                        "[PhotonFusionMatchmakingService] Match ready -> " +
                        "Session=" + assignment.sessionName +
                        " | RuntimeType=" + assignment.runtimeType +
                        " | LocalIsHost=" + assignment.localIsHost +
                        " | LocalIsP1=" + assignment.localPlayerIsPlayer1 +
                        " | RemoteName=" + remoteName +
                        " | RemoteWL=" + remoteSnapshot.totalWins + "W-" + remoteSnapshot.totalLosses + "L" +
                        " | RemoteWR=" + remoteSnapshot.winRatePercent + "%" +
                        " | PlayerCount=" + playerCount,
                        runnerManager
                    );
                }

                return assignment;
            }

            if (ShouldUseMaskedBotFallback(queueType, maskedBotFallbackDelaySeconds, elapsedSeconds, playerCount))
            {
                MatchAssignment botAssignment = await CreateMaskedBotAssignmentAsync(
                    queueType,
                    localPlayer,
                    localSnapshot,
                    rules,
                    maskedBotFallbackDelaySeconds,
                    cancellationToken
                );

                if (logDebug)
                {
                    Debug.Log(
                        "[PhotonFusionMatchmakingService] Masked bot fallback triggered -> " +
                        "Queue=" + queueType +
                        " | Delay=" + maskedBotFallbackDelaySeconds +
                        " | Elapsed=" + elapsedSeconds +
                        " | MatchId=" + botAssignment.matchId +
                        " | BotName=" + (botAssignment.remotePlayer != null ? botAssignment.remotePlayer.displayName : "Bot") +
                        " | BotDifficulty=" + botAssignment.botDifficultyId,
                        runnerManager
                    );
                }

                return botAssignment;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private bool ShouldUseMaskedBotFallback(
        QueueType queueType,
        float fallbackDelaySeconds,
        double elapsedSeconds,
        int playerCount)
    {
        if (!IsMaskedBotAllowedForQueue(queueType))
            return false;

        if (fallbackDelaySeconds <= 0f)
            return false;

        if (playerCount >= 2)
            return false;

        return elapsedSeconds >= fallbackDelaySeconds;
    }

    private async Task<MatchAssignment> CreateMaskedBotAssignmentAsync(
        QueueType queueType,
        OnlinePlayerIdentity localPlayer,
        OnlinePlayerMatchStatsSnapshot localSnapshot,
        QueueRuleSet rules,
        float maskedBotFallbackDelaySeconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (runnerManager != null && runnerManager.HasActiveRunner)
            await runnerManager.ShutdownRunnerAsync();

        cancellationToken.ThrowIfCancellationRequested();

        OnlinePlayerIdentity safeLocalPlayer = new OnlinePlayerIdentity(
            Safe(localPlayer != null ? localPlayer.profileId : null, Safe(localSnapshot != null ? localSnapshot.profileId : null, "local_profile")),
            Safe(localPlayer != null ? localPlayer.onlinePlayerId : null, Safe(localSnapshot != null ? localSnapshot.onlinePlayerId : null, "local_player")),
            Safe(localPlayer != null ? localPlayer.displayName : null, Safe(localSnapshot != null ? localSnapshot.displayName : null, "Player"))
        );

        OnlinePlayerMatchStatsSnapshot safeLocalSnapshot = CloneSnapshot(localSnapshot);
        if (safeLocalSnapshot == null)
        {
            safeLocalSnapshot = OnlinePlayerMatchStatsSnapshot.CreateDefault(
                safeLocalPlayer.displayName,
                safeLocalPlayer.onlinePlayerId,
                safeLocalPlayer.profileId);
        }

        safeLocalSnapshot.Normalize();

        OnlinePlayerMatchStatsSnapshot botSnapshot = BuildMaskedBotSnapshot(safeLocalSnapshot, queueType);
        botSnapshot.Normalize();

        string botProfileId = BuildMaskedBotProfileId(queueType);
        string matchIdPrefix = queueType == QueueType.Ranked ? "ranked_masked_bot_" : "normal_masked_bot_";
        string matchId = matchIdPrefix + Guid.NewGuid().ToString("N");

        MatchRuntimeType runtimeType =
            queueType == QueueType.Ranked
                ? MatchRuntimeType.RankedMaskedBot
                : MatchRuntimeType.NormalMaskedBot;

        MatchAssignment assignment = new MatchAssignment
        {
            matchId = matchId,
            sessionName = matchId,
            queueType = queueType,
            matchMode = rules.matchMode,
            runtimeType = runtimeType,

            pointsToWin = Mathf.Max(1, rules.pointsToWin),
            matchDurationSeconds = Mathf.Max(1f, rules.matchDurationSeconds),
            turnDurationSeconds = Mathf.Max(1f, rules.turnDurationSeconds),

            isRanked = queueType == QueueType.Ranked,

            allowChestRewards = rules.allowChestRewards,
            allowXpRewards = rules.allowXpRewards,
            allowStatsProgression = rules.allowStatsProgression,

            localPlayer = safeLocalPlayer,
            remotePlayer = new OnlinePlayerIdentity(
                botProfileId,
                botSnapshot.onlinePlayerId,
                botSnapshot.displayName
            ),

            localPlayerStats = safeLocalSnapshot,
            remotePlayerStats = botSnapshot,

            localIsHost = true,
            localPlayerIsPlayer1 = true,
            player1StartsOnLeft = true,
            initialTurnOwner = PlayerID.Player1,

            player1SkinUniqueId = string.Empty,
            player2SkinUniqueId = string.Empty,

            isBotMatch = true,
            isMaskedBotMatch = true,
            useLocalBotGameplayAuthority = true,

            botDifficultyId = GetQueueBotDifficulty(queueType).ToString(),
            botProfileId = botProfileId,
            botFallbackDelaySeconds = maskedBotFallbackDelaySeconds
        };

        return assignment;
    }

    private bool IsMaskedBotAllowedForQueue(QueueType queueType)
    {
        if (queueType == QueueType.Ranked)
            return allowRankedMaskedBots;

        return allowNormalMaskedBots;
    }

    private float SampleFallbackDelaySeconds(QueueType queueType)
    {
        Vector2 range = queueType == QueueType.Ranked
            ? rankedBotQueueDelaySeconds
            : normalBotQueueDelaySeconds;

        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);

        return UnityEngine.Random.Range(min, max);
    }

    private BotDifficulty GetQueueBotDifficulty(QueueType queueType)
    {
        return queueType == QueueType.Ranked
            ? rankedBotDifficulty
            : normalBotDifficulty;
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

    private OnlinePlayerMatchStatsSnapshot BuildMaskedBotSnapshot(
        OnlinePlayerMatchStatsSnapshot localSnapshot,
        QueueType queueType)
    {
        IReadOnlyList<string> namePool = BotNameLibrary.GetDefaultGeneratedPool(500);
        string botName = (namePool != null && namePool.Count > 0)
            ? namePool[UnityEngine.Random.Range(0, namePool.Count)]
            : "Bot";

        int localLevel = localSnapshot != null ? Mathf.Max(1, localSnapshot.level) : 1;
        int localMatches = localSnapshot != null ? Mathf.Max(0, localSnapshot.totalMatches) : 0;
        int localWinRate = localSnapshot != null ? Mathf.Clamp(localSnapshot.winRatePercent, 0, 100) : 50;

        BotDifficulty difficulty = GetQueueBotDifficulty(queueType);

        int minLevel = 1;
        int maxLevel = 12;
        int minMatches = 10;
        int maxMatches = 80;
        int minWinRate = 38;
        int maxWinRate = 52;

        switch (difficulty)
        {
            case BotDifficulty.Easy:
                minLevel = 1;
                maxLevel = 10;
                minMatches = 8;
                maxMatches = 60;
                minWinRate = 35;
                maxWinRate = 50;
                break;

            case BotDifficulty.Medium:
                minLevel = 5;
                maxLevel = 18;
                minMatches = 50;
                maxMatches = 250;
                minWinRate = 46;
                maxWinRate = 60;
                break;

            case BotDifficulty.Hard:
                minLevel = 12;
                maxLevel = 30;
                minMatches = 150;
                maxMatches = 900;
                minWinRate = 56;
                maxWinRate = 70;
                break;

            case BotDifficulty.Unbeatable:
                minLevel = 22;
                maxLevel = 50;
                minMatches = 500;
                maxMatches = 2500;
                minWinRate = 72;
                maxWinRate = 90;
                break;
        }

        int botLevel = Mathf.Clamp(localLevel + UnityEngine.Random.Range(-2, 4), minLevel, maxLevel);

        int baseMatches = Mathf.Clamp(localMatches + UnityEngine.Random.Range(-20, 40), minMatches, maxMatches);
        int targetWinRate = Mathf.Clamp(localWinRate + UnityEngine.Random.Range(-8, 9), minWinRate, maxWinRate);

        int botWins = Mathf.Clamp(Mathf.RoundToInt(baseMatches * (targetWinRate / 100f)), 0, baseMatches);
        int botLosses = Mathf.Max(0, baseMatches - botWins);

        if (localMatches < 8)
        {
            baseMatches = UnityEngine.Random.Range(minMatches, maxMatches + 1);
            float sampledWinRate01 = UnityEngine.Random.Range(minWinRate / 100f, maxWinRate / 100f);
            botWins = Mathf.RoundToInt(baseMatches * sampledWinRate01);
            botLosses = Mathf.Max(0, baseMatches - botWins);
        }

        OnlinePlayerMatchStatsSnapshot snapshot = new OnlinePlayerMatchStatsSnapshot
        {
            onlinePlayerId = "bot_" + Guid.NewGuid().ToString("N").Substring(0, 12),
            profileId = "bot_profile_" + Guid.NewGuid().ToString("N").Substring(0, 12),
            displayName = botName,
            level = botLevel,
            totalMatches = baseMatches,
            totalWins = botWins,
            totalLosses = botLosses,
            winRatePercent = 0
        };

        snapshot.Normalize();
        return snapshot;
    }

    private string BuildMaskedBotProfileId(QueueType queueType)
    {
        string prefix = queueType == QueueType.Ranked ? "ranked_masked_bot_" : "normal_masked_bot_";
        return prefix + Guid.NewGuid().ToString("N");
    }

    private static Vector2 NormalizeDelayRange(Vector2 rawRange, float fallbackMin, float fallbackMax)
    {
        float min = Mathf.Max(0.5f, rawRange.x);
        float max = Mathf.Max(min, rawRange.y);

        if (rawRange == Vector2.zero)
        {
            min = fallbackMin;
            max = fallbackMax;
        }

        return new Vector2(min, max);
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