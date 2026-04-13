using UnityEngine;

[DefaultExecutionOrder(-1100)]
public sealed class BotOfflineGameplayBootstrap : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BotOfflineMatchRuntime botOfflineMatchRuntime;
    [SerializeField] private OfflineBotMatchController offlineBotMatchController;

    [Header("Runtime")]
    [SerializeField] private bool bootstrapAttempted = false;
    [SerializeField] private bool bootstrapCompleted = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveDependencies();

        if (bootstrapAttempted)
            return;

        bootstrapAttempted = true;

        if (botOfflineMatchRuntime == null || !botOfflineMatchRuntime.HasPendingRequest)
        {
            if (logDebug)
                Debug.Log("[BotOfflineGameplayBootstrap] No pending offline bot request. Bootstrap skipped.", this);

            return;
        }

        BotOfflineMatchRequest request = botOfflineMatchRuntime.ConsumeRequest();
        if (request == null)
        {
            Debug.LogWarning("[BotOfflineGameplayBootstrap] Pending request flag was set but request is null.", this);
            return;
        }

        if (offlineBotMatchController == null)
        {
            Debug.LogError("[BotOfflineGameplayBootstrap] OfflineBotMatchController reference missing.", this);
            return;
        }

        offlineBotMatchController.ConfigureOfflineMatch(request);
        bootstrapCompleted = true;

        if (logDebug)
        {
            Debug.Log("[BotOfflineGameplayBootstrap] Offline gameplay bootstrap completed -> " + request, this);
        }
    }

    private void ResolveDependencies()
    {
        if (botOfflineMatchRuntime == null)
            botOfflineMatchRuntime = BotOfflineMatchRuntime.Instance;

#if UNITY_2023_1_OR_NEWER
        if (offlineBotMatchController == null)
            offlineBotMatchController = FindFirstObjectByType<OfflineBotMatchController>();
#else
        if (offlineBotMatchController == null)
            offlineBotMatchController = FindObjectOfType<OfflineBotMatchController>();
#endif
    }
}
