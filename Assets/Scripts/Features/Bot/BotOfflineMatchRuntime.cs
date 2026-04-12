using UnityEngine;

public sealed class BotOfflineMatchRuntime : MonoBehaviour
{
    public static BotOfflineMatchRuntime Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Current Request")]
    [SerializeField] private bool hasPendingRequest;
    [SerializeField] private BotOfflineMatchRequest currentRequest;

    public bool HasPendingRequest => hasPendingRequest && currentRequest != null;
    public BotOfflineMatchRequest CurrentRequest => currentRequest;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }

        if (enableDebugLogs)
        {
            Debug.Log("[BotOfflineMatchRuntime] Initialized.", this);
        }
    }

    public void SetRequest(BotOfflineMatchRequest request)
    {
        currentRequest = request;
        hasPendingRequest = currentRequest != null;

        if (enableDebugLogs)
        {
            Debug.Log("[BotOfflineMatchRuntime] SetRequest -> " + (currentRequest != null ? currentRequest.ToString() : "null"), this);
        }
    }

    public BotOfflineMatchRequest ConsumeRequest()
    {
        BotOfflineMatchRequest consumed = currentRequest;
        currentRequest = null;
        hasPendingRequest = false;

        if (enableDebugLogs)
        {
            Debug.Log("[BotOfflineMatchRuntime] ConsumeRequest -> " + (consumed != null ? consumed.ToString() : "null"), this);
        }

        return consumed;
    }

    public void ClearRequest()
    {
        currentRequest = null;
        hasPendingRequest = false;

        if (enableDebugLogs)
        {
            Debug.Log("[BotOfflineMatchRuntime] ClearRequest", this);
        }
    }
}
