using System;
using UnityEngine;

[Serializable]
public enum BotQueueDecisionType
{
    None = 0,
    WaitForHuman = 1,
    EligibleForBot = 2,
    ForceBot = 3
}

[Serializable]
public sealed class BotQueueDecisionResult
{
    [SerializeField] private BotQueueDecisionType decisionType;
    [SerializeField] private bool shouldInjectBot;
    [SerializeField] private float queueElapsedSeconds;
    [SerializeField] private string reason;

    public BotQueueDecisionType DecisionType => decisionType;
    public bool ShouldInjectBot => shouldInjectBot;
    public float QueueElapsedSeconds => queueElapsedSeconds;
    public string Reason => reason;

    public BotQueueDecisionResult(
        BotQueueDecisionType decisionType,
        bool shouldInjectBot,
        float queueElapsedSeconds,
        string reason)
    {
        this.decisionType = decisionType;
        this.shouldInjectBot = shouldInjectBot;
        this.queueElapsedSeconds = Mathf.Max(0f, queueElapsedSeconds);
        this.reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
    }

    public override string ToString()
    {
        return $"BotQueueDecisionResult | " +
               $"DecisionType={decisionType} | ShouldInjectBot={shouldInjectBot} | " +
               $"QueueElapsed={queueElapsedSeconds:0.00}s | Reason={reason}";
    }
}
