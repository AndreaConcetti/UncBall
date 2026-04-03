using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Auth.Services;
using UncballArena.Core.Profile.Repositories;
using UncballArena.Core.Profile.Services;
using UncballArena.Core.Runtime;

namespace UncballArena.Core.Bootstrap
{
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        public static GameCompositionRoot Instance { get; private set; }

        public IAuthService AuthService { get; private set; }
        public IProfileService ProfileService { get; private set; }

        public AuthRuntimeState AuthRuntimeState { get; private set; }
        public ProfileRuntimeState ProfileRuntimeState { get; private set; }

        public bool IsReady { get; private set; }

        [Header("Bootstrap")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private string initialGuestDisplayName = "Guest";

        private async void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                if (transform.parent != null)
                {
                    Debug.LogWarning("[GameCompositionRoot] Cannot use DontDestroyOnLoad on a non-root object. Detaching from parent.");
                    transform.SetParent(null);
                }

                DontDestroyOnLoad(gameObject);
            }

            await BootstrapAsync();
        }

        private async Task BootstrapAsync()
        {
            AuthRuntimeState = new AuthRuntimeState();
            ProfileRuntimeState = new ProfileRuntimeState();

            AuthService = new LocalAuthService();
            ProfileService = new ProfileService(new LocalProfileRepository());

            AuthService.SessionChanged += OnSessionChanged;
            ProfileService.ProfileChanged += OnProfileChanged;

            await AuthService.InitializeAsync();

            if (AuthService.CurrentSession == null || !AuthService.CurrentSession.HasUsableIdentity())
                await AuthService.SignInAsGuestAsync(initialGuestDisplayName);

            string playerId = AuthService.CurrentSession.Identity.PlayerId;
            string authDisplayName = AuthService.CurrentSession.Identity.DisplayName;

            await ProfileService.InitializeAsync(playerId, string.Empty);

            if (ProfileService.CurrentProfile != null &&
                string.IsNullOrWhiteSpace(ProfileService.CurrentProfile.DisplayName) &&
                !string.IsNullOrWhiteSpace(authDisplayName))
            {
                await ProfileService.SetDisplayNameAsync(authDisplayName);
            }

            AuthRuntimeState.Set(AuthService.CurrentSession);
            ProfileRuntimeState.Set(ProfileService.CurrentProfile);

            IsReady = true;

            Debug.Log(
                $"[GameCompositionRoot] Ready. PlayerId={playerId} | DisplayName={ProfileService.CurrentProfile?.DisplayName} | ProfileId={ProfileService.CurrentProfile?.ProfileId}"
            );
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (AuthService != null)
                AuthService.SessionChanged -= OnSessionChanged;

            if (ProfileService != null)
                ProfileService.ProfileChanged -= OnProfileChanged;
        }

        private void OnSessionChanged(Core.Auth.Models.AuthSession session)
        {
            AuthRuntimeState?.Set(session);
        }

        private void OnProfileChanged(Core.Profile.Models.ProfileSnapshot snapshot)
        {
            ProfileRuntimeState?.Set(snapshot);
        }
    }
}