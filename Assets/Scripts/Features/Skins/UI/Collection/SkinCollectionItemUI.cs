using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SkinCollectionItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Image rarityBar;

    [Header("Optional Click Target")]
    [SerializeField] private Graphic clickTargetGraphic;
    [SerializeField] private Button optionalButton;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallSkinData boundSkin;
    private SkinCollectionPagedUI ownerPanel;

    private void Awake()
    {
        EnsureClickableRoot();
    }

    private void Reset()
    {
        AutoResolveReferences();
        EnsureClickableRoot();
    }

    private void OnValidate()
    {
        AutoResolveReferences();
    }

    public void Bind(BallSkinData skin, SkinCollectionPagedUI panel, Texture previewTexture)
    {
        EnsureClickableRoot();

        boundSkin = skin;
        ownerPanel = panel;

        if (previewImage != null)
        {
            previewImage.texture = previewTexture;
            previewImage.raycastTarget = false;
        }

        if (rarityBar != null)
        {
            if (skin != null)
                rarityBar.color = GetRarityColor(skin.rarity);

            rarityBar.raycastTarget = false;
        }

        if (logDebug)
        {
            Debug.Log(
                "[SkinCollectionItemUI] Bind -> Skin=" + (skin != null ? skin.skinUniqueId : "null") +
                " | HasOwner=" + (ownerPanel != null) +
                " | HasPreviewImage=" + (previewImage != null) +
                " | HasClickGraphic=" + (clickTargetGraphic != null),
                this
            );
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerSelection("IPointerClickHandler");
    }

    public void OnButtonClicked()
    {
        TriggerSelection("Button.onClick");
    }

    private void TriggerSelection(string source)
    {
        if (boundSkin == null)
        {
            if (logDebug)
                Debug.LogWarning("[SkinCollectionItemUI] Click ignored, boundSkin is null. Source=" + source, this);

            return;
        }

        if (ownerPanel == null)
        {
            if (logDebug)
                Debug.LogWarning("[SkinCollectionItemUI] Click ignored, ownerPanel is null. Source=" + source, this);

            return;
        }

        if (logDebug)
        {
            Debug.Log(
                "[SkinCollectionItemUI] Clicked skin item: " +
                boundSkin.skinUniqueId +
                " | Source=" + source,
                this
            );
        }

        ownerPanel.SelectSkin(boundSkin);
    }

    private void EnsureClickableRoot()
    {
        if (clickTargetGraphic == null)
            clickTargetGraphic = GetComponent<Graphic>();

        if (clickTargetGraphic == null)
        {
            Image rootImage = gameObject.GetComponent<Image>();
            if (rootImage == null)
                rootImage = gameObject.AddComponent<Image>();

            rootImage.color = new Color(1f, 1f, 1f, 0.001f);
            rootImage.raycastTarget = true;
            clickTargetGraphic = rootImage;

            if (logDebug)
                Debug.Log("[SkinCollectionItemUI] Added transparent root Image for raycast.", this);
        }
        else
        {
            clickTargetGraphic.raycastTarget = true;
        }

        if (optionalButton == null)
            optionalButton = GetComponent<Button>();

        if (optionalButton == null)
            optionalButton = gameObject.AddComponent<Button>();

        var colors = optionalButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        optionalButton.colors = colors;
        optionalButton.transition = Selectable.Transition.None;

        optionalButton.onClick.RemoveListener(OnButtonClicked);
        optionalButton.onClick.AddListener(OnButtonClicked);
    }

    private void AutoResolveReferences()
    {
        if (previewImage == null)
            previewImage = GetComponentInChildren<RawImage>(true);

        if (clickTargetGraphic == null)
            clickTargetGraphic = GetComponent<Graphic>();

        if (rarityBar == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null)
                    continue;

                if (images[i].gameObject.name.ToLowerInvariant().Contains("rarity"))
                {
                    rarityBar = images[i];
                    break;
                }
            }
        }

        if (optionalButton == null)
            optionalButton = GetComponent<Button>();
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