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

            // Inclusivo sul livello raggiunto.
            // Esempio: 1 -> 2 deve includere TargetLevel 2.
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
                softCurrencyReward = GetSoftCurrencyReward(level),
                chestCount = GetChestCount(level),
                chestType = GetChestType(level)
            };

            rewardsByLevel.Add(entry);
        }
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

    private int GetSoftCurrencyReward(int level)
    {
        // Base 25, poi cresce di 10 ogni 5 livelli.
        return 25 + ((level - 2) / 5) * 10;
    }

    private int GetChestCount(int level)
    {
        // 1 chest fino al 19, poi +1 ogni 20 livelli.
        return 1 + (level / 20);
    }

    private ChestType GetChestType(int level)
    {
        // Ogni 10 livelli legendary, ogni 5 epic, altrimenti common.
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