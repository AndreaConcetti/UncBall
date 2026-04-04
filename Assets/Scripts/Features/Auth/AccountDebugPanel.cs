using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UncballArena.Core.Auth.UI
{
    public sealed class AccountDebugPanel : MonoBehaviour
    {
        [Header("Presenter")]
        [SerializeField] private AccountPresenter presenter;

        [Header("Texts")]
        [SerializeField] private TMP_Text accountSummaryText;
        [SerializeField] private TMP_Text providerStatusText;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField displayNameInput;

        [Header("Buttons")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button signInGuestButton;
        [SerializeField] private Button signInGoogleButton;
        [SerializeField] private Button signInAppleButton;
        [SerializeField] private Button linkGoogleButton;
        [SerializeField] private Button linkAppleButton;
        [SerializeField] private Button applyDisplayNameButton;

        [Header("Config")]
        [SerializeField] private string fallbackGuestName = "Guest";
        [SerializeField] private bool logDebug = true;

        private void Reset()
        {
            presenter = GetComponent<AccountPresenter>();
        }

        private void Awake()
        {
            if (presenter == null)
                presenter = GetComponent<AccountPresenter>();

            BindButtons();
        }

        private void OnEnable()
        {
            if (presenter != null)
            {
                presenter.AccountOverviewChanged += HandleOverviewChanged;
                presenter.Refresh();
            }
        }

        private void OnDisable()
        {
            if (presenter != null)
                presenter.AccountOverviewChanged -= HandleOverviewChanged;
        }

        private void BindButtons()
        {
            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnPressRefresh);

            if (signInGuestButton != null)
                signInGuestButton.onClick.AddListener(OnPressGuest);

            if (signInGoogleButton != null)
                signInGoogleButton.onClick.AddListener(OnPressGoogle);

            if (signInAppleButton != null)
                signInAppleButton.onClick.AddListener(OnPressApple);

            if (linkGoogleButton != null)
                linkGoogleButton.onClick.AddListener(OnPressLinkGoogle);

            if (linkAppleButton != null)
                linkAppleButton.onClick.AddListener(OnPressLinkApple);

            if (applyDisplayNameButton != null)
                applyDisplayNameButton.onClick.AddListener(OnPressApplyDisplayName);
        }

        private void HandleOverviewChanged(AccountOverview overview)
        {
            RefreshTexts(overview);
            RefreshButtons(overview);

            if (displayNameInput != null && string.IsNullOrWhiteSpace(displayNameInput.text))
                displayNameInput.text = overview != null ? overview.DisplayName : string.Empty;

            if (logDebug)
                Debug.Log($"[AccountDebugPanel] Overview changed -> {overview}", this);
        }

        private void RefreshTexts(AccountOverview overview)
        {
            if (accountSummaryText != null)
                accountSummaryText.text = BuildAccountSummary(overview);

            if (providerStatusText != null)
                providerStatusText.text = BuildProviderSummary(overview);
        }

        private void RefreshButtons(AccountOverview overview)
        {
            if (overview == null)
            {
                SetButtonState(refreshButton, true);
                SetButtonState(signInGuestButton, false);
                SetButtonState(signInGoogleButton, false);
                SetButtonState(signInAppleButton, false);
                SetButtonState(linkGoogleButton, false);
                SetButtonState(linkAppleButton, false);
                SetButtonState(applyDisplayNameButton, false);
                return;
            }

            bool googleAvailable = false;
            bool appleAvailable = false;
            bool googleCanLink = false;
            bool appleCanLink = false;

            if (overview.ProviderStatuses != null)
            {
                for (int i = 0; i < overview.ProviderStatuses.Count; i++)
                {
                    ProviderLinkStatus status = overview.ProviderStatuses[i];
                    if (status == null)
                        continue;

                    if (status.ProviderType == AuthProviderType.GooglePlayGames)
                    {
                        googleAvailable = status.IsAvailable;
                        googleCanLink = status.CanLink;
                    }
                    else if (status.ProviderType == AuthProviderType.Apple)
                    {
                        appleAvailable = status.IsAvailable;
                        appleCanLink = status.CanLink;
                    }
                }
            }

            SetButtonState(refreshButton, true);
            SetButtonState(signInGuestButton, true);
            SetButtonState(signInGoogleButton, googleAvailable);
            SetButtonState(signInAppleButton, appleAvailable);
            SetButtonState(linkGoogleButton, googleCanLink);
            SetButtonState(linkAppleButton, appleCanLink);
            SetButtonState(applyDisplayNameButton, true);
        }

        private string BuildAccountSummary(AccountOverview overview)
        {
            if (overview == null)
                return "Account not available.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ACCOUNT");
            sb.AppendLine("PlayerId: " + overview.PlayerId);
            sb.AppendLine("DisplayName: " + overview.DisplayName);
            sb.AppendLine("Authenticated: " + overview.IsAuthenticated);
            sb.AppendLine("Guest: " + overview.IsGuest);
            sb.AppendLine("Linked: " + overview.IsLinked);
            sb.AppendLine("Current Provider: " + overview.CurrentProviderType);
            return sb.ToString();
        }

        private string BuildProviderSummary(AccountOverview overview)
        {
            if (overview == null || overview.ProviderStatuses == null)
                return "No provider data.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PROVIDERS");

            for (int i = 0; i < overview.ProviderStatuses.Count; i++)
            {
                ProviderLinkStatus status = overview.ProviderStatuses[i];
                if (status == null)
                    continue;

                sb.AppendLine(
                    $"{status.ProviderType} | Available={status.IsAvailable} | Linked={status.IsLinked} | CanLink={status.CanLink}"
                );
            }

            return sb.ToString();
        }

        private void OnPressRefresh()
        {
            presenter?.Refresh();
        }

        private void OnPressGuest()
        {
            string guestName = GetRequestedDisplayNameOrFallback();
            presenter?.SignInAsGuest(guestName);
        }

        private void OnPressGoogle()
        {
            presenter?.SignInWithGoogle();
        }

        private void OnPressApple()
        {
            presenter?.SignInWithApple();
        }

        private void OnPressLinkGoogle()
        {
            presenter?.LinkGoogle();
        }

        private void OnPressLinkApple()
        {
            presenter?.LinkApple();
        }

        private void OnPressApplyDisplayName()
        {
            string requested = GetRequestedDisplayNameOrFallback();
            presenter?.UpdateDisplayName(requested);
        }

        private string GetRequestedDisplayNameOrFallback()
        {
            if (displayNameInput == null || string.IsNullOrWhiteSpace(displayNameInput.text))
                return fallbackGuestName;

            return displayNameInput.text.Trim();
        }

        private static void SetButtonState(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.interactable = enabled;
        }
    }
}