using UnityEngine;

public class ChestDebugPanel : MonoBehaviour
{
    [Header("Optional Direct Reference")]
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveInventory();
    }

    private void OnEnable()
    {
        ResolveInventory();
    }

    public void GiveRandomChest()
    {
        GiveChest(ChestType.Random);
    }

    public void GiveGuaranteedCommonChest()
    {
        GiveChest(ChestType.GuaranteedCommon);
    }

    public void GiveGuaranteedRareChest()
    {
        GiveChest(ChestType.GuaranteedRare);
    }

    public void GiveGuaranteedEpicChest()
    {
        GiveChest(ChestType.GuaranteedEpic);
    }

    public void GiveGuaranteedLegendaryChest()
    {
        GiveChest(ChestType.GuaranteedLegendary);
    }

    public void ClearAllChestData()
    {
        ResolveInventory();

        if (playerChestSlotInventory == null)
        {
            Debug.LogError("[ChestDebugPanel] PlayerChestSlotInventory not found. Cannot clear chest data.", this);
            return;
        }

        playerChestSlotInventory.ClearAllChestDataForDebug();

        if (logDebug)
        {
            Debug.Log("[ChestDebugPanel] Cleared all chest data for debug.", this);
        }
    }

    private void GiveChest(ChestType chestType)
    {
        ResolveInventory();

        if (playerChestSlotInventory == null)
        {
            Debug.LogError(
                "[ChestDebugPanel] PlayerChestSlotInventory not found. Cannot award chest of type: " + chestType,
                this
            );
            return;
        }

        bool success = playerChestSlotInventory.AwardChest(chestType);

        if (logDebug)
        {
            Debug.Log(
                "[ChestDebugPanel] GiveChest called." +
                " | Type=" + chestType +
                " | Success=" + success +
                " | ActiveProfileId=" + playerChestSlotInventory.ActiveProfileId +
                " | OccupiedSlots=" + playerChestSlotInventory.GetOccupiedSlotCount() +
                " | Queued=" + playerChestSlotInventory.GetQueuedChestCount(),
                this
            );
        }
    }

    private void ResolveInventory()
    {
        if (playerChestSlotInventory != null)
            return;

        playerChestSlotInventory = PlayerChestSlotInventory.Instance;

        if (playerChestSlotInventory != null)
            return;

#if UNITY_2023_1_OR_NEWER
        playerChestSlotInventory = FindFirstObjectByType<PlayerChestSlotInventory>(FindObjectsInactive.Include);
#else
        playerChestSlotInventory = FindObjectOfType<PlayerChestSlotInventory>(true);
#endif
    }
}