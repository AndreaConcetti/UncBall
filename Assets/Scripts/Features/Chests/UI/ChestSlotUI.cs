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
    [SerializeField] private float dependencyRetryInterval = 0.25f;

    [Header("Editor Preview")]
    [SerializeField] private bool forceOccupiedPreviewInEditor = false;
    [SerializeField] private bool forceEmptyPreviewInEditor = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;
    [SerializeField] private bool logMissingInventoryWarning = false;

    private float timerRefreshElapsed = 0f;
    private float dependencyRetryElapsed = 0f;
    private bool isSubscribedToInventory = false;

    private void Awake()
    {
        ResolveDependencies();
        TrySubscribeToInventory();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        TrySubscribeToInventory();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
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
        {
            dependencyRetryElapsed += Time.unscaledDeltaTime;

            if (dependencyRetryElapsed >= dependencyRetryInterval)
            {
                dependencyRetryElapsed = 0f;
                ResolveDependencies();
                TrySubscribeToInventory();
                RefreshUI();
            }

            return;
        }

        if (!isSubscribedToInventory)
            TrySubscribeToInventory();

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

        if (dependencyRetryInterval < 0.05f)
            dependencyRetryInterval = 0.05f;

        HandleEditorPreview();
    }
#endif

    public void RefreshUI()
    {
        timerRefreshElapsed = 0f;
        dependencyRetryElapsed = 0f;

        ResolveDependencies();
        TrySubscribeToInventory();

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
            emptySlotView.SetActive(true);
            occupiedSlotView.SetActive(false);

            if (logMissingInventoryWarning || logDebug)
            {
                Debug.LogWarning(
                    "[ChestSlotUI] PlayerChestSlotInventory not ready yet on " + name +
                    ". UI will retry automatically.",
                    this
                );
            }

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

        if (obtainNewSkin == null)
            obtainNewSkin = FindFirstObjectByTypeSafe<ObtainNewSkin>();
    }

    private void TrySubscribeToInventory()
    {
        if (isSubscribedToInventory)
            return;

        if (playerChestSlotInventory == null)
            return;

        playerChestSlotInventory.OnChestInventoryChanged -= RefreshUI;
        playerChestSlotInventory.OnChestInventoryChanged += RefreshUI;
        isSubscribedToInventory = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (!isSubscribedToInventory)
            return;

        if (playerChestSlotInventory != null)
            playerChestSlotInventory.OnChestInventoryChanged -= RefreshUI;

        isSubscribedToInventory = false;
    }

    private T FindFirstObjectByTypeSafe<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<T>(true);
#endif
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