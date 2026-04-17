using System;
using System.Reflection;
using UnityEngine;

public class DailyLoginRewardService : MonoBehaviour
{
    public static DailyLoginRewardService Instance { get; private set; }

    [Header("Definitions")]
    [SerializeField]
    private DailyLoginRewardDefinition[] rewardDefinitions =
    {
        new DailyLoginRewardDefinition
        {
            dayIndex = 1,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 50,
            chestType = ChestType.Random,
            customLabel = ""
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 2,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 75,
            chestType = ChestType.Random,
            customLabel = ""
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 3,
            rewardType = DailyLoginRewardType.Chest,
            amount = 1,
            chestType = ChestType.Random,
            customLabel = "CHEST"
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 4,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 100,
            chestType = ChestType.Random,
            customLabel = ""
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 5,
            rewardType = DailyLoginRewardType.PremiumCurrency,
            amount = 10,
            chestType = ChestType.Random,
            customLabel = ""
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 6,
            rewardType = DailyLoginRewardType.Chest,
            amount = 1,
            chestType = ChestType.Random,
            customLabel = "RARE CHEST"
        },
        new DailyLoginRewardDefinition
        {
            dayIndex = 7,
            rewardType = DailyLoginRewardType.Chest,
            amount = 1,
            chestType = ChestType.Random,
            customLabel = "EPIC CHEST"
        }
    };

    [Header("Options")]
    [SerializeField] private bool verboseLogs = true;

    private const int CycleLength = 7;
    private const string LastClaimUnixKey = "DAILY_LOGIN_LAST_CLAIM_UNIX";
    private const string CurrentStreakKey = "DAILY_LOGIN_CURRENT_STREAK";
    private const string LastClaimDayIndexKey = "DAILY_LOGIN_LAST_CLAIM_DAY_INDEX";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] Awake -> ready.", this);
    }

    public DailyLoginRewardDefinition[] GetDefinitions()
    {
        DailyLoginRewardDefinition[] copy = new DailyLoginRewardDefinition[rewardDefinitions.Length];
        Array.Copy(rewardDefinitions, copy, rewardDefinitions.Length);
        return copy;
    }

    public DailyLoginPreviewState GetPreviewState()
    {
        long now = GetNowUnixSeconds();
        long lastClaimUnix = PlayerPrefs.GetInt(LastClaimUnixKey, 0);
        int streak = Mathf.Max(0, PlayerPrefs.GetInt(CurrentStreakKey, 0));

        bool alreadyClaimedToday = IsSameUtcDay(lastClaimUnix, now);
        bool streakBroken = lastClaimUnix > 0 && !alreadyClaimedToday && !IsYesterdayUtcDay(lastClaimUnix, now);

        if (streakBroken)
            streak = 0;

        int nextDayIndex = Mathf.Clamp((streak % CycleLength) + 1, 1, CycleLength);
        bool canClaimNow = !alreadyClaimedToday;

        DailyLoginPreviewState state = new DailyLoginPreviewState
        {
            isReady = true,
            currentStreakDay = streak,
            nextClaimDayIndex = nextDayIndex,
            canClaimNow = canClaimNow,
            nextResetUnixSeconds = GetNextUtcMidnightUnix(now),
            days = BuildDayStates(streak, nextDayIndex, canClaimNow)
        };

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] GetPreviewState -> " +
                "Streak=" + streak +
                " | NextDay=" + nextDayIndex +
                " | CanClaim=" + canClaimNow +
                " | LastClaimUnix=" + lastClaimUnix,
                this);
        }

        return state;
    }

    public DailyLoginClaimResult ClaimTodayReward()
    {
        DailyLoginPreviewState preview = GetPreviewState();

        if (!preview.canClaimNow)
        {
            return new DailyLoginClaimResult
            {
                success = false,
                claimedDayIndex = preview.nextClaimDayIndex,
                rewardType = DailyLoginRewardType.None,
                amount = 0,
                chestType = ChestType.Random,
                customLabel = string.Empty,
                updatedStreakDay = preview.currentStreakDay,
                nextResetUnixSeconds = preview.nextResetUnixSeconds,
                failureReason = "Reward already claimed today."
            };
        }

        DailyLoginRewardDefinition definition = GetDefinitionForDay(preview.nextClaimDayIndex);

        GrantReward(definition);

        int updatedStreak = Mathf.Clamp(preview.currentStreakDay + 1, 1, CycleLength);
        long now = GetNowUnixSeconds();

        PlayerPrefs.SetInt(LastClaimUnixKey, SafeUnixToInt(now));
        PlayerPrefs.SetInt(CurrentStreakKey, updatedStreak);
        PlayerPrefs.SetInt(LastClaimDayIndexKey, definition.dayIndex);
        PlayerPrefs.Save();

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] ClaimTodayReward -> " +
                "Day=" + definition.dayIndex +
                " | Type=" + definition.rewardType +
                " | Amount=" + definition.amount +
                " | ChestType=" + definition.chestType +
                " | UpdatedStreak=" + updatedStreak,
                this);
        }

        return new DailyLoginClaimResult
        {
            success = true,
            claimedDayIndex = definition.dayIndex,
            rewardType = definition.rewardType,
            amount = definition.amount,
            chestType = definition.chestType,
            customLabel = definition.customLabel,
            updatedStreakDay = updatedStreak,
            nextResetUnixSeconds = GetNextUtcMidnightUnix(now),
            failureReason = string.Empty
        };
    }

    private DailyLoginDayState[] BuildDayStates(int currentStreak, int nextClaimDayIndex, bool canClaimNow)
    {
        DailyLoginDayState[] result = new DailyLoginDayState[CycleLength];

        for (int i = 1; i <= CycleLength; i++)
        {
            DailyLoginRewardDefinition definition = GetDefinitionForDay(i);

            bool isClaimed = i <= currentStreak;
            bool isToday = i == nextClaimDayIndex;
            bool isClaimable = isToday && canClaimNow;

            result[i - 1] = new DailyLoginDayState
            {
                dayIndex = i,
                rewardType = definition.rewardType,
                amount = definition.amount,
                chestType = definition.chestType,
                customLabel = definition.customLabel,
                isToday = isToday,
                isClaimable = isClaimable,
                isClaimed = isClaimed
            };
        }

        return result;
    }

    private DailyLoginRewardDefinition GetDefinitionForDay(int dayIndex)
    {
        for (int i = 0; i < rewardDefinitions.Length; i++)
        {
            if (rewardDefinitions[i].dayIndex == dayIndex)
                return rewardDefinitions[i];
        }

        return new DailyLoginRewardDefinition
        {
            dayIndex = dayIndex,
            rewardType = DailyLoginRewardType.None,
            amount = 0,
            chestType = ChestType.Random,
            customLabel = string.Empty
        };
    }

    private void GrantReward(DailyLoginRewardDefinition definition)
    {
        switch (definition.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                GrantSoftCurrency(definition.amount);
                break;

            case DailyLoginRewardType.PremiumCurrency:
                GrantPremiumCurrency(definition.amount);
                break;

            case DailyLoginRewardType.Chest:
                GrantChest(definition.chestType, definition.amount);
                break;

            case DailyLoginRewardType.FreeLuckyShot:
                GrantFreeLuckyShot(definition.amount);
                break;
        }
    }

    private void GrantSoftCurrency(int amount)
    {
        PlayerProfileManager profile = PlayerProfileManager.Instance;
        if (profile == null || amount <= 0)
            return;

        profile.AddSoftCurrency(amount);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantSoftCurrency -> " + amount, this);
    }

    private void GrantPremiumCurrency(int amount)
    {
        PlayerProfileManager profile = PlayerProfileManager.Instance;
        if (profile == null || amount <= 0)
            return;

        profile.AddPremiumCurrency(amount);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantPremiumCurrency -> " + amount, this);
    }

    private void GrantChest(ChestType chestType, int amount)
    {
        if (amount <= 0)
            return;

#if UNITY_2023_1_OR_NEWER
        PlayerChestSlotInventory inventory = FindFirstObjectByType<PlayerChestSlotInventory>();
#else
        PlayerChestSlotInventory inventory = FindObjectOfType<PlayerChestSlotInventory>();
#endif
        if (inventory == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[DailyLoginRewardService] GrantChest failed -> PlayerChestSlotInventory not found.", this);
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            bool added = TryGrantChestThroughKnownMethods(inventory, chestType);
            if (!added && verboseLogs)
            {
                Debug.LogWarning(
                    "[DailyLoginRewardService] GrantChest failed -> no compatible inventory method found. " +
                    "ChestType=" + chestType,
                    this);
            }
        }

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantChest -> Type=" + chestType + " | Amount=" + amount, this);
    }

    private bool TryGrantChestThroughKnownMethods(PlayerChestSlotInventory inventory, ChestType chestType)
    {
        Type inventoryType = inventory.GetType();

        string[] candidateNames =
        {
            "TryAddRewardChest",
            "TryAddRewardChestFromBackendOrSystem",
            "TryAddChestReward",
            "TryAddChest",
            "AddRewardChest",
            "AddChestReward",
            "AwardChest"
        };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            MethodInfo method = inventoryType.GetMethod(
                candidateNames[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
                continue;

            ParameterInfo[] parameters = method.GetParameters();

            try
            {
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ChestType))
                {
                    object result = method.Invoke(inventory, new object[] { chestType });
                    return result is bool boolResult ? boolResult : true;
                }

                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(ChestType) &&
                    parameters[1].ParameterType == typeof(string))
                {
                    object result = method.Invoke(inventory, new object[] { chestType, "daily_login" });
                    return result is bool boolResult ? boolResult : true;
                }

                if (parameters.Length == 0)
                {
                    object result = method.Invoke(inventory, null);
                    return result is bool boolResult ? boolResult : true;
                }
            }
            catch (Exception ex)
            {
                if (verboseLogs)
                    Debug.LogWarning("[DailyLoginRewardService] Chest grant reflection failed on method " + candidateNames[i] + " -> " + ex.Message, this);
            }
        }

        return false;
    }

    private void GrantFreeLuckyShot(int amount)
    {
        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantFreeLuckyShot -> Amount=" + amount + " (placeholder only).", this);
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private long GetNextUtcMidnightUnix(long currentUnix)
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(currentUnix);
        DateTimeOffset nextMidnight = new DateTimeOffset(
            now.UtcDateTime.Date.AddDays(1),
            TimeSpan.Zero);

        return nextMidnight.ToUnixTimeSeconds();
    }

    private bool IsSameUtcDay(long aUnix, long bUnix)
    {
        if (aUnix <= 0 || bUnix <= 0)
            return false;

        DateTime a = DateTimeOffset.FromUnixTimeSeconds(aUnix).UtcDateTime.Date;
        DateTime b = DateTimeOffset.FromUnixTimeSeconds(bUnix).UtcDateTime.Date;
        return a == b;
    }

    private bool IsYesterdayUtcDay(long lastClaimUnix, long nowUnix)
    {
        if (lastClaimUnix <= 0 || nowUnix <= 0)
            return false;

        DateTime last = DateTimeOffset.FromUnixTimeSeconds(lastClaimUnix).UtcDateTime.Date;
        DateTime now = DateTimeOffset.FromUnixTimeSeconds(nowUnix).UtcDateTime.Date;
        return last == now.AddDays(-1);
    }

    private int SafeUnixToInt(long unix)
    {
        if (unix < int.MinValue)
            return int.MinValue;

        if (unix > int.MaxValue)
            return int.MaxValue;

        return (int)unix;
    }
}
