using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LevelRewardEntry
{
    [Min(2)]
    public int targetLevel;

    [Min(0)]
    public int softCurrencyReward;

    [Min(0)]
    public int chestCount;

    public ChestType chestType;
}

[CreateAssetMenu(
    fileName = "LevelUpRewardsConfig",
    menuName = "Uncball Arena/Rewards/Level Up Rewards Config")]
public class LevelUpRewardsConfig : ScriptableObject
{
    [Header("Rewards")]
    [SerializeField] private List<LevelRewardEntry> rewardsByLevel = new List<LevelRewardEntry>();

    public IReadOnlyList<LevelRewardEntry> RewardsByLevel => rewardsByLevel;

    public bool TryGetRewardForLevel(int targetLevel, out LevelRewardEntry entry)
    {
        int safeLevel = Mathf.Max(2, targetLevel);

        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            if (rewardsByLevel[i].targetLevel == safeLevel)
            {
                entry = rewardsByLevel[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    public void GetRewardsBetweenLevels(int previousLevel, int newLevel, List<LevelRewardEntry> output)
    {
        if (output == null)
            return;

        output.Clear();

        int fromLevel = Mathf.Max(1, previousLevel);
        int toLevel = Mathf.Max(fromLevel, newLevel);

        if (toLevel <= fromLevel)
            return;

        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            LevelRewardEntry entry = rewardsByLevel[i];

            if (entry.targetLevel > fromLevel && entry.targetLevel <= toLevel)
                output.Add(entry);
        }

        output.Sort((a, b) => a.targetLevel.CompareTo(b.targetLevel));
    }

    public void AutoGenerateRewards()
    {
        rewardsByLevel.Clear();

        for (int level = 2; level <= 100; level++)
        {
            LevelRewardEntry entry = new LevelRewardEntry
            {
                targetLevel = level,
                softCurrencyReward = GetLegacySoftCurrencyReward(level),
                chestCount = GetLegacyChestCount(level),
                chestType = GetLegacyChestType(level)
            };

            rewardsByLevel.Add(entry);
        }
    }

    public void ApplyEconomyV2PresetUpTo9999()
    {
        ApplyEconomyV2PresetUpToLevel(9999);
    }

    public void ApplyEconomyV2PresetUpToLevel(int maxLevel)
    {
        rewardsByLevel.Clear();

        int safeMaxLevel = Mathf.Max(2, maxLevel);

        for (int level = 2; level <= safeMaxLevel; level++)
        {
            LevelRewardEntry entry = new LevelRewardEntry
            {
                targetLevel = level,
                softCurrencyReward = GetEconomyV2SoftCurrencyReward(level),
                chestCount = GetEconomyV2ChestCount(level),
                chestType = GetEconomyV2ChestType(level)
            };

            rewardsByLevel.Add(entry);
        }

        SortAndDeduplicate();
    }

    public void SortAndDeduplicate()
    {
        Dictionary<int, LevelRewardEntry> uniqueByLevel = new Dictionary<int, LevelRewardEntry>();

        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            LevelRewardEntry entry = rewardsByLevel[i];
            int safeLevel = Mathf.Max(2, entry.targetLevel);

            entry.targetLevel = safeLevel;
            entry.softCurrencyReward = Mathf.Max(0, entry.softCurrencyReward);
            entry.chestCount = Mathf.Max(0, entry.chestCount);

            uniqueByLevel[safeLevel] = entry;
        }

        List<int> levels = new List<int>(uniqueByLevel.Keys);
        levels.Sort();

        rewardsByLevel.Clear();

        for (int i = 0; i < levels.Count; i++)
            rewardsByLevel.Add(uniqueByLevel[levels[i]]);
    }

    public void ClearRewards()
    {
        rewardsByLevel.Clear();
    }

    private int GetEconomyV2SoftCurrencyReward(int level)
    {
        if (level <= 4)
            return 25;

        if (level <= 9)
            return 35;

        if (level <= 14)
            return 50;

        if (level <= 19)
            return 60;

        if (level <= 29)
            return 75;

        if (level <= 49)
            return 90;

        if (level <= 74)
            return 110;

        if (level <= 99)
            return 130;

        if (level <= 149)
            return 150;

        if (level <= 249)
            return 175;

        if (level <= 499)
            return 200;

        if (level <= 999)
            return 225;

        if (level <= 1999)
            return 250;

        if (level <= 4999)
            return 300;

        return 350;
    }

    private int GetEconomyV2ChestCount(int level)
    {
        if (level == 2)
            return 1;

        if (level % 100 == 0)
            return 1;

        if (level % 25 == 0)
            return 1;

        if (level <= 20 && (level == 5 || level == 10 || level == 15 || level == 20))
            return 1;

        return 0;
    }

    private ChestType GetEconomyV2ChestType(int level)
    {
        if (level == 2)
            return ChestType.GuaranteedCommon;

        if (level == 5)
            return ChestType.GuaranteedRare;

        if (level == 10 || level == 15)
            return ChestType.GuaranteedEpic;

        if (level == 20)
            return ChestType.GuaranteedLegendary;

        if (level % 100 == 0)
            return ChestType.GuaranteedLegendary;

        if (level % 25 == 0)
            return ChestType.GuaranteedEpic;

        return ChestType.Random;
    }

    private int GetLegacySoftCurrencyReward(int level)
    {
        return 25 + ((level - 2) / 5) * 10;
    }

    private int GetLegacyChestCount(int level)
    {
        return 1 + (level / 20);
    }

    private ChestType GetLegacyChestType(int level)
    {
        if (level % 10 == 0)
            return ChestType.GuaranteedLegendary;

        if (level % 5 == 0)
            return ChestType.GuaranteedEpic;

        return ChestType.GuaranteedCommon;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            LevelRewardEntry entry = rewardsByLevel[i];
            entry.targetLevel = Mathf.Max(2, entry.targetLevel);
            entry.softCurrencyReward = Mathf.Max(0, entry.softCurrencyReward);
            entry.chestCount = Mathf.Max(0, entry.chestCount);
            rewardsByLevel[i] = entry;
        }
    }
#endif
}
