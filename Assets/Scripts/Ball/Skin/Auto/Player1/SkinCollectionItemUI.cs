using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkinCollectionItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Image rarityBar;

    private BallSkinData boundSkin;
    private SkinCollectionPagedUI ownerPanel;

    public void Bind(BallSkinData skin, SkinCollectionPagedUI panel, Texture previewTexture)
    {
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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (boundSkin == null || ownerPanel == null)
            return;

        Debug.Log("[SkinCollectionItemUI] Clicked skin item: " + boundSkin.skinUniqueId, this);
        ownerPanel.SelectSkin(boundSkin);
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