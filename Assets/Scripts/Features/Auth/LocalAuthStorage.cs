using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class LocalAuthStorage
    {
        private const string KeyGuestPlayerId = "auth.guest.playerId";
        private const string KeyDisplayName = "auth.displayName";
        private const string KeyCurrentProvider = "auth.currentProvider";

        private const string KeyGoogleLinked = "auth.google.linked";
        private const string KeyGoogleProviderUserId = "auth.google.providerUserId";

        private const string KeyAppleLinked = "auth.apple.linked";
        private const string KeyAppleProviderUserId = "auth.apple.providerUserId";

        public string GetOrCreateGuestPlayerId()
        {
            string existing = PlayerPrefs.GetString(KeyGuestPlayerId, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            string created = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(KeyGuestPlayerId, created);
            PlayerPrefs.Save();
            return created;
        }

        public string GetDisplayName(string fallback = "Guest")
        {
            string value = PlayerPrefs.GetString(KeyDisplayName, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        public void SetDisplayName(string displayName)
        {
            string safeValue = string.IsNullOrWhiteSpace(displayName) ? "Guest" : displayName.Trim();
            PlayerPrefs.SetString(KeyDisplayName, safeValue);
            PlayerPrefs.Save();
        }

        public AuthProviderType GetCurrentProvider(AuthProviderType fallback = AuthProviderType.Guest)
        {
            if (!PlayerPrefs.HasKey(KeyCurrentProvider))
                return fallback;

            return (AuthProviderType)PlayerPrefs.GetInt(KeyCurrentProvider, (int)fallback);
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

        public string GetGoogleProviderUserId()
        {
            return PlayerPrefs.GetString(KeyGoogleProviderUserId, string.Empty).Trim();
        }

        public void SetGoogleProviderUserId(string providerUserId)
        {
            PlayerPrefs.SetString(KeyGoogleProviderUserId, providerUserId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void ClearGoogleLink()
        {
            PlayerPrefs.SetInt(KeyGoogleLinked, 0);
            PlayerPrefs.DeleteKey(KeyGoogleProviderUserId);
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

        public string GetAppleProviderUserId()
        {
            return PlayerPrefs.GetString(KeyAppleProviderUserId, string.Empty).Trim();
        }

        public void SetAppleProviderUserId(string providerUserId)
        {
            PlayerPrefs.SetString(KeyAppleProviderUserId, providerUserId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void ClearAppleLink()
        {
            PlayerPrefs.SetInt(KeyAppleLinked, 0);
            PlayerPrefs.DeleteKey(KeyAppleProviderUserId);
            PlayerPrefs.Save();
        }

        public void ClearAllExternalLinks()
        {
            ClearGoogleLink();
            ClearAppleLink();
            SetCurrentProvider(AuthProviderType.Guest);
        }
    }
}