using System;
using UnityEngine;

public sealed class BotQueueDecisionService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotQueuePolicyConfig queuePolicyConfig;

    [Header("Runtime")]
    [SerializeField] private bool useDeterministicSeed = false;
    [SerializeField] private int deterministicSeed = 92831;

    private System.Random random;

    public BotQueuePolicyConfig QueuePolicyConfig => queuePolicyConfig;

    private void Awake()
    {
        InitializeRandom();
    }

    private void InitializeRandom()
    {
        if (useDeterministicSeed)
        {
            random = new System.Random(deterministicSeed);
            Debug.Log($"[BotQueueDecisionService] Initialized with deterministic seed={deterministicSeed}.", this);
            return;
        }

        random = new System.Random(Environment.TickCount);
        Debug.Log("[BotQueueDecisionService] Initialized with non-deterministic seed.", this);
    }

    public BotQueueDecisionResult EvaluateQueueState(
        float queueElapsedSeconds,
        bool humanCandidateFound,
        bool forceOfflineFallback = false)
    {
        if (queuePolicyConfig == null)
        {
            Debug.LogError("[BotQueueDecisionService] Missing BotQueuePolicyConfig reference.", this);
            return new BotQueueDecisionResult(
                BotQueueDecisionType.None,
                false,
                queueElapsedSeconds,
                "Missing queue policy config");
        }

        float safeElapsed = Mathf.Max(0f, queueElapsedSeconds);

        if (forceOfflineFallback && queuePolicyConfig.AllowImmediateBotForOfflineFallback)
        {
            return LogAndReturn(new BotQueueDecisionResult(
                BotQueueDecisionType.ForceBot,
                true,
                safeElapsed,
                "Immediate bot allowed for offline fallback"));
        }

        if (!queuePolicyConfig.AllowBotsInQueue)
        {
            return LogAndReturn(new BotQueueDecisionResult(
                BotQueueDecisionType.WaitForHuman,
                false,
                safeElapsed,
                "Bots in queue disabled"));
        }

        if (queuePolicyConfig.RequireNoHumanMatchFound && humanCandidateFound)
        {
            return LogAndReturn(new BotQueueDecisionResult(
                BotQueueDecisionType.WaitForHuman,
                false,
                safeElapsed,
                "Human candidate already found"));
        }

        if (safeElapsed >= queuePolicyConfig.ForcedBotAfterQueueSeconds)
        {
            return LogAndReturn(new BotQueueDecisionResult(
                BotQueueDecisionType.ForceBot,
                true,
                safeElapsed,
                "Forced bot threshold reached"));
        }

        if (safeElapsed < queuePolicyConfig.MinimumQueueSecondsBeforeBotEligible)
        {
            return LogAndReturn(new BotQueueDecisionResult(
                BotQueueDecisionType.WaitForHuman,
                false,
                safeElapsed,
                "Minimum queue time for bot not reached"));
        }

        bool injectBot = RollChance(queuePolicyConfig.ChanceToUseBotWhenEligible);

        return LogAndReturn(new BotQueueDecisionResult(
            BotQueueDecisionType.EligibleForBot,
            injectBot,
            safeElapsed,
            injectBot
                ? "Bot eligible and random roll accepted"
                : "Bot eligible but random roll rejected"));
    }

    private bool RollChance(float chance)
    {
        chance = Mathf.Clamp01(chance);
        if (chance <= 0f)
        {
            return false;
        }

        if (chance >= 1f)
        {
            return true;
        }

        return random.NextDouble() <= chance;
    }

    private BotQueueDecisionResult LogAndReturn(BotQueueDecisionResult result)
    {
        if (queuePolicyConfig != null && queuePolicyConfig.EnableDebugLogs && result != null)
        {
            Debug.Log($"[BotQueueDecisionService] {result}", this);
        }

        return result;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Evaluate Queue 0s")]
    private void DebugEvaluate0()
    {
        EvaluateQueueState(0f, false, false);
    }

    [ContextMenu("Debug Evaluate Queue 15s")]
    private void DebugEvaluate15()
    {
        EvaluateQueueState(15f, false, false);
    }

    [ContextMenu("Debug Evaluate Queue 30s")]
    private void DebugEvaluate30()
    {
        EvaluateQueueState(30f, false, false);
    }
#endif
}
