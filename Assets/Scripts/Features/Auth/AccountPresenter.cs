using System;
using System.Threading;
using UnityEngine;
using UncballArena.Core.Profile.Services;

namespace UncballArena.Core.Auth.UI
{
    public sealed class AccountPresenter : MonoBehaviour
    {
        [Header("Behavior")]
        [SerializeField] private bool autoRefreshOnEnable = true;
        [SerializeField] private bool logDebug = true;
        [SerializeField] private bool preventDoubleClick = true;

        public event Action<AccountOverview> AccountOverviewChanged;

        private IAuthService authService;
        private IProfileService profileService;
        private bool subscribed;
        private bool actionInProgress;

        public AccountOverview CurrentOverview { get; private set; }

        public bool IsBusy => actionInProgress;

        private void OnEnable()
        {
            TryBind();

            if (autoRefreshOnEnable)
                Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            actionInProgress = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            actionInProgress = false;
        }

        public bool TryBind()
        {
            if (!AccountServiceLocator.TryGetServices(out authService, out profileService))
            {
                if (logDebug)
                    Debug.LogWarning("[AccountPresenter] TryBind failed. Services not available yet.", this);

                return false;
            }

            if (!subscribed)
            {
                authService.SessionChanged += HandleSessionChanged;
                profileService.ProfileChanged += HandleProfileChanged;
                subscribed = true;

                if (logDebug)
                    Debug.Log("[AccountPresenter] Services bound and event subscriptions registered.", this);
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
            {
                Debug.Log(
                    $"[AccountPresenter] Refresh -> Provider={CurrentOverview.CurrentProviderType} | " +
                    $"Authenticated={CurrentOverview.IsAuthenticated} | Guest={CurrentOverview.IsGuest} | " +
                    $"Linked={CurrentOverview.IsLinked} | DisplayName={CurrentOverview.DisplayName}",
                    this);
            }
        }

        public async void SignInAsGuest(string displayName = "")
        {
            if (!BeginAction("SignInAsGuest"))
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
            finally
            {
                EndAction("SignInAsGuest");
            }
        }

        public async void SignInWithGoogle()
        {
            if (!BeginAction("SignInWithGoogle"))
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
            finally
            {
                EndAction("SignInWithGoogle");
            }
        }

        public async void SignInWithApple()
        {
            if (!BeginAction("SignInWithApple"))
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
            finally
            {
                EndAction("SignInWithApple");
            }
        }

        public async void LinkGoogle()
        {
            if (!BeginAction("LinkGoogle"))
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
            finally
            {
                EndAction("LinkGoogle");
            }
        }

        public async void LinkApple()
        {
            if (!BeginAction("LinkApple"))
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
            finally
            {
                EndAction("LinkApple");
            }
        }

        public async void UpdateDisplayName(string newDisplayName)
        {
            if (!BeginAction("UpdateDisplayName"))
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(newDisplayName))
                {
                    Debug.LogWarning("[AccountPresenter] UpdateDisplayName ignored because input is empty.", this);
                    return;
                }

                await profileService.SetDisplayNameAsync(newDisplayName.Trim());
                Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AccountPresenter] UpdateDisplayName failed: {ex.Message}", this);
            }
            finally
            {
                EndAction("UpdateDisplayName");
            }
        }

        private bool BeginAction(string actionName)
        {
            if (!TryBind())
                return false;

            if (preventDoubleClick && actionInProgress)
            {
                if (logDebug)
                    Debug.LogWarning($"[AccountPresenter] Ignored {actionName} because another auth action is already running.", this);

                return false;
            }

            actionInProgress = true;

            if (logDebug)
                Debug.Log($"[AccountPresenter] Starting action: {actionName}", this);

            return true;
        }

        private void EndAction(string actionName)
        {
            actionInProgress = false;

            if (logDebug)
                Debug.Log($"[AccountPresenter] Finished action: {actionName}", this);
        }

        private void HandleSessionChanged(AuthSession session)
        {
            if (logDebug)
            {
                Debug.Log(
                    $"[AccountPresenter] SessionChanged received -> Provider={session.ProviderType} | " +
                    $"Authenticated={session.IsAuthenticated} | Guest={session.IsGuest} | " +
                    $"Linked={session.IsLinked} | DisplayName={session.DisplayName}",
                    this);
            }

            Refresh();
        }

        private void HandleProfileChanged(Core.Profile.Models.ProfileSnapshot snapshot)
        {
            if (logDebug)
                Debug.Log("[AccountPresenter] ProfileChanged received.", this);

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

            if (logDebug)
                Debug.Log("[AccountPresenter] Event subscriptions removed.", this);
        }
    }
}