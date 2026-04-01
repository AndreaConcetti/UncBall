using System;
using UnityEngine;

[Serializable]
public class MatchRewardResult
{
    public bool grantedReward;
    public ChestType grantedChestType = ChestType.Random;
    public string source = "";
    public string reason = "";
    public PlayerID winner = PlayerID.None;
}

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Reward Rules")]
    [SerializeField] private bool enableMatchWinChestReward = true;
    [SerializeField] private ChestType defaultWinChestType = ChestType.Random;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool matchRewardAlreadyGranted = false;

    public event Action<MatchRewardResult> OnMatchRewardGranted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveDependencies();
    }

    public void BeginNewMatchRewardCycle()
    {
        matchRewardAlreadyGranted = false;

        if (logDebug)
            Debug.Log("[RewardManager] New match reward cycle started.", this);
    }

    public bool TryGrantMatchWinReward(PlayerID winner)
    {
        MatchRewardResult result = EvaluateMatchWinReward(winner);

        if (!result.grantedReward)
        {
            if (logDebug)
            {
                Debug.Log(
                    "[RewardManager] No match reward granted. " +
                    "Winner=" + winner +
                    " | Reason=" + result.reason,
                    this
                );
            }

            return false;
        }

        bool granted = GrantChestReward(result.grantedChestType, result.source, result.reason, result.winner);

        if (granted)
            matchRewardAlreadyGranted = true;

        return granted;
    }

    public MatchRewardResult EvaluateMatchWinReward(PlayerID winner)
    {
        MatchRewardResult result = new MatchRewardResult
        {
            grantedReward = false,
            grantedChestType = defaultWinChestType,
            source = "match_win",
            reason = "",
            winner = winner
        };

        if (!enableMatchWinChestReward)
        {
            result.reason = "match_win_reward_disabled";
            return result;
        }

        if (matchRewardAlreadyGranted)
        {
            result.reason = "match_reward_already_granted";
            return result;
        }

        if (winner == PlayerID.None)
        {
            result.reason = "no_winner";
            return result;
        }

        result.grantedReward = true;
        result.reason = "granted";

        return result;
    }

    public bool GrantChestReward(ChestType chestType, string source, string reason, PlayerID winner = PlayerID.None)
    {
        ResolveDependencies();

        if (playerChestSlotInventory == null)
        {
            Debug.LogError("[RewardManager] PlayerChestSlotInventory missing.", this);
            return false;
        }

        bool awarded = playerChestSlotInventory.AwardChest(chestType);

        if (!awarded)
        {
            Debug.LogWarning(
                "[RewardManager] Failed to award chest. " +
                "Type=" + chestType +
                " | Source=" + source +
                " | Reason=" + reason,
                this
            );
            return false;
        }

        MatchRewardResult grantedResult = new MatchRewardResult
        {
            grantedReward = true,
            grantedChestType = chestType,
            source = source,
            reason = reason,
            winner = winner
        };

        OnMatchRewardGranted?.Invoke(grantedResult);

        if (logDebug)
        {
            Debug.Log(
                "[RewardManager] Chest reward granted. " +
                "Type=" + chestType +
                " | Source=" + source +
                " | Reason=" + reason +
                " | Winner=" + winner,
                this
            );
        }

        return true;
    }

    private void ResolveDependencies()
    {
        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;
    }
}