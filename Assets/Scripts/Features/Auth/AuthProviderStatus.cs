using System;

namespace UncballArena.Core.Auth.Models
{
    [Serializable]
    public sealed class AuthProviderStatus
    {
        public AuthProviderType ProviderType { get; }
        public bool IsAvailable { get; }
        public bool IsLinked { get; }
        public bool CanSignIn { get; }
        public bool CanLink { get; }

        public AuthProviderStatus(
            AuthProviderType providerType,
            bool isAvailable,
            bool isLinked,
            bool canSignIn,
            bool canLink)
        {
            ProviderType = providerType;
            IsAvailable = isAvailable;
            IsLinked = isLinked;
            CanSignIn = canSignIn;
            CanLink = canLink;
        }

        public override string ToString()
        {
            return $"AuthProviderStatus(Type={ProviderType}, Available={IsAvailable}, Linked={IsLinked}, CanSignIn={CanSignIn}, CanLink={CanLink})";
        }
    }
}