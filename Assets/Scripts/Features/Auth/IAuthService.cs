using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Auth
{
    public interface IAuthService
    {
        bool IsInitialized { get; }
        PlayerIdentity CurrentIdentity { get; }
        AuthSession CurrentSession { get; }

        event Action<AuthSession> SessionChanged;

        Task InitializeAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> SignInGuestAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> SignInWithGoogleAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> SignInWithAppleAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> LinkGoogleAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> LinkAppleAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken);
        Task SignOutAsync(CancellationToken cancellationToken);

        bool IsProviderAvailable(AuthProviderType providerType);
        bool IsProviderLinked(AuthProviderType providerType);
        bool CanLinkProvider(AuthProviderType providerType);
        IReadOnlyList<ProviderLinkStatus> GetProviderStatuses();
        AccountOverview GetAccountOverview();

        Task<PlayerIdentity> SignInAsGuestAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> SignInWithGooglePlayAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> LinkCurrentGuestToGooglePlayAsync(CancellationToken cancellationToken);
        Task<PlayerIdentity> LinkCurrentGuestToAppleAsync(CancellationToken cancellationToken);
    }
}