using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class LocalDevMatchmakingService : IMatchmakingService
{
    public bool IsSearching { get; private set; }

    public async Task<MatchAssignment> EnqueueAsync(
        QueueType queueType,
        OnlinePlayerIdentity localPlayer,
        CancellationToken cancellationToken)
    {
        if (IsSearching)
            throw new InvalidOperationException("Matchmaking search already in progress.");

        IsSearching = true;

        try
        {
            await Task.Delay(1500, cancellationToken);

            string localSkinId = ResolveLocalPrimarySkinUniqueId();

            MatchAssignment assignment = new MatchAssignment
            {
                matchId = Guid.NewGuid().ToString("N"),
                sessionName = BuildSessionName(queueType),
                queueType = queueType,
                matchMode = queueType == QueueType.Ranked ? MatchMode.TimeLimit : MatchMode.ScoreTarget,
                pointsToWin = 16,
                matchDurationSeconds = 180f,
                isRanked = queueType == QueueType.Ranked,

                allowChestRewards = false,
                allowXpRewards = false,
                allowStatsProgression = false,

                localPlayer = localPlayer,
                remotePlayer = new OnlinePlayerIdentity(
                    "remote_profile",
                    "remote_online_player",
                    queueType == QueueType.Ranked ? "RankedOpponent" : "NormalOpponent"
                ),
                localIsHost = true,

                player1SkinUniqueId = localSkinId,
                player2SkinUniqueId = string.Empty
            };

            return assignment;
        }
        finally
        {
            IsSearching = false;
        }
    }

    public Task CancelAsync()
    {
        IsSearching = false;
        return Task.CompletedTask;
    }

    private string BuildSessionName(QueueType queueType)
    {
        string prefix = queueType == QueueType.Ranked ? "ranked" : "normal";
        return $"unkball_{prefix}_{Guid.NewGuid():N}";
    }

    private string ResolveLocalPrimarySkinUniqueId()
    {
        if (PlayerSkinLoadout.Instance == null)
            return string.Empty;

        BallSkinData skin = PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer1();
        if (skin == null || string.IsNullOrWhiteSpace(skin.skinUniqueId))
            return string.Empty;

        return skin.skinUniqueId.Trim();
    }
}