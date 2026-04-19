using System;
using UnityEngine;

public enum DailyLoginRewardType
{
    SoftCurrency = 0,
    PremiumCurrency = 1,
    Chest = 2,
    FreeLuckyShot = 3
}

[Serializable]
public struct DailyLoginRewardDefinition
{
    public int dayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string customLabel;

    public string label
    {
        get => customLabel;
        set => customLabel = value;
    }

    public int claimedDay
    {
        get => dayIndex;
        set => dayIndex = value;
    }
}

[Serializable]
public struct DailyLoginDayState
{
    public int dayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string customLabel;
    public bool isClaimed;
    public bool isClaimable;
    public bool isToday;
    public bool isMissed;

    public int dayNumber
    {
        get => dayIndex;
        set => dayIndex = value;
    }

    public bool canClaim
    {
        get => isClaimable;
        set => isClaimable = value;
    }

    public bool chestGranted
    {
        get => rewardType == DailyLoginRewardType.Chest;
        set { }
    }

    public int softCurrency
    {
        get => rewardType == DailyLoginRewardType.SoftCurrency ? amount : 0;
        set
        {
            if (value > 0)
            {
                rewardType = DailyLoginRewardType.SoftCurrency;
                amount = value;
            }
        }
    }

    public int premiumCurrency
    {
        get => rewardType == DailyLoginRewardType.PremiumCurrency ? amount : 0;
        set
        {
            if (value > 0)
            {
                rewardType = DailyLoginRewardType.PremiumCurrency;
                amount = value;
            }
        }
    }

    public string label
    {
        get => customLabel;
        set => customLabel = value;
    }
}

[Serializable]
public struct DailyLoginPreviewState
{
    public bool isReady;
    public int currentStreakDay;
    public int nextClaimDayIndex;
    public bool canClaimNow;
    public bool alreadyClaimedToday;
    public string lastClaimDateUtc;
    public long nextResetUnixSeconds;
    public DailyLoginDayState[] days;

    public int todayDayIndex
    {
        get => nextClaimDayIndex;
        set => nextClaimDayIndex = value;
    }

    public int nextClaimDay
    {
        get => nextClaimDayIndex;
        set => nextClaimDayIndex = value;
    }

    public bool canClaimToday
    {
        get => canClaimNow;
        set => canClaimNow = value;
    }

    public long nowUnixSeconds
    {
        get => nextResetUnixSeconds;
        set => nextResetUnixSeconds = value;
    }
}

[Serializable]
public struct DailyLoginClaimResult
{
    public bool success;
    public bool alreadyClaimed;
    public string failureReason;

    public int claimedDayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string customLabel;

    public int newSoftCurrencyTotal;
    public int newPremiumCurrencyTotal;
    public long claimedAtUnixSeconds;

    public DailyLoginPreviewState previewAfterClaim;

    public int claimedDay
    {
        get => claimedDayIndex;
        set => claimedDayIndex = value;
    }

    public DailyLoginRewardDefinition reward
    {
        get
        {
            return new DailyLoginRewardDefinition
            {
                dayIndex = claimedDayIndex,
                rewardType = rewardType,
                amount = amount,
                chestType = chestType,
                customLabel = customLabel
            };
        }
        set
        {
            claimedDayIndex = value.dayIndex;
            rewardType = value.rewardType;
            amount = value.amount;
            chestType = value.chestType;
            customLabel = value.customLabel;
        }
    }

    public static DailyLoginClaimResult Success(
        int claimedDayIndex,
        DailyLoginRewardType rewardType,
        int amount,
        ChestType chestType,
        string customLabel,
        int newSoftCurrencyTotal,
        int newPremiumCurrencyTotal,
        long claimedAtUnixSeconds,
        DailyLoginPreviewState previewAfterClaim)
    {
        return new DailyLoginClaimResult
        {
            success = true,
            alreadyClaimed = false,
            failureReason = string.Empty,
            claimedDayIndex = claimedDayIndex,
            rewardType = rewardType,
            amount = amount,
            chestType = chestType,
            customLabel = customLabel ?? string.Empty,
            newSoftCurrencyTotal = newSoftCurrencyTotal,
            newPremiumCurrencyTotal = newPremiumCurrencyTotal,
            claimedAtUnixSeconds = claimedAtUnixSeconds,
            previewAfterClaim = previewAfterClaim
        };
    }

    public static DailyLoginClaimResult Failed(DailyLoginPreviewState previewAfterClaim, string failureReason = "")
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = false,
            failureReason = failureReason ?? string.Empty,
            claimedDayIndex = 0,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 0,
            chestType = default,
            customLabel = string.Empty,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0L,
            previewAfterClaim = previewAfterClaim
        };
    }

    public static DailyLoginClaimResult AlreadyClaimed(DailyLoginPreviewState previewAfterClaim)
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = true,
            failureReason = string.Empty,
            claimedDayIndex = 0,
            rewardType = DailyLoginRewardType.SoftCurrency,
            amount = 0,
            chestType = default,
            customLabel = string.Empty,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0L,
            previewAfterClaim = previewAfterClaim
        };
    }
}
