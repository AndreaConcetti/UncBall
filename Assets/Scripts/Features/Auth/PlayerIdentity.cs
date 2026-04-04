using System;

namespace UncballArena.Core.Auth
{
    [Serializable]
    public sealed class PlayerIdentity
    {
        public string PlayerId;
        public AuthProviderType ProviderType;
        public string ProviderUserId;
        public string DisplayName;
        public bool IsGuest;
        public bool IsLinked;

        public bool IsValid => !string.IsNullOrWhiteSpace(PlayerId);

        public PlayerIdentity()
        {
            PlayerId = string.Empty;
            ProviderType = AuthProviderType.None;
            ProviderUserId = string.Empty;
            DisplayName = string.Empty;
            IsGuest = false;
            IsLinked = false;
        }

        public PlayerIdentity(
            string playerId,
            AuthProviderType providerType,
            string providerUserId,
            string displayName,
            bool isGuest,
            bool isLinked)
        {
            PlayerId = playerId ?? string.Empty;
            ProviderType = providerType;
            ProviderUserId = providerUserId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsGuest = isGuest;
            IsLinked = isLinked;
        }

        public PlayerIdentity Clone()
        {
            return new PlayerIdentity(
                PlayerId,
                ProviderType,
                ProviderUserId,
                DisplayName,
                IsGuest,
                IsLinked);
        }

        public static PlayerIdentity Invalid()
        {
            return new PlayerIdentity();
        }

        public override string ToString()
        {
            return $"PlayerIdentity(PlayerId={PlayerId}, ProviderType={ProviderType}, ProviderUserId={ProviderUserId}, DisplayName={DisplayName}, IsGuest={IsGuest}, IsLinked={IsLinked})";
        }
    }
}