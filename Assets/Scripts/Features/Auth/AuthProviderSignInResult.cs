namespace UncballArena.Core.Auth
{
    public sealed class AuthProviderSignInResult
    {
        public bool Succeeded { get; }
        public PlayerIdentity Identity { get; }
        public string ErrorMessage { get; }

        public AuthProviderSignInResult(bool succeeded, PlayerIdentity identity, string errorMessage)
        {
            Succeeded = succeeded;
            Identity = identity ?? PlayerIdentity.Invalid();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static AuthProviderSignInResult Success(PlayerIdentity identity)
        {
            return new AuthProviderSignInResult(true, identity, string.Empty);
        }

        public static AuthProviderSignInResult Failure(string errorMessage)
        {
            return new AuthProviderSignInResult(false, PlayerIdentity.Invalid(), errorMessage);
        }
    }
}