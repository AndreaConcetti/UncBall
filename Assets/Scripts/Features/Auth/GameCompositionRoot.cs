using System.Threading;
using System.Threading.Tasks;
using UncballArena.Core.Auth;
using UncballArena.Core.Profile.Repositories;
using UncballArena.Core.Profile.Services;
using UncballArena.Core.Runtime;
using UnityEngine;

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

        [Header("Backend Auth")]
        [SerializeField] private bool usePlayFabBackendAuth = false;
        [SerializeField] private string playFabTitleId = "";

        [Header("Backend Profile")]
        [SerializeField] private bool usePlayFabProfileRepository = true;

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

            LocalAuthStorage localAuthStorage = new LocalAuthStorage();
            GuestAuthProvider guestProvider = new GuestAuthProvider(localAuthStorage, initialGuestDisplayName);
            GooglePlayAuthProvider googlePlayProvider = new GooglePlayAuthProvider();
            AppleAuthProvider appleProvider = new AppleAuthProvider();

            IBackendAuthService backendAuthService = CreateBackendAuthService();

            AuthService = new AuthService(
                localAuthStorage,
                guestProvider,
                googlePlayProvider,
                appleProvider,
                backendAuthService
            );

            IProfileRepository localCacheRepository = new LocalProfileRepository();
            IProfileRepository profileRepository = CreateProfileRepository(localCacheRepository);

            ProfileService = new ProfileService(
                profileRepository,
                AuthService
            );

            AuthService.SessionChanged += OnSessionChanged;
            ProfileService.ProfileChanged += OnProfileChanged;

            await AuthService.InitializeAsync(CancellationToken.None);

            if (AuthService.CurrentSession == null || !AuthService.CurrentSession.HasUsableIdentity())
                await AuthService.SignInAsGuestAsync(CancellationToken.None);

            string effectivePlayerId = AuthService.CurrentSession.EffectivePlayerId;
            string authDisplayName = AuthService.CurrentSession.DisplayName;

            await ProfileService.InitializeAsync(effectivePlayerId, authDisplayName);

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
                $"[GameCompositionRoot] Ready. " +
                $"LocalPlayerId={AuthService.CurrentSession.PlayerId} | " +
                $"EffectivePlayerId={AuthService.CurrentSession.EffectivePlayerId} | " +
                $"BackendAuthenticated={AuthService.CurrentSession.HasBackendSession} | " +
                $"BackendPlayerId={AuthService.CurrentSession.BackendPlayerId} | " +
                $"DisplayName={ProfileService.CurrentProfile?.DisplayName} | " +
                $"ProfileId={ProfileService.CurrentProfile?.ProfileId}");
        }

        private IBackendAuthService CreateBackendAuthService()
        {
            if (usePlayFabBackendAuth)
                return new PlayFabBackendAuthService(playFabTitleId);

            return new NullBackendAuthService();
        }

        private IProfileRepository CreateProfileRepository(IProfileRepository localCacheRepository)
        {
            if (usePlayFabProfileRepository)
            {
                IProfileRepository remoteRepository = new PlayFabProfileRepository();
                return new CachedProfileRepository(remoteRepository, localCacheRepository);
            }

            return localCacheRepository;
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

        private void OnSessionChanged(AuthSession session)
        {
            AuthRuntimeState?.Set(session);
        }

        private void OnProfileChanged(Core.Profile.Models.ProfileSnapshot snapshot)
        {
            ProfileRuntimeState?.Set(snapshot);
        }
    }
}