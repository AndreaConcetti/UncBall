using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinCollectionPagedUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Player1SkinInventory player1SkinInventory;
    [SerializeField] private BallSkinThumbnailRenderer thumbnailRenderer;

    [Header("Grid UI")]
    [SerializeField] private Transform gridContent;
    [SerializeField] private SkinCollectionItemUI itemPrefab;

    [Header("Filter UI")]
    [SerializeField] private TMP_Text filterLabelText;
    [SerializeField] private TMP_Text pageInfoText;

    [Header("Selected Preview Popup")]
    [SerializeField] private GameObject selectedPreviewSectionRoot;
    [SerializeField] private BallSkinApplier selectedPreviewSkinApplier;
    [SerializeField] private Button equipSelectedButton;
    [SerializeField] private Button closePreviewButton;
    [SerializeField] private Image selectedRarityBar;

    [Header("Optional Selected Info UI")]
    [SerializeField] private TMP_Text selectedRarityText;
    [SerializeField] private TMP_Text selectedSkinIdText;
    [SerializeField] private string noSelectionText = "NO SELECTION";

    [Header("Hide When Preview Open")]
    [SerializeField] private List<GameObject> objectsToHideWhenPreviewOpen = new List<GameObject>();

    [Header("Paging")]
    [SerializeField] private int itemsPerPage = 9;
    [SerializeField] private bool rebuildOnEnable = true;

    [Header("State")]
    [SerializeField] private CollectionFilterType currentFilter = CollectionFilterType.All;
    [SerializeField] private int currentPageIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private readonly List<BallSkinData> filteredSkins = new List<BallSkinData>();

    private BallSkinData selectedSkin;

    private void Awake()
    {
        if (equipSelectedButton != null)
        {
            equipSelectedButton.onClick.RemoveAllListeners();
            equipSelectedButton.onClick.AddListener(EquipSelectedSkin);
        }

        if (closePreviewButton != null)
        {
            closePreviewButton.onClick.RemoveAllListeners();
            closePreviewButton.onClick.AddListener(CloseSelectedPreview);
        }

        if (selectedPreviewSectionRoot != null)
            selectedPreviewSectionRoot.SetActive(false);

        SetHiddenObjectsVisible(true);
        ClearSelectedPreviewVisuals();
    }

    private void OnEnable()
    {
        if (rebuildOnEnable)
            Rebuild();
    }

    public void Rebuild()
    {
        StopAllCoroutines();
        StartCoroutine(RebuildCoroutine());
    }

    public void NextFilter()
    {
        int max = System.Enum.GetValues(typeof(CollectionFilterType)).Length;
        currentFilter = (CollectionFilterType)(((int)currentFilter + 1) % max);
        currentPageIndex = 0;
        Rebuild();
    }

    public void PreviousFilter()
    {
        int max = System.Enum.GetValues(typeof(CollectionFilterType)).Length;
        int next = (int)currentFilter - 1;
        if (next < 0)
            next = max - 1;

        currentFilter = (CollectionFilterType)next;
        currentPageIndex = 0;
        Rebuild();
    }

    public void NextPage()
    {
        int totalPages = GetTotalPages();
        if (totalPages <= 0)
            return;

        currentPageIndex++;
        if (currentPageIndex >= totalPages)
            currentPageIndex = totalPages - 1;

        Rebuild();
    }

    public void PreviousPage()
    {
        currentPageIndex--;
        if (currentPageIndex < 0)
            currentPageIndex = 0;

        Rebuild();
    }

    public void SelectSkin(BallSkinData skin)
    {
        if (skin == null)
            return;

        selectedSkin = skin;

        if (logDebug)
            Debug.Log("[SkinCollectionPagedUI] Selected skin: " + selectedSkin.skinUniqueId, this);

        OpenSelectedPreview();
        RefreshSelectedPreview();
    }

    public void EquipSelectedSkin()
    {
        if (player1SkinInventory == null)
            player1SkinInventory = Player1SkinInventory.Instance;

        if (player1SkinInventory == null)
        {
            Debug.LogError("[SkinCollectionPagedUI] Player1SkinInventory missing.", this);
            return;
        }

        if (selectedSkin == null)
        {
            Debug.LogWarning("[SkinCollectionPagedUI] No selected skin to equip.", this);
            return;
        }

        bool success = player1SkinInventory.EquipSkin(selectedSkin.skinUniqueId);
        if (!success)
            return;

        if (logDebug)
            Debug.Log("[SkinCollectionPagedUI] Equipped Player1 skin: " + selectedSkin.skinUniqueId, this);

        Rebuild();
        CloseSelectedPreview();
    }

    public void OpenSelectedPreview()
    {
        if (selectedPreviewSectionRoot != null)
            selectedPreviewSectionRoot.SetActive(true);

        SetHiddenObjectsVisible(false);
    }

    public void CloseSelectedPreview()
    {
        if (selectedPreviewSectionRoot != null)
            selectedPreviewSectionRoot.SetActive(false);

        SetHiddenObjectsVisible(true);
    }

    private IEnumerator RebuildCoroutine()
    {
        if (player1SkinInventory == null)
            player1SkinInventory = Player1SkinInventory.Instance;

        if (player1SkinInventory == null)
        {
            Debug.LogError("[SkinCollectionPagedUI] Player1SkinInventory not found.", this);
            yield break;
        }

        if (thumbnailRenderer == null)
        {
            Debug.LogError("[SkinCollectionPagedUI] Thumbnail renderer missing.", this);
            yield break;
        }

        if (gridContent == null)
        {
            Debug.LogError("[SkinCollectionPagedUI] GridContent missing.", this);
            yield break;
        }

        if (itemPrefab == null)
        {
            Debug.LogError("[SkinCollectionPagedUI] Item prefab missing.", this);
            yield break;
        }

        CleanupGeneratedTextures();
        ClearGrid();

        BuildFilteredList();

        int totalPages = GetTotalPages();
        if (totalPages == 0)
            currentPageIndex = 0;
        else if (currentPageIndex >= totalPages)
            currentPageIndex = totalPages - 1;

        RefreshHeaderTexts(totalPages);

        if (filteredSkins.Count == 0)
        {
            selectedSkin = null;
            RefreshSelectedPreview();
            yield break;
        }

        int startIndex = currentPageIndex * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, filteredSkins.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            BallSkinData skin = filteredSkins[i];
            if (skin == null)
                continue;

            Texture2D thumbnail = null;

            yield return StartCoroutine(
                thumbnailRenderer.RenderThumbnailCoroutine(
                    player1SkinInventory.Database,
                    skin,
                    tex => thumbnail = tex
                )
            );

            if (thumbnail != null)
                generatedTextures.Add(thumbnail);

            SkinCollectionItemUI item = Instantiate(itemPrefab, gridContent);
            item.Bind(skin, this, thumbnail);
        }

        bool selectedStillVisible = false;

        for (int i = 0; i < filteredSkins.Count; i++)
        {
            if (selectedSkin != null &&
                filteredSkins[i] != null &&
                filteredSkins[i].skinUniqueId == selectedSkin.skinUniqueId)
            {
                selectedStillVisible = true;
                break;
            }
        }

        if (!selectedStillVisible)
            selectedSkin = null;

        RefreshSelectedPreview();
    }

    private void BuildFilteredList()
    {
        filteredSkins.Clear();

        IReadOnlyList<BallSkinData> allSkins = player1SkinInventory.UnlockedSkins;

        for (int i = 0; i < allSkins.Count; i++)
        {
            BallSkinData skin = allSkins[i];
            if (skin == null)
                continue;

            if (PassesFilter(skin))
                filteredSkins.Add(skin);
        }
    }

    private bool PassesFilter(BallSkinData skin)
    {
        switch (currentFilter)
        {
            case CollectionFilterType.All:
                return true;
            case CollectionFilterType.Common:
                return skin.rarity == SkinRarity.Common;
            case CollectionFilterType.Rare:
                return skin.rarity == SkinRarity.Rare;
            case CollectionFilterType.Epic:
                return skin.rarity == SkinRarity.Epic;
            case CollectionFilterType.Legendary:
                return skin.rarity == SkinRarity.Legendary;
            default:
                return true;
        }
    }

    private int GetTotalPages()
    {
        if (itemsPerPage <= 0)
            return 0;

        if (filteredSkins.Count == 0)
            return 0;

        return Mathf.CeilToInt(filteredSkins.Count / (float)itemsPerPage);
    }

    private void RefreshHeaderTexts(int totalPages)
    {
        if (filterLabelText != null)
            filterLabelText.text = currentFilter.ToString().ToUpperInvariant();

        if (pageInfoText != null)
        {
            if (totalPages <= 0)
                pageInfoText.text = "Page 0 / 0";
            else
                pageInfoText.text = "Page " + (currentPageIndex + 1) + " / " + totalPages;
        }
    }

    private void RefreshSelectedPreview()
    {
        if (selectedSkin == null)
        {
            ClearSelectedPreviewVisuals();
            return;
        }

        if (selectedRarityText != null)
            selectedRarityText.text = selectedSkin.rarity.ToString().ToUpperInvariant();

        if (selectedSkinIdText != null)
            selectedSkinIdText.text = selectedSkin.skinUniqueId;

        if (selectedRarityBar != null)
            selectedRarityBar.color = GetRarityColor(selectedSkin.rarity);

        if (selectedPreviewSkinApplier != null && player1SkinInventory != null)
        {
            bool applied = selectedPreviewSkinApplier.ApplySkinData(player1SkinInventory.Database, selectedSkin);

            if (!applied)
                Debug.LogError("[SkinCollectionPagedUI] Failed to apply selected preview skin: " + selectedSkin.skinUniqueId, this);
        }
        else
        {
            Debug.LogWarning("[SkinCollectionPagedUI] Selected preview references missing.", this);
        }
    }

    private void ClearSelectedPreviewVisuals()
    {
        if (selectedRarityText != null)
            selectedRarityText.text = noSelectionText;

        if (selectedSkinIdText != null)
            selectedSkinIdText.text = string.Empty;

        if (selectedRarityBar != null)
            selectedRarityBar.color = Color.clear;
    }

    private void SetHiddenObjectsVisible(bool visible)
    {
        if (objectsToHideWhenPreviewOpen == null)
            return;

        for (int i = 0; i < objectsToHideWhenPreviewOpen.Count; i++)
        {
            if (objectsToHideWhenPreviewOpen[i] != null)
                objectsToHideWhenPreviewOpen[i].SetActive(visible);
        }
    }

    private void ClearGrid()
    {
        for (int i = gridContent.childCount - 1; i >= 0; i--)
            Destroy(gridContent.GetChild(i).gameObject);
    }

    private void CleanupGeneratedTextures()
    {
        for (int i = 0; i < generatedTextures.Count; i++)
        {
            if (generatedTextures[i] != null)
                Destroy(generatedTextures[i]);
        }

        generatedTextures.Clear();
    }

    private Color GetRarityColor(SkinRarity rarity)
    {
        switch (rarity)
        {
            case SkinRarity.Common:
                return new Color(0.75f, 0.75f, 0.75f);
            case SkinRarity.Rare:
                return new Color(0.25f, 0.55f, 1f);
            case SkinRarity.Epic:
                return new Color(0.7f, 0.3f, 1f);
            case SkinRarity.Legendary:
                return new Color(1f, 0.65f, 0.1f);
            default:
                return Color.white;
        }
    }
}