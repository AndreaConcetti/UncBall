namespace UncballArena.Core.Auth
{
    public sealed class AuthSession
    {
        public static readonly AuthSession SignedOut = new AuthSession(
            PlayerIdentity.Invalid(),
            false,
            BackendAuthState.None);

        public PlayerIdentity Identity { get; }
        public bool IsAuthenticated { get; }
        public BackendAuthState BackendState { get; }

        public string PlayerId => Identity != null ? Identity.PlayerId : string.Empty;
        public string DisplayName => Identity != null ? Identity.DisplayName : string.Empty;
        public bool IsGuest => Identity != null && Identity.IsGuest;
        public bool IsLinked => Identity != null && Identity.IsLinked;
        public AuthProviderType ProviderType => Identity != null ? Identity.ProviderType : AuthProviderType.None;

        public bool HasBackendSession => BackendState != null && BackendState.HasUsableIdentity;
        public string BackendPlayerId => BackendState != null ? BackendState.BackendPlayerId : string.Empty;
        public string BackendDisplayName => BackendState != null ? BackendState.BackendDisplayName : string.Empty;

        public string EffectivePlayerId => HasBackendSession ? BackendPlayerId : PlayerId;

        public AuthSession(PlayerIdentity identity, bool isAuthenticated)
            : this(identity, isAuthenticated, BackendAuthState.None)
        {
        }

        public AuthSession(PlayerIdentity identity, bool isAuthenticated, BackendAuthState backendState)
        {
            Identity = identity ?? PlayerIdentity.Invalid();
            BackendState = backendState ?? BackendAuthState.None;
            IsAuthenticated = isAuthenticated && Identity.IsValid;
        }

        public bool HasUsableIdentity()
        {
            return Identity != null && Identity.IsValid && !string.IsNullOrWhiteSpace(Identity.PlayerId);
        }

        public bool HasUsableAuthoritativeIdentity()
        {
            return !string.IsNullOrWhiteSpace(EffectivePlayerId);
        }
    }
}