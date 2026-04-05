using System.Threading.Tasks;

namespace UncballArena.Core.Auth
{
    public sealed class GuestAuthProvider : IAuthProvider
    {
        private readonly LocalAuthStorage storage;
        private readonly string defaultGuestDisplayName;

        public AuthProviderType ProviderType => AuthProviderType.Guest;
        public bool IsAvailable => true;

        public GuestAuthProvider(LocalAuthStorage storage, string defaultGuestDisplayName = "Guest")
        {
            this.storage = storage;
            this.defaultGuestDisplayName = string.IsNullOrWhiteSpace(defaultGuestDisplayName)
                ? "Guest"
                : defaultGuestDisplayName.Trim();
        }

        public Task<AuthProviderSignInResult> SignInAsync()
        {
            string playerId = storage.GetOrCreateGuestPlayerId();
            string displayName = storage.GetDisplayName(defaultGuestDisplayName);

            PlayerIdentity identity = new PlayerIdentity(
                playerId: playerId,
                providerType: AuthProviderType.Guest,
                providerUserId: playerId,
                displayName: displayName,
                isGuest: true,
                isLinked: false);

            return Task.FromResult(AuthProviderSignInResult.Success(identity));
        }

        public Task SignOutAsync()
        {
            return Task.CompletedTask;
        }
    }
}