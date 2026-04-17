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

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(Hide);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (hideOnAwake)
            HideImmediate();
    }

    public void Show(DailyLoginClaimResult result)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
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

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginRewardPopupUI] Show -> " +
                "Day=" + result.claimedDayIndex +
                " | Type=" + result.rewardType +
                " | Amount=" + result.amount +
                " | ChestType=" + result.chestType +
                " | Label=" + result.customLabel,
                this);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
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
                return "X" + Mathf.Max(1, result.amount);

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
}
