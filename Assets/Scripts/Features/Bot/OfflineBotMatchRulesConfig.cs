using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OfflineBotMatchRulesConfig",
    menuName = "Uncball Arena/Bots/Offline Bot Match Rules Config")]
public sealed class OfflineBotMatchRulesConfig : ScriptableObject
{
    [SerializeField] private OfflineBotRuleSet easy = OfflineBotRuleSet.CreateDefaultEasy();
    [SerializeField] private OfflineBotRuleSet medium = OfflineBotRuleSet.CreateDefaultMedium();
    [SerializeField] private OfflineBotRuleSet hard = OfflineBotRuleSet.CreateDefaultHard();

    public OfflineBotRuleSet GetRules(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return easy;
            case BotDifficulty.Medium:
                return medium;
            case BotDifficulty.Hard:
                return hard;
            default:
                return medium;
        }
    }

    private void OnValidate()
    {
        easy?.Validate();
        medium?.Validate();
        hard?.Validate();
    }
}

[Serializable]
public sealed class OfflineBotRuleSet
{
    public MatchMode matchMode = MatchMode.ScoreTarget;
    public int pointsToWin = 4;
    public float matchDurationSeconds = 180f;
    public float turnDurationSeconds = 12f;

    public void Validate()
    {
        pointsToWin = Mathf.Max(1, pointsToWin);
        matchDurationSeconds = Mathf.Max(1f, matchDurationSeconds);
        turnDurationSeconds = Mathf.Max(1f, turnDurationSeconds);
    }

    public static OfflineBotRuleSet CreateDefaultEasy()
    {
        return new OfflineBotRuleSet
        {
            matchMode = MatchMode.ScoreTarget,
            pointsToWin = 4,
            matchDurationSeconds = 180f,
            turnDurationSeconds = 14f
        };
    }

    public static OfflineBotRuleSet CreateDefaultMedium()
    {
        return new OfflineBotRuleSet
        {
            matchMode = MatchMode.ScoreTarget,
            pointsToWin = 6,
            matchDurationSeconds = 180f,
            turnDurationSeconds = 12f
        };
    }

    public static OfflineBotRuleSet CreateDefaultHard()
    {
        return new OfflineBotRuleSet
        {
            matchMode = MatchMode.TimeLimit,
            pointsToWin = 1,
            matchDurationSeconds = 180f,
            turnDurationSeconds = 10f
        };
    }
}
