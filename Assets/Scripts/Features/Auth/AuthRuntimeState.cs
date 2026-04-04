namespace UncballArena.Core.Auth
{
    public sealed class AuthRuntimeState
    {
        public AuthSession Session { get; private set; } = AuthSession.SignedOut;

        public bool IsAuthenticated => Session != null && Session.IsAuthenticated;
        public string PlayerId => Session != null ? Session.PlayerId : string.Empty;
        public string DisplayName => Session != null ? Session.DisplayName : string.Empty;
        public bool IsGuest => Session != null && Session.IsGuest;
        public bool IsLinked => Session != null && Session.IsLinked;
        public AuthProviderType ProviderType => Session != null ? Session.ProviderType : AuthProviderType.None;

        public void Set(AuthSession session)
        {
            Session = session ?? AuthSession.SignedOut;
        }

        public void Clear()
        {
            Session = AuthSession.SignedOut;
        }
    }
}