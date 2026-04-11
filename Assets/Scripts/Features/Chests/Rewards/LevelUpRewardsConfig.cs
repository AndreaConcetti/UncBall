using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LevelRewardEntry
{
    [Min(2)] public int targetLevel;
    [Min(0)] public int softCurrencyReward;
    [Min(0)] public int chestCount;
    public ChestType chestType;
}

[CreateAssetMenu(
    fileName = "LevelUpRewardsConfig",
    menuName = "Uncball Arena/Profile/Level Up Rewards Config")]
public class LevelUpRewardsConfig : ScriptableObject
{
    [Header("Rewards")]
    [SerializeField] private List<LevelRewardEntry> rewardsByLevel = new List<LevelRewardEntry>();

    [Header("Auto Generate")]
    [SerializeField, Min(2)] private int autoGenerateMaxLevel = 100;
    [SerializeField, Min(0)] private int baseSoftCurrencyReward = 25;
    [SerializeField, Min(0)] private int softCurrencyIncreasePer5Levels = 10;
    [SerializeField, Min(1)] private int baseChestCount = 1;
    [SerializeField, Min(1)] private int extraChestEveryNLevels = 20;
    [SerializeField] private ChestType defaultChestType = ChestType.GuaranteedCommon;
    [SerializeField] private ChestType every5LevelsChestType = ChestType.GuaranteedEpic;
    [SerializeField] private ChestType every10LevelsChestType = ChestType.GuaranteedLegendary;

    public IReadOnlyList<LevelRewardEntry> RewardsByLevel => rewardsByLevel;

    public int AutoGenerateMaxLevel => autoGenerateMaxLevel;
    public int BaseSoftCurrencyReward => baseSoftCurrencyReward;
    public int SoftCurrencyIncreasePer5Levels => softCurrencyIncreasePer5Levels;
    public int BaseChestCount => baseChestCount;
    public int ExtraChestEveryNLevels => extraChestEveryNLevels;
    public ChestType DefaultChestType => defaultChestType;
    public ChestType Every5LevelsChestType => every5LevelsChestType;
    public ChestType Every10LevelsChestType => every10LevelsChestType;

    public LevelRewardEntry GetRewardForLevel(int targetLevel)
    {
        int safeLevel = Mathf.Max(2, targetLevel);

        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            if (rewardsByLevel[i].targetLevel == safeLevel)
                return rewardsByLevel[i];
        }

        return new LevelRewardEntry
        {
            targetLevel = safeLevel,
            softCurrencyReward = 0,
            chestCount = 0,
            chestType = defaultChestType
        };
    }

    public void GetRewardsBetweenLevels(int previousLevel, int newLevel, List<LevelRewardEntry> output)
    {
        if (output == null)
            return;

        output.Clear();

        int fromExclusive = Mathf.Max(1, previousLevel);
        int toInclusive = Mathf.Max(fromExclusive, newLevel);

        for (int level = fromExclusive + 1; level <= toInclusive; level++)
        {
            LevelRewardEntry reward = GetRewardForLevel(level);

            if (reward.softCurrencyReward <= 0 && reward.chestCount <= 0)
                continue;

            output.Add(reward);
        }
    }

    public void AutoGenerateRewards()
    {
        rewardsByLevel.Clear();

        int maxLevel = Mathf.Max(2, autoGenerateMaxLevel);

        for (int level = 2; level <= maxLevel; level++)
        {
            LevelRewardEntry entry = BuildEntryForLevel(level);
            rewardsByLevel.Add(entry);
        }
    }

    public LevelRewardEntry BuildEntryForLevel(int level)
    {
        int safeLevel = Mathf.Max(2, level);

        int softReward = ComputeSoftCurrencyReward(safeLevel);
        int chestCount = ComputeChestCount(safeLevel);
        ChestType chestType = ComputeChestType(safeLevel);

        return new LevelRewardEntry
        {
            targetLevel = safeLevel,
            softCurrencyReward = softReward,
            chestCount = chestCount,
            chestType = chestType
        };
    }

    public int ComputeSoftCurrencyReward(int level)
    {
        int safeLevel = Mathf.Max(2, level);
        int bonusSteps = Mathf.Max(0, (safeLevel - 1) / 5);
        return Mathf.Max(0, baseSoftCurrencyReward + bonusSteps * softCurrencyIncreasePer5Levels);
    }

    public int ComputeChestCount(int level)
    {
        int safeLevel = Mathf.Max(2, level);

        if (extraChestEveryNLevels <= 0)
            return Mathf.Max(1, baseChestCount);

        int extraSteps = safeLevel / extraChestEveryNLevels;
        return Mathf.Max(1, baseChestCount + extraSteps);
    }

    public ChestType ComputeChestType(int level)
    {
        int safeLevel = Mathf.Max(2, level);

        if (safeLevel % 10 == 0)
            return every10LevelsChestType;

        if (safeLevel % 5 == 0)
            return every5LevelsChestType;

        return defaultChestType;
    }

    public void SortAndDeduplicate()
    {
        Dictionary<int, LevelRewardEntry> uniqueByLevel = new Dictionary<int, LevelRewardEntry>();

        for (int i = 0; i < rewardsByLevel.Count; i++)
        {
            LevelRewardEntry entry = rewardsByLevel[i];
            int safeLevel = Mathf.Max(2, entry.targetLevel);
            entry.targetLevel = safeLevel;
            uniqueByLevel[safeLevel] = entry;
        }

        List<int> keys = new List<int>(uniqueByLevel.Keys);
        keys.Sort();

        rewardsByLevel.Clear();

        for (int i = 0; i < keys.Count; i++)
            rewardsByLevel.Add(uniqueByLevel[keys[i]]);
    }

    public void ClearRewards()
    {
        rewardsByLevel.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        autoGenerateMaxLevel = Mathf.Max(2, autoGenerateMaxLevel);
        baseChestCount = Mathf.Max(1, baseChestCount);
        extraChestEveryNLevels = Mathf.Max(1, extraChestEveryNLevels);
    }
#endif
}