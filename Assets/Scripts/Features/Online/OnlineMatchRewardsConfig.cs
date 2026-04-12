using System;
using UnityEngine;

public enum OnlineRewardCategory
{
    None = 0,
    NormalCompletionWin = 1,
    NormalCompletionLoss = 2,
    Draw = 3,
    DisconnectWin = 4,
    DisconnectLoss = 5,
    SurrenderWin = 6,
    SurrenderLoss = 7,
    ReconnectTimeoutLoss = 8,
    PrematchHostLeftWin = 9,
    MatchCancelled = 10,
    QueueTimeout = 11
}

[Serializable]
public struct OnlineRewardRule
{
    public bool countAsMatchPlayed;
    public bool countAsWin;
    public bool countAsLoss;
    public int xpReward;
    public int softCurrencyReward;
    public int premiumCurrencyReward;
    public bool grantChest;
    public ChestType chestType;
    public int rankedLpDelta;

    public static OnlineRewardRule Create(
        bool countAsMatchPlayed,
        bool countAsWin,
        bool countAsLoss,
        int xpReward,
        int softCurrencyReward,
        int premiumCurrencyReward,
        bool grantChest,
        ChestType chestType,
        int rankedLpDelta)
    {
        return new OnlineRewardRule
        {
            countAsMatchPlayed = countAsMatchPlayed,
            countAsWin = countAsWin,
            countAsLoss = countAsLoss,
            xpReward = xpReward,
            softCurrencyReward = softCurrencyReward,
            premiumCurrencyReward = premiumCurrencyReward,
            grantChest = grantChest,
            chestType = chestType,
            rankedLpDelta = rankedLpDelta
        };
    }
}

[Serializable]
public struct OnlineRewardRuleSet
{
    public OnlineRewardRule normal;
    public OnlineRewardRule ranked;

    public OnlineRewardRule GetRule(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? ranked : normal;
    }
}

[CreateAssetMenu(
    fileName = "OnlineMatchRewardsConfig",
    menuName = "Uncball Arena/Online/Online Match Rewards Config")]
public class OnlineMatchRewardsConfig : ScriptableObject
{
    [Header("Normal Completion")]
    [SerializeField]
    private OnlineRewardRuleSet normalCompletionWin = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 20,
            softCurrencyReward = 30,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 25,
            softCurrencyReward = 35,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 25
        }
    };

    [SerializeField]
    private OnlineRewardRuleSet normalCompletionLoss = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 12,
            softCurrencyReward = 15,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 15,
            softCurrencyReward = 12,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = -20
        }
    };

    [SerializeField]
    private OnlineRewardRuleSet draw = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = false,
            xpReward = 15,
            softCurrencyReward = 20,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = false,
            xpReward = 18,
            softCurrencyReward = 18,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        }
    };

    [Header("Disconnect")]
    [SerializeField]
    private OnlineRewardRuleSet disconnectWin = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 20,
            softCurrencyReward = 30,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 25,
            softCurrencyReward = 35,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 25
        }
    };

    [SerializeField]
    private OnlineRewardRuleSet disconnectLoss = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = -20
        }
    };

    [Header("Surrender")]
    [SerializeField]
    private OnlineRewardRuleSet surrenderWin = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 20,
            softCurrencyReward = 30,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = true,
            countAsLoss = false,
            xpReward = 25,
            softCurrencyReward = 35,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 25
        }
    };

    [SerializeField]
    private OnlineRewardRuleSet surrenderLoss = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = -20
        }
    };

    [Header("Reconnect Timeout")]
    [SerializeField]
    private OnlineRewardRuleSet reconnectTimeoutLoss = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = true,
            countAsWin = false,
            countAsLoss = true,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = -20
        }
    };

    [Header("Prematch Host Left")]
    [SerializeField]
    private OnlineRewardRuleSet prematchHostLeftWin = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule
        {
            countAsMatchPlayed = false,
            countAsWin = false,
            countAsLoss = false,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        },
        ranked = new OnlineRewardRule
        {
            countAsMatchPlayed = false,
            countAsWin = false,
            countAsLoss = false,
            xpReward = 0,
            softCurrencyReward = 0,
            premiumCurrencyReward = 0,
            grantChest = false,
            chestType = ChestType.Random,
            rankedLpDelta = 0
        }
    };

    [Header("Neutral")]
    [SerializeField]
    private OnlineRewardRuleSet matchCancelled = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule(),
        ranked = new OnlineRewardRule()
    };

    [SerializeField]
    private OnlineRewardRuleSet queueTimeout = new OnlineRewardRuleSet
    {
        normal = new OnlineRewardRule(),
        ranked = new OnlineRewardRule()
    };

    public OnlineRewardRule GetRule(OnlineRewardCategory category, QueueType queueType)
    {
        switch (category)
        {
            case OnlineRewardCategory.NormalCompletionWin:
                return normalCompletionWin.GetRule(queueType);

            case OnlineRewardCategory.NormalCompletionLoss:
                return normalCompletionLoss.GetRule(queueType);

            case OnlineRewardCategory.Draw:
                return draw.GetRule(queueType);

            case OnlineRewardCategory.DisconnectWin:
                return disconnectWin.GetRule(queueType);

            case OnlineRewardCategory.DisconnectLoss:
                return disconnectLoss.GetRule(queueType);

            case OnlineRewardCategory.SurrenderWin:
                return surrenderWin.GetRule(queueType);

            case OnlineRewardCategory.SurrenderLoss:
                return surrenderLoss.GetRule(queueType);

            case OnlineRewardCategory.ReconnectTimeoutLoss:
                return reconnectTimeoutLoss.GetRule(queueType);

            case OnlineRewardCategory.PrematchHostLeftWin:
                return prematchHostLeftWin.GetRule(queueType);

            case OnlineRewardCategory.MatchCancelled:
                return matchCancelled.GetRule(queueType);

            case OnlineRewardCategory.QueueTimeout:
                return queueTimeout.GetRule(queueType);

            case OnlineRewardCategory.None:
            default:
                return default;
        }
    }
}
