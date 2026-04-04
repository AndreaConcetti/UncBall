using System;

namespace UncballArena.Core.Auth
{
    [Serializable]
    public sealed class ProviderLinkStatus
    {
        public AuthProviderType ProviderType;
        public bool IsAvailable;
        public bool IsLinked;
        public bool CanLink;

        public ProviderLinkStatus(
            AuthProviderType providerType,
            bool isAvailable,
            bool isLinked,
            bool canLink)
        {
            ProviderType = providerType;
            IsAvailable = isAvailable;
            IsLinked = isLinked;
            CanLink = canLink;
        }
    }
}