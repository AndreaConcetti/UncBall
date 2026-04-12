using UnityEngine;

public sealed class BotSessionRuntime : MonoBehaviour
{
    public static BotSessionRuntime Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Current Session")]
    [SerializeField] private bool hasActiveBot;
    [SerializeField] private BotProfileRuntimeData currentBotProfile;
    [SerializeField] private OpponentPresentationProfile currentOpponentPresentation;

    public bool HasActiveBot => hasActiveBot;
    public BotProfileRuntimeData CurrentBotProfile => currentBotProfile;
    public OpponentPresentationProfile CurrentOpponentPresentation => currentOpponentPresentation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[BotSessionRuntime] Duplicate instance destroyed.", this);
            }

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebugLogs)
        {
            Debug.Log("[BotSessionRuntime] Initialized.", this);
        }
    }

    public void SetCurrentBot(BotProfileRuntimeData botProfile)
    {
        if (botProfile == null)
        {
            Debug.LogWarning("[BotSessionRuntime] SetCurrentBot called with null profile.", this);
            ClearCurrentBot();
            return;
        }

        currentBotProfile = botProfile;
        currentOpponentPresentation = OpponentPresentationProfile.FromBot(botProfile);
        hasActiveBot = currentOpponentPresentation != null;

        if (enableDebugLogs)
        {
            Debug.Log($"[BotSessionRuntime] Current bot set -> {currentBotProfile}", this);
            Debug.Log($"[BotSessionRuntime] Current opponent presentation set -> {currentOpponentPresentation}", this);
        }
    }

    public void ClearCurrentBot()
    {
        currentBotProfile = null;
        currentOpponentPresentation = null;
        hasActiveBot = false;

        if (enableDebugLogs)
        {
            Debug.Log("[BotSessionRuntime] Cleared current bot session.", this);
        }
    }
}
