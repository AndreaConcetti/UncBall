using System.Threading;
using System.Threading.Tasks;

public interface IMatchSessionService
{
    bool HasActiveSession { get; }

    Task<bool> JoinAssignedMatchAsync(
        MatchSessionContext context,
        CancellationToken cancellationToken);

    Task<bool> LoadGameplaySceneAsync(
        MatchSessionContext context,
        CancellationToken cancellationToken);

    Task ShutdownSessionAsync();
}