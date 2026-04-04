using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Services;

namespace UncballArena.Core.Auth.UI
{
    public static class AccountServiceLocator
    {
        public static bool TryGetServices(out IAuthService authService, out IProfileService profileService)
        {
            authService = null;
            profileService = null;

            GameCompositionRoot root = GameCompositionRoot.Instance;
            if (root == null || !root.IsReady)
                return false;

            authService = root.AuthService;
            profileService = root.ProfileService;

            return authService != null && profileService != null;
        }
    }
}