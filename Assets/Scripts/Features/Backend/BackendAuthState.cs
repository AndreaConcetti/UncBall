namespace UncballArena.Core.Auth
{
    public sealed class BackendAuthState
    {
        public static readonly BackendAuthState None = new BackendAuthState(
            backendPlayerId: string.Empty,
            sessionToken: string.Empty,
            isAuthenticated: false,
            backendDisplayName: string.Empty);

        public string BackendPlayerId { get; }
        public string SessionToken { get; }
        public bool IsAuthenticated { get; }
        public string BackendDisplayName { get; }

        public bool HasUsableIdentity => IsAuthenticated && !string.IsNullOrWhiteSpace(BackendPlayerId);

        public BackendAuthState(
            string backendPlayerId,
            string sessionToken,
            bool isAuthenticated,
            string backendDisplayName)
        {
            BackendPlayerId = backendPlayerId ?? string.Empty;
            SessionToken = sessionToken ?? string.Empty;
            BackendDisplayName = backendDisplayName ?? string.Empty;
            IsAuthenticated = isAuthenticated && !string.IsNullOrWhiteSpace(BackendPlayerId);
        }

        public override string ToString()
        {
            return $"BackendAuthState(BackendPlayerId={BackendPlayerId}, IsAuthenticated={IsAuthenticated}, BackendDisplayName={BackendDisplayName})";
        }
    }
}