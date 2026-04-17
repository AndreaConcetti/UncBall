using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginPanelUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform daysContainer;
    [SerializeField] private DailyLoginRewardItemUI dayItemPrefab;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text streakText;
    [SerializeField] private TMP_Text resetTimerText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Buttons")]
    [SerializeField] private Button claimButton;
    [SerializeField] private Button closeButton;

    [Header("Optional popup presenter")]
    [SerializeField] private DailyLoginRewardPopupUI rewardPopup;

    [Header("Options")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool verboseLogs = true;

    private DailyLoginRewardService service;
    private CancellationTokenSource cts;
    private DailyLoginPreviewState currentPreview;
    private DailyLoginRewardItemUI[] spawnedItems = Array.Empty<DailyLoginRewardItemUI>();

    private void Awake()
    {
        service = DailyLoginRewardService.Instance;

        if (claimButton != null)
            claimButton.onClick.AddListener(OnPressClaim);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        cts = new CancellationTokenSource();

        if (service == null)
            service = DailyLoginRewardService.Instance;

        if (service != null)
        {
            service.PreviewUpdated += HandlePreviewUpdated;
            service.ClaimCompleted += HandleClaimCompleted;
        }

        if (refreshOnEnable)
            _ = RefreshAsync();
    }

    private void OnDisable()
    {
        if (service != null)
        {
            service.PreviewUpdated -= HandlePreviewUpdated;
            service.ClaimCompleted -= HandleClaimCompleted;
        }

        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void Update()
    {
        if (resetTimerText == null || !currentPreview.isReady)
            return;

        long seconds = Mathf.Max(0, (int)(currentPreview.nextResetUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        resetTimerText.text = $"NEXT RESET IN {span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    public async Task RefreshAsync()
    {
        if (service == null)
            service = DailyLoginRewardService.Instance;

        if (service == null)
            return;

        DailyLoginPreviewState preview = await service.RefreshPreviewAsync(cts != null ? cts.Token : default);
        ApplyPreview(preview);
    }

    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        _ = RefreshAsync();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private async void OnPressClaim()
    {
        if (service == null)
            return;

        SetFeedback("Claiming...");
        SetClaimInteractable(false);
        await service.ClaimTodayAsync(cts != null ? cts.Token : default);
    }

    private void HandlePreviewUpdated(DailyLoginPreviewState preview)
    {
        ApplyPreview(preview);
    }

    private void HandleClaimCompleted(DailyLoginClaimResult result)
    {
        ApplyPreview(result.previewAfterClaim);

        if (result.success)
        {
            SetFeedback(BuildClaimFeedback(result));

            if (rewardPopup != null)
                rewardPopup.Show(result);
        }
        else if (result.alreadyClaimed)
        {
            SetFeedback("Reward already claimed today.");
        }
        else
        {
            SetFeedback("Claim failed.");
        }
    }

    private void ApplyPreview(DailyLoginPreviewState preview)
    {
        currentPreview = preview;

        if (titleText != null)
            titleText.text = "DAILY LOGIN";

        if (streakText != null)
            streakText.text = $"DAY {Mathf.Clamp(preview.nextClaimDay, 1, 7)} / 7";

        RebuildItems(preview);
        SetClaimInteractable(HasClaimableDay(preview));

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] ApplyPreview -> " +
                "Ready=" + preview.isReady +
                " | CurrentStreakDay=" + preview.currentStreakDay +
                " | NextClaimDay=" + preview.nextClaimDay,
                this);
        }
    }

    private void RebuildItems(DailyLoginPreviewState preview)
    {
        if (daysContainer == null || dayItemPrefab == null)
            return;

        for (int i = daysContainer.childCount - 1; i >= 0; i--)
            Destroy(daysContainer.GetChild(i).gameObject);

        if (preview.days == null)
        {
            spawnedItems = Array.Empty<DailyLoginRewardItemUI>();
            return;
        }

        spawnedItems = new DailyLoginRewardItemUI[preview.days.Length];

        for (int i = 0; i < preview.days.Length; i++)
        {
            DailyLoginRewardItemUI item = Instantiate(dayItemPrefab, daysContainer);
            item.Bind(preview.days[i]);
            spawnedItems[i] = item;
        }
    }

    private bool HasClaimableDay(DailyLoginPreviewState preview)
    {
        if (preview.days == null)
            return false;

        for (int i = 0; i < preview.days.Length; i++)
        {
            if (preview.days[i].isClaimable)
                return true;
        }

        return false;
    }

    private void SetClaimInteractable(bool interactable)
    {
        if (claimButton != null)
            claimButton.interactable = interactable;
    }

    private string BuildClaimFeedback(DailyLoginClaimResult result)
    {
        if (result.reward.rewardType == DailyLoginRewardType.SoftCurrency)
            return $"Claimed {result.reward.amount} coins.";

        if (result.reward.rewardType == DailyLoginRewardType.PremiumCurrency)
            return $"Claimed {result.reward.amount} gems.";

        if (result.reward.rewardType == DailyLoginRewardType.Chest)
            return $"Claimed chest x{Mathf.Max(1, result.reward.amount)}.";

        return "Reward claimed.";
    }

    private void SetFeedback(string text)
    {
        if (feedbackText != null)
            feedbackText.text = text;
    }
}
