namespace UncballArena.Core.Auth.Models
{
    public readonly struct ExternalAuthResult
    {
        public bool Success { get; }
        public string ProviderUserId { get; }
        public string DisplayName { get; }
        public string IdToken { get; }
        public string Email { get; }
        public string ErrorMessage { get; }

        public ExternalAuthResult(
            bool success,
            string providerUserId,
            string displayName,
            string idToken,
            string email,
            string errorMessage)
        {
            Success = success;
            ProviderUserId = providerUserId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IdToken = idToken ?? string.Empty;
            Email = email ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static ExternalAuthResult Failed(string errorMessage)
        {
            return new ExternalAuthResult(
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                errorMessage);
        }

        public static ExternalAuthResult Succeeded(
            string providerUserId,
            string displayName,
            string idToken = "",
            string email = "")
        {
            return new ExternalAuthResult(
                true,
                providerUserId,
                displayName,
                idToken,
                email,
                string.Empty);
        }

        public override string ToString()
        {
            return $"Success={Success} | ProviderUserId={ProviderUserId} | DisplayName={DisplayName} | Email={Email} | Error={ErrorMessage}";
        }
    }
}