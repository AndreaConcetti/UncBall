using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UncballArena.Core.Auth.DisplayName;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Services;

namespace UncballArena.Core.Auth.UI
{
    public sealed class FirstLaunchNamePanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DisplayNameValidator validator;
        [SerializeField] private FirstLaunchNamePolicy policy;

        [Header("UI")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button continueButton;

        [Header("Config")]
        [SerializeField] private string panelTitle = "Choose your name";
        [SerializeField] private string placeholderSuggestedName = "";
        [SerializeField] private bool hideOnStartIfNotNeeded = true;
        [SerializeField] private bool lockMainMenuUnderPanel = false;
        [SerializeField] private GameObject[] objectsToDisableWhileOpen;
        [SerializeField] private bool logDebug = true;

        private IAuthService authService;
        private IProfileService profileService;
        private bool isOpen;
        private bool waitingForCompositionRoot;

        private void Reset()
        {
            rootPanel = gameObject;
        }

        private void Awake()
        {
            if (validator == null)
                validator = GetComponent<DisplayNameValidator>();

            if (policy == null)
                policy = GetComponent<FirstLaunchNamePolicy>();

            if (titleText != null)
                titleText.text = panelTitle;

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ApplyAndCloseIfValid);
                continueButton.onClick.AddListener(ApplyAndCloseIfValid);
            }

            if (errorText != null)
                errorText.text = string.Empty;

            waitingForCompositionRoot = true;

            if (hideOnStartIfNotNeeded && rootPanel != null)
                rootPanel.SetActive(false);
        }

        private void Update()
        {
            if (!waitingForCompositionRoot)
                return;

            GameCompositionRoot root = GameCompositionRoot.Instance;
            if (root == null || !root.IsReady)
                return;

            waitingForCompositionRoot = false;
            EvaluateOpenState();
        }

        public void EvaluateOpenState()
        {
            if (!TryResolveServices())
            {
                if (logDebug)
                    Debug.LogWarning("[FirstLaunchNamePanel] EvaluateOpenState skipped: services not ready.", this);
                return;
            }

            string profileDisplayName = profileService != null && profileService.CurrentProfile != null
                ? profileService.CurrentProfile.DisplayName
                : string.Empty;

            bool mustOpen = policy == null ||
                            policy.ShouldRequireName(authService.CurrentSession, profileDisplayName);

            if (logDebug)
            {
                Debug.Log(
                    $"[FirstLaunchNamePanel] EvaluateOpenState -> MustOpen={mustOpen} | " +
                    $"AuthName={authService.CurrentSession?.Identity?.DisplayName} | " +
                    $"ProfileName={profileDisplayName}",
                    this
                );
            }

            if (mustOpen)
                Open();
            else
                Close();
        }

        public void Open()
        {
            if (rootPanel != null)
                rootPanel.SetActive(true);

            if (titleText != null)
                titleText.text = panelTitle;

            if (errorText != null)
                errorText.text = string.Empty;

            string currentName = ResolveCurrentDisplayName();

            if (nameInput != null)
            {
                if (ShouldPreFillInput(currentName))
                    nameInput.text = currentName;
                else
                    nameInput.text = placeholderSuggestedName;

                nameInput.ActivateInputField();
                nameInput.Select();
            }

            SetOverlayTargetsEnabled(false);
            isOpen = true;

            if (logDebug)
                Debug.Log("[FirstLaunchNamePanel] Opened.", this);
        }

        public void Close()
        {
            if (rootPanel != null)
                rootPanel.SetActive(false);

            SetOverlayTargetsEnabled(true);
            isOpen = false;

            if (logDebug)
                Debug.Log("[FirstLaunchNamePanel] Closed.", this);
        }

        public async void ApplyAndCloseIfValid()
        {
            if (!TryResolveServices())
            {
                if (logDebug)
                    Debug.LogWarning("[FirstLaunchNamePanel] Apply skipped: services not ready.", this);
                return;
            }

            if (validator == null)
            {
                Debug.LogError("[FirstLaunchNamePanel] Missing DisplayNameValidator.", this);
                return;
            }

            string requested = nameInput != null ? nameInput.text : string.Empty;
            DisplayNameValidationResult result = validator.Validate(requested);

            if (!result.IsValid)
            {
                if (errorText != null)
                    errorText.text = result.ErrorMessage;

                if (logDebug)
                    Debug.LogWarning("[FirstLaunchNamePanel] Invalid name -> " + result.ErrorMessage, this);

                return;
            }

            if (policy != null && policy.IsRequiredPlaceholderName(result.SanitizedValue))
            {
                if (errorText != null)
                    errorText.text = "Choose a real player name.";

                if (logDebug)
                    Debug.LogWarning("[FirstLaunchNamePanel] Rejected placeholder name.", this);

                return;
            }

            if (errorText != null)
                errorText.text = string.Empty;

            await profileService.SetDisplayNameAsync(result.SanitizedValue);

            if (logDebug)
                Debug.Log("[FirstLaunchNamePanel] Name applied -> " + result.SanitizedValue, this);

            Close();
        }

        public bool IsOpen()
        {
            return isOpen;
        }

        private bool TryResolveServices()
        {
            GameCompositionRoot root = GameCompositionRoot.Instance;
            if (root == null || !root.IsReady)
                return false;

            authService = root.AuthService;
            profileService = root.ProfileService;

            return authService != null && profileService != null;
        }

        private string ResolveCurrentDisplayName()
        {
            if (profileService != null &&
                profileService.CurrentProfile != null &&
                !string.IsNullOrWhiteSpace(profileService.CurrentProfile.DisplayName))
            {
                return profileService.CurrentProfile.DisplayName;
            }

            if (authService != null &&
                authService.CurrentSession != null &&
                authService.CurrentSession.Identity != null &&
                !string.IsNullOrWhiteSpace(authService.CurrentSession.Identity.DisplayName))
            {
                return authService.CurrentSession.Identity.DisplayName;
            }

            return string.Empty;
        }

        private bool ShouldPreFillInput(string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return false;

            if (policy != null && policy.IsRequiredPlaceholderName(currentName))
                return false;

            return true;
        }

        private void SetOverlayTargetsEnabled(bool enabled)
        {
            if (!lockMainMenuUnderPanel)
                return;

            for (int i = 0; i < objectsToDisableWhileOpen.Length; i++)
            {
                GameObject target = objectsToDisableWhileOpen[i];
                if (target == null)
                    continue;

                target.SetActive(enabled);
            }
        }
    }
}