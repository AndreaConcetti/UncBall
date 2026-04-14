using UnityEngine;

public sealed class BotMenuSelectionBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotMatchSetupController botMatchSetupController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public void StartEasyBotMatch() => StartBotMatch(BotDifficulty.Easy);
    public void StartMediumBotMatch() => StartBotMatch(BotDifficulty.Medium);
    public void StartHardBotMatch() => StartBotMatch(BotDifficulty.Hard);
    public void StartInsaneBotMatch() => StartBotMatch(BotDifficulty.Unbeatable);

    public void StartBotMatch(BotDifficulty difficulty)
    {
        if (!TryResolveSetupController())
        {
            Debug.LogError("[BotMenuSelectionBridge] Missing BotMatchSetupController reference.", this);
            return;
        }

        botMatchSetupController.CreateBotMatch(difficulty);

        if (enableDebugLogs)
            Debug.Log("[BotMenuSelectionBridge] StartBotMatch -> Difficulty=" + difficulty, this);
    }

    private void Awake()
    {
        TryResolveSetupController();
    }

    private void OnEnable()
    {
        TryResolveSetupController();
    }

    private bool TryResolveSetupController()
    {
        if (botMatchSetupController != null)
            return true;

        BotSessionRuntime runtime = BotSessionRuntime.Instance;
        if (runtime != null)
        {
            botMatchSetupController = runtime.GetComponent<BotMatchSetupController>();
            if (botMatchSetupController != null)
                return true;

            botMatchSetupController = runtime.GetComponentInParent<BotMatchSetupController>(true);
            if (botMatchSetupController != null)
                return true;

            botMatchSetupController = runtime.GetComponentInChildren<BotMatchSetupController>(true);
            if (botMatchSetupController != null)
                return true;
        }

#if UNITY_2023_1_OR_NEWER
        botMatchSetupController = FindFirstObjectByType<BotMatchSetupController>(FindObjectsInactive.Include);
#else
        botMatchSetupController = FindObjectOfType<BotMatchSetupController>(true);
#endif

        if (botMatchSetupController != null && enableDebugLogs)
            Debug.Log("[BotMenuSelectionBridge] Resolved BotMatchSetupController dynamically.", this);

        return botMatchSetupController != null;
    }
}