using System;
using UnityEngine;

[Serializable]
public struct OfflineBotRewardRule
{
    public bool countAsMatchPlayed;
    public bool countAsWin;
    public bool countAsLoss;

    [Min(0)] public int xpReward;
    [Min(0)] public int softCurrencyReward;

    public bool grantChest;
    public ChestType chestType;

    public static OfflineBotRewardRule Create(
        bool countAsMatchPlayed,
        bool countAsWin,
        bool countAsLoss,
        int xpReward,
        int softCurrencyReward,
        bool grantChest,
        ChestType chestType)
    {
        return new OfflineBotRewardRule
        {
            countAsMatchPlayed = countAsMatchPlayed,
            countAsWin = countAsWin,
            countAsLoss = countAsLoss,
            xpReward = Mathf.Max(0, xpReward),
            softCurrencyReward = Mathf.Max(0, softCurrencyReward),
            grantChest = grantChest,
            chestType = chestType
        };
    }
}

[Serializable]
public struct OfflineBotDifficultyRewardSet
{
    public OfflineBotRewardRule win;
    public OfflineBotRewardRule loss;
    public OfflineBotRewardRule surrenderLoss;
}

[CreateAssetMenu(
    fileName = "OfflineBotMatchRewardsConfig",
    menuName = "Uncball Arena/Bot/Offline Bot Match Rewards Config")]
public class OfflineBotMatchRewardsConfig : ScriptableObject
{
    [Header("Easy")]
    [SerializeField]
    private OfflineBotDifficultyRewardSet easy = new OfflineBotDifficultyRewardSet
    {
        win = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 10,
            softCurrencyReward = 12,
            grantChest = false,
            chestType = ChestType.Random
        },
        loss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 4,
            softCurrencyReward = 4,
            grantChest = false,
            chestType = ChestType.Random
        },
        surrenderLoss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random
        }
    };

    [Header("Medium")]
    [SerializeField]
    private OfflineBotDifficultyRewardSet medium = new OfflineBotDifficultyRewardSet
    {
        win = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 14,
            softCurrencyReward = 18,
            grantChest = false,
            chestType = ChestType.Random
        },
        loss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 5,
            softCurrencyReward = 5,
            grantChest = false,
            chestType = ChestType.Random
        },
        surrenderLoss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random
        }
    };

    [Header("Hard")]
    [SerializeField]
    private OfflineBotDifficultyRewardSet hard = new OfflineBotDifficultyRewardSet
    {
        win = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 18,
            softCurrencyReward = 24,
            grantChest = false,
            chestType = ChestType.Random
        },
        loss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 6,
            softCurrencyReward = 6,
            grantChest = false,
            chestType = ChestType.Random
        },
        surrenderLoss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random
        }
    };

    [Header("Unbeatable / Insane")]
    [SerializeField]
    private OfflineBotDifficultyRewardSet unbeatable = new OfflineBotDifficultyRewardSet
    {
        win = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 24,
            softCurrencyReward = 32,
            grantChest = true,
            chestType = ChestType.GuaranteedRare
        },
        loss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 8,
            softCurrencyReward = 8,
            grantChest = false,
            chestType = ChestType.Random
        },
        surrenderLoss = new OfflineBotRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random
        }
    };

    public OfflineBotRewardRule GetWinRule(BotDifficulty difficulty)
    {
        return GetSet(difficulty).win;
    }

    public OfflineBotRewardRule GetLossRule(BotDifficulty difficulty)
    {
        return GetSet(difficulty).loss;
    }

    public OfflineBotRewardRule GetSurrenderLossRule(BotDifficulty difficulty)
    {
        return GetSet(difficulty).surrenderLoss;
    }

    private OfflineBotDifficultyRewardSet GetSet(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return easy;
            case BotDifficulty.Medium:
                return medium;
            case BotDifficulty.Hard:
                return hard;
            case BotDifficulty.Unbeatable:
                return unbeatable;
            default:
                return medium;
        }
    }
}