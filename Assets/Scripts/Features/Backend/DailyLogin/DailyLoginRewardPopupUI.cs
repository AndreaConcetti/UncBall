using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginRewardPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private Image chestImage;
    [SerializeField] private ChestVisualMapping[] chestVisualMappings;

    public void Show(DailyLoginClaimResult result)
    {
        if (root != null)
            root.SetActive(true);

        if (titleText != null)
            titleText.text = "DAILY REWARD CLAIMED";

        if (rewardText != null)
            rewardText.text = BuildRewardText(result.reward);

        if (subText != null)
            subText.text = "DAY " + result.claimedDay;

        ApplyChestVisual(result.reward);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private string BuildRewardText(DailyLoginRewardDefinition reward)
    {
        switch (reward.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                return "+" + reward.amount + " COINS";
            case DailyLoginRewardType.PremiumCurrency:
                return "+" + reward.amount + " GEMS";
            case DailyLoginRewardType.Chest:
                return "CHEST X" + Mathf.Max(1, reward.amount);
            default:
                return "REWARD";
        }
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
