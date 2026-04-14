using UnityEngine;

public sealed class BotMenuSelectionBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotMatchSetupController botMatchSetupController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public void StartEasyBotMatch()
    {
        StartBotMatch(BotDifficulty.Easy);
    }

    public void StartMediumBotMatch()
    {
        StartBotMatch(BotDifficulty.Medium);
    }

    public void StartHardBotMatch()
    {
        StartBotMatch(BotDifficulty.Hard);
    }

    public void StartInsaneBotMatch()
    {
        StartBotMatch(BotDifficulty.Unbeatable);
    }

    public void StartBotMatch(int difficultyIndex)
    {
        BotDifficulty parsedDifficulty = ConvertToDifficulty(difficultyIndex);
        StartBotMatch(parsedDifficulty);
    }

    public void StartBotMatch(BotDifficulty difficulty)
    {
        if (botMatchSetupController == null)
        {
            Debug.LogError("[BotMenuSelectionBridge] Missing BotMatchSetupController reference.", this);
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[BotMenuSelectionBridge] Starting bot match. Difficulty={difficulty}", this);
        }

        botMatchSetupController.CreateBotMatch(difficulty);
    }

    private BotDifficulty ConvertToDifficulty(int difficultyIndex)
    {
        switch (difficultyIndex)
        {
            case 0:
                return BotDifficulty.Easy;

            case 1:
                return BotDifficulty.Medium;

            case 2:
                return BotDifficulty.Hard;

            case 3:
                return BotDifficulty.Unbeatable;

            default:
                Debug.LogWarning($"[BotMenuSelectionBridge] Unsupported difficultyIndex={difficultyIndex}. Fallback=Medium.", this);
                return BotDifficulty.Medium;
        }
    }
}