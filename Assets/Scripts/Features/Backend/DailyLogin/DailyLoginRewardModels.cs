using System;
using UnityEngine;

public enum DailyLoginRewardType
{
    None = 0,
    SoftCurrency = 1,
    PremiumCurrency = 2,
    Chest = 3,
    FreeLuckyShot = 4
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
}

[Serializable]
public struct DailyLoginDayState
{
    public int dayIndex;
    public DailyLoginRewardDefinition reward;
    public bool isToday;
    public bool isClaimable;
    public bool isClaimed;
    public bool isMissed;

    public DailyLoginRewardType rewardType => reward.rewardType;
    public int amount => reward.amount;
    public ChestType chestType => reward.chestType;
    public string customLabel => reward.customLabel;
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

    public int nextClaimDayIndex => nextClaimDay;

    public bool canClaimNow
    {
        get
        {
            if (days == null)
                return false;

            for (int i = 0; i < days.Length; i++)
            {
                if (days[i].isClaimable)
                    return true;
            }

            return false;
        }
    }
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
    public string failureReason;

    public int claimedDayIndex => claimedDay;
    public DailyLoginRewardType rewardType => reward.rewardType;
    public int amount => reward.amount;
    public ChestType chestType => reward.chestType;
    public string customLabel => reward.customLabel;
    public int updatedStreakDay => previewAfterClaim.currentStreakDay;
    public long nextResetUnixSeconds => previewAfterClaim.nextResetUnixSeconds;

    public static DailyLoginClaimResult Failed(DailyLoginPreviewState preview, string reason = "Claim failed.")
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = false,
            claimedDay = Mathf.Clamp(preview.nextClaimDay, 1, 7),
            reward = default,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0,
            previewAfterClaim = preview,
            failureReason = string.IsNullOrWhiteSpace(reason) ? "Claim failed." : reason
        };
    }

    public static DailyLoginClaimResult AlreadyClaimed(DailyLoginPreviewState preview)
    {
        return new DailyLoginClaimResult
        {
            success = false,
            alreadyClaimed = true,
            claimedDay = Mathf.Clamp(preview.nextClaimDay, 1, 7),
            reward = default,
            newSoftCurrencyTotal = 0,
            newPremiumCurrencyTotal = 0,
            claimedAtUnixSeconds = 0,
            previewAfterClaim = preview,
            failureReason = "Reward already claimed today."
        };
    }
}
