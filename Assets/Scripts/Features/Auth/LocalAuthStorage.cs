using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class LocalAuthStorage
    {
        private const string KeyGuestPlayerId = "AUTH_GUEST_PLAYER_ID";
        private const string KeyDisplayName = "AUTH_DISPLAY_NAME";
        private const string KeyCurrentProvider = "AUTH_CURRENT_PROVIDER";
        private const string KeyGoogleLinked = "AUTH_GOOGLE_LINKED";
        private const string KeyAppleLinked = "AUTH_APPLE_LINKED";
        private const string KeyGoogleProviderUserId = "AUTH_GOOGLE_PROVIDER_USER_ID";
        private const string KeyAppleProviderUserId = "AUTH_APPLE_PROVIDER_USER_ID";

        public string GetOrCreateGuestPlayerId()
        {
            string playerId = PlayerPrefs.GetString(KeyGuestPlayerId, string.Empty);
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                return playerId;
            }

            playerId = "guest_" + System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(KeyGuestPlayerId, playerId);
            PlayerPrefs.Save();
            return playerId;
        }

        public string GetDisplayName(string fallback)
        {
            return PlayerPrefs.GetString(KeyDisplayName, fallback ?? string.Empty);
        }

        public void SetDisplayName(string displayName)
        {
            PlayerPrefs.SetString(KeyDisplayName, displayName ?? string.Empty);
            PlayerPrefs.Save();
        }

        public AuthProviderType GetCurrentProvider()
        {
            return (AuthProviderType)PlayerPrefs.GetInt(KeyCurrentProvider, (int)AuthProviderType.Guest);
        }

        public void SetCurrentProvider(AuthProviderType providerType)
        {
            PlayerPrefs.SetInt(KeyCurrentProvider, (int)providerType);
            PlayerPrefs.Save();
        }

        public bool IsGoogleLinked()
        {
            return PlayerPrefs.GetInt(KeyGoogleLinked, 0) == 1;
        }

        public void SetGoogleLinked(bool value)
        {
            PlayerPrefs.SetInt(KeyGoogleLinked, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool IsAppleLinked()
        {
            return PlayerPrefs.GetInt(KeyAppleLinked, 0) == 1;
        }

        public void SetAppleLinked(bool value)
        {
            PlayerPrefs.SetInt(KeyAppleLinked, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public string GetGoogleProviderUserId()
        {
            return PlayerPrefs.GetString(KeyGoogleProviderUserId, string.Empty);
        }

        public void SetGoogleProviderUserId(string providerUserId)
        {
            PlayerPrefs.SetString(KeyGoogleProviderUserId, providerUserId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public string GetAppleProviderUserId()
        {
            return PlayerPrefs.GetString(KeyAppleProviderUserId, string.Empty);
        }

        public void SetAppleProviderUserId(string providerUserId)
        {
            PlayerPrefs.SetString(KeyAppleProviderUserId, providerUserId ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}