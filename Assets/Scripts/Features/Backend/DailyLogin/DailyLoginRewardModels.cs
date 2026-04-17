using System;

[Serializable]
public enum DailyLoginRewardType
{
    None = 0,
    SoftCurrency = 1,
    PremiumCurrency = 2,
    Chest = 3
}

[Serializable]
public struct DailyLoginRewardDefinition
{
    public int dayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string label;

    public bool HasChest => rewardType == DailyLoginRewardType.Chest && amount > 0;
    public bool HasSoft => rewardType == DailyLoginRewardType.SoftCurrency && amount > 0;
    public bool HasPremium => rewardType == DailyLoginRewardType.PremiumCurrency && amount > 0;
}

[Serializable]
public struct DailyLoginDayState
{
    public int dayIndex;
    public DailyLoginRewardDefinition reward;
    public bool isClaimed;
    public bool isToday;
    public bool isClaimable;
    public bool isMissed;
}

[Serializable]
public struct DailyLoginPreviewState
{
    public bool isReady;
    public int currentStreakDay;
    public int nextClaimDay;
    public long nowUnixSeconds;
    public long nextResetUnixSeconds;
    public DailyLoginDayState[] days;
}

[Serializable]
public struct DailyLoginClaimResult
{
    public bool success;
    public bool alreadyClaimed;
    public int claimedDay;
    public DailyLoginRewardDefinition reward;
    public int newSoftCurrencyTotal;
    public int newPremiumCurrencyTotal;
    public long claimedAtUnixSeconds;
    public DailyLoginPreviewState previewAfterClaim;

    public static DailyLoginClaimResult Failed(DailyLoginPreviewState preview)
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = false,
            claimedDay = 0,
            reward = default,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0,
            previewAfterClaim = preview
        };
    }

    public static DailyLoginClaimResult AlreadyClaimed(DailyLoginPreviewState preview)
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = true,
            claimedDay = preview.nextClaimDay,
            reward = default,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0,
            previewAfterClaim = preview
        };
    }
}
