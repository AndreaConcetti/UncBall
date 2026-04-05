using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UncballArena.Core.Auth;
using UncballArena.Core.Auth.UI;

public class PlayerProfileAccountPanelUI : MonoBehaviour
{
    private enum PendingOperation
    {
        None = 0,
        LinkGoogle = 1,
        LinkApple = 2,
        Unlink = 3
    }

    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private AccountPresenter accountPresenter;
    [SerializeField] private AccountOperationFeedbackPanel feedbackPanel;

    [Header("Identity UI")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private string displayNamePrefix = "";
    [SerializeField] private string displayNameSuffix = "";

    [Header("Single Status UI")]
    [SerializeField] private TMP_Text statusDisplayText;
    [SerializeField] private string guestStatusMessage = "LOGGED AS GUEST";
    [SerializeField] private string googleStatusMessage = "LOGGED WITH GOOGLE PLAY";
    [SerializeField] private string appleStatusMessage = "LOGGED WITH APPLE";
    [SerializeField] private string facebookStatusMessage = "LOGGED WITH FACEBOOK";
    [SerializeField] private string genericLinkedStatusMessage = "ACCOUNT LINKED";

    [Header("Rank UI")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private string rankPrefix = "RANK: ";
    [SerializeField] private string rankSuffix = " LP";
    [SerializeField] private int fallbackRankPoints = 1000;

    [Header("Section Labels")]
    [SerializeField] private GameObject linkAccountTextRoot;

    [Header("Provider Roots")]
    [SerializeField] private GameObject googleLoginRoot;
    [SerializeField] private GameObject appleLoginRoot;
    [SerializeField] private GameObject facebookLoginRoot;
    [SerializeField] private GameObject unlinkAccountRoot;

    [Header("Provider Buttons")]
    [SerializeField] private Button googleButton;
    [SerializeField] private Button appleButton;
    [SerializeField] private Button facebookButton;
    [SerializeField] private Button unlinkButton;

    [Header("Platform Rules")]
    [SerializeField] private bool showGoogleOnAndroid = true;
    [SerializeField] private bool showAppleOniOS = true;
    [SerializeField] private bool showFacebookEverywhere = false;
    [SerializeField] private bool hideProviderButtonsInEditor = true;

    [Header("Feedback Messages")]
    [SerializeField] private string linkSuccessMessage = "ACCOUNT LINKED SUCCESSFULLY";
    [SerializeField] private string unlinkSuccessMessage = "ACCOUNT UNLINKED";
    [SerializeField] private string linkFailedMessage = "ACCOUNT LINK FAILED";
    [SerializeField] private string unlinkFailedMessage = "ACCOUNT UNLINK FAILED";
    [SerializeField] private string facebookNotImplementedMessage = "FACEBOOK NOT IMPLEMENTED YET";

    [Header("Operation Confirmation")]
    [SerializeField] private float operationTimeoutSeconds = 3f;

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool autoWireButtons = true;
    [SerializeField] private bool autoFindDependencies = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool subscribedProfile;
    private bool subscribedAccount;

    private PendingOperation pendingOperation = PendingOperation.None;
    private Coroutine pendingOperationRoutine;
    private AccountSnapshot preOperationSnapshot;

    private void Awake()
    {
        ResolveDependencies();
        WireButtonsIfNeeded();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        WireButtonsIfNeeded();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnwireButtons();
        CancelPendingOperation();
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        RefreshPlayerName();
        RefreshStatusDisplay();
        RefreshRank();
        RefreshAccountActionVisibility();

        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] RefreshUI completed.", this);
    }

    private void RefreshPlayerName()
    {
        string displayName = "GUEST";

        AccountOverview overview = GetCurrentOverview();

        if (overview != null && !string.IsNullOrWhiteSpace(overview.DisplayName))
        {
            displayName = overview.DisplayName;
        }
        else if (profileManager != null && profileManager.ActiveProfile != null)
        {
            string profileName = profileManager.ActiveProfile.displayName;
            if (!string.IsNullOrWhiteSpace(profileName))
                displayName = profileName;
        }

        if (displayNameText != null)
            displayNameText.text = displayNamePrefix + displayName.ToUpperInvariant() + displayNameSuffix;

        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] Player name -> " + displayName, this);
    }

    private void RefreshStatusDisplay()
    {
        if (statusDisplayText == null)
            return;

        AccountSnapshot snapshot = ReadAccountSnapshot();
        statusDisplayText.text = GetStatusMessage(snapshot);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileAccountPanelUI] StatusDisplay -> " +
                statusDisplayText.text +
                " | IsAuthenticated=" + snapshot.IsAuthenticated +
                " | IsGuest=" + snapshot.IsGuest +
                " | IsLinked=" + snapshot.IsLinked +
                " | ProviderType=" + snapshot.ProviderType,
                this
            );
        }
    }

    private void RefreshRank()
    {
        if (rankText == null)
            return;

        int points = fallbackRankPoints;
        rankText.text = rankPrefix + points + rankSuffix;

        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] Rank text -> " + rankText.text, this);
    }

    private void RefreshAccountActionVisibility()
    {
        AccountSnapshot snapshot = ReadAccountSnapshot();

        bool isAndroid = Application.platform == RuntimePlatform.Android;
        bool isIOS = Application.platform == RuntimePlatform.IPhonePlayer;
        bool isEditor = Application.isEditor;

        bool canShowGoogle = showGoogleOnAndroid && isAndroid;
        bool canShowApple = showAppleOniOS && isIOS;
        bool canShowFacebook = showFacebookEverywhere;

        if (hideProviderButtonsInEditor && isEditor)
        {
            canShowGoogle = false;
            canShowApple = false;
            canShowFacebook = false;
        }

        bool showProviderButtons = snapshot.IsAuthenticated && !snapshot.IsLinked;
        bool showUnlink = snapshot.IsAuthenticated && snapshot.IsLinked;

        bool showGoogle = showProviderButtons && canShowGoogle;
        bool showApple = showProviderButtons && canShowApple;
        bool showFacebook = showProviderButtons && canShowFacebook;
        bool showLinkLabel = showGoogle || showApple || showFacebook;

        if (googleLoginRoot != null)
            googleLoginRoot.SetActive(showGoogle);

        if (appleLoginRoot != null)
            appleLoginRoot.SetActive(showApple);

        if (facebookLoginRoot != null)
            facebookLoginRoot.SetActive(showFacebook);

        if (unlinkAccountRoot != null)
            unlinkAccountRoot.SetActive(showUnlink);

        if (linkAccountTextRoot != null)
            linkAccountTextRoot.SetActive(showLinkLabel);

        if (googleButton != null)
            googleButton.gameObject.SetActive(showGoogle);

        if (appleButton != null)
            appleButton.gameObject.SetActive(showApple);

        if (facebookButton != null)
            facebookButton.gameObject.SetActive(showFacebook);

        if (unlinkButton != null)
            unlinkButton.gameObject.SetActive(showUnlink);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileAccountPanelUI] Action visibility -> " +
                "Google=" + showGoogle +
                " | Apple=" + showApple +
                " | Facebook=" + showFacebook +
                " | Unlink=" + showUnlink +
                " | LinkLabel=" + showLinkLabel,
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (accountPresenter == null && autoFindDependencies)
            accountPresenter = GetComponent<AccountPresenter>();

        if (accountPresenter == null && autoFindDependencies)
            accountPresenter = FindFirstObjectByType<AccountPresenter>(FindObjectsInactive.Include);

        if (feedbackPanel == null && autoFindDependencies)
            feedbackPanel = FindFirstObjectByType<AccountOperationFeedbackPanel>(FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (!subscribedProfile && profileManager != null)
        {
            profileManager.OnActiveProfileChanged += HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
            subscribedProfile = true;
        }

        if (!subscribedAccount && accountPresenter != null)
        {
            accountPresenter.AccountOverviewChanged += HandleAccountOverviewChanged;
            subscribedAccount = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedProfile && profileManager != null)
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
            subscribedProfile = false;
        }

        if (subscribedAccount && accountPresenter != null)
        {
            accountPresenter.AccountOverviewChanged -= HandleAccountOverviewChanged;
            subscribedAccount = false;
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUI();
    }

    private void HandleAccountOverviewChanged(AccountOverview _)
    {
        RefreshUI();
        EvaluatePendingOperation();
    }

    private void WireButtonsIfNeeded()
    {
        if (!autoWireButtons)
            return;

        if (googleButton != null)
        {
            googleButton.onClick.RemoveListener(OnGoogleClicked);
            googleButton.onClick.AddListener(OnGoogleClicked);
        }

        if (appleButton != null)
        {
            appleButton.onClick.RemoveListener(OnAppleClicked);
            appleButton.onClick.AddListener(OnAppleClicked);
        }

        if (facebookButton != null)
        {
            facebookButton.onClick.RemoveListener(OnFacebookClicked);
            facebookButton.onClick.AddListener(OnFacebookClicked);
        }

        if (unlinkButton != null)
        {
            unlinkButton.onClick.RemoveListener(OnUnlinkClicked);
            unlinkButton.onClick.AddListener(OnUnlinkClicked);
        }
    }

    private void UnwireButtons()
    {
        if (!autoWireButtons)
            return;

        if (googleButton != null)
            googleButton.onClick.RemoveListener(OnGoogleClicked);

        if (appleButton != null)
            appleButton.onClick.RemoveListener(OnAppleClicked);

        if (facebookButton != null)
            facebookButton.onClick.RemoveListener(OnFacebookClicked);

        if (unlinkButton != null)
            unlinkButton.onClick.RemoveListener(OnUnlinkClicked);
    }

    private void OnGoogleClicked()
    {
        if (accountPresenter == null || accountPresenter.IsBusy)
            return;

        AccountSnapshot snapshot = ReadAccountSnapshot();

        if (snapshot.IsLinked && snapshot.ProviderType == AuthProviderType.GooglePlayGames)
        {
            if (logDebug)
                Debug.Log("[PlayerProfileAccountPanelUI] Google click ignored because Google is already linked.", this);
            return;
        }

        BeginPendingOperation(PendingOperation.LinkGoogle, snapshot);
        accountPresenter.LinkGoogle();
    }

    private void OnAppleClicked()
    {
        if (accountPresenter == null || accountPresenter.IsBusy)
            return;

        AccountSnapshot snapshot = ReadAccountSnapshot();

        if (snapshot.IsLinked && snapshot.ProviderType == AuthProviderType.Apple)
        {
            if (logDebug)
                Debug.Log("[PlayerProfileAccountPanelUI] Apple click ignored because Apple is already linked.", this);
            return;
        }

        BeginPendingOperation(PendingOperation.LinkApple, snapshot);
        accountPresenter.LinkApple();
    }

    private void OnFacebookClicked()
    {
        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] Facebook click ignored. Not implemented yet.", this);

        ShowFeedback(facebookNotImplementedMessage);
    }

    private void OnUnlinkClicked()
    {
        if (accountPresenter == null || accountPresenter.IsBusy)
            return;

        AccountSnapshot snapshot = ReadAccountSnapshot();
        if (!snapshot.IsLinked)
        {
            if (logDebug)
                Debug.Log("[PlayerProfileAccountPanelUI] Unlink click ignored because no provider is linked.", this);
            return;
        }

        BeginPendingOperation(PendingOperation.Unlink, snapshot);
        accountPresenter.UnlinkCurrentProvider();
    }

    private void BeginPendingOperation(PendingOperation operation, AccountSnapshot beforeSnapshot)
    {
        CancelPendingOperation();

        pendingOperation = operation;
        preOperationSnapshot = beforeSnapshot;

        if (pendingOperationRoutine != null)
            StopCoroutine(pendingOperationRoutine);

        pendingOperationRoutine = StartCoroutine(PendingOperationTimeoutRoutine(operation));

        if (logDebug)
        {
            Debug.Log(
                $"[PlayerProfileAccountPanelUI] BeginPendingOperation -> {operation} | " +
                $"Before: Provider={beforeSnapshot.ProviderType}, Guest={beforeSnapshot.IsGuest}, Linked={beforeSnapshot.IsLinked}",
                this);
        }
    }

    private IEnumerator PendingOperationTimeoutRoutine(PendingOperation operation)
    {
        float elapsed = 0f;

        while (elapsed < operationTimeoutSeconds)
        {
            if (pendingOperation != operation)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (pendingOperation == operation)
        {
            if (logDebug)
                Debug.LogWarning($"[PlayerProfileAccountPanelUI] Pending operation timeout -> {operation}", this);

            switch (operation)
            {
                case PendingOperation.LinkGoogle:
                case PendingOperation.LinkApple:
                    ShowFeedback(linkFailedMessage);
                    break;

                case PendingOperation.Unlink:
                    ShowFeedback(unlinkFailedMessage);
                    break;
            }

            CancelPendingOperation();
        }
    }

    private void EvaluatePendingOperation()
    {
        if (pendingOperation == PendingOperation.None)
            return;

        AccountSnapshot current = ReadAccountSnapshot();

        bool success = false;
        string successMessage = string.Empty;

        switch (pendingOperation)
        {
            case PendingOperation.LinkGoogle:
                success =
                    current.IsAuthenticated &&
                    current.IsLinked &&
                    current.ProviderType == AuthProviderType.GooglePlayGames &&
                    (!preOperationSnapshot.IsLinked || preOperationSnapshot.ProviderType != AuthProviderType.GooglePlayGames);

                successMessage = linkSuccessMessage;
                break;

            case PendingOperation.LinkApple:
                success =
                    current.IsAuthenticated &&
                    current.IsLinked &&
                    current.ProviderType == AuthProviderType.Apple &&
                    (!preOperationSnapshot.IsLinked || preOperationSnapshot.ProviderType != AuthProviderType.Apple);

                successMessage = linkSuccessMessage;
                break;

            case PendingOperation.Unlink:
                success =
                    current.IsAuthenticated &&
                    current.IsGuest &&
                    !current.IsLinked &&
                    preOperationSnapshot.IsLinked;

                successMessage = unlinkSuccessMessage;
                break;
        }

        if (!success)
            return;

        if (logDebug)
        {
            Debug.Log(
                $"[PlayerProfileAccountPanelUI] Pending operation confirmed -> {pendingOperation} | " +
                $"After: Provider={current.ProviderType}, Guest={current.IsGuest}, Linked={current.IsLinked}",
                this);
        }

        ShowFeedback(successMessage);
        CancelPendingOperation();
    }

    private void CancelPendingOperation()
    {
        pendingOperation = PendingOperation.None;
        preOperationSnapshot = default;

        if (pendingOperationRoutine != null)
        {
            StopCoroutine(pendingOperationRoutine);
            pendingOperationRoutine = null;
        }
    }

    private AccountOverview GetCurrentOverview()
    {
        ResolveDependencies();

        if (accountPresenter == null)
            return null;

        if (accountPresenter.CurrentOverview == null)
            accountPresenter.Refresh();

        return accountPresenter.CurrentOverview;
    }

    private AccountSnapshot ReadAccountSnapshot()
    {
        AccountOverview overview = GetCurrentOverview();
        if (overview == null)
        {
            return new AccountSnapshot
            {
                IsAuthenticated = true,
                IsGuest = true,
                IsLinked = false,
                ProviderType = AuthProviderType.Guest
            };
        }

        return new AccountSnapshot
        {
            IsAuthenticated = overview.IsAuthenticated,
            IsGuest = overview.IsGuest,
            IsLinked = overview.IsLinked,
            ProviderType = overview.CurrentProviderType
        };
    }

    private string GetStatusMessage(AccountSnapshot snapshot)
    {
        if (!snapshot.IsAuthenticated)
            return guestStatusMessage;

        if (snapshot.IsGuest || !snapshot.IsLinked)
            return guestStatusMessage;

        switch (snapshot.ProviderType)
        {
            case AuthProviderType.GooglePlayGames:
                return googleStatusMessage;

            case AuthProviderType.Apple:
                return appleStatusMessage;

            default:
                return genericLinkedStatusMessage;
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackPanel == null)
            return;

        feedbackPanel.Show(message);
    }

    private struct AccountSnapshot
    {
        public bool IsAuthenticated;
        public bool IsGuest;
        public bool IsLinked;
        public AuthProviderType ProviderType;
    }
}