using System.Threading.Tasks;

namespace UncballArena.Core.Auth
{
    public sealed class GuestAuthProvider : IAuthProvider
    {
        private readonly LocalAuthStorage storage;
        private readonly string fallbackGuestName;

        public AuthProviderType ProviderType => AuthProviderType.Guest;
        public bool IsAvailable => true;

        public GuestAuthProvider(LocalAuthStorage storage, string fallbackGuestName = "Guest")
        {
            this.storage = storage;
            this.fallbackGuestName = string.IsNullOrWhiteSpace(fallbackGuestName) ? "Guest" : fallbackGuestName;
        }

        public Task<AuthProviderSignInResult> SignInAsync()
        {
            string guestPlayerId = storage.GetOrCreateGuestPlayerId();
            string displayName = storage.GetDisplayName(fallbackGuestName);

            PlayerIdentity identity = new PlayerIdentity(
                guestPlayerId,
                AuthProviderType.Guest,
                guestPlayerId,
                displayName,
                true,
                storage.IsGoogleLinked() || storage.IsAppleLinked());

            return Task.FromResult(AuthProviderSignInResult.Success(identity));
        }
    }
}