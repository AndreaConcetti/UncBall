using System.Collections.Generic;

namespace UncballArena.Core.Auth
{
    public sealed class AccountOverview
    {
        public string PlayerId { get; }
        public string DisplayName { get; }
        public bool IsAuthenticated { get; }
        public bool IsGuest { get; }
        public bool IsLinked { get; }
        public AuthProviderType CurrentProviderType { get; }
        public IReadOnlyList<ProviderLinkStatus> ProviderStatuses { get; }

        public AccountOverview(
            string playerId,
            string displayName,
            bool isAuthenticated,
            bool isGuest,
            bool isLinked,
            AuthProviderType currentProviderType,
            IReadOnlyList<ProviderLinkStatus> providerStatuses)
        {
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsAuthenticated = isAuthenticated;
            IsGuest = isGuest;
            IsLinked = isLinked;
            CurrentProviderType = currentProviderType;
            ProviderStatuses = providerStatuses ?? new List<ProviderLinkStatus>();
        }
    }
}