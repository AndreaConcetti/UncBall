using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct DailyLoginPopupRewardVisualMapping
{
    public DailyLoginRewardType rewardType;
    public Sprite sprite;
    public string labelOverride;
}

public class DailyLoginRewardPopupUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text amountText;

    [Header("Visuals")]
    [SerializeField] private Image rewardIcon;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;

    [Header("Reward mappings")]
    [SerializeField] private DailyLoginPopupRewardVisualMapping[] rewardVisualMappings;

    [Header("Options")]
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool verboseLogs = true;

    private bool suppressInitialHide;
    private bool listenersBound;

    private void Awake()
    {
        BindButtons();

        if (hideOnAwake && !suppressInitialHide)
            HideImmediate();

        suppressInitialHide = false;
    }

    private void OnEnable()
    {
        BindButtons();
    }

    public void Show(DailyLoginClaimResult result)
    {
        BindButtons();

        suppressInitialHide = true;

        GameObject root = GetEffectiveRoot();
        if (root != null)
            root.SetActive(true);

        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = "DAILY REWARD";

        if (dayText != null)
            dayText.text = "DAY " + Mathf.Max(1, result.claimedDayIndex);

        if (rewardText != null)
            rewardText.text = BuildRewardLabel(result);

        if (amountText != null)
            amountText.text = BuildAmountText(result);

        ApplyIcon(result.rewardType);

        ForceCanvasRefresh();

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardPopupUI] Show -> " +
                "Day=" + result.claimedDayIndex +
                " | Type=" + result.rewardType +
                " | Amount=" + result.amount +
                " | ChestType=" + result.chestType +
                " | Label=" + result.customLabel +
                " | Root=" + GetName(root),
                this);
        }
    }

    public void Hide()
    {
        GameObject root = GetEffectiveRoot();
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        GameObject root = GetEffectiveRoot();
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void BindButtons()
    {
        if (listenersBound)
            return;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Hide);
            continueButton.onClick.AddListener(Hide);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        listenersBound = true;
    }

    private GameObject GetEffectiveRoot()
    {
        if (panelRoot != null)
            return panelRoot;

        return gameObject;
    }

    private void ForceCanvasRefresh()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform rootRect = null;
        GameObject root = GetEffectiveRoot();

        if (root != null)
            rootRect = root.transform as RectTransform;

        if (rootRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

        if (rewardIcon != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardIcon.transform as RectTransform);
    }

    private void ApplyIcon(DailyLoginRewardType rewardType)
    {
        if (rewardIcon == null)
            return;

        DailyLoginPopupRewardVisualMapping mapping = FindMapping(rewardType);

        if (mapping.sprite != null)
        {
            rewardIcon.gameObject.SetActive(true);
            rewardIcon.enabled = true;
            rewardIcon.sprite = mapping.sprite;
            rewardIcon.preserveAspect = true;
        }
        else
        {
            rewardIcon.gameObject.SetActive(false);
        }
    }

    private string BuildRewardLabel(DailyLoginClaimResult result)
    {
        DailyLoginPopupRewardVisualMapping mapping = FindMapping(result.rewardType);

        if (!string.IsNullOrWhiteSpace(mapping.labelOverride))
            return mapping.labelOverride.ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(result.customLabel))
            return result.customLabel.ToUpperInvariant();

        switch (result.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                return "COINS";

            case DailyLoginRewardType.PremiumCurrency:
                return "GEMS";

            case DailyLoginRewardType.Chest:
                return "CHEST";

            case DailyLoginRewardType.FreeLuckyShot:
                return "FREE LUCKY SHOT";

            default:
                return "REWARD";
        }
    }

    private string BuildAmountText(DailyLoginClaimResult result)
    {
        switch (result.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
            case DailyLoginRewardType.PremiumCurrency:
                return "+" + Mathf.Max(0, result.amount);

            case DailyLoginRewardType.Chest:
            case DailyLoginRewardType.FreeLuckyShot:
                return "X" + Mathf.Max(1, result.amount);

            default:
                return string.Empty;
        }
    }

    private DailyLoginPopupRewardVisualMapping FindMapping(DailyLoginRewardType rewardType)
    {
        if (rewardVisualMappings != null)
        {
            for (int i = 0; i < rewardVisualMappings.Length; i++)
            {
                if (rewardVisualMappings[i].rewardType == rewardType)
                    return rewardVisualMappings[i];
            }
        }

        return default;
    }

    private string GetName(UnityEngine.Object target)
    {
        return target == null ? "<null>" : target.name;
    }
}