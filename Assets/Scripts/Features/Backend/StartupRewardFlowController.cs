using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class StartupRewardFlowController : MonoBehaviour
{
    [Header("Daily Login")]
    [SerializeField] private DailyLoginRewardService dailyLoginRewardService;
    [SerializeField] private DailyLoginPanelUI dailyLoginPanel;
    [SerializeField] private GameObject dailyLoginPanelRoot;

    [Header("Daily Free Lucky Shot")]
    [SerializeField] private DailyFreeLuckyShotRewardService dailyFreeLuckyShotRewardService;
    [SerializeField] private LuckyShotBackendService luckyShotBackendService;
    [SerializeField] private LuckyShotEntryService luckyShotEntryService;
    [SerializeField] private LuckyShotEntryPanelUI luckyShotEntryPanel;
    [SerializeField] private GameObject luckyShotEntryPanelRoot;

    [Header("Startup")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private float startupDelaySeconds = 0.75f;
    [SerializeField] private float serviceResolveTimeoutSeconds = 10f;
    [SerializeField] private float panelClosePollingIntervalSeconds = 0.1f;

    [Header("Panel Rules")]
    [SerializeField] private bool openDailyPanelOnlyIfClaimable = true;
    [SerializeField] private bool openLuckyShotPanelOnlyIfTokenAvailable = true;
    [SerializeField] private bool grantDailyFreeLuckyShotBeforeOpeningPanel = true;
    [SerializeField] private bool closeDailyPanelBeforeFlow = true;
    [SerializeField] private bool closeLuckyShotPanelBeforeFlow = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private CancellationTokenSource cts;
    private bool flowRunning;
    private bool flowCompleted;

    public bool FlowRunning => flowRunning;
    public bool FlowCompleted => flowCompleted;

    private void Awake()
    {
        ResolveReferences();

        if (closeDailyPanelBeforeFlow)
            SetRootActive(dailyLoginPanelRoot, dailyLoginPanel, false);

        if (closeLuckyShotPanelBeforeFlow)
            SetRootActive(luckyShotEntryPanelRoot, luckyShotEntryPanel, false);
    }

    private void Start()
    {
        if (runOnStart)
            StartFlow();
    }

    private void OnDestroy()
    {
        CancelFlow();
    }

    public void StartFlow()
    {
        if (flowRunning)
            return;

        CancelFlow();

        cts = new CancellationTokenSource();
        _ = RunFlowAsync(cts.Token);
    }

    public void CancelFlow()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        flowRunning = false;
    }

    private async Task RunFlowAsync(CancellationToken cancellationToken)
    {
        flowRunning = true;
        flowCompleted = false;

        try
        {
            if (startupDelaySeconds > 0f)
                await DelaySecondsAsync(startupDelaySeconds, cancellationToken);

            bool ready = await WaitForCoreReferencesAsync(cancellationToken);

            if (!ready)
            {
                Debug.LogWarning("[StartupRewardFlowController] RunFlowAsync stopped -> required references not ready.", this);
                return;
            }

            await RunDailyLoginStepAsync(cancellationToken);
            await RunDailyFreeLuckyShotStepAsync(cancellationToken);

            flowCompleted = true;

            if (verboseLogs)
                Debug.Log("[StartupRewardFlowController] RunFlowAsync -> completed.", this);
        }
        catch (OperationCanceledException)
        {
            if (verboseLogs)
                Debug.Log("[StartupRewardFlowController] RunFlowAsync -> cancelled.", this);
        }
        catch (Exception ex)
        {
            Debug.LogError("[StartupRewardFlowController] RunFlowAsync exception -> " + ex, this);
        }
        finally
        {
            flowRunning = false;
        }
    }

    private async Task RunDailyLoginStepAsync(CancellationToken cancellationToken)
    {
        ResolveReferences();

        if (dailyLoginRewardService == null || dailyLoginPanel == null)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[StartupRewardFlowController] RunDailyLoginStepAsync skipped -> " +
                    "DailyService=" + (dailyLoginRewardService != null) +
                    " | DailyPanel=" + (dailyLoginPanel != null),
                    this);
            }

            return;
        }

        DailyLoginPreviewState preview;

        try
        {
            preview = await dailyLoginRewardService.RefreshPreviewAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StartupRewardFlowController] RunDailyLoginStepAsync -> RefreshPreviewAsync failed. Panel skipped. Error=" + ex.Message, this);
            return;
        }

        bool dailyRewardAvailable = preview.canClaimNow;

        if (openDailyPanelOnlyIfClaimable && !dailyRewardAvailable)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[StartupRewardFlowController] RunDailyLoginStepAsync -> daily reward not available. Panel skipped. " +
                    "CanClaim=" + preview.canClaimNow +
                    " | CurrentStreakDay=" + preview.currentStreakDay +
                    " | NextClaimDayIndex=" + preview.nextClaimDayIndex,
                    this);
            }

            return;
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[StartupRewardFlowController] RunDailyLoginStepAsync -> opening Daily Login panel. " +
                "CanClaim=" + preview.canClaimNow +
                " | CurrentStreakDay=" + preview.currentStreakDay +
                " | NextClaimDayIndex=" + preview.nextClaimDayIndex,
                this);
        }

        dailyLoginPanel.OpenPanel();

        await WaitUntilDailyPanelClosedAsync(cancellationToken);

        if (verboseLogs)
            Debug.Log("[StartupRewardFlowController] RunDailyLoginStepAsync -> Daily Login panel closed.", this);
    }

    private async Task RunDailyFreeLuckyShotStepAsync(CancellationToken cancellationToken)
    {
        ResolveReferences();

        if (luckyShotEntryPanel == null)
        {
            if (verboseLogs)
                Debug.Log("[StartupRewardFlowController] RunDailyFreeLuckyShotStepAsync skipped -> LuckyShotEntryPanel missing.", this);

            return;
        }

        int tokensBeforeGrant = await RefreshLuckyShotTokensAsync(cancellationToken);

        bool dailyFreeClaimAvailable =
            dailyFreeLuckyShotRewardService != null &&
            dailyFreeLuckyShotRewardService.CanClaimToday();

        bool dailyFreeGrantedNow = false;

        if (grantDailyFreeLuckyShotBeforeOpeningPanel && dailyFreeClaimAvailable)
        {
            dailyFreeGrantedNow = await dailyFreeLuckyShotRewardService.TryGrantDailyFreeLuckyShotAsync(cancellationToken);
        }

        int tokensAfterGrant = await RefreshLuckyShotTokensAsync(cancellationToken);

        bool hasTokenAvailable = tokensAfterGrant > 0;

        if (openLuckyShotPanelOnlyIfTokenAvailable && !hasTokenAvailable)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[StartupRewardFlowController] RunDailyFreeLuckyShotStepAsync -> Lucky Shot panel skipped. " +
                    "TokensBefore=" + tokensBeforeGrant +
                    " | DailyFreeClaimAvailable=" + dailyFreeClaimAvailable +
                    " | DailyFreeGrantedNow=" + dailyFreeGrantedNow +
                    " | TokensAfter=" + tokensAfterGrant,
                    this);
            }

            return;
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[StartupRewardFlowController] RunDailyFreeLuckyShotStepAsync -> opening Lucky Shot Entry panel. " +
                "TokensBefore=" + tokensBeforeGrant +
                " | DailyFreeClaimAvailable=" + dailyFreeClaimAvailable +
                " | DailyFreeGrantedNow=" + dailyFreeGrantedNow +
                " | TokensAfter=" + tokensAfterGrant,
                this);
        }

        luckyShotEntryPanel.OpenPanel();
    }

    private async Task<int> RefreshLuckyShotTokensAsync(CancellationToken cancellationToken)
    {
        ResolveReferences();

        if (luckyShotEntryService != null)
        {
            try
            {
                object result = await InvokeTaskMethodAsync(luckyShotEntryService, "RefreshTokens", cancellationToken);

                int reflectedValue = TryConvertToInt(result, -1);
                if (reflectedValue >= 0)
                    return reflectedValue;

                int cachedTokensFromService = TryReadIntMember(luckyShotEntryService, -1, "CachedTokens", "cachedTokens", "CurrentTokens", "currentTokens", "Tokens", "tokens");
                if (cachedTokensFromService >= 0)
                    return cachedTokensFromService;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StartupRewardFlowController] RefreshLuckyShotTokensAsync -> LuckyShotEntryService.RefreshTokens failed. Error=" + ex.Message, this);
            }
        }

        if (luckyShotBackendService != null)
        {
            int backendTokens = TryReadIntMember(
                luckyShotBackendService,
                -1,
                "CachedTokens",
                "cachedTokens",
                "CurrentTokens",
                "currentTokens",
                "LuckyShotTokens",
                "luckyShotTokens");

            if (backendTokens >= 0)
                return backendTokens;
        }

        int panelTokens = TryReadIntMember(
            luckyShotEntryPanel,
            -1,
            "CachedTokens",
            "cachedTokens",
            "CurrentTokens",
            "currentTokens",
            "Tokens",
            "tokens");

        if (panelTokens >= 0)
            return panelTokens;

        object profileManager = ResolvePlayerProfileManagerObject();

        if (profileManager != null)
        {
            object activeProfile = TryReadObjectMember(
                profileManager,
                "ActiveProfile",
                "activeProfile",
                "RuntimeData",
                "runtimeData",
                "CurrentProfile",
                "currentProfile",
                "ActiveRuntimeData",
                "activeRuntimeData");

            int profileTokens = TryReadIntMember(
                activeProfile,
                -1,
                "LuckyShotTokens",
                "luckyShotTokens",
                "LuckyShotEntryTokens",
                "luckyShotEntryTokens",
                "EntryTokens",
                "entryTokens");

            if (profileTokens >= 0)
                return profileTokens;

            profileTokens = TryReadIntMember(
                profileManager,
                -1,
                "LuckyShotTokens",
                "luckyShotTokens",
                "LuckyShotEntryTokens",
                "luckyShotEntryTokens",
                "EntryTokens",
                "entryTokens");

            if (profileTokens >= 0)
                return profileTokens;
        }

        if (verboseLogs)
            Debug.LogWarning("[StartupRewardFlowController] RefreshLuckyShotTokensAsync -> unable to resolve token count. Returning 0.", this);

        return 0;
    }

    private async Task<bool> WaitForCoreReferencesAsync(CancellationToken cancellationToken)
    {
        float startTime = Time.unscaledTime;

        while (Time.unscaledTime - startTime < serviceResolveTimeoutSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResolveReferences();

            bool hasDailySystem = dailyLoginRewardService != null && dailyLoginPanel != null;
            bool hasLuckyShotSystem = luckyShotEntryPanel != null && (luckyShotEntryService != null || luckyShotBackendService != null);

            if (hasDailySystem || hasLuckyShotSystem)
            {
                if (verboseLogs)
                {
                    Debug.Log(
                        "[StartupRewardFlowController] WaitForCoreReferencesAsync -> ready. " +
                        "DailyService=" + (dailyLoginRewardService != null) +
                        " | DailyPanel=" + (dailyLoginPanel != null) +
                        " | DailyFreeLuckyShotService=" + (dailyFreeLuckyShotRewardService != null) +
                        " | LuckyShotBackend=" + (luckyShotBackendService != null) +
                        " | LuckyShotEntryService=" + (luckyShotEntryService != null) +
                        " | LuckyShotEntryPanel=" + (luckyShotEntryPanel != null),
                        this);
                }

                return true;
            }

            await Task.Yield();
        }

        ResolveReferences();

        Debug.LogWarning(
            "[StartupRewardFlowController] WaitForCoreReferencesAsync timeout -> " +
            "DailyService=" + (dailyLoginRewardService != null) +
            " | DailyPanel=" + (dailyLoginPanel != null) +
            " | DailyFreeLuckyShotService=" + (dailyFreeLuckyShotRewardService != null) +
            " | LuckyShotBackend=" + (luckyShotBackendService != null) +
            " | LuckyShotEntryService=" + (luckyShotEntryService != null) +
            " | LuckyShotEntryPanel=" + (luckyShotEntryPanel != null),
            this);

        return dailyLoginRewardService != null ||
               dailyLoginPanel != null ||
               dailyFreeLuckyShotRewardService != null ||
               luckyShotBackendService != null ||
               luckyShotEntryService != null ||
               luckyShotEntryPanel != null;
    }

    private async Task WaitUntilDailyPanelClosedAsync(CancellationToken cancellationToken)
    {
        while (IsDailyPanelOpen())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DelaySecondsAsync(panelClosePollingIntervalSeconds, cancellationToken);
        }
    }

    private bool IsDailyPanelOpen()
    {
        if (dailyLoginPanelRoot != null)
            return dailyLoginPanelRoot.activeInHierarchy;

        if (dailyLoginPanel != null)
            return dailyLoginPanel.gameObject.activeInHierarchy;

        return false;
    }

    private void ResolveReferences()
    {
        if (dailyLoginRewardService == null)
        {
            dailyLoginRewardService = DailyLoginRewardService.Instance;

#if UNITY_2023_1_OR_NEWER
            if (dailyLoginRewardService == null)
                dailyLoginRewardService = FindFirstObjectByType<DailyLoginRewardService>();
#else
            if (dailyLoginRewardService == null)
                dailyLoginRewardService = FindObjectOfType<DailyLoginRewardService>();
#endif
        }

        if (dailyLoginPanel == null)
        {
#if UNITY_2023_1_OR_NEWER
            dailyLoginPanel = FindFirstObjectByType<DailyLoginPanelUI>(FindObjectsInactive.Include);
#else
            DailyLoginPanelUI[] panels = Resources.FindObjectsOfTypeAll<DailyLoginPanelUI>();
            if (panels != null && panels.Length > 0)
                dailyLoginPanel = panels[0];
#endif
        }

        if (dailyFreeLuckyShotRewardService == null)
        {
            dailyFreeLuckyShotRewardService = DailyFreeLuckyShotRewardService.Instance;

#if UNITY_2023_1_OR_NEWER
            if (dailyFreeLuckyShotRewardService == null)
                dailyFreeLuckyShotRewardService = FindFirstObjectByType<DailyFreeLuckyShotRewardService>();
#else
            if (dailyFreeLuckyShotRewardService == null)
                dailyFreeLuckyShotRewardService = FindObjectOfType<DailyFreeLuckyShotRewardService>();
#endif
        }

        if (luckyShotBackendService == null)
        {
            luckyShotBackendService = LuckyShotBackendService.Instance;

#if UNITY_2023_1_OR_NEWER
            if (luckyShotBackendService == null)
                luckyShotBackendService = FindFirstObjectByType<LuckyShotBackendService>();
#else
            if (luckyShotBackendService == null)
                luckyShotBackendService = FindObjectOfType<LuckyShotBackendService>();
#endif
        }

        if (luckyShotEntryService == null)
        {
            luckyShotEntryService = LuckyShotEntryService.Instance;

#if UNITY_2023_1_OR_NEWER
            if (luckyShotEntryService == null)
                luckyShotEntryService = FindFirstObjectByType<LuckyShotEntryService>();
#else
            if (luckyShotEntryService == null)
                luckyShotEntryService = FindObjectOfType<LuckyShotEntryService>();
#endif
        }

        if (luckyShotEntryPanel == null)
        {
#if UNITY_2023_1_OR_NEWER
            luckyShotEntryPanel = FindFirstObjectByType<LuckyShotEntryPanelUI>(FindObjectsInactive.Include);
#else
            LuckyShotEntryPanelUI[] panels = Resources.FindObjectsOfTypeAll<LuckyShotEntryPanelUI>();
            if (panels != null && panels.Length > 0)
                luckyShotEntryPanel = panels[0];
#endif
        }
    }

    private void SetRootActive(GameObject explicitRoot, MonoBehaviour fallbackBehaviour, bool active)
    {
        if (explicitRoot != null)
        {
            explicitRoot.SetActive(active);
            return;
        }

        if (fallbackBehaviour != null)
            fallbackBehaviour.gameObject.SetActive(active);
    }

    private async Task DelaySecondsAsync(float seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0f)
        {
            await Task.Yield();
            return;
        }

        float startTime = Time.unscaledTime;

        while (Time.unscaledTime - startTime < seconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private async Task<object> InvokeTaskMethodAsync(object target, string methodName, CancellationToken cancellationToken)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
            return null;

        Type type = target.GetType();

        MethodInfo methodWithToken = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(CancellationToken) },
            null);

        object invocationResult;

        if (methodWithToken != null)
        {
            invocationResult = methodWithToken.Invoke(target, new object[] { cancellationToken });
        }
        else
        {
            MethodInfo methodWithoutToken = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            if (methodWithoutToken == null)
                return null;

            invocationResult = methodWithoutToken.Invoke(target, null);
        }

        if (invocationResult == null)
            return null;

        Task task = invocationResult as Task;

        if (task == null)
            return invocationResult;

        await task;

        Type taskType = task.GetType();

        if (taskType.IsGenericType)
        {
            PropertyInfo resultProperty = taskType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProperty != null)
                return resultProperty.GetValue(task);
        }

        return null;
    }

    private int TryConvertToInt(object value, int fallback)
    {
        if (value == null)
            return fallback;

        if (value is int intValue)
            return intValue;

        if (value is long longValue)
            return Mathf.Clamp((int)longValue, int.MinValue, int.MaxValue);

        if (value is short shortValue)
            return shortValue;

        if (value is byte byteValue)
            return byteValue;

        if (int.TryParse(value.ToString(), out int parsed))
            return parsed;

        return fallback;
    }

    private int TryReadIntMember(object target, int fallback, params string[] names)
    {
        object value = TryReadObjectMember(target, names);
        return TryConvertToInt(value, fallback);
    }

    private object TryReadObjectMember(object target, params string[] names)
    {
        if (target == null || names == null)
            return null;

        Type type = target.GetType();

        for (int i = 0; i < names.Length; i++)
        {
            PropertyInfo property = type.GetProperty(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
                return property.GetValue(target);

            FieldInfo field = type.GetField(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(target);
        }

        return null;
    }

    private object ResolvePlayerProfileManagerObject()
    {
        Type type = Type.GetType("PlayerProfileManager");

        if (type != null)
        {
            PropertyInfo instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
            {
                object instance = instanceProperty.GetValue(null);
                if (instance != null)
                    return instance;
            }
        }

#if UNITY_2023_1_OR_NEWER
        PlayerProfileManager found = FindFirstObjectByType<PlayerProfileManager>();
#else
        PlayerProfileManager found = FindObjectOfType<PlayerProfileManager>();
#endif

        return found;
    }
}