using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginRewardItemUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text stateText;

    [Header("Visuals")]
    [SerializeField] private Image chestImage;
    [SerializeField] private GameObject claimedMark;
    [SerializeField] private GameObject todayHighlight;
    [SerializeField] private GameObject claimableHighlight;

    [Header("Chest mappings optional")]
    [SerializeField] private ChestVisualMapping[] chestVisualMappings;

    public void Bind(DailyLoginDayState state)
    {
        if (dayText != null)
            dayText.text = "DAY " + state.dayIndex;

        if (rewardText != null)
            rewardText.text = BuildRewardLabel(state.reward);

        if (stateText != null)
            stateText.text = BuildStateLabel(state);

        if (claimedMark != null)
            claimedMark.SetActive(state.isClaimed);

        if (todayHighlight != null)
            todayHighlight.SetActive(state.isToday);

        if (claimableHighlight != null)
            claimableHighlight.SetActive(state.isClaimable);

        ApplyChestVisual(state.reward);
    }

    private string BuildRewardLabel(DailyLoginRewardDefinition reward)
    {
        if (!string.IsNullOrWhiteSpace(reward.label))
            return reward.label.ToUpperInvariant();

        switch (reward.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                return reward.amount + " COINS";
            case DailyLoginRewardType.PremiumCurrency:
                return reward.amount + " GEMS";
            case DailyLoginRewardType.Chest:
                return "CHEST X" + Mathf.Max(1, reward.amount);
            default:
                return "-";
        }
    }

    private string BuildStateLabel(DailyLoginDayState state)
    {
        if (state.isClaimed)
            return "CLAIMED";

        if (state.isClaimable)
            return "TODAY";

        if (state.isMissed)
            return "UPCOMING";

        return string.Empty;
    }

    private void ApplyChestVisual(DailyLoginRewardDefinition reward)
    {
        if (chestImage == null)
            return;

        bool showChest = reward.rewardType == DailyLoginRewardType.Chest && reward.amount > 0;
        chestImage.gameObject.SetActive(showChest);

        if (!showChest)
            return;

        for (int i = 0; i < chestVisualMappings.Length; i++)
        {
            if (chestVisualMappings[i].chestType == reward.chestType)
            {
                chestImage.sprite = chestVisualMappings[i].sprite;
                return;
            }
        }
    }
}
