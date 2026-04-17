using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct DailyLoginRewardVisualMapping
{
    public DailyLoginRewardType rewardType;
    public Sprite sprite;
    public string labelOverride;
}

public class DailyLoginRewardItemUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text stateText;

    [Header("Visuals")]
    [SerializeField] private Image rewardIcon;
    [SerializeField] private GameObject claimedMark;
    [SerializeField] private GameObject todayHighlight;
    [SerializeField] private GameObject claimableHighlight;

    [Header("Reward mappings")]
    [SerializeField] private DailyLoginRewardVisualMapping[] rewardVisualMappings;

    private DailyLoginDayState currentState;

    public void Bind(DailyLoginDayState state)
    {
        currentState = state;

        if (dayText != null)
            dayText.text = "DAY " + Mathf.Max(1, state.dayIndex);

        if (rewardText != null)
            rewardText.text = BuildRewardText(state);

        if (stateText != null)
            stateText.text = BuildStateText(state);

        if (claimedMark != null)
            claimedMark.SetActive(state.isClaimed);

        if (todayHighlight != null)
            todayHighlight.SetActive(state.isToday);

        if (claimableHighlight != null)
            claimableHighlight.SetActive(state.isClaimable);

        ApplyIcon(state);
    }

    private void ApplyIcon(DailyLoginDayState state)
    {
        if (rewardIcon == null)
            return;

        DailyLoginRewardVisualMapping mapping = FindMapping(state.rewardType);

        if (mapping.sprite != null)
        {
            rewardIcon.gameObject.SetActive(true);
            rewardIcon.sprite = mapping.sprite;
            rewardIcon.enabled = true;
        }
        else
        {
            rewardIcon.gameObject.SetActive(false);
        }
    }

    private string BuildRewardText(DailyLoginDayState state)
    {
        switch (state.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                return state.amount + " COINS";

            case DailyLoginRewardType.PremiumCurrency:
                return state.amount + " GEMS";

            case DailyLoginRewardType.Chest:
                if (!string.IsNullOrWhiteSpace(state.customLabel))
                    return state.customLabel.ToUpperInvariant();

                return "CHEST";

            case DailyLoginRewardType.FreeLuckyShot:
                return state.amount > 1
                    ? state.amount + " FREE SHOTS"
                    : "FREE SHOT";

            default:
                return string.Empty;
        }
    }

    private string BuildStateText(DailyLoginDayState state)
    {
        if (state.isClaimed)
            return "CLAIMED";

        if (state.isClaimable)
            return "CLAIM NOW";

        if (state.isToday)
            return "READY";

        return "LOCKED";
    }

    private DailyLoginRewardVisualMapping FindMapping(DailyLoginRewardType rewardType)
    {
        if (rewardVisualMappings != null)
        {
            for (int i = 0; i < rewardVisualMappings.Length; i++)
            {
                if (rewardVisualMappings[i].rewardType == rewardType)
                    return rewardVisualMappings[i];
            }
        }

        return new DailyLoginRewardVisualMapping
        {
            rewardType = rewardType,
            sprite = null,
            labelOverride = string.Empty
        };
    }
}