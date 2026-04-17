using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayGamePanelHUD : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Main HUD Texts")]
    [SerializeField] private TMP_Text currentNameText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text softCurrencyText;
    [SerializeField] private TMP_Text premiumCurrencyText;

    [Header("Formatting")]
    [SerializeField] private string emptyNameFallback = "GUEST";
    [SerializeField] private string rankPrefix = "RANK ";
    [SerializeField] private string rankSuffix = " LP";
    [SerializeField] private string softPrefix = "";
    [SerializeField] private string softSuffix = "";
    [SerializeField] private string premiumPrefix = "";
    [SerializeField] private string premiumSuffix = "";

    [Header("Behavior")]
    [SerializeField] private bool logDebug = true;
    [SerializeField] private bool enableLightPolling = true;
    [SerializeField][Min(0.05f)] private float pollingInterval = 0.25f;

    [Header("Scene Filtering")]
    [SerializeField] private bool restrictToExpectedScenes = true;
    [SerializeField] private List<string> expectedSceneNames = new List<string> { "MainMenu" };
    [SerializeField] private bool warnWhenMissingInExpectedScene = true;

    private float pollingTimer;
    private bool subscribed;
    private PlayerProfileManager subscribedProfileManager;

    private string lastAppliedName = string.Empty;
    private int lastAppliedRankedLp = int.MinValue;
    private int lastAppliedSoft = int.MinValue;
    private int lastAppliedPremium = int.MinValue;

    private bool lastResolvedTextsState;
    private string lastActiveSceneName = string.Empty;
    private bool hasRenderedAtLeastOnce;

    private void Awake()
    {
        TryResolveProfileManager();
        ClearDestroyedReferences();
        TryResolveSceneTexts(force: true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        TryResolveProfileManager();
        SubscribeProfileEvents();

        ClearDestroyedReferences();
        TryResolveSceneTexts(force: true);
        RefreshUi(forceLog: true);
    }

    private void Start()
    {
        TryResolveProfileManager();
        SubscribeProfileEvents();

        ClearDestroyedReferences();
        TryResolveSceneTexts(force: true);
        RefreshUi(forceLog: true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeProfileEvents();
    }

    private void Update()
    {
        if (!enableLightPolling)
            return;

        if (!IsSceneExpectedForHud())
            return;

        pollingTimer -= Time.unscaledDeltaTime;
        if (pollingTimer > 0f)
            return;

        pollingTimer = pollingInterval;

        TryResolveProfileManager();
        SubscribeProfileEvents();

        bool hadMissingRefs = !HasAllTextReferences();
        if (hadMissingRefs)
        {
            ClearDestroyedReferences();
            TryResolveSceneTexts(force: false);
        }

        RefreshUi(forceLog: false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pollingTimer = 0f;

        TryResolveProfileManager();
        SubscribeProfileEvents();

        ClearDestroyedReferences();
        TryResolveSceneTexts(force: true);
        RefreshUi(forceLog: true);

        if (logDebug)
        {
            Debug.Log(
                "[PlayGamePanelHUD] SceneLoaded -> " +
                "Scene=" + scene.name +
                " | ResolvedTexts=" + HasAllTextReferences(),
                this);
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUi(forceLog: true);
    }

    private void TryResolveProfileManager()
    {
        if (profileManager != null)
            return;

        profileManager = GetComponent<PlayerProfileManager>();

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif
    }

    private void SubscribeProfileEvents()
    {
        if (profileManager == null)
            return;

        if (subscribed && subscribedProfileManager == profileManager)
            return;

        UnsubscribeProfileEvents();

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;

        subscribed = true;
        subscribedProfileManager = profileManager;
    }

    private void UnsubscribeProfileEvents()
    {
        if (!subscribed || subscribedProfileManager == null)
        {
            subscribed = false;
            subscribedProfileManager = null;
            return;
        }

        subscribedProfileManager.OnActiveProfileChanged -= HandleProfileChanged;
        subscribedProfileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        subscribed = false;
        subscribedProfileManager = null;
    }

    private void ClearDestroyedReferences()
    {
        if (currentNameText == null)
            currentNameText = null;

        if (rankText == null)
            rankText = null;

        if (softCurrencyText == null)
            softCurrencyText = null;

        if (premiumCurrencyText == null)
            premiumCurrencyText = null;
    }

    private bool HasAllTextReferences()
    {
        return currentNameText != null &&
               rankText != null &&
               softCurrencyText != null &&
               premiumCurrencyText != null;
    }

    private bool IsSceneExpectedForHud()
    {
        if (!restrictToExpectedScenes)
            return true;

        string activeSceneName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < expectedSceneNames.Count; i++)
        {
            if (string.Equals(expectedSceneNames[i], activeSceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void TryResolveSceneTexts(bool force)
    {
        if (!IsSceneExpectedForHud())
            return;

        if (!force && HasAllTextReferences())
            return;

        Transform playGamePanel = FindPlayGamePanelRoot();
        if (playGamePanel == null)
        {
            if (logDebug && force && warnWhenMissingInExpectedScene)
                Debug.LogWarning("[PlayGamePanelHUD] PlayGamePanel root non trovato.", this);

            return;
        }

        if (currentNameText == null)
            currentNameText = FindTextByName(playGamePanel, "PlayerCurrentNameText");

        if (rankText == null)
            rankText = FindTextByName(playGamePanel, "PlayerEloText");

        if (softCurrencyText == null)
            softCurrencyText = FindTextByName(playGamePanel, "SoftCurrencyText");

        if (premiumCurrencyText == null)
            premiumCurrencyText = FindTextByName(playGamePanel, "PremiumCurrencyText");

        bool resolvedNow = HasAllTextReferences();
        if (logDebug && (force || resolvedNow != lastResolvedTextsState))
        {
            Debug.Log(
                "[PlayGamePanelHUD] TryResolveSceneTexts -> " +
                "Name=" + (currentNameText != null) +
                " | Rank=" + (rankText != null) +
                " | Soft=" + (softCurrencyText != null) +
                " | Premium=" + (premiumCurrencyText != null),
                this);
        }

        lastResolvedTextsState = resolvedNow;
    }

    private Transform FindPlayGamePanelRoot()
    {
        Transform result;

        result = FindTransformByScenePath("MainMenu/Canvas_Buttons/SafeAreaRoot/PlayGamePanel");
        if (result != null)
            return result;

        result = FindTransformByScenePath("Canvas_Buttons/SafeAreaRoot/PlayGamePanel");
        if (result != null)
            return result;

        GameObject direct = GameObject.Find("PlayGamePanel");
        if (direct != null)
            return direct.transform;

        Transform safeAreaRoot = FindTransformAnywhere("SafeAreaRoot");
        if (safeAreaRoot != null)
        {
            Transform nested = safeAreaRoot.Find("PlayGamePanel");
            if (nested != null)
                return nested;
        }

        Transform canvasButtons = FindTransformAnywhere("Canvas_Buttons");
        if (canvasButtons != null)
        {
            Transform nested = canvasButtons.Find("SafeAreaRoot/PlayGamePanel");
            if (nested != null)
                return nested;
        }

        Transform mainMenu = FindTransformAnywhere("MainMenu");
        if (mainMenu != null)
        {
            Transform nested = mainMenu.Find("Canvas_Buttons/SafeAreaRoot/PlayGamePanel");
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Transform FindTransformByScenePath(string path)
    {
        GameObject root = GameObject.Find(path);
        return root != null ? root.transform : null;
    }

    private Transform FindTransformAnywhere(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    private TMP_Text FindTextByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (!string.Equals(all[i].name, childName, StringComparison.Ordinal))
                continue;

            TMP_Text tmp = all[i].GetComponent<TMP_Text>();
            if (tmp != null)
                return tmp;
        }

        return null;
    }

    private bool HasUsableProfileData()
    {
        TryResolveProfileManager();

        if (profileManager == null || profileManager.ActiveProfile == null)
            return false;

        string profileId = profileManager.ActiveProfile.profileId;
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        return true;
    }

    public void RefreshUi(bool forceLog = false)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool sceneChanged = !string.Equals(lastActiveSceneName, activeSceneName, StringComparison.Ordinal);
        lastActiveSceneName = activeSceneName;

        if (!IsSceneExpectedForHud())
            return;

        TryResolveProfileManager();
        SubscribeProfileEvents();

        if (profileManager == null)
        {
            if (logDebug && forceLog)
                Debug.LogWarning("[PlayGamePanelHUD] RefreshUi skipped -> profileManager null.", this);
            return;
        }

        if (!HasAllTextReferences())
        {
            ClearDestroyedReferences();
            TryResolveSceneTexts(force: false);
        }

        if (!HasAllTextReferences())
        {
            if (logDebug && forceLog && warnWhenMissingInExpectedScene)
                Debug.LogWarning("[PlayGamePanelHUD] RefreshUi skipped -> text refs mancanti.", this);

            return;
        }

        if (!HasUsableProfileData())
        {
            if (logDebug && forceLog)
                Debug.Log("[PlayGamePanelHUD] RefreshUi deferred while resolved profile is not ready.", this);

            return;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        string resolvedName = string.IsNullOrWhiteSpace(profile.displayName)
            ? emptyNameFallback
            : profile.displayName.Trim();

        int resolvedRankedLp = Mathf.Max(0, profile.rankedLp);
        int resolvedSoft = Mathf.Max(0, profile.softCurrency);
        int resolvedPremium = Mathf.Max(0, profile.premiumCurrency);

        bool changed =
            !string.Equals(lastAppliedName, resolvedName, StringComparison.Ordinal) ||
            lastAppliedRankedLp != resolvedRankedLp ||
            lastAppliedSoft != resolvedSoft ||
            lastAppliedPremium != resolvedPremium;

        if (!changed && !forceLog && !sceneChanged && hasRenderedAtLeastOnce)
            return;

        string rankValue = $"{rankPrefix}{resolvedRankedLp}{rankSuffix}";
        string softValue = $"{softPrefix}{resolvedSoft}{softSuffix}";
        string premiumValue = $"{premiumPrefix}{resolvedPremium}{premiumSuffix}";

        if (currentNameText != null && !string.Equals(currentNameText.text, resolvedName, StringComparison.Ordinal))
            currentNameText.text = resolvedName;

        if (rankText != null && !string.Equals(rankText.text, rankValue, StringComparison.Ordinal))
            rankText.text = rankValue;

        if (softCurrencyText != null && !string.Equals(softCurrencyText.text, softValue, StringComparison.Ordinal))
            softCurrencyText.text = softValue;

        if (premiumCurrencyText != null && !string.Equals(premiumCurrencyText.text, premiumValue, StringComparison.Ordinal))
            premiumCurrencyText.text = premiumValue;

        lastAppliedName = resolvedName;
        lastAppliedRankedLp = resolvedRankedLp;
        lastAppliedSoft = resolvedSoft;
        lastAppliedPremium = resolvedPremium;
        hasRenderedAtLeastOnce = true;

        if (logDebug && (forceLog || changed || sceneChanged))
        {
            Debug.Log(
                "[PlayGamePanelHUD] RefreshUi -> " +
                "Name=" + resolvedName +
                " | RankedLp=" + resolvedRankedLp +
                " | Soft=" + resolvedSoft +
                " | Premium=" + resolvedPremium,
                this);
        }
    }
}