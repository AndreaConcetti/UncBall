using System;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private PlayerProfileManager playerProfileManager;
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Config")]
    [SerializeField] private bool autoResolveDependencies = true;
    [SerializeField] private bool useUtcDay = true;
    [SerializeField] private int resetHourUtc = 0;
    [SerializeField] private bool verboseLogs = true;

    [Header("7-Day Reward Track")]
    [SerializeField]
    private DailyLoginRewardDefinition[] rewards = new DailyLoginRewardDefinition[7]
    {
        new DailyLoginRewardDefinition { dayIndex = 1, rewardType = DailyLoginRewardType.SoftCurrency, amount = 50, chestType = ChestType.Random, label = "50 Coins" },
        new DailyLoginRewardDefinition { dayIndex = 2, rewardType = DailyLoginRewardType.SoftCurrency, amount = 75, chestType = ChestType.Random, label = "75 Coins" },
        new DailyLoginRewardDefinition { dayIndex = 3, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.Random, label = "Chest" },
        new DailyLoginRewardDefinition { dayIndex = 4, rewardType = DailyLoginRewardType.SoftCurrency, amount = 100, chestType = ChestType.Random, label = "100 Coins" },
        new DailyLoginRewardDefinition { dayIndex = 5, rewardType = DailyLoginRewardType.PremiumCurrency, amount = 10, chestType = ChestType.Random, label = "10 Gems" },
        new DailyLoginRewardDefinition { dayIndex = 6, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.GuaranteedRare, label = "Rare Chest" },
        new DailyLoginRewardDefinition { dayIndex = 7, rewardType = DailyLoginRewardType.Chest, amount = 1, chestType = ChestType.GuaranteedEpic, label = "Epic Chest" },
    };

    public event Action<DailyLoginPreviewState> PreviewUpdated;
    public event Action<DailyLoginClaimResult> ClaimCompleted;

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
    }

    public DailyLoginPreviewState GetPreview()
    {
        ResolveDependencies();

        ProfileSnapshot snapshot = GetCurrentSnapshot();
        return BuildPreview(snapshot);
    }

    public async Task<DailyLoginPreviewState> RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        DailyLoginPreviewState preview = GetPreview();
        PreviewUpdated?.Invoke(preview);
        return preview;
    }

    public async Task<DailyLoginClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
    {
        ResolveDependencies();

        if (isBusy)
        {
            DailyLoginClaimResult busyResult = DailyLoginClaimResult.Failed(GetPreview());
            ClaimCompleted?.Invoke(busyResult);
            return busyResult;
        }

        ProfileService profileService = GetProfileService();
        if (profileService == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[DailyLoginRewardService] ClaimTodayAsync aborted: ProfileService not ready.", this);

            DailyLoginClaimResult failResult = DailyLoginClaimResult.Failed(GetPreview());
            ClaimCompleted?.Invoke(failResult);
            return failResult;
        }

        isBusy = true;

        try
        {
            ProfileSnapshot snapshot = GetCurrentSnapshot();
            DailyLoginPreviewState previewBefore = BuildPreview(snapshot);

            if (!previewBefore.isReady)
            {
                DailyLoginClaimResult notReady = DailyLoginClaimResult.Failed(previewBefore);
                ClaimCompleted?.Invoke(notReady);
                return notReady;
            }

            DailyLoginDayState? todayState = GetTodayState(previewBefore.days);
            if (!todayState.HasValue)
            {
                DailyLoginClaimResult failResult = DailyLoginClaimResult.Failed(previewBefore);
                ClaimCompleted?.Invoke(failResult);
                return failResult;
            }

            if (!todayState.Value.isClaimable)
            {
                DailyLoginClaimResult alreadyClaimed = DailyLoginClaimResult.AlreadyClaimed(previewBefore);
                ClaimCompleted?.Invoke(alreadyClaimed);
                return alreadyClaimed;
            }

            DailyLoginRewardDefinition reward = todayState.Value.reward;
            int claimedDay = todayState.Value.dayIndex;

            int newSoft = snapshot.SoftCurrency;
            int newHard = snapshot.HardCurrency;

            if (reward.rewardType == DailyLoginRewardType.SoftCurrency)
                newSoft += Mathf.Max(0, reward.amount);
            else if (reward.rewardType == DailyLoginRewardType.PremiumCurrency)
                newHard += Mathf.Max(0, reward.amount);

            DateTime claimRewardDate = GetCurrentRewardDate();
            string claimDateString = FormatRewardDate(claimRewardDate);
            int newConsecutiveDays = Mathf.Clamp(claimedDay, 1, 7);

            ProfileSnapshot updatedSnapshot = snapshot.WithCurrenciesRankedLpAndDailyLogin(
                newSoftCurrency: newSoft,
                newHardCurrency: newHard,
                newRankedLp: snapshot.RankedLp,
                newLastDailyLoginClaimDateUtc: claimDateString,
                newConsecutiveLoginDays: newConsecutiveDays);

            await profileService.ApplyAuthoritativeSnapshotAsync(updatedSnapshot);

            if (reward.rewardType == DailyLoginRewardType.Chest)
                GrantChestReward(reward);

            DailyLoginPreviewState previewAfter = BuildPreview(GetCurrentSnapshot());

            DailyLoginClaimResult result = new DailyLoginClaimResult
            {
                success = true,
                alreadyClaimed = false,
                claimedDay = claimedDay,
                reward = reward,
                newSoftCurrencyTotal = newSoft,
                newPremiumCurrencyTotal = newHard,
                claimedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                previewAfterClaim = previewAfter
            };

            if (verboseLogs)
            {
                Debug.Log(
                    "[DailyLoginRewardService] ClaimTodayAsync success -> " +
                    "ClaimedDay=" + claimedDay +
                    " | RewardType=" + reward.rewardType +
                    " | Amount=" + reward.amount +
                    " | ChestType=" + reward.chestType +
                    " | NewSoft=" + newSoft +
                    " | NewHard=" + newHard +
                    " | ClaimDate=" + claimDateString,
                    this);
            }

            PreviewUpdated?.Invoke(previewAfter);
            ClaimCompleted?.Invoke(result);
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginRewardService] ClaimTodayAsync failed -> " + ex, this);

            DailyLoginClaimResult failResult = DailyLoginClaimResult.Failed(GetPreview());
            ClaimCompleted?.Invoke(failResult);
            return failResult;
        }
        finally
        {
            isBusy = false;
        }
    }

    private DailyLoginPreviewState BuildPreview(ProfileSnapshot snapshot)
    {
        DailyLoginPreviewState preview = new DailyLoginPreviewState();
        List<DailyLoginRewardDefinition> normalizedRewards = GetNormalizedRewards();

        if (snapshot == null || !snapshot.IsValid())
        {
            preview.isReady = false;
            preview.currentStreakDay = 0;
            preview.nextClaimDay = 1;
            preview.nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            preview.nextResetUnixSeconds = GetNextResetUnixSeconds(preview.nowUnixSeconds);
            preview.days = BuildFallbackDays(normalizedRewards, 1);
            return preview;
        }

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        DateTime currentRewardDate = GetCurrentRewardDate();
        DateTime? lastClaimRewardDate = ParseRewardDate(snapshot.LastDailyLoginClaimDateUtc);

        bool alreadyClaimedToday =
            lastClaimRewardDate.HasValue &&
            lastClaimRewardDate.Value.Date == currentRewardDate.Date;

        int storedStreak = Mathf.Clamp(snapshot.ConsecutiveLoginDays, 0, 7);
        bool continuesStreak =
            lastClaimRewardDate.HasValue &&
            lastClaimRewardDate.Value.Date == currentRewardDate.Date.AddDays(-1);

        int claimableDay;
        if (alreadyClaimedToday)
        {
            claimableDay = storedStreak >= 7 ? 1 : Mathf.Clamp(storedStreak + 1, 1, 7);
        }
        else if (continuesStreak)
        {
            claimableDay = Mathf.Clamp(storedStreak + 1, 1, 7);
        }
        else
        {
            claimableDay = 1;
        }

        int highlightedTodayDay = alreadyClaimedToday
            ? Mathf.Clamp(Mathf.Max(1, storedStreak), 1, 7)
            : claimableDay;

        DailyLoginDayState[] states = new DailyLoginDayState[normalizedRewards.Count];

        for (int i = 0; i < normalizedRewards.Count; i++)
        {
            DailyLoginRewardDefinition reward = normalizedRewards[i];

            bool isClaimed;
            if (alreadyClaimedToday)
                isClaimed = reward.dayIndex <= highlightedTodayDay;
            else if (claimableDay <= 1)
                isClaimed = false;
            else
                isClaimed = reward.dayIndex < claimableDay;

            bool isToday = reward.dayIndex == highlightedTodayDay;
            bool isClaimable = !alreadyClaimedToday && reward.dayIndex == claimableDay;
            bool isMissed = !isClaimed && !isToday && !isClaimable;

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
        preview.currentStreakDay = storedStreak;
        preview.nextClaimDay = claimableDay;
        preview.nowUnixSeconds = nowUnix;
        preview.nextResetUnixSeconds = GetNextResetUnixSeconds(nowUnix);
        preview.days = states;
        return preview;
    }

    private DailyLoginDayState[] BuildFallbackDays(List<DailyLoginRewardDefinition> normalizedRewards, int todayDay)
    {
        DailyLoginDayState[] states = new DailyLoginDayState[normalizedRewards.Count];

        for (int i = 0; i < normalizedRewards.Count; i++)
        {
            DailyLoginRewardDefinition reward = normalizedRewards[i];
            bool isToday = reward.dayIndex == todayDay;

            states[i] = new DailyLoginDayState
            {
                dayIndex = reward.dayIndex,
                reward = reward,
                isClaimed = false,
                isToday = isToday,
                isClaimable = isToday,
                isMissed = !isToday
            };
        }

        return states;
    }

    private DailyLoginDayState? GetTodayState(DailyLoginDayState[] days)
    {
        if (days == null)
            return null;

        for (int i = 0; i < days.Length; i++)
        {
            if (days[i].isToday)
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
                DailyLoginRewardDefinition reward = rewards[i];
                reward.dayIndex = Mathf.Clamp(reward.dayIndex <= 0 ? i + 1 : reward.dayIndex, 1, 7);
                reward.amount = Mathf.Max(0, reward.amount);
                list.Add(reward);
            }
        }

        list.Sort((a, b) => a.dayIndex.CompareTo(b.dayIndex));

        if (list.Count == 0)
        {
            for (int i = 1; i <= 7; i++)
            {
                list.Add(new DailyLoginRewardDefinition
                {
                    dayIndex = i,
                    rewardType = DailyLoginRewardType.SoftCurrency,
                    amount = 0,
                    chestType = ChestType.Random,
                    label = string.Empty
                });
            }
        }

        return list;
    }

    private void GrantChestReward(DailyLoginRewardDefinition reward)
    {
        ResolveDependencies();

        if (playerChestSlotInventory == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] Chest reward skipped: PlayerChestSlotInventory not found.", this);
            return;
        }

        int count = Mathf.Max(1, reward.amount);
        ChestType chestType = reward.chestType;

        for (int i = 0; i < count; i++)
            playerChestSlotInventory.AwardChest(chestType);

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] GrantChestReward -> " +
                "ChestType=" + chestType +
                " | Count=" + count,
                this);
        }
    }

    private ProfileSnapshot GetCurrentSnapshot()
    {
        ProfileService profileService = GetProfileService();
        if (profileService != null && profileService.CurrentProfile != null && profileService.CurrentProfile.IsValid())
            return profileService.CurrentProfile;

        return null;
    }

    private ProfileService GetProfileService()
    {
        GameCompositionRoot root = GameCompositionRoot.Instance;
        if (root == null || root.ProfileService == null)
            return null;

        return root.ProfileService as ProfileService;
    }

    private void ResolveDependencies()
    {
        if (playerProfileManager == null)
            playerProfileManager = PlayerProfileManager.Instance;

        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;
    }

    private DateTime GetCurrentRewardDate()
    {
        DateTime utcNow = DateTime.UtcNow;

        if (!useUtcDay)
            return utcNow.Date;

        DateTime shifted = utcNow.AddHours(-resetHourUtc);
        return shifted.Date;
    }

    private long GetNextResetUnixSeconds(long nowUnix)
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(nowUnix);
        DateTime utcNow = now.UtcDateTime;

        DateTime nextResetUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddHours(resetHourUtc);

        if (utcNow.Hour < resetHourUtc)
        {
            nextResetUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc)
                .AddHours(resetHourUtc);
        }

        return new DateTimeOffset(nextResetUtc).ToUnixTimeSeconds();
    }

    private static string FormatRewardDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTime? ParseRewardDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime fallback))
        {
            return fallback.Date;
        }

        return null;
    }
}
