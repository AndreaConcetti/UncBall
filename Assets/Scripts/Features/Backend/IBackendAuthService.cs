using System.Threading;
using System.Threading.Tasks;

namespace UncballArena.Core.Auth
{
    public interface IBackendAuthService
    {
        bool IsInitialized { get; }

        Task InitializeAsync(CancellationToken cancellationToken);

        Task<BackendAuthState> RestoreSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken);

        Task<BackendAuthState> SignInAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken);

        Task<BackendAuthState> RefreshSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken);

        Task SignOutAsync(CancellationToken cancellationToken);
    }
}