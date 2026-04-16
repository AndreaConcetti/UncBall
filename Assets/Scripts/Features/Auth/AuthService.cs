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
        private readonly IBackendAuthService backendAuthService;
        private readonly SemaphoreSlim actionLock = new SemaphoreSlim(1, 1);

        public bool IsInitialized { get; private set; }
        public PlayerIdentity CurrentIdentity { get; private set; } = PlayerIdentity.Invalid();
        public AuthSession CurrentSession { get; private set; } = AuthSession.SignedOut;
        public BackendAuthState CurrentBackendState { get; private set; } = BackendAuthState.None;

        public event Action<AuthSession> SessionChanged;

        public AuthService(
            LocalAuthStorage storage,
            GuestAuthProvider guestAuthProvider,
            GooglePlayAuthProvider googlePlayAuthProvider,
            AppleAuthProvider appleAuthProvider,
            IBackendAuthService backendAuthService)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.guestAuthProvider = guestAuthProvider ?? throw new ArgumentNullException(nameof(guestAuthProvider));
            this.googlePlayAuthProvider = googlePlayAuthProvider ?? throw new ArgumentNullException(nameof(googlePlayAuthProvider));
            this.appleAuthProvider = appleAuthProvider ?? throw new ArgumentNullException(nameof(appleAuthProvider));
            this.backendAuthService = backendAuthService ?? throw new ArgumentNullException(nameof(backendAuthService));
        }

        public AuthService(LocalAuthStorage storage)
            : this(
                storage,
                new GuestAuthProvider(storage),
                new GooglePlayAuthProvider(),
                new AppleAuthProvider(),
                new NullBackendAuthService())
        {
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (IsInitialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                if (IsInitialized)
                    return;

                await backendAuthService.InitializeAsync(cancellationToken);

                PlayerIdentity identity = RestoreOrCreateIdentity();
                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.Restore);

                IsInitialized = true;

                Debug.Log(
                    $"[AuthService] Initialized. Provider={CurrentSession.ProviderType} | " +
                    $"Authenticated={CurrentSession.IsAuthenticated} | Guest={CurrentSession.IsGuest} | " +
                    $"Linked={CurrentSession.IsLinked} | DisplayName={CurrentSession.DisplayName} | " +
                    $"BackendAuthenticated={CurrentSession.HasBackendSession} | " +
                    $"BackendPlayerId={CurrentSession.BackendPlayerId}");
            }
            finally
            {
                actionLock.Release();
            }
        }

        public async Task<PlayerIdentity> SignInGuestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                AuthProviderSignInResult result = await guestAuthProvider.SignInAsync();
                if (!result.Succeeded || !result.Identity.IsValid)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Guest sign in failed."
                            : result.ErrorMessage);
                }

                PlayerIdentity identity = BuildGuestIdentity();
                storage.SetCurrentProvider(AuthProviderType.Guest);

                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.SignIn);

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public Task<PlayerIdentity> SignInAsGuestAsync(CancellationToken cancellationToken)
        {
            return SignInGuestAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> SignInWithGoogleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                AuthProviderSignInResult result = await googlePlayAuthProvider.SignInAsync();
                if (!result.Succeeded || !result.Identity.IsValid)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Google sign in failed."
                            : result.ErrorMessage);
                }

                ClearAllExternalLinksInternal();

                PlayerIdentity identity = BuildGoogleIdentityFromResult(result.Identity);

                storage.SetGoogleLinked(true);
                storage.SetGoogleProviderUserId(identity.ProviderUserId);
                storage.SetCurrentProvider(AuthProviderType.GooglePlayGames);
                storage.SetDisplayName(identity.DisplayName);

                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log(
                    $"[AuthService] Signed in with Google. " +
                    $"DisplayName={identity.DisplayName} | ProviderUserId={identity.ProviderUserId}");

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public Task<PlayerIdentity> SignInWithGooglePlayAsync(CancellationToken cancellationToken)
        {
            return SignInWithGoogleAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> SignInWithAppleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                AuthProviderSignInResult result = await appleAuthProvider.SignInAsync();
                if (!result.Succeeded || !result.Identity.IsValid)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Apple sign in failed."
                            : result.ErrorMessage);
                }

                ClearAllExternalLinksInternal();

                PlayerIdentity identity = BuildAppleIdentityFromResult(result.Identity);

                storage.SetAppleLinked(true);
                storage.SetAppleProviderUserId(identity.ProviderUserId);
                storage.SetCurrentProvider(AuthProviderType.Apple);
                storage.SetDisplayName(identity.DisplayName);

                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log(
                    $"[AuthService] Signed in with Apple. " +
                    $"DisplayName={identity.DisplayName} | ProviderUserId={identity.ProviderUserId}");

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public async Task<PlayerIdentity> LinkGoogleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                AuthProviderSignInResult result = await googlePlayAuthProvider.SignInAsync();
                if (!result.Succeeded || !result.Identity.IsValid)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Google link failed."
                            : result.ErrorMessage);
                }

                string stablePlayerId = GetStablePlayerId();
                string displayName = GetPreferredDisplayName(result.Identity.DisplayName);

                ClearAllExternalLinksInternal();

                storage.SetGoogleLinked(true);
                storage.SetGoogleProviderUserId(result.Identity.ProviderUserId);
                storage.SetCurrentProvider(AuthProviderType.GooglePlayGames);
                storage.SetDisplayName(displayName);

                PlayerIdentity identity = new PlayerIdentity(
                    playerId: stablePlayerId,
                    providerType: AuthProviderType.GooglePlayGames,
                    providerUserId: result.Identity.ProviderUserId,
                    displayName: displayName,
                    isGuest: false,
                    isLinked: true);

                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log(
                    $"[AuthService] Google linked successfully. " +
                    $"StablePlayerId={identity.PlayerId} | ProviderUserId={identity.ProviderUserId}");

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public Task<PlayerIdentity> LinkCurrentGuestToGooglePlayAsync(CancellationToken cancellationToken)
        {
            return LinkGoogleAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> LinkAppleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                AuthProviderSignInResult result = await appleAuthProvider.SignInAsync();
                if (!result.Succeeded || !result.Identity.IsValid)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Apple link failed."
                            : result.ErrorMessage);
                }

                string stablePlayerId = GetStablePlayerId();
                string displayName = GetPreferredDisplayName(result.Identity.DisplayName);

                ClearAllExternalLinksInternal();

                storage.SetAppleLinked(true);
                storage.SetAppleProviderUserId(result.Identity.ProviderUserId);
                storage.SetCurrentProvider(AuthProviderType.Apple);
                storage.SetDisplayName(displayName);

                PlayerIdentity identity = new PlayerIdentity(
                    playerId: stablePlayerId,
                    providerType: AuthProviderType.Apple,
                    providerUserId: result.Identity.ProviderUserId,
                    displayName: displayName,
                    isGuest: false,
                    isLinked: true);

                await ApplyIdentityAndBackendStateAsync(identity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log(
                    $"[AuthService] Apple linked successfully. " +
                    $"StablePlayerId={identity.PlayerId} | ProviderUserId={identity.ProviderUserId}");

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public Task<PlayerIdentity> LinkCurrentGuestToAppleAsync(CancellationToken cancellationToken)
        {
            return LinkAppleAsync(cancellationToken);
        }

        public async Task<PlayerIdentity> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                string sanitized = (displayName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sanitized))
                    throw new InvalidOperationException("Display name cannot be empty.");

                storage.SetDisplayName(sanitized);

                PlayerIdentity identity = CurrentIdentity.Clone();
                identity.DisplayName = sanitized;

                SetCurrentIdentity(identity, CurrentBackendState);

                return CurrentIdentity;
            }
            finally
            {
                actionLock.Release();
            }
        }

        public Task UnlinkGoogleAsync(CancellationToken cancellationToken)
        {
            return UnlinkProviderAsync(AuthProviderType.GooglePlayGames, cancellationToken);
        }

        public Task UnlinkAppleAsync(CancellationToken cancellationToken)
        {
            return UnlinkProviderAsync(AuthProviderType.Apple, cancellationToken);
        }

        public async Task UnlinkProviderAsync(AuthProviderType providerType, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                if (CurrentSession == null || !CurrentSession.IsAuthenticated)
                {
                    Debug.LogWarning("[AuthService] UnlinkProviderAsync ignored because there is no authenticated session.");
                    return;
                }

                switch (providerType)
                {
                    case AuthProviderType.GooglePlayGames:
                        storage.ClearGoogleLink();
                        storage.SetCurrentProvider(AuthProviderType.Guest);

                        try
                        {
                            await googlePlayAuthProvider.SignOutAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AuthService] Google provider SignOutAsync warning: {ex.Message}");
                        }

                        break;

                    case AuthProviderType.Apple:
                        storage.ClearAppleLink();
                        storage.SetCurrentProvider(AuthProviderType.Guest);

                        try
                        {
                            await appleAuthProvider.SignOutAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AuthService] Apple provider SignOutAsync warning: {ex.Message}");
                        }

                        break;

                    default:
                        throw new InvalidOperationException($"Unlink for provider {providerType} is not supported.");
                }

                PlayerIdentity guestIdentity = BuildGuestIdentity();
                await ApplyIdentityAndBackendStateAsync(guestIdentity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log(
                    $"[AuthService] Unlinked provider {providerType}. " +
                    $"Now back to Guest. PlayerId={guestIdentity.PlayerId} | DisplayName={guestIdentity.DisplayName}");
            }
            finally
            {
                actionLock.Release();
            }
        }

        public async Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await actionLock.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    await googlePlayAuthProvider.SignOutAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthService] Google provider SignOutAsync warning during SignOutAsync: {ex.Message}");
                }

                try
                {
                    await appleAuthProvider.SignOutAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthService] Apple provider SignOutAsync warning during SignOutAsync: {ex.Message}");
                }

                await backendAuthService.SignOutAsync(cancellationToken);

                storage.SetCurrentProvider(AuthProviderType.Guest);

                PlayerIdentity guestIdentity = BuildGuestIdentity();
                await ApplyIdentityAndBackendStateAsync(guestIdentity, cancellationToken, AuthBackendSyncMode.SignIn);

                Debug.Log("[AuthService] Signed out to guest.");
            }
            finally
            {
                actionLock.Release();
            }
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

            if (!CurrentSession.IsGuest)
                return false;

            return IsProviderAvailable(providerType) && !IsProviderLinked(providerType);
        }

        public IReadOnlyList<ProviderLinkStatus> GetProviderStatuses()
        {
            List<ProviderLinkStatus> statuses = new List<ProviderLinkStatus>
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

            return statuses;
        }

        public AccountOverview GetAccountOverview()
        {
            return new AccountOverview(
                CurrentSession.EffectivePlayerId,
                CurrentSession.DisplayName,
                CurrentSession.IsAuthenticated,
                CurrentSession.IsGuest,
                CurrentSession.IsLinked,
                CurrentSession.ProviderType,
                GetProviderStatuses());
        }

        private PlayerIdentity RestoreOrCreateIdentity()
        {
            AuthProviderType currentProvider = storage.GetCurrentProvider(AuthProviderType.Guest);

            if (currentProvider == AuthProviderType.GooglePlayGames && storage.IsGoogleLinked())
                return BuildStoredGoogleIdentity();

            if (currentProvider == AuthProviderType.Apple && storage.IsAppleLinked())
                return BuildStoredAppleIdentity();

            if (storage.IsGoogleLinked())
                return BuildStoredGoogleIdentity();

            if (storage.IsAppleLinked())
                return BuildStoredAppleIdentity();

            return BuildGuestIdentity();
        }

        private PlayerIdentity BuildGuestIdentity()
        {
            string playerId = GetStablePlayerId();
            string displayName = storage.GetDisplayName("Guest");

            return new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.Guest,
                providerUserId: playerId,
                displayName: displayName,
                isGuest: true,
                isLinked: false);
        }

        private PlayerIdentity BuildStoredGoogleIdentity()
        {
            string playerId = GetStablePlayerId();
            string displayName = storage.GetDisplayName("Guest");
            string providerUserId = storage.GetGoogleProviderUserId();

            return new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.GooglePlayGames,
                providerUserId: providerUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: true);
        }

        private PlayerIdentity BuildStoredAppleIdentity()
        {
            string playerId = GetStablePlayerId();
            string displayName = storage.GetDisplayName("Guest");
            string providerUserId = storage.GetAppleProviderUserId();

            return new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.Apple,
                providerUserId: providerUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: true);
        }

        private PlayerIdentity BuildGoogleIdentityFromResult(PlayerIdentity providerIdentity)
        {
            string playerId = GetStablePlayerId();
            string displayName = GetPreferredDisplayName(providerIdentity.DisplayName);

            return new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.GooglePlayGames,
                providerUserId: providerIdentity.ProviderUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: true);
        }

        private PlayerIdentity BuildAppleIdentityFromResult(PlayerIdentity providerIdentity)
        {
            string playerId = GetStablePlayerId();
            string displayName = GetPreferredDisplayName(providerIdentity.DisplayName);

            return new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.Apple,
                providerUserId: providerIdentity.ProviderUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: true);
        }

        private string GetStablePlayerId()
        {
            if (CurrentIdentity != null &&
                CurrentIdentity.IsValid &&
                !string.IsNullOrWhiteSpace(CurrentIdentity.PlayerId))
            {
                return CurrentIdentity.PlayerId;
            }

            return storage.GetOrCreateGuestPlayerId();
        }

        private string GetPreferredDisplayName(string providerDisplayName)
        {
            string currentStored = storage.GetDisplayName(string.Empty);
            if (!string.IsNullOrWhiteSpace(currentStored))
                return currentStored.Trim();

            if (!string.IsNullOrWhiteSpace(providerDisplayName))
                return providerDisplayName.Trim();

            return "Guest";
        }

        private void ClearAllExternalLinksInternal()
        {
            storage.ClearGoogleLink();
            storage.ClearAppleLink();
        }

        private async Task ApplyIdentityAndBackendStateAsync(
            PlayerIdentity identity,
            CancellationToken cancellationToken,
            AuthBackendSyncMode syncMode)
        {
            BackendAuthState backendState = await ResolveBackendStateAsync(identity, cancellationToken, syncMode);
            SetCurrentIdentity(identity, backendState);
        }

        private async Task<BackendAuthState> ResolveBackendStateAsync(
            PlayerIdentity identity,
            CancellationToken cancellationToken,
            AuthBackendSyncMode syncMode)
        {
            if (identity == null || !identity.IsValid)
                return BackendAuthState.None;

            try
            {
                switch (syncMode)
                {
                    case AuthBackendSyncMode.Restore:
                        return await backendAuthService.RestoreSessionAsync(identity, cancellationToken);

                    case AuthBackendSyncMode.Refresh:
                        return await backendAuthService.RefreshSessionAsync(identity, cancellationToken);

                    case AuthBackendSyncMode.SignIn:
                    default:
                        return await backendAuthService.SignInAsync(identity, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[AuthService] Backend auth sync failed. " +
                    $"Mode={syncMode} | LocalPlayerId={identity.PlayerId} | Error={ex.Message}");

                return BackendAuthState.None;
            }
        }

        private void SetCurrentIdentity(PlayerIdentity identity, BackendAuthState backendState)
        {
            CurrentIdentity = identity ?? PlayerIdentity.Invalid();
            CurrentBackendState = backendState ?? BackendAuthState.None;
            CurrentSession = new AuthSession(CurrentIdentity, CurrentIdentity.IsValid, CurrentBackendState);
            SessionChanged?.Invoke(CurrentSession);
        }

        private enum AuthBackendSyncMode
        {
            Restore = 0,
            SignIn = 1,
            Refresh = 2
        }
    }
}