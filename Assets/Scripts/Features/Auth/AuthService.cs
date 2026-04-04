using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class AuthService : IAuthService
    {
        private readonly LocalAuthStorage storage;
        private readonly GuestAuthProvider guestAuthProvider;
        private readonly GooglePlayAuthProvider googlePlayAuthProvider;
        private readonly AppleAuthProvider appleAuthProvider;

        public bool IsInitialized { get; private set; }
        public PlayerIdentity CurrentIdentity { get; private set; } = PlayerIdentity.Invalid();
        public AuthSession CurrentSession { get; private set; } = AuthSession.SignedOut;

        public event Action<AuthSession> SessionChanged;

        public AuthService(
            LocalAuthStorage storage,
            GuestAuthProvider guestAuthProvider,
            GooglePlayAuthProvider googlePlayAuthProvider,
            AppleAuthProvider appleAuthProvider)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.guestAuthProvider = guestAuthProvider ?? throw new ArgumentNullException(nameof(guestAuthProvider));
            this.googlePlayAuthProvider = googlePlayAuthProvider ?? throw new ArgumentNullException(nameof(googlePlayAuthProvider));
            this.appleAuthProvider = appleAuthProvider ?? throw new ArgumentNullException(nameof(appleAuthProvider));
        }

        public AuthService(LocalAuthStorage storage)
            : this(
                storage,
                new GuestAuthProvider(storage),
                new GooglePlayAuthProvider(),
                new AppleAuthProvider())
        {
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (IsInitialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            PlayerIdentity identity = await SignInGuestAsync(cancellationToken);
            SetCurrentIdentity(identity);

            IsInitialized = true;

            Debug.Log($"[AuthService] Initialized. IsAuthenticated={CurrentSession.IsAuthenticated} | Identity={CurrentIdentity}");
        }

        public async Task<PlayerIdentity> SignInGuestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuthProviderSignInResult result = await guestAuthProvider.SignInAsync();
            if (!result.Succeeded || !result.Identity.IsValid)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Guest sign in failed."
                    : result.ErrorMessage);
            }

            storage.SetCurrentProvider(AuthProviderType.Guest);
            SetCurrentIdentity(result.Identity);
            return CurrentIdentity;
        }

        public Task<PlayerIdentity> SignInAsGuestAsync(CancellationToken cancellationToken)
        {
            return SignInGuestAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> SignInWithGoogleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuthProviderSignInResult result = await googlePlayAuthProvider.SignInAsync();
            if (!result.Succeeded || !result.Identity.IsValid)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Google sign in failed."
                    : result.ErrorMessage);
            }

            string displayName = GetPreferredDisplayName(result.Identity.DisplayName);
            PlayerIdentity identity = new PlayerIdentity(
                CurrentIdentity.IsValid ? CurrentIdentity.PlayerId : storage.GetOrCreateGuestPlayerId(),
                AuthProviderType.GooglePlayGames,
                result.Identity.ProviderUserId,
                displayName,
                false,
                true);

            storage.SetGoogleLinked(true);
            storage.SetGoogleProviderUserId(identity.ProviderUserId);
            storage.SetCurrentProvider(AuthProviderType.GooglePlayGames);
            storage.SetDisplayName(identity.DisplayName);

            SetCurrentIdentity(identity);

            Debug.Log($"[AuthService] Signed in with Google. PlayerId={identity.PlayerId} | DisplayName={identity.DisplayName}");
            return CurrentIdentity;
        }

        public Task<PlayerIdentity> SignInWithGooglePlayAsync(CancellationToken cancellationToken)
        {
            return SignInWithGoogleAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> SignInWithAppleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuthProviderSignInResult result = await appleAuthProvider.SignInAsync();
            if (!result.Succeeded || !result.Identity.IsValid)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Apple sign in failed."
                    : result.ErrorMessage);
            }

            string displayName = GetPreferredDisplayName(result.Identity.DisplayName);
            PlayerIdentity identity = new PlayerIdentity(
                CurrentIdentity.IsValid ? CurrentIdentity.PlayerId : storage.GetOrCreateGuestPlayerId(),
                AuthProviderType.Apple,
                result.Identity.ProviderUserId,
                displayName,
                false,
                true);

            storage.SetAppleLinked(true);
            storage.SetAppleProviderUserId(identity.ProviderUserId);
            storage.SetCurrentProvider(AuthProviderType.Apple);
            storage.SetDisplayName(identity.DisplayName);

            SetCurrentIdentity(identity);

            Debug.Log($"[AuthService] Signed in with Apple. PlayerId={identity.PlayerId} | DisplayName={identity.DisplayName}");
            return CurrentIdentity;
        }

        public async Task<PlayerIdentity> LinkGoogleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuthProviderSignInResult result = await googlePlayAuthProvider.SignInAsync();
            if (!result.Succeeded || !result.Identity.IsValid)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Google link failed."
                    : result.ErrorMessage);
            }

            storage.SetGoogleLinked(true);
            storage.SetGoogleProviderUserId(result.Identity.ProviderUserId);

            PlayerIdentity identity = CurrentIdentity.Clone();
            identity.IsLinked = storage.IsGoogleLinked() || storage.IsAppleLinked();
            SetCurrentIdentity(identity);

            Debug.Log($"[AuthService] Google linked. PlayerId={identity.PlayerId} | DisplayName={identity.DisplayName}");
            return CurrentIdentity;
        }

        public Task<PlayerIdentity> LinkCurrentGuestToGooglePlayAsync(CancellationToken cancellationToken)
        {
            return LinkGoogleAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> LinkAppleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AuthProviderSignInResult result = await appleAuthProvider.SignInAsync();
            if (!result.Succeeded || !result.Identity.IsValid)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Apple link failed."
                    : result.ErrorMessage);
            }

            storage.SetAppleLinked(true);
            storage.SetAppleProviderUserId(result.Identity.ProviderUserId);

            PlayerIdentity identity = CurrentIdentity.Clone();
            identity.IsLinked = storage.IsGoogleLinked() || storage.IsAppleLinked();
            SetCurrentIdentity(identity);

            Debug.Log($"[AuthService] Apple linked. PlayerId={identity.PlayerId} | DisplayName={identity.DisplayName}");
            return CurrentIdentity;
        }

        public Task<PlayerIdentity> LinkCurrentGuestToAppleAsync(CancellationToken cancellationToken)
        {
            return LinkAppleAsync(cancellationToken);
        }

        public Task<PlayerIdentity> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sanitized = (displayName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
                throw new InvalidOperationException("Display name cannot be empty.");

            storage.SetDisplayName(sanitized);

            PlayerIdentity identity = CurrentIdentity.Clone();
            identity.DisplayName = sanitized;
            SetCurrentIdentity(identity);

            Debug.Log($"[AuthService] Display name updated. PlayerId={identity.PlayerId} | DisplayName={identity.DisplayName}");
            return Task.FromResult(CurrentIdentity);
        }

        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetCurrentIdentity(PlayerIdentity.Invalid());
            storage.SetCurrentProvider(AuthProviderType.Guest);

            Debug.Log("[AuthService] Signed out.");
            return Task.CompletedTask;
        }

        public bool IsProviderAvailable(AuthProviderType providerType)
        {
            switch (providerType)
            {
                case AuthProviderType.Guest:
                    return guestAuthProvider.IsAvailable;
                case AuthProviderType.GooglePlayGames:
                    return googlePlayAuthProvider.IsAvailable;
                case AuthProviderType.Apple:
                    return appleAuthProvider.IsAvailable;
                default:
                    return false;
            }
        }

        public bool IsProviderLinked(AuthProviderType providerType)
        {
            switch (providerType)
            {
                case AuthProviderType.GooglePlayGames:
                    return storage.IsGoogleLinked();
                case AuthProviderType.Apple:
                    return storage.IsAppleLinked();
                case AuthProviderType.Guest:
                    return CurrentIdentity.IsValid && CurrentIdentity.ProviderType == AuthProviderType.Guest;
                default:
                    return false;
            }
        }

        public bool CanLinkProvider(AuthProviderType providerType)
        {
            if (!CurrentSession.IsAuthenticated)
                return false;

            if (!CurrentIdentity.IsGuest)
                return false;

            return IsProviderAvailable(providerType) && !IsProviderLinked(providerType);
        }

        public IReadOnlyList<ProviderLinkStatus> GetProviderStatuses()
        {
            List<ProviderLinkStatus> list = new List<ProviderLinkStatus>
            {
                new ProviderLinkStatus(
                    AuthProviderType.Guest,
                    guestAuthProvider.IsAvailable,
                    CurrentIdentity.IsGuest,
                    false),

                new ProviderLinkStatus(
                    AuthProviderType.GooglePlayGames,
                    googlePlayAuthProvider.IsAvailable,
                    storage.IsGoogleLinked(),
                    CanLinkProvider(AuthProviderType.GooglePlayGames)),

                new ProviderLinkStatus(
                    AuthProviderType.Apple,
                    appleAuthProvider.IsAvailable,
                    storage.IsAppleLinked(),
                    CanLinkProvider(AuthProviderType.Apple))
            };

            return list;
        }

        public AccountOverview GetAccountOverview()
        {
            return new AccountOverview(
                CurrentSession.PlayerId,
                CurrentSession.DisplayName,
                CurrentSession.IsAuthenticated,
                CurrentSession.IsGuest,
                CurrentSession.IsLinked,
                CurrentSession.ProviderType,
                GetProviderStatuses());
        }

        private void SetCurrentIdentity(PlayerIdentity identity)
        {
            CurrentIdentity = identity ?? PlayerIdentity.Invalid();
            CurrentSession = new AuthSession(CurrentIdentity, CurrentIdentity.IsValid);
            SessionChanged?.Invoke(CurrentSession);
        }

        private string GetPreferredDisplayName(string providerDisplayName)
        {
            string current = storage.GetDisplayName("Guest");
            if (!string.IsNullOrWhiteSpace(current))
                return current;

            if (!string.IsNullOrWhiteSpace(providerDisplayName))
                return providerDisplayName.Trim();

            return "Guest";
        }
    }
}