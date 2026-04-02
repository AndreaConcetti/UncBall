using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OnlineQueueRulesConfig",
    menuName = "Uncball Arena/Online/Queue Rules Config")]
public class OnlineQueueRulesConfig : ScriptableObject
{
    [Header("Normal Queue")]
    [SerializeField]
    private QueueRuleSet normal = new QueueRuleSet
    {
        matchMode = MatchMode.ScoreTarget,
        pointsToWin = 4,
        matchDurationSeconds = 20f,
        turnDurationSeconds = 12f,
        allowChestRewards = true,
        allowXpRewards = true,
        allowStatsProgression = true
    };

    [Header("Ranked Queue")]
    [SerializeField]
    private QueueRuleSet ranked = new QueueRuleSet
    {
        matchMode = MatchMode.TimeLimit,
        pointsToWin = 1,
        matchDurationSeconds = 40f,
        turnDurationSeconds = 10f,
        allowChestRewards = true,
        allowXpRewards = true,
        allowStatsProgression = true
    };

    public QueueRuleSet GetRules(QueueType queueType)
    {
        return queueType == QueueType.Ranked ? ranked : normal;
    }
}

[Serializable]
public class QueueRuleSet
{
    public MatchMode matchMode = MatchMode.ScoreTarget;
    public int pointsToWin = 4;
    public float matchDurationSeconds = 20f;
    public float turnDurationSeconds = 12f;

    [Header("Rewards / Progression")]
    public bool allowChestRewards = true;
    public bool allowXpRewards = true;
    public bool allowStatsProgression = true;
}