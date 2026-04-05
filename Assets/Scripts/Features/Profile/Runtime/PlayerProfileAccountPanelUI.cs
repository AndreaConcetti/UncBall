using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileAccountPanelUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Identity UI")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private string displayNamePrefix = "";
    [SerializeField] private string displayNameSuffix = "";

    [Header("Guest Status UI")]
    [SerializeField] private TMP_Text guestStatusText;
    [SerializeField] private string guestStatusMessage = "LOGGED AS GUEST";

    [Header("Linked Status UI")]
    [SerializeField] private TMP_Text linkedStatusText;
    [SerializeField] private string linkedStatusMessage = "ACCOUNT LINKED";
    [SerializeField] private bool showLinkedStatusText = false;

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

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool subscribed;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        RefreshPlayerName();
        RefreshGuestAndLinkedStatus();
        RefreshRank();
        RefreshAccountActionVisibility();

        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] RefreshUI completed.", this);
    }

    private void RefreshPlayerName()
    {
        string displayName = "GUEST";

        if (profileManager != null && profileManager.ActiveProfile != null)
        {
            string profileName = profileManager.ActiveProfile.displayName;
            if (!string.IsNullOrWhiteSpace(profileName))
                displayName = profileName;
        }

        if (displayNameText != null)
            displayNameText.text = displayNamePrefix + displayName + displayNameSuffix;

        if (logDebug)
            Debug.Log("[PlayerProfileAccountPanelUI] Player name -> " + displayName, this);
    }

    private void RefreshGuestAndLinkedStatus()
    {
        AccountSnapshot snapshot = ReadAccountSnapshot();

        bool showGuest = snapshot.IsAuthenticated && snapshot.IsGuest && !snapshot.IsLinked;
        bool showLinked = snapshot.IsAuthenticated && snapshot.IsLinked && showLinkedStatusText;

        if (guestStatusText != null)
        {
            guestStatusText.text = guestStatusMessage;
            guestStatusText.gameObject.SetActive(showGuest);
        }

        if (linkedStatusText != null)
        {
            linkedStatusText.text = linkedStatusMessage;
            linkedStatusText.gameObject.SetActive(showLinked);
        }

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileAccountPanelUI] Status -> " +
                "IsAuthenticated=" + snapshot.IsAuthenticated +
                " | IsGuest=" + snapshot.IsGuest +
                " | IsLinked=" + snapshot.IsLinked +
                " | ShowGuest=" + showGuest +
                " | ShowLinked=" + showLinked,
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
    }

    private void Subscribe()
    {
        if (subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
        subscribed = false;
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUI();
    }

    private AccountSnapshot ReadAccountSnapshot()
    {
        object overview = TryGetAccountOverviewByReflection();
        if (overview == null)
        {
            return new AccountSnapshot
            {
                IsAuthenticated = true,
                IsGuest = true,
                IsLinked = false
            };
        }

        return new AccountSnapshot
        {
            IsAuthenticated = ReadMemberAsBool(overview, "IsAuthenticated", true),
            IsGuest = ReadMemberAsBool(overview, "IsGuest", true),
            IsLinked = ReadMemberAsBool(overview, "IsLinked", false)
        };
    }

    private object TryGetAccountOverviewByReflection()
    {
        try
        {
            Type locatorType =
                Type.GetType("UncballArena.Core.Auth.AccountServiceLocator") ??
                Type.GetType("AccountServiceLocator");

            if (locatorType == null)
                return null;

            object authService = null;

            PropertyInfo serviceProperty =
                locatorType.GetProperty("Service", BindingFlags.Public | BindingFlags.Static) ??
                locatorType.GetProperty("AuthService", BindingFlags.Public | BindingFlags.Static);

            if (serviceProperty != null)
                authService = serviceProperty.GetValue(null);

            if (authService == null)
                return null;

            MethodInfo overviewMethod = authService.GetType().GetMethod("GetAccountOverview", Type.EmptyTypes);
            if (overviewMethod == null)
                return null;

            return overviewMethod.Invoke(authService, null);
        }
        catch (Exception ex)
        {
            if (logDebug)
                Debug.LogWarning("[PlayerProfileAccountPanelUI] Reflection error while reading account overview: " + ex.Message, this);

            return null;
        }
    }

    private bool ReadMemberAsBool(object target, string memberName, bool fallback)
    {
        if (target == null || string.IsNullOrEmpty(memberName))
            return fallback;

        Type type = target.GetType();

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.PropertyType == typeof(bool))
            return (bool)property.GetValue(target);

        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(bool))
            return (bool)field.GetValue(target);

        return fallback;
    }

    private struct AccountSnapshot
    {
        public bool IsAuthenticated;
        public bool IsGuest;
        public bool IsLinked;
    }
}