using UnityEngine;
using UncballArena.Core.Bootstrap;

namespace UncballArena.Core.Runtime
{
    public static class OnlineLocalPlayerContext
    {
        public static bool IsAvailable
        {
            get
            {
                return GameCompositionRoot.Instance != null &&
                       GameCompositionRoot.Instance.IsReady &&
                       GameCompositionRoot.Instance.AuthService != null &&
                       GameCompositionRoot.Instance.ProfileService != null &&
                       GameCompositionRoot.Instance.AuthService.CurrentSession != null &&
                       GameCompositionRoot.Instance.AuthService.CurrentSession.Identity != null &&
                       GameCompositionRoot.Instance.ProfileService.CurrentProfile != null;
            }
        }

        public static string PlayerId
        {
            get
            {
                if (!IsAvailable)
                    return string.Empty;

                return GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.PlayerId;
            }
        }

        public static string DisplayName
        {
            get
            {
                if (!IsAvailable)
                    return "Guest";

                string profileName = GameCompositionRoot.Instance.ProfileService.CurrentProfile.DisplayName;
                if (!string.IsNullOrWhiteSpace(profileName))
                    return profileName;

                string authName = GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.DisplayName;
                return string.IsNullOrWhiteSpace(authName) ? "Guest" : authName;
            }
        }

        public static string EquippedBallSkinId
        {
            get
            {
                if (!IsAvailable)
                    return string.Empty;

                return GameCompositionRoot.Instance.ProfileService.CurrentProfile.EquippedBallSkinId;
            }
        }

        public static string EquippedTableSkinId
        {
            get
            {
                if (!IsAvailable)
                    return string.Empty;

                return GameCompositionRoot.Instance.ProfileService.CurrentProfile.EquippedTableSkinId;
            }
        }

        public static int Level
        {
            get
            {
                if (!IsAvailable)
                    return 1;

                return Mathf.Max(1, GameCompositionRoot.Instance.ProfileService.CurrentProfile.Level);
            }
        }

        public static int TotalWins
        {
            get
            {
                if (!IsAvailable)
                    return 0;

                return Mathf.Max(0, GameCompositionRoot.Instance.ProfileService.CurrentProfile.TotalWins);
            }
        }

        public static int TotalMatches
        {
            get
            {
                if (!IsAvailable)
                    return 0;

                return Mathf.Max(0, GameCompositionRoot.Instance.ProfileService.CurrentProfile.TotalMatches);
            }
        }
    }
}