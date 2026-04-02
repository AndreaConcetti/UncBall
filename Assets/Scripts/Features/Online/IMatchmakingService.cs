using System;
using System.Threading;
using System.Threading.Tasks;

public interface IMatchmakingService
{
    bool IsSearching { get; }

    Task<MatchAssignment> EnqueueAsync(
        QueueType queueType,
        OnlinePlayerIdentity localPlayer,
        CancellationToken cancellationToken);

    Task CancelAsync();
}