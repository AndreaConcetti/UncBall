using System;
using System.Threading;
using UnityEngine;
using UncballArena.Core.Profile.Services;

namespace UncballArena.Core.Auth.UI
{
    public sealed class AccountPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoRefreshOnEnable = true;
        [SerializeField] private bool logDebug = true;

        public event Action<AccountOverview> AccountOverviewChanged;

        private IAuthService authService;
        private IProfileService profileService;
        private bool subscribed;

        public AccountOverview CurrentOverview { get; private set; }

        private void OnEnable()
        {
            TryBind();

            if (autoRefreshOnEnable)
                Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public bool TryBind()
        {
            if (!AccountServiceLocator.TryGetServices(out authService, out profileService))
                return false;

            if (!subscribed)
            {
                authService.SessionChanged += HandleSessionChanged;
                profileService.ProfileChanged += HandleProfileChanged;
                subscribed = true;
            }

            return true;
        }

        public void Refresh()
        {
            if (!TryBind())
                return;

            CurrentOverview = authService.GetAccountOverview();
            AccountOverviewChanged?.Invoke(CurrentOverview);

            if (logDebug)
                Debug.Log($"[AccountPresenter] Refresh -> {CurrentOverview}", this);
        }

        public async void SignInAsGuest(string displayName = "")
        {
            if (!TryBind())
                return;

            try
            {
                await authService.SignInAsGuestAsync(CancellationToken.None);

                if (!string.IsNullOrWhiteSpace(displayName))
                    await profileService.SetDisplayNameAsync(displayName.Trim());

                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] SignInAsGuest failed: {ex.Message}", this);
            }
        }

        public async void SignInWithGoogle()
        {
            if (!TryBind())
                return;

            try
            {
                await authService.SignInWithGooglePlayAsync(CancellationToken.None);
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] SignInWithGoogle failed: {ex.Message}", this);
            }
        }

        public async void SignInWithApple()
        {
            if (!TryBind())
                return;

            try
            {
                await authService.SignInWithAppleAsync(CancellationToken.None);
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] SignInWithApple failed: {ex.Message}", this);
            }
        }

        public async void LinkGoogle()
        {
            if (!TryBind())
                return;

            try
            {
                await authService.LinkCurrentGuestToGooglePlayAsync(CancellationToken.None);
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] LinkGoogle failed: {ex.Message}", this);
            }
        }

        public async void LinkApple()
        {
            if (!TryBind())
                return;

            try
            {
                await authService.LinkCurrentGuestToAppleAsync(CancellationToken.None);
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] LinkApple failed: {ex.Message}", this);
            }
        }

        public async void UpdateDisplayName(string newDisplayName)
        {
            if (!TryBind())
                return;

            try
            {
                await profileService.SetDisplayNameAsync(newDisplayName);
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] UpdateDisplayName failed: {ex.Message}", this);
            }
        }

        private void HandleSessionChanged(AuthSession session)
        {
            Refresh();
        }

        private void HandleProfileChanged(Core.Profile.Models.ProfileSnapshot snapshot)
        {
            Refresh();
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (authService != null)
                authService.SessionChanged -= HandleSessionChanged;

            if (profileService != null)
                profileService.ProfileChanged -= HandleProfileChanged;

            subscribed = false;
        }
    }
}