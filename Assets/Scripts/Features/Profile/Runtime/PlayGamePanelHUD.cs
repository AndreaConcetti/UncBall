using System;
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

    private float pollingTimer;
    private bool subscribed;

    private string lastAppliedName;
    private int lastAppliedRankedLp = int.MinValue;
    private int lastAppliedSoft = int.MinValue;
    private int lastAppliedPremium = int.MinValue;

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

        pollingTimer -= Time.unscaledDeltaTime;
        if (pollingTimer > 0f)
            return;

        pollingTimer = pollingInterval;

        TryResolveProfileManager();

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
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
    }

    private void SubscribeProfileEvents()
    {
        if (subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;

        subscribed = true;
    }

    private void UnsubscribeProfileEvents()
    {
        if (!subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        subscribed = false;
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

    private void TryResolveSceneTexts(bool force)
    {
        if (!force && HasAllTextReferences())
            return;

        Transform playGamePanel = FindPlayGamePanelRoot();
        if (playGamePanel == null)
        {
            if (logDebug && force)
            {
                Debug.LogWarning("[PlayGamePanelHUD] PlayGamePanel root non trovato.", this);
            }

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

        if (logDebug)
        {
            Debug.Log(
                "[PlayGamePanelHUD] TryResolveSceneTexts -> " +
                "Name=" + (currentNameText != null) +
                " | Rank=" + (rankText != null) +
                " | Soft=" + (softCurrencyText != null) +
                " | Premium=" + (premiumCurrencyText != null),
                this);
        }
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

    public void RefreshUi(bool forceLog = false)
    {
        TryResolveProfileManager();

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
            if (logDebug && forceLog)
            {
                Debug.LogWarning("[PlayGamePanelHUD] RefreshUi skipped -> text refs mancanti.", this);
            }

            return;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        string resolvedName = emptyNameFallback;
        int resolvedRankedLp = 0;
        int resolvedSoft = 0;
        int resolvedPremium = 0;

        if (profile != null)
        {
            resolvedName = string.IsNullOrWhiteSpace(profile.displayName)
                ? emptyNameFallback
                : profile.displayName.Trim();

            resolvedRankedLp = Mathf.Max(0, profile.rankedLp);
            resolvedSoft = Mathf.Max(0, profile.softCurrency);
            resolvedPremium = Mathf.Max(0, profile.premiumCurrency);
        }

        currentNameText.text = resolvedName;
        rankText.text = $"{rankPrefix}{resolvedRankedLp}{rankSuffix}";
        softCurrencyText.text = $"{softPrefix}{resolvedSoft}{softSuffix}";
        premiumCurrencyText.text = $"{premiumPrefix}{resolvedPremium}{premiumSuffix}";

        bool changed =
            lastAppliedName != resolvedName ||
            lastAppliedRankedLp != resolvedRankedLp ||
            lastAppliedSoft != resolvedSoft ||
            lastAppliedPremium != resolvedPremium;

        lastAppliedName = resolvedName;
        lastAppliedRankedLp = resolvedRankedLp;
        lastAppliedSoft = resolvedSoft;
        lastAppliedPremium = resolvedPremium;

        if (logDebug && (forceLog || changed))
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