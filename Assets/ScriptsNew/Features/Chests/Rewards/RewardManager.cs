using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Dependencies")]
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Reward Rules")]
    [SerializeField] private bool rewardPlayer1OnWin = true;
    [SerializeField] private ChestType defaultWinRewardChestType = ChestType.Random;
    [SerializeField] private int defaultWinRewardChestAmount = 1;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool rewardGrantedForCurrentMatch = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;
    }

    public void BeginNewMatchRewardCycle()
    {
        rewardGrantedForCurrentMatch = false;

        if (logDebug)
            Debug.Log("[RewardManager] New match reward cycle started.", this);
    }

    public bool TryGrantMatchWinReward(PlayerID winner)
    {
        if (rewardGrantedForCurrentMatch)
        {
            if (logDebug)
                Debug.Log("[RewardManager] Reward already granted for this match, skipping.", this);

            return false;
        }

        if (!rewardPlayer1OnWin)
        {
            if (logDebug)
                Debug.Log("[RewardManager] RewardPlayer1OnWin disabled, skipping reward.", this);

            return false;
        }

        if (winner != PlayerID.Player1)
        {
            if (logDebug)
                Debug.Log("[RewardManager] Winner is not Player1, no reward granted.", this);

            return false;
        }

        if (!ValidateDependencies())
            return false;

        ChestType chestTypeToGrant = ResolveRewardChestTypeForCurrentContext();
        int amountToGrant = Mathf.Max(1, defaultWinRewardChestAmount);

        for (int i = 0; i < amountToGrant; i++)
            playerChestSlotInventory.AwardChest(chestTypeToGrant);

        rewardGrantedForCurrentMatch = true;

        if (logDebug)
        {
            Debug.Log(
                "[RewardManager] Granted chest reward to Player1 -> " +
                chestTypeToGrant +
                " x" + amountToGrant,
                this
            );
        }

        return true;
    }

    private ChestType ResolveRewardChestTypeForCurrentContext()
    {
        return defaultWinRewardChestType;
    }

    private bool ValidateDependencies()
    {
        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;

        if (playerChestSlotInventory == null)
        {
            Debug.LogError("[RewardManager] PlayerChestSlotInventory missing.", this);
            return false;
        }

        return true;
    }

    [ContextMenu("DEBUG - Grant Default Reward Chest")]
    public void DebugGrantDefaultRewardChest()
    {
        if (!ValidateDependencies())
            return;

        for (int i = 0; i < Mathf.Max(1, defaultWinRewardChestAmount); i++)
            playerChestSlotInventory.AwardChest(defaultWinRewardChestType);
    }

    [ContextMenu("DEBUG - Reset Match Reward Cycle")]
    public void DebugResetMatchRewardCycle()
    {
        BeginNewMatchRewardCycle();
    }
}