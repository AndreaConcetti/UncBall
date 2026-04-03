using System;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Auth.Services
{
    public sealed class LocalAuthService : IAuthService
    {
        private const string SessionKey = "uncball.auth.session";
        private const string GuestIdKey = "uncball.auth.guestid";
        private const string DefaultGuestDisplayName = "Guest";

        [Serializable]
        private sealed class PersistedAuthSession
        {
            public bool isAuthenticated;
            public bool isExpired;
            public string accessToken;
            public string refreshToken;
            public long issuedAtUnixSeconds;
            public long expiresAtUnixSeconds;

            public string playerId;
            public int providerType;
            public string providerUserId;
            public string displayName;
            public bool isGuest;
            public bool isLinked;
            public long createdAtUnixSeconds;
            public long lastLoginAtUnixSeconds;
        }

        public AuthSession CurrentSession { get; private set; } = AuthSession.CreateLoggedOut();
        public bool IsInitialized { get; private set; }
        public bool IsAuthenticated => CurrentSession != null && CurrentSession.IsAuthenticated;

        public event Action<AuthSession> SessionChanged;

        public async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            await RestoreSessionAsync();

            if (CurrentSession == null || !CurrentSession.HasUsableIdentity())
                await SignInAsGuestAsync(DefaultGuestDisplayName);

            IsInitialized = true;

            Debug.Log($"[LocalAuthService] Initialized. IsAuthenticated={IsAuthenticated} | Identity={CurrentSession?.Identity}");
        }

        public Task<AuthSession> RestoreSessionAsync()
        {
            if (!PlayerPrefs.HasKey(SessionKey))
            {
                CurrentSession = AuthSession.CreateLoggedOut();
                RaiseSessionChanged();
                return Task.FromResult(CurrentSession);
            }

            string json = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                CurrentSession = AuthSession.CreateLoggedOut();
                RaiseSessionChanged();
                return Task.FromResult(CurrentSession);
            }

            try
            {
                PersistedAuthSession persisted = JsonUtility.FromJson<PersistedAuthSession>(json);
                if (persisted == null)
                {
                    CurrentSession = AuthSession.CreateLoggedOut();
                    RaiseSessionChanged();
                    return Task.FromResult(CurrentSession);
                }

                PlayerIdentity identity = new PlayerIdentity(
                    persisted.playerId,
                    (AuthProviderType)persisted.providerType,
                    persisted.providerUserId,
                    persisted.displayName,
                    persisted.isGuest,
                    persisted.isLinked,
                    persisted.createdAtUnixSeconds,
                    persisted.lastLoginAtUnixSeconds
                );

                CurrentSession = new AuthSession(
                    persisted.isAuthenticated,
                    persisted.isExpired,
                    persisted.accessToken,
                    persisted.refreshToken,
                    persisted.issuedAtUnixSeconds,
                    persisted.expiresAtUnixSeconds,
                    identity
                );

                if (!CurrentSession.HasUsableIdentity())
                    CurrentSession = AuthSession.CreateLoggedOut();

                RaiseSessionChanged();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAuthService] RestoreSessionAsync failed. Exception: {ex}");
                CurrentSession = AuthSession.CreateLoggedOut();
                RaiseSessionChanged();
            }

            return Task.FromResult(CurrentSession);
        }

        public Task<AuthSession> SignInAsGuestAsync(string displayName = "")
        {
            string guestId = GetOrCreateGuestId();
            string sanitizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? DefaultGuestDisplayName
                : displayName.Trim();

            PlayerIdentity identity = PlayerIdentity.CreateGuest(guestId, sanitizedDisplayName);
            CurrentSession = AuthSession.CreateGuestSession(identity);

            PersistSession(CurrentSession);
            RaiseSessionChanged();

            Debug.Log($"[LocalAuthService] Signed in as Guest. PlayerId={identity.PlayerId}");
            return Task.FromResult(CurrentSession);
        }

        public Task<AuthSession> SignInWithGooglePlayAsync()
        {
            return SimulateProviderSignInAsync(AuthProviderType.GooglePlayGames, "GooglePlayer");
        }

        public Task<AuthSession> SignInWithAppleAsync()
        {
            return SimulateProviderSignInAsync(AuthProviderType.Apple, "ApplePlayer");
        }

        public Task<AuthSession> SignInWithFacebookAsync()
        {
            return SimulateProviderSignInAsync(AuthProviderType.Facebook, "FacebookPlayer");
        }

        public Task<AuthSession> LinkCurrentGuestToGooglePlayAsync()
        {
            return SimulateGuestLinkAsync(AuthProviderType.GooglePlayGames);
        }

        public Task<AuthSession> LinkCurrentGuestToAppleAsync()
        {
            return SimulateGuestLinkAsync(AuthProviderType.Apple);
        }

        public Task<AuthSession> LinkCurrentGuestToFacebookAsync()
        {
            return SimulateGuestLinkAsync(AuthProviderType.Facebook);
        }

        public Task<AuthSession> UpdateDisplayNameAsync(string displayName)
        {
            if (CurrentSession == null || !CurrentSession.HasUsableIdentity())
                return Task.FromResult(CurrentSession);

            string sanitized = string.IsNullOrWhiteSpace(displayName)
                ? DefaultGuestDisplayName
                : displayName.Trim();

            PlayerIdentity updatedIdentity = CurrentSession.Identity
                .WithDisplayName(sanitized)
                .WithLastLoginNow();

            CurrentSession = CurrentSession.WithIdentity(updatedIdentity);

            PersistSession(CurrentSession);
            RaiseSessionChanged();

            Debug.Log($"[LocalAuthService] Display name updated. PlayerId={updatedIdentity.PlayerId} | DisplayName={updatedIdentity.DisplayName}");
            return Task.FromResult(CurrentSession);
        }

        public Task SignOutAsync()
        {
            CurrentSession = AuthSession.CreateLoggedOut();

            if (PlayerPrefs.HasKey(SessionKey))
            {
                PlayerPrefs.DeleteKey(SessionKey);
                PlayerPrefs.Save();
            }

            RaiseSessionChanged();
            Debug.Log("[LocalAuthService] Signed out.");

            return Task.CompletedTask;
        }

        private Task<AuthSession> SimulateProviderSignInAsync(AuthProviderType providerType, string fallbackDisplayName)
        {
            string providerUserId = $"{ProviderPrefix(providerType)}_{Guid.NewGuid():N}";
            string playerId = providerUserId;
            string displayName = fallbackDisplayName;

            PlayerIdentity identity = PlayerIdentity.CreateLinked(
                providerType,
                providerUserId,
                playerId,
                displayName,
                true
            );

            CurrentSession = AuthSession.CreateAuthenticatedSession(
                identity,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
            );

            PersistSession(CurrentSession);
            RaiseSessionChanged();

            Debug.Log($"[LocalAuthService] Simulated provider sign-in. Provider={providerType} | PlayerId={playerId}");
            return Task.FromResult(CurrentSession);
        }

        private Task<AuthSession> SimulateGuestLinkAsync(AuthProviderType providerType)
        {
            if (CurrentSession == null || !CurrentSession.HasUsableIdentity())
                return Task.FromResult(AuthSession.CreateLoggedOut());

            PlayerIdentity current = CurrentSession.Identity;
            if (!current.IsGuest)
            {
                Debug.LogWarning("[LocalAuthService] Link requested but current identity is not a guest.");
                return Task.FromResult(CurrentSession);
            }

            string providerUserId = $"{ProviderPrefix(providerType)}_{Guid.NewGuid():N}";
            PlayerIdentity linkedIdentity = current.AsLinked(providerType, providerUserId);

            CurrentSession = AuthSession.CreateAuthenticatedSession(
                linkedIdentity,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
            );

            PersistSession(CurrentSession);
            RaiseSessionChanged();

            Debug.Log($"[LocalAuthService] Guest linked. NewProvider={providerType} | PlayerId={linkedIdentity.PlayerId}");
            return Task.FromResult(CurrentSession);
        }

        private static string ProviderPrefix(AuthProviderType providerType)
        {
            switch (providerType)
            {
                case AuthProviderType.GooglePlayGames: return "gpgs";
                case AuthProviderType.Apple: return "apple";
                case AuthProviderType.Facebook: return "fb";
                case AuthProviderType.Guest: return "guest";
                default: return "unknown";
            }
        }

        private static string GetOrCreateGuestId()
        {
            if (PlayerPrefs.HasKey(GuestIdKey))
            {
                string existing = PlayerPrefs.GetString(GuestIdKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            string guestId = $"guest_{Guid.NewGuid():N}";
            PlayerPrefs.SetString(GuestIdKey, guestId);
            PlayerPrefs.Save();
            return guestId;
        }

        private static void PersistSession(AuthSession session)
        {
            if (session == null || session.Identity == null)
                return;

            PersistedAuthSession persisted = new PersistedAuthSession
            {
                isAuthenticated = session.IsAuthenticated,
                isExpired = session.IsExpired,
                accessToken = session.AccessToken,
                refreshToken = session.RefreshToken,
                issuedAtUnixSeconds = session.IssuedAtUnixSeconds,
                expiresAtUnixSeconds = session.ExpiresAtUnixSeconds,

                playerId = session.Identity.PlayerId,
                providerType = (int)session.Identity.ProviderType,
                providerUserId = session.Identity.ProviderUserId,
                displayName = session.Identity.DisplayName,
                isGuest = session.Identity.IsGuest,
                isLinked = session.Identity.IsLinked,
                createdAtUnixSeconds = session.Identity.CreatedAtUnixSeconds,
                lastLoginAtUnixSeconds = session.Identity.LastLoginAtUnixSeconds
            };

            string json = JsonUtility.ToJson(persisted);
            PlayerPrefs.SetString(SessionKey, json);
            PlayerPrefs.Save();
        }

        private void RaiseSessionChanged()
        {
            SessionChanged?.Invoke(CurrentSession);
        }
    }
}