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
}

[Serializable]
public struct DailyLoginDayState
{
    public int dayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string customLabel;

    public bool isToday;
    public bool isClaimable;
    public bool isClaimed;
}

[Serializable]
public struct DailyLoginPreviewState
{
    public bool isReady;
    public int currentStreakDay;
    public int nextClaimDayIndex;
    public bool canClaimNow;
    public long nextResetUnixSeconds;
    public DailyLoginDayState[] days;
}

[Serializable]
public struct DailyLoginClaimResult
{
    public bool success;
    public int claimedDayIndex;
    public DailyLoginRewardType rewardType;
    public int amount;
    public ChestType chestType;
    public string customLabel;

    public int updatedStreakDay;
    public long nextResetUnixSeconds;
    public string failureReason;
}