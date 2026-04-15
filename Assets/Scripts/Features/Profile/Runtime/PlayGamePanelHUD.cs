using System;
using System.Reflection;
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
    [SerializeField][Min(0.1f)] private float pollingInterval = 0.25f;

    private float pollingTimer;
    private bool subscribed;

    private string lastAppliedName;
    private int lastAppliedRankedLp = int.MinValue;
    private int lastAppliedSoft = int.MinValue;
    private int lastAppliedPremium = int.MinValue;

    private void Awake()
    {
        TryResolveProfileManager();
        TryResolveSceneTexts(force: true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryResolveProfileManager();
        SubscribeProfileEvents();
        TryResolveSceneTexts(force: true);
        RefreshUi(forceLog: true);
    }

    private void Start()
    {
        TryResolveProfileManager();
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
            TryResolveSceneTexts(force: false);

        RefreshUi(forceLog: false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolveProfileManager();
        ClearDestroyedReferences();
        TryResolveSceneTexts(force: true);
        RefreshUi(forceLog: true);

        if (logDebug)
        {
            Debug.Log(
                $"[PlayGamePanelHUD] SceneLoaded -> Scene={scene.name} | ResolvedTexts={HasAllTextReferences()}",
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
        if (profileManager != null)
            return;

        profileManager = FindFirstObjectByType<PlayerProfileManager>();
    }

    private void SubscribeProfileEvents()
    {
        if (subscribed || profileManager == null)
            return;

        try
        {
            profileManager.OnActiveProfileChanged += HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
            subscribed = true;
        }
        catch
        {
            if (logDebug)
            {
                Debug.LogWarning("[PlayGamePanelHUD] SubscribeProfileEvents fallita.", this);
            }
        }
    }

    private void UnsubscribeProfileEvents()
    {
        if (!subscribed || profileManager == null)
            return;

        try
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
        }
        catch
        {
        }

        subscribed = false;
    }

    private void TryResolveSceneTexts(bool force)
    {
        ClearDestroyedReferences();

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
                $"Name={(currentNameText != null)} | " +
                $"Rank={(rankText != null)} | " +
                $"Soft={(softCurrencyText != null)} | " +
                $"Premium={(premiumCurrencyText != null)}",
                this);
        }
    }

    private void ClearDestroyedReferences()
    {
        if (currentNameText == null) currentNameText = null;
        if (rankText == null) rankText = null;
        if (softCurrencyText == null) softCurrencyText = null;
        if (premiumCurrencyText == null) premiumCurrencyText = null;
    }

    private bool HasAllTextReferences()
    {
        return currentNameText != null &&
               rankText != null &&
               softCurrencyText != null &&
               premiumCurrencyText != null;
    }

    private Transform FindPlayGamePanelRoot()
    {
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

    private Transform FindTransformAnywhere(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }

    private TMP_Text FindTextByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (!string.Equals(children[i].name, childName, StringComparison.Ordinal))
                continue;

            TMP_Text text = children[i].GetComponent<TMP_Text>();
            if (text != null)
                return text;
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
            TryResolveSceneTexts(force: false);

        if (!HasAllTextReferences())
        {
            if (logDebug && forceLog)
                Debug.LogWarning("[PlayGamePanelHUD] RefreshUi skipped -> text refs mancanti.", this);
            return;
        }

        ProfileHudSnapshot snapshot = BuildSnapshot(profileManager);

        currentNameText.text = snapshot.DisplayName;
        rankText.text = $"{rankPrefix}{snapshot.RankedLp}{rankSuffix}";
        softCurrencyText.text = $"{softPrefix}{snapshot.SoftCurrency}{softSuffix}";
        premiumCurrencyText.text = $"{premiumPrefix}{snapshot.PremiumCurrency}{premiumSuffix}";

        bool changed =
            lastAppliedName != snapshot.DisplayName ||
            lastAppliedRankedLp != snapshot.RankedLp ||
            lastAppliedSoft != snapshot.SoftCurrency ||
            lastAppliedPremium != snapshot.PremiumCurrency;

        lastAppliedName = snapshot.DisplayName;
        lastAppliedRankedLp = snapshot.RankedLp;
        lastAppliedSoft = snapshot.SoftCurrency;
        lastAppliedPremium = snapshot.PremiumCurrency;

        if (logDebug && (forceLog || changed))
        {
            Debug.Log(
                $"[PlayGamePanelHUD] RefreshUi -> Name={snapshot.DisplayName} | RankedLp={snapshot.RankedLp} | Soft={snapshot.SoftCurrency} | Premium={snapshot.PremiumCurrency}",
                this);
        }
    }

    private ProfileHudSnapshot BuildSnapshot(PlayerProfileManager manager)
    {
        object source = FindBestProfileSource(manager);

        string displayName =
            ReadString(source, "DisplayName", "PlayerName", "CurrentDisplayName", "Name") ??
            ReadString(manager, "DisplayName", "PlayerName", "CurrentDisplayName", "Name") ??
            emptyNameFallback;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = emptyNameFallback;

        int rankedLp =
            ReadInt(source, "RankedLp", "RankLP", "Elo", "RankPoints", "CurrentRankedLp") ??
            ReadInt(manager, "RankedLp", "RankLP", "Elo", "RankPoints", "CurrentRankedLp") ??
            0;

        int softCurrency =
            ReadInt(source, "SoftCurrency", "Soft", "Coins", "SoftCoins", "Gold") ??
            ReadInt(manager, "SoftCurrency", "Soft", "Coins", "SoftCoins", "Gold") ??
            0;

        int premiumCurrency =
            ReadInt(source, "PremiumCurrency", "Premium", "Gems", "Diamonds", "HardCurrency") ??
            ReadInt(manager, "PremiumCurrency", "Premium", "Gems", "Diamonds", "HardCurrency") ??
            0;

        return new ProfileHudSnapshot(
            displayName,
            Mathf.Max(0, rankedLp),
            Mathf.Max(0, softCurrency),
            Mathf.Max(0, premiumCurrency));
    }

    private object FindBestProfileSource(PlayerProfileManager manager)
    {
        object source =
            ReadObject(manager, "ActiveProfileData", "CurrentProfileData", "ProfileData", "RuntimeData", "ActiveProfile", "CurrentProfile");

        if (source != null)
            return source;

        source =
            InvokeObject(manager, "GetActiveProfileData", "GetCurrentProfileData", "GetProfileData", "GetActiveProfile", "GetCurrentProfile");

        return source ?? manager;
    }

    private static object ReadObject(object target, params string[] memberNames)
    {
        if (target == null)
            return null;

        Type type = target.GetType();

        for (int i = 0; i < memberNames.Length; i++)
        {
            string memberName = memberNames[i];

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                try
                {
                    return property.GetValue(target);
                }
                catch
                {
                }
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    return field.GetValue(target);
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static object InvokeObject(object target, params string[] methodNames)
    {
        if (target == null)
            return null;

        Type type = target.GetType();

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = type.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
                continue;

            try
            {
                return method.Invoke(target, null);
            }
            catch
            {
            }
        }

        return null;
    }

    private static string ReadString(object target, params string[] memberNames)
    {
        object value = ReadObject(target, memberNames);
        if (value == null)
            return null;

        return value as string ?? value.ToString();
    }

    private static int? ReadInt(object target, params string[] memberNames)
    {
        object value = ReadObject(target, memberNames);
        return ConvertToInt(value);
    }

    private static int? ConvertToInt(object value)
    {
        if (value == null)
            return null;

        switch (value)
        {
            case int intValue:
                return intValue;
            case long longValue:
                return (int)longValue;
            case short shortValue:
                return shortValue;
            case byte byteValue:
                return byteValue;
            case float floatValue:
                return Mathf.RoundToInt(floatValue);
            case double doubleValue:
                return (int)Math.Round(doubleValue);
            case decimal decimalValue:
                return (int)Math.Round(decimalValue);
            case string stringValue when int.TryParse(stringValue, out int parsed):
                return parsed;
            default:
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return null;
                }
        }
    }

    private readonly struct ProfileHudSnapshot
    {
        public readonly string DisplayName;
        public readonly int RankedLp;
        public readonly int SoftCurrency;
        public readonly int PremiumCurrency;

        public ProfileHudSnapshot(string displayName, int rankedLp, int softCurrency, int premiumCurrency)
        {
            DisplayName = displayName;
            RankedLp = rankedLp;
            SoftCurrency = softCurrency;
            PremiumCurrency = premiumCurrency;
        }
    }
}