using UnityEngine;

public class ChestSlotUI : MonoBehaviour
{
    [Header("Slot")]
    [SerializeField] private int slotIndex = 0;

    [Header("Dependencies")]
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;
    [SerializeField] private ObtainNewSkin obtainNewSkin;

    [Header("Views")]
    [SerializeField] private GameObject emptySlotView;
    [SerializeField] private GameObject occupiedSlotView;
    [SerializeField] private ChestCardUI chestCardUI;

    [Header("Refresh")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private float timerRefreshInterval = 1f;

    [Header("Editor Preview")]
    [SerializeField] private bool forceOccupiedPreviewInEditor = false;
    [SerializeField] private bool forceEmptyPreviewInEditor = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private float timerRefreshElapsed = 0f;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (playerChestSlotInventory != null)
            playerChestSlotInventory.OnChestInventoryChanged += RefreshUI;

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        if (playerChestSlotInventory != null)
            playerChestSlotInventory.OnChestInventoryChanged -= RefreshUI;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            HandleEditorPreview();
            return;
        }
#endif

        if (playerChestSlotInventory == null)
            return;

        if (!playerChestSlotInventory.HasChestInSlot(slotIndex))
            return;

        timerRefreshElapsed += Time.unscaledDeltaTime;

        if (timerRefreshElapsed >= timerRefreshInterval)
        {
            timerRefreshElapsed = 0f;

            if (chestCardUI != null)
                chestCardUI.RefreshUI();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (timerRefreshInterval < 0.1f)
            timerRefreshInterval = 0.1f;

        HandleEditorPreview();
    }
#endif

    public void RefreshUI()
    {
        timerRefreshElapsed = 0f;

        ResolveDependencies();

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            HandleEditorPreview();
#endif
            return;
        }

        if (emptySlotView == null)
        {
            Debug.LogError("[ChestSlotUI] EmptySlotView missing on " + name, this);
            return;
        }

        if (occupiedSlotView == null)
        {
            Debug.LogError("[ChestSlotUI] OccupiedSlotView missing on " + name, this);
            return;
        }

        if (playerChestSlotInventory == null)
        {
            Debug.LogError("[ChestSlotUI] PlayerChestSlotInventory missing on " + name, this);
            emptySlotView.SetActive(true);
            occupiedSlotView.SetActive(false);
            return;
        }

        bool hasChest = playerChestSlotInventory.HasChestInSlot(slotIndex);

        emptySlotView.SetActive(!hasChest);
        occupiedSlotView.SetActive(hasChest);

        if (hasChest)
        {
            if (chestCardUI == null)
            {
                Debug.LogError("[ChestSlotUI] ChestCardUI missing on " + name, this);
                return;
            }

            chestCardUI.Bind(playerChestSlotInventory, slotIndex, obtainNewSkin);
        }

        if (logDebug)
        {
            Debug.Log(
                "[ChestSlotUI] RefreshUI -> Slot=" + slotIndex +
                " | HasChest=" + hasChest +
                " | QueueCount=" + playerChestSlotInventory.GetQueuedChestCount(),
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;
    }

#if UNITY_EDITOR
    private void HandleEditorPreview()
    {
        if (Application.isPlaying)
            return;

        if (emptySlotView == null || occupiedSlotView == null)
            return;

        if (forceOccupiedPreviewInEditor && !forceEmptyPreviewInEditor)
        {
            emptySlotView.SetActive(false);
            occupiedSlotView.SetActive(true);
            return;
        }

        if (forceEmptyPreviewInEditor && !forceOccupiedPreviewInEditor)
        {
            emptySlotView.SetActive(true);
            occupiedSlotView.SetActive(false);
            return;
        }
    }
#endif
}