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

        int nextDayIndex = Mathf.Clamp(streak + 1, 1, CycleLength);
        bool canClaimNow = !alreadyClaimedToday;

        DailyLoginPreviewState preview = new DailyLoginPreviewState
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

        return preview;
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

    public void DebugResetDailyLogin()
    {
        PlayerPrefs.DeleteKey(LastClaimUnixKey);
        PlayerPrefs.DeleteKey(CurrentStreakKey);
        PlayerPrefs.DeleteKey(LastClaimDayIndexKey);
        PlayerPrefs.Save();

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] DebugResetDailyLogin -> state cleared.", this);
    }

    public void DebugForceClaimAvailableToday()
    {
        int streak = Mathf.Max(0, PlayerPrefs.GetInt(CurrentStreakKey, 0));
        int previousDay = Mathf.Clamp(streak, 0, CycleLength - 1);

        long fakeYesterdayUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();

        PlayerPrefs.SetInt(LastClaimUnixKey, SafeUnixToInt(fakeYesterdayUnix));
        PlayerPrefs.SetInt(CurrentStreakKey, previousDay);
        PlayerPrefs.SetInt(LastClaimDayIndexKey, previousDay);
        PlayerPrefs.Save();

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardService] DebugForceClaimAvailableToday -> " +
                "LastClaimUnix=fakeYesterday | CurrentStreak=" + previousDay,
                this);
        }
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
        PlayerProfileManager profileManager = FindProfileManager();
        if (profileManager == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantSoftCurrency failed -> PlayerProfileManager not found.", this);
            return;
        }

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        profileManager.AddSoftCurrency(safeAmount);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantSoftCurrency -> " + safeAmount, this);
    }

    private void GrantPremiumCurrency(int amount)
    {
        PlayerProfileManager profileManager = FindProfileManager();
        if (profileManager == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantPremiumCurrency failed -> PlayerProfileManager not found.", this);
            return;
        }

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        profileManager.AddPremiumCurrency(safeAmount);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantPremiumCurrency -> " + safeAmount, this);
    }

    private void GrantChest(ChestType chestType, int amount)
    {
        PlayerChestSlotInventory inventory = FindChestInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[DailyLoginRewardService] GrantChest failed -> PlayerChestSlotInventory not found.", this);
            return;
        }

        int safeAmount = Mathf.Max(1, amount);

        for (int i = 0; i < safeAmount; i++)
        {
            bool granted = TryInvokeChestGrant(inventory, chestType);

            if (!granted)
            {
                Debug.LogWarning(
                    "[DailyLoginRewardService] GrantChest failed -> no supported inventory method found for chest reward.",
                    this);
                return;
            }
        }

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantChest -> Type=" + chestType + " | Amount=" + safeAmount, this);
    }

    private bool TryInvokeChestGrant(PlayerChestSlotInventory inventory, ChestType chestType)
    {
        if (inventory == null)
            return false;

        Type inventoryType = inventory.GetType();

        string[] candidateNames =
        {
            "TryAddRewardChestFromBackendOrSystem",
            "TryAddRewardChest",
            "TryAddChestReward",
            "TryGrantChestReward",
            "TryAddChest",
            "AddChestReward",
            "GrantChestReward"
        };

        object[] singleChestArgument = { chestType };
        object[] singleChestAndAmountArgument = { chestType, 1 };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            try
            {
                MethodInfo method = inventoryType.GetMethod(
                    candidateNames[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (method == null)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ChestType))
                {
                    object result = method.Invoke(inventory, singleChestArgument);

                    if (method.ReturnType == typeof(bool))
                        return result is bool b && b;

                    return true;
                }

                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(ChestType) &&
                    parameters[1].ParameterType == typeof(int))
                {
                    object result = method.Invoke(inventory, singleChestAndAmountArgument);

                    if (method.ReturnType == typeof(bool))
                        return result is bool b && b;

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[DailyLoginRewardService] Chest grant reflection failed on method " +
                    candidateNames[i] + " -> " + ex.Message,
                    this);
            }
        }

        return false;
    }

    private void GrantFreeLuckyShot(int amount)
    {
        int safeAmount = Mathf.Max(1, amount);

        if (verboseLogs)
            Debug.Log("[DailyLoginRewardService] GrantFreeLuckyShot -> Amount=" + safeAmount + " (placeholder only).", this);
    }

    private PlayerProfileManager FindProfileManager()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<PlayerProfileManager>();
#else
        return FindObjectOfType<PlayerProfileManager>();
#endif
    }

    private PlayerChestSlotInventory FindChestInventory()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<PlayerChestSlotInventory>();
#else
        return FindObjectOfType<PlayerChestSlotInventory>();
#endif
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private long GetNextUtcMidnightUnix(long nowUnix)
    {
        DateTime utcNow = DateTimeOffset.FromUnixTimeSeconds(nowUnix).UtcDateTime;
        DateTime nextMidnight = utcNow.Date.AddDays(1);
        return new DateTimeOffset(nextMidnight, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private bool IsSameUtcDay(long unixA, long unixB)
    {
        if (unixA <= 0 || unixB <= 0)
            return false;

        DateTime a = DateTimeOffset.FromUnixTimeSeconds(unixA).UtcDateTime.Date;
        DateTime b = DateTimeOffset.FromUnixTimeSeconds(unixB).UtcDateTime.Date;
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
        if (unix <= 0)
            return 0;

        if (unix > int.MaxValue)
            return int.MaxValue;

        return (int)unix;
    }
}