using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class NullBackendAuthService : IBackendAuthService
    {
        private readonly bool logDebug;

        public bool IsInitialized { get; private set; }

        public NullBackendAuthService(bool logDebug = true)
        {
            this.logDebug = logDebug;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = true;

            if (logDebug)
                Debug.Log("[NullBackendAuthService] Initialized. Backend auth disabled.");

            return Task.CompletedTask;
        }

        public Task<BackendAuthState> RestoreSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BackendAuthState.None);
        }

        public Task<BackendAuthState> SignInAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BackendAuthState.None);
        }

        public Task<BackendAuthState> RefreshSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BackendAuthState.None);
        }

        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}