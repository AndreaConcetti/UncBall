using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Services;

public class DailyLoginRewardService : MonoBehaviour
{
    public static DailyLoginRewardService Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Release Config")]
    [SerializeField] private bool autoResolveDependencies = true;
    [SerializeField] private bool useUtcDay = true;
    [SerializeField][Range(0, 23)] private int resetHourUtc = 0;
    [SerializeField] private bool resetStreakIfOneDailyWindowIsMissed = true;
    [SerializeField] private bool verboseLogs = true;

    [Header("7-Day Reward Track")]
    [SerializeField]
    private DailyLoginRewardDefinition[] rewards = new DailyLoginRewardDefinition[7]
    {
        new DailyLoginRewardDefinition { dayIndex = 1, rewardType = DailyLoginRewardType.SoftCurrency, amount = 50, chestType = ChestType.Random, customLabel = "50 COINS" },
        new DailyLoginRewardDefinition { dayIndex = 2, rewardType = DailyLoginRewardType.SoftCurrency, amount = 75, chestType = ChestType.Random, customLabel = "75 COINS" },
        new DailyLoginRewardDefinition { dayIndex = 3, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.GuaranteedCommon, customLabel = "CHEST" },
        new DailyLoginRewardDefinition { dayIndex = 4, rewardType = DailyLoginRewardType.SoftCurrency, amount = 100, chestType = ChestType.Random, customLabel = "100 COINS" },
        new DailyLoginRewardDefinition { dayIndex = 5, rewardType = DailyLoginRewardType.PremiumCurrency, amount = 10, chestType = ChestType.Random, customLabel = "10 GEMS" },
        new DailyLoginRewardDefinition { dayIndex = 6, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.GuaranteedRare, customLabel = "RARE CHEST" },
        new DailyLoginRewardDefinition { dayIndex = 7, rewardType = DailyLoginRewardType.FreeLuckyShot, amount = 1, chestType = ChestType.Random, customLabel = "FREE LUCKY SHOT" },
    };

    public event Action<DailyLoginPreviewState> PreviewUpdated;
    public event Action<DailyLoginClaimResult> ClaimCompleted;

    private IProfileService profileService;
    private bool isBusy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (autoResolveDependencies)
            ResolveDependencies();

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] Awake -> ready.", this);
    }

    public DailyLoginRewardDefinition[] GetDefinitions()
    {
        List<DailyLoginRewardDefinition> normalized = GetNormalizedRewards();
        return normalized.ToArray();
    }

    public DailyLoginPreviewState GetPreview()
    {
        ResolveDependencies();
        ProfileSnapshot snapshot = GetCurrentSnapshot();
        return BuildPreview(snapshot);
    }

    public DailyLoginPreviewState GetPreviewState()
    {
        return GetPreview();
    }

    public async Task<DailyLoginPreviewState> RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        DailyLoginPreviewState preview = GetPreview();
        PreviewUpdated?.Invoke(preview);
        return preview;
    }

    public async Task<DailyLoginClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
    {
        ResolveDependencies();

        if (isBusy)
        {
            DailyLoginClaimResult busyResult = DailyLoginClaimResult.Failed(GetPreview(), "Daily login is busy.");
            ClaimCompleted?.Invoke(busyResult);
            return busyResult;
        }

        if (profileService == null)
        {
            DailyLoginClaimResult failResult = DailyLoginClaimResult.Failed(default, "Profile service missing.");
            ClaimCompleted?.Invoke(failResult);
            return failResult;
        }

        isBusy = true;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProfileSnapshot snapshot = GetCurrentSnapshot();
            DailyLoginPreviewState previewBefore = BuildPreview(snapshot);

            if (!previewBefore.isReady)
            {
                DailyLoginClaimResult notReady = DailyLoginClaimResult.Failed(previewBefore, "Daily login preview not ready.");
                ClaimCompleted?.Invoke(notReady);
                return notReady;
            }

            DailyLoginDayState? claimableState = GetClaimableState(previewBefore.days);
            if (!claimableState.HasValue)
            {
                DailyLoginClaimResult alreadyClaimed = DailyLoginClaimResult.AlreadyClaimed(previewBefore);
                ClaimCompleted?.Invoke(alreadyClaimed);
                return alreadyClaimed;
            }

            DailyLoginRewardDefinition reward = claimableState.Value.reward;
            RewardComputation computation = ComputeReward(snapshot, reward);
            GrantNonProfileReward(reward, claimableState.Value.dayIndex);

            long nowUnix = GetCurrentUnixSeconds();
            string claimDateKey = GetRewardDateKey(nowUnix);
            int updatedStreak = Mathf.Clamp(claimableState.Value.dayIndex, 1, 7);

            ProfileSnapshot updatedSnapshot = snapshot
                .WithCurrencies(computation.newSoftCurrency, computation.newPremiumCurrency)
                .WithDailyLoginState(claimDateKey, updatedStreak);

            updatedSnapshot = TryApplyLuckyShotTokenToSnapshot(updatedSnapshot, reward);

            await profileService.ApplyAuthoritativeSnapshotAsync(updatedSnapshot, cancellationToken);

            DailyLoginPreviewState previewAfter = BuildPreview(updatedSnapshot);

            DailyLoginClaimResult result = new DailyLoginClaimResult
            {
                success = true,
                alreadyClaimed = false,
                claimedDay = claimableState.Value.dayIndex,
                reward = reward,
                newSoftCurrencyTotal = computation.newSoftCurrency,
                newPremiumCurrencyTotal = computation.newPremiumCurrency,
                claimedAtUnixSeconds = nowUnix,
                previewAfterClaim = previewAfter,
                failureReason = string.Empty
            };

            if (verboseLogs)
            {
                Debug.Log(
                    "[DailyLoginRewardService] ClaimTodayAsync success -> " +
                    "ClaimedDay=" + result.claimedDay +
                    " | RewardType=" + reward.rewardType +
                    " | Amount=" + reward.amount +
                    " | ChestType=" + reward.chestType +
                    " | NewSoft=" + result.newSoftCurrencyTotal +
                    " | NewPremium=" + result.newPremiumCurrencyTotal +
                    " | ClaimDateKey=" + claimDateKey,
                    this);
            }

            PreviewUpdated?.Invoke(previewAfter);
            ClaimCompleted?.Invoke(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginRewardService] ClaimTodayAsync failed -> " + ex, this);
            DailyLoginClaimResult failResult = DailyLoginClaimResult.Failed(GetPreview(), ex.Message);
            ClaimCompleted?.Invoke(failResult);
            return failResult;
        }
        finally
        {
            isBusy = false;
        }
    }

    public DailyLoginClaimResult ClaimTodayReward()
    {
        try
        {
            return ClaimTodayAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginRewardService] ClaimTodayReward wrapper failed -> " + ex, this);
            return DailyLoginClaimResult.Failed(GetPreview(), ex.Message);
        }
    }

    public void DebugResetDailyLogin()
    {
        ResolveDependencies();
        if (profileService == null || profileService.CurrentProfile == null)
            return;

        ProfileSnapshot resetSnapshot = profileService.CurrentProfile.WithDailyLoginState(string.Empty, 0);
        _ = profileService.ApplyAuthoritativeSnapshotAsync(resetSnapshot);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] DebugResetDailyLogin -> backend daily login state cleared.", this);
    }

    public void DebugForceClaimAvailableToday()
    {
        ResolveDependencies();
        if (profileService == null || profileService.CurrentProfile == null)
            return;

        ProfileSnapshot snapshot = profileService.CurrentProfile;
        int currentStreak = Mathf.Clamp(snapshot.ConsecutiveLoginDays, 0, 7);
        string previousWindowKey = GetRewardDateKey(GetCurrentUnixSeconds() - 86400L);
        ProfileSnapshot forcedSnapshot = snapshot.WithDailyLoginState(previousWindowKey, currentStreak);
        _ = profileService.ApplyAuthoritativeSnapshotAsync(forcedSnapshot);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] DebugForceClaimAvailableToday -> backend state moved to previous reward window.", this);
    }

    [ContextMenu("Debug/Reset Daily Login")]
    private void ContextDebugResetDailyLogin()
    {
        DebugResetDailyLogin();
    }

    [ContextMenu("Debug/Force Claim Available Today")]
    private void ContextDebugForceClaimAvailableToday()
    {
        DebugForceClaimAvailableToday();
    }

    private DailyLoginPreviewState BuildPreview(ProfileSnapshot snapshot)
    {
        DailyLoginPreviewState preview = new DailyLoginPreviewState();
        if (snapshot == null || !snapshot.IsValid())
            return preview;

        long nowUnix = GetCurrentUnixSeconds();
        string currentWindowKey = GetRewardDateKey(nowUnix);
        string previousWindowKey = GetRewardDateKey(nowUnix - 86400L);

        string lastClaimKey = string.IsNullOrWhiteSpace(snapshot.LastDailyLoginClaimDateUtc)
            ? string.Empty
            : snapshot.LastDailyLoginClaimDateUtc.Trim();

        int storedStreak = Mathf.Clamp(snapshot.ConsecutiveLoginDays, 0, 7);
        bool alreadyClaimedInCurrentWindow = string.Equals(lastClaimKey, currentWindowKey, StringComparison.Ordinal);
        bool claimedInPreviousWindow = string.Equals(lastClaimKey, previousWindowKey, StringComparison.Ordinal);

        int effectiveStreak = storedStreak;
        if (!alreadyClaimedInCurrentWindow)
        {
            if (!claimedInPreviousWindow && resetStreakIfOneDailyWindowIsMissed)
                effectiveStreak = 0;
        }

        int nextClaimDay = NormalizeNextClaimDay(effectiveStreak);
        List<DailyLoginRewardDefinition> normalizedRewards = GetNormalizedRewards();
        DailyLoginDayState[] states = new DailyLoginDayState[normalizedRewards.Count];

        for (int i = 0; i < normalizedRewards.Count; i++)
        {
            DailyLoginRewardDefinition reward = normalizedRewards[i];

            bool isClaimed = reward.dayIndex <= effectiveStreak;
            bool isToday = reward.dayIndex == nextClaimDay;
            bool isClaimable = isToday && !alreadyClaimedInCurrentWindow;
            bool isMissed = reward.dayIndex > nextClaimDay;

            states[i] = new DailyLoginDayState
            {
                dayIndex = reward.dayIndex,
                reward = reward,
                isClaimed = isClaimed,
                isToday = isToday,
                isClaimable = isClaimable,
                isMissed = isMissed
            };
        }

        preview.isReady = true;
        preview.currentStreakDay = effectiveStreak;
        preview.nextClaimDay = nextClaimDay;
        preview.nowUnixSeconds = nowUnix;
        preview.nextResetUnixSeconds = GetNextResetUnixSeconds(nowUnix);
        preview.days = states;

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] BuildPreview -> " +
                "Streak=" + effectiveStreak +
                " | NextDay=" + nextClaimDay +
                " | CanClaim=" + preview.canClaimNow +
                " | LastClaimDateKey=" + lastClaimKey,
                this);
        }

        return preview;
    }

    private DailyLoginDayState? GetClaimableState(DailyLoginDayState[] days)
    {
        if (days == null)
            return null;

        for (int i = 0; i < days.Length; i++)
        {
            if (days[i].isClaimable)
                return days[i];
        }

        return null;
    }

    private List<DailyLoginRewardDefinition> GetNormalizedRewards()
    {
        List<DailyLoginRewardDefinition> list = new List<DailyLoginRewardDefinition>();

        if (rewards != null)
        {
            for (int i = 0; i < rewards.Length; i++)
            {
                DailyLoginRewardDefinition def = rewards[i];
                if (def.dayIndex <= 0)
                    def.dayIndex = i + 1;
                list.Add(def);
            }
        }

        if (list.Count == 0)
        {
            for (int i = 0; i < 7; i++)
            {
                list.Add(new DailyLoginRewardDefinition
                {
                    dayIndex = i + 1,
                    rewardType = DailyLoginRewardType.SoftCurrency,
                    amount = 50,
                    chestType = ChestType.Random,
                    customLabel = "50 COINS"
                });
            }
        }

        list.Sort((a, b) => a.dayIndex.CompareTo(b.dayIndex));
        return list;
    }

    private RewardComputation ComputeReward(ProfileSnapshot snapshot, DailyLoginRewardDefinition reward)
    {
        RewardComputation result = new RewardComputation
        {
            newSoftCurrency = snapshot.SoftCurrency,
            newPremiumCurrency = snapshot.HardCurrency
        };

        switch (reward.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                result.newSoftCurrency += Mathf.Max(0, reward.amount);
                break;

            case DailyLoginRewardType.PremiumCurrency:
                result.newPremiumCurrency += Mathf.Max(0, reward.amount);
                break;
        }

        return result;
    }

    private void GrantNonProfileReward(DailyLoginRewardDefinition reward, int claimedDay)
    {
        switch (reward.rewardType)
        {
            case DailyLoginRewardType.Chest:
                GrantChestReward(reward, claimedDay);
                break;

            case DailyLoginRewardType.FreeLuckyShot:
                GrantLuckyShotReward(reward, claimedDay);
                break;
        }
    }

    private void GrantChestReward(DailyLoginRewardDefinition reward, int claimedDay)
    {
        ResolveDependencies();
        if (playerChestSlotInventory == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantChestReward skipped -> PlayerChestSlotInventory missing.", this);
            return;
        }

        int grantCount = Mathf.Max(1, reward.amount);
        for (int i = 0; i < grantCount; i++)
            playerChestSlotInventory.AwardChest(reward.chestType);

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] GrantChestReward -> " +
                "Day=" + claimedDay +
                " | Count=" + grantCount +
                " | ChestType=" + reward.chestType,
                this);
        }
    }

    private void GrantLuckyShotReward(DailyLoginRewardDefinition reward, int claimedDay)
    {
        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] GrantLuckyShotReward -> " +
                "Day=" + claimedDay +
                " | Amount=" + Mathf.Max(1, reward.amount) +
                " | Backend token path=" + TryDescribeLuckyShotBackendPath(),
                this);
        }
    }

    private ProfileSnapshot TryApplyLuckyShotTokenToSnapshot(ProfileSnapshot snapshot, DailyLoginRewardDefinition reward)
    {
        if (reward.rewardType != DailyLoginRewardType.FreeLuckyShot)
            return snapshot;

        int amount = Mathf.Max(1, reward.amount);
        Type snapshotType = snapshot.GetType();

        PropertyInfo currentValueProperty = snapshotType.GetProperty("LuckyShotTokens", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo withTokensMethod = snapshotType.GetMethod("WithLuckyShotTokens", BindingFlags.Public | BindingFlags.Instance);

        if (currentValueProperty != null && currentValueProperty.PropertyType == typeof(int) && withTokensMethod != null)
        {
            int currentValue = (int)currentValueProperty.GetValue(snapshot);
            object updated = withTokensMethod.Invoke(snapshot, new object[] { currentValue + amount });
            if (updated is ProfileSnapshot typedSnapshot)
                return typedSnapshot;
        }

        Debug.LogWarning(
            "[DailyLoginRewardService] FreeLuckyShot granted logically, but backend profile has no LuckyShotTokens/WithLuckyShotTokens path yet. " +
            "Add those to ProfileSnapshot before enabling this reward in production.",
            this);

        return snapshot;
    }

    private string TryDescribeLuckyShotBackendPath()
    {
        ProfileSnapshot snapshot = GetCurrentSnapshot();
        if (snapshot == null)
            return "missing snapshot";

        Type snapshotType = snapshot.GetType();
        PropertyInfo currentValueProperty = snapshotType.GetProperty("LuckyShotTokens", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo withTokensMethod = snapshotType.GetMethod("WithLuckyShotTokens", BindingFlags.Public | BindingFlags.Instance);

        if (currentValueProperty != null && withTokensMethod != null)
            return "profile-supported";

        return "not-yet-supported";
    }

    private string GetRewardDateKey(long unixSeconds)
    {
        DateTimeOffset utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        DateTime rewardDate = useUtcDay
            ? utc.UtcDateTime.AddHours(-resetHourUtc).Date
            : utc.LocalDateTime.AddHours(-resetHourUtc).Date;

        return rewardDate.ToString("yyyy-MM-dd");
    }

    private long GetNextResetUnixSeconds(long unixSeconds)
    {
        DateTimeOffset utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        DateTime now = useUtcDay ? utc.UtcDateTime : utc.LocalDateTime;
        DateTime todayResetBase = now.Date.AddHours(resetHourUtc);
        DateTime nextReset = now >= todayResetBase ? todayResetBase.AddDays(1) : todayResetBase;
        DateTime resetUtc = useUtcDay ? nextReset : nextReset.ToUniversalTime();
        return new DateTimeOffset(resetUtc).ToUnixTimeSeconds();
    }

    private long GetCurrentUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private int NormalizeNextClaimDay(int effectiveStreak)
    {
        if (effectiveStreak <= 0)
            return 1;

        if (effectiveStreak >= 7)
            return 1;

        return effectiveStreak + 1;
    }

    private ProfileSnapshot GetCurrentSnapshot()
    {
        ResolveDependencies();
        return profileService != null ? profileService.CurrentProfile : null;
    }

    private void ResolveDependencies()
    {
        if (profileService == null && GameCompositionRoot.Instance != null)
            profileService = GameCompositionRoot.Instance.ProfileService;

#if UNITY_2023_1_OR_NEWER
        if (playerChestSlotInventory == null)
            playerChestSlotInventory = FindFirstObjectByType<PlayerChestSlotInventory>();
#else
        if (playerChestSlotInventory == null)
            playerChestSlotInventory = FindObjectOfType<PlayerChestSlotInventory>();
#endif
    }

    private struct RewardComputation
    {
        public int newSoftCurrency;
        public int newPremiumCurrency;
    }
}
