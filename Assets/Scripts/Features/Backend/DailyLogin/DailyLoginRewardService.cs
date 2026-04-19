using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DailyLoginRewardService : MonoBehaviour
{
    public static DailyLoginRewardService Instance { get; private set; }

    public event Action<DailyLoginPreviewState> PreviewUpdated;
    public event Action<DailyLoginClaimResult> ClaimCompleted;

    [Header("Schedule")]
    [SerializeField] private bool useUtcDay = true;
    [SerializeField][Range(0, 23)] private int resetHourUtc = 0;

    [Header("Rewards")]
    [SerializeField]
    private DailyLoginRewardDefinition[] rewards = new DailyLoginRewardDefinition[]
    {
        new DailyLoginRewardDefinition { dayIndex = 1, rewardType = DailyLoginRewardType.SoftCurrency, amount = 50, chestType = default, customLabel = "Coins" },
        new DailyLoginRewardDefinition { dayIndex = 2, rewardType = DailyLoginRewardType.SoftCurrency, amount = 75, chestType = default, customLabel = "Coins" },
        new DailyLoginRewardDefinition { dayIndex = 3, rewardType = DailyLoginRewardType.PremiumCurrency, amount = 5, chestType = default, customLabel = "Gems" },
        new DailyLoginRewardDefinition { dayIndex = 4, rewardType = DailyLoginRewardType.SoftCurrency, amount = 125, chestType = default, customLabel = "Coins" },
        new DailyLoginRewardDefinition { dayIndex = 5, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.GuaranteedRare, customLabel = "Chest" },
        new DailyLoginRewardDefinition { dayIndex = 6, rewardType = DailyLoginRewardType.PremiumCurrency, amount = 10, chestType = default, customLabel = "Gems" },
        new DailyLoginRewardDefinition { dayIndex = 7, rewardType = DailyLoginRewardType.FreeLuckyShot, amount = 1, chestType = default, customLabel = "Free Lucky Shot" }
    };

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private const int MaxDailyDays = 7;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        NormalizeRewards();

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] Awake -> ready.", this);
    }

    public DailyLoginRewardDefinition[] GetDefinitions()
    {
        DailyLoginRewardDefinition[] copy = new DailyLoginRewardDefinition[rewards.Length];
        Array.Copy(rewards, copy, rewards.Length);
        return copy;
    }

    public DailyLoginPreviewState GetPreview()
    {
        DailyLoginPreviewState preview = BuildPreviewState(GetBackendState(), GetNowUtc());
        PreviewUpdated?.Invoke(preview);
        return preview;
    }

    public DailyLoginPreviewState GetPreviewState()
    {
        return GetPreview();
    }

    public async Task<DailyLoginPreviewState> RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (cancellationToken.IsCancellationRequested)
            cancellationToken.ThrowIfCancellationRequested();

        DailyLoginPreviewState preview = GetPreview();

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] RefreshPreviewAsync -> " +
                "Streak=" + preview.currentStreakDay +
                " | NextDay=" + preview.nextClaimDayIndex +
                " | CanClaim=" + preview.canClaimNow,
                this);
        }

        return preview;
    }

    public async Task<DailyLoginClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (cancellationToken.IsCancellationRequested)
            cancellationToken.ThrowIfCancellationRequested();

        BackendDailyLoginState state = GetBackendState();
        DateTime nowUtc = GetNowUtc();

        DailyLoginPreviewState beforePreview = BuildPreviewState(state, nowUtc);

        if (!beforePreview.canClaimNow)
        {
            DailyLoginClaimResult blocked = beforePreview.alreadyClaimedToday
                ? DailyLoginClaimResult.AlreadyClaimed(beforePreview)
                : DailyLoginClaimResult.Failed(beforePreview, "Reward not claimable yet.");

            ClaimCompleted?.Invoke(blocked);
            return blocked;
        }

        int claimDay = Mathf.Clamp(beforePreview.nextClaimDayIndex, 1, MaxDailyDays);
        DailyLoginRewardDefinition reward = GetRewardForDay(claimDay);

        int newSoftCurrencyTotal = GetCurrentSoftCurrency();
        int newPremiumCurrencyTotal = GetCurrentPremiumCurrency();

        switch (reward.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                newSoftCurrencyTotal = GrantSoftCurrency(reward.amount);
                break;

            case DailyLoginRewardType.PremiumCurrency:
                newPremiumCurrencyTotal = GrantPremiumCurrency(reward.amount);
                break;

            case DailyLoginRewardType.Chest:
                GrantChest(reward.chestType);
                break;

            case DailyLoginRewardType.FreeLuckyShot:
                await GrantLuckyShotTokenAsync(Mathf.Max(1, reward.amount), cancellationToken);
                break;
        }

        state.lastClaimDateUtc = GetWindowDateKey(nowUtc);
        state.lastClaimWindowIndex = GetWindowIndex(nowUtc);
        state.consecutiveDays = claimDay;

        bool saveOk = await SaveBackendStateAsync(state, cancellationToken);
        if (!saveOk)
        {
            DailyLoginPreviewState failedPreview = BuildPreviewState(GetBackendState(), GetNowUtc());
            DailyLoginClaimResult failed = DailyLoginClaimResult.Failed(failedPreview, "Backend save failed.");
            ClaimCompleted?.Invoke(failed);
            return failed;
        }

        DailyLoginPreviewState afterPreview = BuildPreviewState(GetBackendState(), GetNowUtc());

        DailyLoginClaimResult result = DailyLoginClaimResult.Success(
            claimDay,
            reward.rewardType,
            reward.amount,
            reward.chestType,
            reward.customLabel,
            newSoftCurrencyTotal,
            newPremiumCurrencyTotal,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            afterPreview);

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] ClaimTodayAsync -> " +
                "Day=" + claimDay +
                " | Type=" + reward.rewardType +
                " | Amount=" + reward.amount +
                " | ChestType=" + reward.chestType,
                this);
        }

        PreviewUpdated?.Invoke(afterPreview);
        ClaimCompleted?.Invoke(result);
        return result;
    }

    public DailyLoginClaimResult ClaimTodayReward()
    {
        try
        {
            return ClaimTodayAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginRewardService] ClaimTodayReward sync wrapper failed -> " + ex, this);
            return DailyLoginClaimResult.Failed(GetPreview(), ex.Message);
        }
    }

    [ContextMenu("Debug/Reset Daily Login")]
    public void DebugResetDailyLogin()
    {
        _ = DebugResetDailyLoginAsync();
    }

    [ContextMenu("Debug/Force Claim Available Today")]
    public void DebugForceClaimAvailableToday()
    {
        _ = DebugForceClaimAvailableTodayAsync();
    }

    private async Task DebugResetDailyLoginAsync()
    {
        BackendDailyLoginState state = GetBackendState();
        state.lastClaimDateUtc = string.Empty;
        state.lastClaimWindowIndex = int.MinValue;
        state.consecutiveDays = 0;
        await SaveBackendStateAsync(state, CancellationToken.None);
        PreviewUpdated?.Invoke(GetPreview());
    }

    private async Task DebugForceClaimAvailableTodayAsync()
    {
        BackendDailyLoginState state = GetBackendState();
        DateTime nowUtc = GetNowUtc();
        state.lastClaimDateUtc = GetWindowDateKey(nowUtc.AddDays(-2));
        state.lastClaimWindowIndex = GetWindowIndex(nowUtc) - 2;
        await SaveBackendStateAsync(state, CancellationToken.None);
        PreviewUpdated?.Invoke(GetPreview());
    }

    private DailyLoginPreviewState BuildPreviewState(BackendDailyLoginState state, DateTime nowUtc)
    {
        int currentWindowIndex = GetWindowIndex(nowUtc);
        bool claimedInCurrentWindow = state.lastClaimWindowIndex == currentWindowIndex && !string.IsNullOrEmpty(state.lastClaimDateUtc);

        int nextClaimDayIndex = 1;
        if (claimedInCurrentWindow)
        {
            nextClaimDayIndex = WrapDay(state.consecutiveDays + 1);
        }
        else
        {
            if (state.lastClaimWindowIndex == currentWindowIndex - 1 && state.consecutiveDays > 0)
                nextClaimDayIndex = WrapDay(state.consecutiveDays + 1);
            else
                nextClaimDayIndex = 1;
        }

        DailyLoginDayState[] dayStates = new DailyLoginDayState[MaxDailyDays];
        for (int i = 1; i <= MaxDailyDays; i++)
        {
            DailyLoginRewardDefinition definition = GetRewardForDay(i);

            DailyLoginDayState dayState = new DailyLoginDayState
            {
                dayIndex = i,
                rewardType = definition.rewardType,
                amount = definition.amount,
                chestType = definition.chestType,
                customLabel = definition.customLabel,
                isClaimed = i <= state.consecutiveDays,
                isClaimable = !claimedInCurrentWindow && i == nextClaimDayIndex,
                isToday = i == nextClaimDayIndex,
                isMissed = false
            };

            if (claimedInCurrentWindow && i == nextClaimDayIndex)
                dayState.isToday = true;

            dayStates[i - 1] = dayState;
        }

        DailyLoginPreviewState preview = new DailyLoginPreviewState
        {
            isReady = true,
            currentStreakDay = Mathf.Clamp(state.consecutiveDays, 0, MaxDailyDays),
            nextClaimDayIndex = nextClaimDayIndex,
            canClaimNow = !claimedInCurrentWindow,
            alreadyClaimedToday = claimedInCurrentWindow,
            lastClaimDateUtc = state.lastClaimDateUtc ?? string.Empty,
            nextResetUnixSeconds = new DateTimeOffset(GetNextResetUtc(nowUtc)).ToUnixTimeSeconds(),
            days = dayStates
        };

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] GetPreviewState -> " +
                "Streak=" + preview.currentStreakDay +
                " | NextDay=" + preview.nextClaimDayIndex +
                " | CanClaim=" + preview.canClaimNow +
                " | LastClaimDateUtc=" + preview.lastClaimDateUtc,
                this);
        }

        return preview;
    }

    private int WrapDay(int rawDay)
    {
        if (rawDay <= 0)
            return 1;

        int wrapped = ((rawDay - 1) % MaxDailyDays) + 1;
        return wrapped;
    }

    private DailyLoginRewardDefinition GetRewardForDay(int dayIndex)
    {
        NormalizeRewards();

        for (int i = 0; i < rewards.Length; i++)
        {
            if (rewards[i].dayIndex == dayIndex)
                return rewards[i];
        }

        return new DailyLoginRewardDefinition
        {
            dayIndex = dayIndex,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 0,
            chestType = default,
            customLabel = "Reward"
        };
    }

    private void NormalizeRewards()
    {
        if (rewards == null || rewards.Length == 0)
            return;

        for (int i = 0; i < rewards.Length; i++)
        {
            if (rewards[i].dayIndex <= 0)
                rewards[i].dayIndex = i + 1;
        }
    }

    private DateTime GetNowUtc()
    {
        return useUtcDay ? DateTime.UtcNow : DateTime.Now.ToUniversalTime();
    }

    private int GetWindowIndex(DateTime utcNow)
    {
        DateTime resetAnchor = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, resetHourUtc, 0, 0, DateTimeKind.Utc);
        if (utcNow < resetAnchor)
            resetAnchor = resetAnchor.AddDays(-1);

        return (int)(resetAnchor - DateTime.UnixEpoch).TotalDays;
    }

    private string GetWindowDateKey(DateTime utcNow)
    {
        DateTime resetAnchor = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, resetHourUtc, 0, 0, DateTimeKind.Utc);
        if (utcNow < resetAnchor)
            resetAnchor = resetAnchor.AddDays(-1);

        return resetAnchor.ToString("yyyy-MM-dd");
    }

    private DateTime GetNextResetUtc(DateTime utcNow)
    {
        DateTime resetAnchor = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, resetHourUtc, 0, 0, DateTimeKind.Utc);
        if (utcNow < resetAnchor)
            return resetAnchor;

        return resetAnchor.AddDays(1);
    }

    private int GetCurrentSoftCurrency()
    {
        PlayerProfileManager manager = ResolvePlayerProfileManager();
        if (manager == null)
            return 0;

        return TryReadIntProperty(manager, "SoftCurrency", "softCurrency", "Coins", "coins");
    }

    private int GetCurrentPremiumCurrency()
    {
        PlayerProfileManager manager = ResolvePlayerProfileManager();
        if (manager == null)
            return 0;

        return TryReadIntProperty(manager, "PremiumCurrency", "premiumCurrency", "Gems", "gems");
    }

    private int GrantSoftCurrency(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        PlayerProfileManager manager = ResolvePlayerProfileManager();
        if (manager != null)
        {
            InvokeAny(manager, new[] { "AddSoftCurrency", "AddCoins" }, safeAmount);
        }

        int total = GetCurrentSoftCurrency();

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantSoftCurrency -> " + safeAmount, this);

        return total;
    }

    private int GrantPremiumCurrency(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        PlayerProfileManager manager = ResolvePlayerProfileManager();
        if (manager != null)
        {
            InvokeAny(manager, new[] { "AddPremiumCurrency", "AddGems" }, safeAmount);
        }

        int total = GetCurrentPremiumCurrency();

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantPremiumCurrency -> " + safeAmount, this);

        return total;
    }

    private void GrantChest(ChestType chestType)
    {
        PlayerChestSlotInventory inventory = ResolveChestInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantChest -> chest inventory missing.", this);
            return;
        }

        if (!InvokeAny(inventory, new[] { "AwardChest", "TryAddRewardChest", "TryAddRewardChestFromBackendOrSystem", "TryAddRewardChestFromBackend" }, chestType))
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantChest -> no compatible chest grant method found.", this);
        }
    }

    private async Task GrantLuckyShotTokenAsync(int amount, CancellationToken cancellationToken)
    {
        int safeAmount = Mathf.Max(1, amount);

        LuckyShotBackendService luckyService = ResolveLuckyShotBackendService();
        if (luckyService != null)
        {
            await luckyService.GrantTokensAsync(safeAmount, cancellationToken);
            return;
        }

        object profileService = ResolveProfileServiceObject();
        if (profileService == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantLuckyShotTokenAsync -> profile service missing.", this);
            return;
        }

        object snapshot = GetCurrentProfileSnapshot(profileService);
        if (snapshot == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantLuckyShotTokenAsync -> current profile missing.", this);
            return;
        }

        int currentTokens = TryReadIntMember(snapshot, "LuckyShotTokens", "luckyShotTokens");
        object updated = TryInvokeSnapshotWith(snapshot, "WithLuckyShotTokens", currentTokens + safeAmount);
        if (updated == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantLuckyShotTokenAsync -> snapshot lacks WithLuckyShotTokens.", this);
            return;
        }

        await ApplyAuthoritativeSnapshotAsync(profileService, updated, cancellationToken);
    }

    private BackendDailyLoginState GetBackendState()
    {
        object profileService = ResolveProfileServiceObject();
        object snapshot = GetCurrentProfileSnapshot(profileService);

        if (snapshot == null)
            return default;

        BackendDailyLoginState state = new BackendDailyLoginState
        {
            lastClaimDateUtc = TryReadStringMember(snapshot, "LastDailyLoginClaimDateUtc", "lastDailyLoginClaimDateUtc"),
            consecutiveDays = TryReadIntMember(snapshot, "ConsecutiveLoginDays", "consecutiveLoginDays"),
            lastClaimWindowIndex = TryReadIntMember(snapshot, "LastDailyLoginClaimWindowIndex", "lastDailyLoginClaimWindowIndex")
        };

        if (state.lastClaimWindowIndex == 0 && string.IsNullOrEmpty(state.lastClaimDateUtc))
            state.lastClaimWindowIndex = int.MinValue;

        return state;
    }

    private async Task<bool> SaveBackendStateAsync(BackendDailyLoginState state, CancellationToken cancellationToken)
    {
        object profileService = ResolveProfileServiceObject();
        if (profileService == null)
            return false;

        object snapshot = GetCurrentProfileSnapshot(profileService);
        if (snapshot == null)
            return false;

        object updated = TryInvokeSnapshotWith(snapshot, "WithDailyLoginState", state.lastClaimDateUtc ?? string.Empty, state.consecutiveDays, state.lastClaimWindowIndex);
        if (updated == null)
            updated = TryInvokeSnapshotWith(snapshot, "WithDailyLoginState", state.lastClaimDateUtc ?? string.Empty, state.consecutiveDays);

        if (updated == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] SaveBackendStateAsync -> snapshot lacks WithDailyLoginState overload.", this);
            return false;
        }

        await ApplyAuthoritativeSnapshotAsync(profileService, updated, cancellationToken);
        return true;
    }

    private async Task ApplyAuthoritativeSnapshotAsync(object profileService, object snapshot, CancellationToken cancellationToken)
    {
        MethodInfo method = profileService.GetType().GetMethod("ApplyAuthoritativeSnapshotAsync", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
            throw new MissingMethodException(profileService.GetType().Name, "ApplyAuthoritativeSnapshotAsync");

        object result;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 2)
            result = method.Invoke(profileService, new[] { snapshot, (object)cancellationToken });
        else if (parameters.Length == 1)
            result = method.Invoke(profileService, new[] { snapshot });
        else
            throw new InvalidOperationException("Unsupported ApplyAuthoritativeSnapshotAsync signature.");

        if (result is Task task)
            await task;
    }

    private object ResolveProfileServiceObject()
    {
        Type rootType = Type.GetType("UncballArena.Core.Bootstrap.GameCompositionRoot, Assembly-CSharp");
        if (rootType == null)
            return null;

        PropertyInfo instanceProp = rootType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        object root = instanceProp != null ? instanceProp.GetValue(null) : null;
        if (root == null)
            return null;

        PropertyInfo profileServiceProp = rootType.GetProperty("ProfileService", BindingFlags.Public | BindingFlags.Instance);
        return profileServiceProp != null ? profileServiceProp.GetValue(root) : null;
    }

    private object GetCurrentProfileSnapshot(object profileService)
    {
        if (profileService == null)
            return null;

        PropertyInfo prop = profileService.GetType().GetProperty("CurrentProfile", BindingFlags.Public | BindingFlags.Instance);
        return prop != null ? prop.GetValue(profileService) : null;
    }

    private object TryInvokeSnapshotWith(object snapshot, string methodName, params object[] args)
    {
        if (snapshot == null)
            return null;

        MethodInfo[] methods = snapshot.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
                continue;

            try
            {
                return method.Invoke(snapshot, args);
            }
            catch
            {
            }
        }

        return null;
    }

    private int TryReadIntMember(object target, params string[] memberNames)
    {
        if (target == null || memberNames == null)
            return 0;

        Type type = target.GetType();
        for (int i = 0; i < memberNames.Length; i++)
        {
            string name = memberNames[i];

            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int))
                return (int)property.GetValue(target);

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(target);
        }

        return 0;
    }

    private string TryReadStringMember(object target, params string[] memberNames)
    {
        if (target == null || memberNames == null)
            return string.Empty;

        Type type = target.GetType();
        for (int i = 0; i < memberNames.Length; i++)
        {
            string name = memberNames[i];

            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(string))
                return (string)property.GetValue(target) ?? string.Empty;

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string))
                return (string)field.GetValue(target) ?? string.Empty;
        }

        return string.Empty;
    }

    private int TryReadIntProperty(object target, params string[] propertyNames)
    {
        if (target == null)
            return 0;

        Type type = target.GetType();
        for (int i = 0; i < propertyNames.Length; i++)
        {
            PropertyInfo property = type.GetProperty(propertyNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int))
                return (int)property.GetValue(target);
        }

        return 0;
    }

    private bool InvokeAny(object target, string[] methodNames, params object[] args)
    {
        if (target == null || methodNames == null)
            return false;

        Type type = target.GetType();
        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int j = 0; j < methods.Length; j++)
            {
                MethodInfo method = methods[j];
                if (!string.Equals(method.Name, methodNames[i], StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                    continue;

                try
                {
                    method.Invoke(target, args);
                    return true;
                }
                catch
                {
                }
            }
        }

        return false;
    }

    private PlayerProfileManager ResolvePlayerProfileManager()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<PlayerProfileManager>();
#else
        return FindObjectOfType<PlayerProfileManager>();
#endif
    }

    private PlayerChestSlotInventory ResolveChestInventory()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<PlayerChestSlotInventory>();
#else
        return FindObjectOfType<PlayerChestSlotInventory>();
#endif
    }

    private LuckyShotBackendService ResolveLuckyShotBackendService()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<LuckyShotBackendService>();
#else
        return FindObjectOfType<LuckyShotBackendService>();
#endif
    }

    [Serializable]
    private struct BackendDailyLoginState
    {
        public string lastClaimDateUtc;
        public int consecutiveDays;
        public int lastClaimWindowIndex;
    }
}
