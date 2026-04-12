using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BotQueuePolicyConfig",
    menuName = "Uncball Arena/Bots/Bot Queue Policy Config")]
public sealed class BotQueuePolicyConfig : ScriptableObject
{
    [Header("Global Switch")]
    [SerializeField] private bool allowBotsInQueue = false;

    [Header("Queue Timing")]
    [SerializeField] private float minimumQueueSecondsBeforeBotEligible = 12f;
    [SerializeField] private float forcedBotAfterQueueSeconds = 25f;

    [Header("Eligibility Rules")]
    [SerializeField] private bool requireNoHumanMatchFound = true;
    [SerializeField] private bool allowImmediateBotForOfflineFallback = false;

    [Header("Weighted Injection")]
    [SerializeField][Range(0f, 1f)] private float chanceToUseBotWhenEligible = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public bool AllowBotsInQueue => allowBotsInQueue;
    public float MinimumQueueSecondsBeforeBotEligible => Mathf.Max(0f, minimumQueueSecondsBeforeBotEligible);
    public float ForcedBotAfterQueueSeconds => Mathf.Max(MinimumQueueSecondsBeforeBotEligible, forcedBotAfterQueueSeconds);
    public bool RequireNoHumanMatchFound => requireNoHumanMatchFound;
    public bool AllowImmediateBotForOfflineFallback => allowImmediateBotForOfflineFallback;
    public float ChanceToUseBotWhenEligible => Mathf.Clamp01(chanceToUseBotWhenEligible);
    public bool EnableDebugLogs => enableDebugLogs;

    public void OnValidate()
    {
        minimumQueueSecondsBeforeBotEligible = Mathf.Max(0f, minimumQueueSecondsBeforeBotEligible);
        forcedBotAfterQueueSeconds = Mathf.Max(minimumQueueSecondsBeforeBotEligible, forcedBotAfterQueueSeconds);
        chanceToUseBotWhenEligible = Mathf.Clamp01(chanceToUseBotWhenEligible);
    }
}
