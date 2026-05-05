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
    [SerializeField] private CanvasGroup claimButtonCanvasGroup;
    [SerializeField] private TMP_Text claimButtonLabel;
    [SerializeField] private Button closeButton;

    [Header("Optional popup presenter")]
    [SerializeField] private DailyLoginRewardPopupUI rewardPopup;

    [Header("Options")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool clearFeedbackWhenClaimable = true;
    [SerializeField] private bool verboseLogs = true;

    private DailyLoginRewardService service;
    private CancellationTokenSource cts;
    private DailyLoginPreviewState currentPreview;
    private DailyLoginRewardItemUI[] spawnedItems = Array.Empty<DailyLoginRewardItemUI>();
    private bool canClaimCached;
    private bool claimInProgress;

    private void Awake()
    {
        ResolveService();
        ResolveOptionalButtonVisuals();

        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(OnPressClaim);
            claimButton.onClick.AddListener(OnPressClaim);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        ApplyClaimState(false, "CLAIM");

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] Awake -> " +
                "Service=" + (service != null) +
                " | PanelRoot=" + GetName(panelRoot) +
                " | DaysContainer=" + GetName(daysContainer) +
                " | DayItemPrefab=" + GetName(dayItemPrefab),
                this);
        }
    }

    private void OnEnable()
    {
        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] OnEnable called.", this);

        ResetCancellationToken();
        ResolveService();
        ResolveOptionalButtonVisuals();
        SubscribeServiceEvents();

        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] OnEnable -> Service=" + (service != null), this);

        if (refreshOnEnable)
            _ = RefreshAsync();
    }

    private void OnDisable()
    {
        UnsubscribeServiceEvents();
        DisposeCancellationToken();
        claimInProgress = false;
    }

    private void Update()
    {
        if (resetTimerText == null || !currentPreview.isReady)
            return;

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long secondsRemaining = Math.Max(0L, currentPreview.nextResetUnixSeconds - nowUnix);
        TimeSpan span = TimeSpan.FromSeconds(secondsRemaining);

        int totalHours = Mathf.Max(0, (int)span.TotalHours);
        resetTimerText.text = "NEXT RESET IN " + totalHours.ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
    }

    public void RefreshNow()
    {
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        ResolveService();

        if (service == null)
        {
            SetFeedback("Daily login service missing.");
            ApplyClaimState(false, "CLAIM");
            return;
        }

        try
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[DailyLoginPanelUI] RefreshAsync start -> " +
                    "Service=" + (service != null) +
                    " | Container=" + GetName(daysContainer) +
                    " | Prefab=" + GetName(dayItemPrefab),
                    this);
            }

            DailyLoginPreviewState preview = await service.RefreshPreviewAsync(cts != null ? cts.Token : default);
            ApplyPreview(preview);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginPanelUI] RefreshAsync failed -> " + ex, this);
            SetFeedback("Failed to refresh daily rewards.");
            ApplyClaimState(false, "CLAIM");
        }
    }

    public void OpenPanel()
    {
        SetPanelVisible(true);
        RefreshNow();
    }

    public async Task<bool> OpenPanelIfClaimAvailableAsync()
    {
        ResolveService();

        if (service == null)
        {
            SetPanelVisible(false);
            return false;
        }

        try
        {
            ResetCancellationToken();
            SubscribeServiceEvents();

            DailyLoginPreviewState preview = await service.RefreshPreviewAsync(cts != null ? cts.Token : default);
            ApplyPreview(preview);

            bool canOpen = IsPreviewClaimable(preview);
            SetPanelVisible(canOpen);

            if (verboseLogs)
                Debug.Log("[DailyLoginPanelUI] OpenPanelIfClaimAvailableAsync -> CanOpen=" + canOpen, this);

            return canOpen;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginPanelUI] OpenPanelIfClaimAvailableAsync failed -> " + ex, this);
            SetPanelVisible(false);
            return false;
        }
    }

    public void ClosePanel()
    {
        SetPanelVisible(false);
    }

    private async void OnPressClaim()
    {
        ResolveService();

        if (service == null)
        {
            SetFeedback("Daily login service missing.");
            ApplyClaimState(false, "CLAIM");
            return;
        }

        if (claimInProgress)
            return;

        if (!canClaimCached || !IsPreviewClaimable(currentPreview))
        {
            SetFeedback(currentPreview.alreadyClaimedToday ? "Reward already claimed." : "No daily reward available.");
            ApplyClaimState(false, currentPreview.alreadyClaimedToday ? "CLAIMED" : "CLAIM");
            return;
        }

        try
        {
            claimInProgress = true;
            SetFeedback("Claiming...");
            ApplyClaimState(false, "CLAIMING...");

            DailyLoginClaimResult result = await service.ClaimTodayAsync(cts != null ? cts.Token : default);

            if (result.previewAfterClaim.isReady)
                ApplyPreview(result.previewAfterClaim);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyLoginPanelUI] Claim failed -> " + ex, this);
            SetFeedback("Claim failed.");
            ApplyClaimState(IsPreviewClaimable(currentPreview), "CLAIM");
        }
        finally
        {
            claimInProgress = false;
            ApplyClaimState(IsPreviewClaimable(currentPreview), IsPreviewClaimable(currentPreview) ? "CLAIM" : "CLAIMED");
        }
    }

    private void HandlePreviewUpdated(DailyLoginPreviewState preview)
    {
        ApplyPreview(preview);
    }

    private void HandleClaimCompleted(DailyLoginClaimResult result)
    {
        if (result.previewAfterClaim.isReady)
            ApplyPreview(result.previewAfterClaim);

        if (result.success)
        {
            SetFeedback(BuildClaimFeedback(result));

            if (rewardPopup != null)
                rewardPopup.Show(result);
        }
        else if (result.alreadyClaimed)
        {
            SetFeedback("Reward already claimed.");
            ApplyClaimState(false, "CLAIMED");
        }
        else
        {
            SetFeedback(string.IsNullOrWhiteSpace(result.failureReason) ? "Claim failed." : result.failureReason);
            ApplyClaimState(IsPreviewClaimable(currentPreview), IsPreviewClaimable(currentPreview) ? "CLAIM" : "CLAIMED");
        }
    }

    private void ApplyPreview(DailyLoginPreviewState preview)
    {
        currentPreview = preview;

        if (titleText != null)
            titleText.text = "DAILY LOGIN";

        if (streakText != null)
            streakText.text = "DAY " + Mathf.Clamp(preview.currentStreakDay, 0, 7) + "/7";

        if (resetTimerText != null)
            resetTimerText.text = BuildResetText(preview.nextResetUnixSeconds);

        RebuildItems(preview);

        bool canClaim = IsPreviewClaimable(preview);
        string claimLabel = canClaim ? "CLAIM" : "CLAIMED";
        ApplyClaimState(canClaim && !claimInProgress, claimInProgress ? "CLAIMING..." : claimLabel);

        if (feedbackText != null && !claimInProgress)
        {
            if (canClaim && clearFeedbackWhenClaimable)
                feedbackText.text = string.Empty;
            else if (!canClaim && preview.alreadyClaimedToday)
                feedbackText.text = "Come back tomorrow.";
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] ApplyPreview -> " +
                "Ready=" + preview.isReady +
                " | CurrentStreakDay=" + preview.currentStreakDay +
                " | NextClaimDay=" + preview.nextClaimDayIndex +
                " | CanClaim=" + canClaim +
                " | AlreadyClaimedToday=" + preview.alreadyClaimedToday +
                " | SpawnedItems=" + (preview.days != null ? preview.days.Length : 0),
                this);
        }
    }

    private void RebuildItems(DailyLoginPreviewState preview)
    {
        if (daysContainer == null || dayItemPrefab == null)
        {
            SetFeedback("Daily login UI references missing.");
            return;
        }

        ClearContainer(daysContainer);

        if (preview.days == null || preview.days.Length == 0)
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

            if (verboseLogs)
            {
                Debug.Log(
                    "[DailyLoginPanelUI] Spawning item index=" + i +
                    " | DaySlot=" + preview.days[i].dayIndex +
                    " | Claimable=" + preview.days[i].isClaimable +
                    " | Claimed=" + preview.days[i].isClaimed,
                    this);
            }
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] RebuildItems complete -> " +
                "Spawned=" + preview.days.Length +
                " | ChildrenNow=" + daysContainer.childCount,
                this);
        }
    }

    private bool IsPreviewClaimable(DailyLoginPreviewState preview)
    {
        if (!preview.isReady || !preview.canClaimNow || preview.alreadyClaimedToday)
            return false;

        if (preview.days == null || preview.days.Length == 0)
            return false;

        for (int i = 0; i < preview.days.Length; i++)
        {
            if (preview.days[i].isClaimable)
                return true;
        }

        return false;
    }

    private void ApplyClaimState(bool interactable, string label)
    {
        canClaimCached = interactable;

        if (claimButton != null)
            claimButton.interactable = interactable;

        if (claimButtonCanvasGroup != null)
        {
            claimButtonCanvasGroup.alpha = interactable ? 1f : 0.45f;
            claimButtonCanvasGroup.interactable = interactable;
            claimButtonCanvasGroup.blocksRaycasts = interactable;
        }

        if (claimButtonLabel != null)
            claimButtonLabel.text = string.IsNullOrWhiteSpace(label) ? (interactable ? "CLAIM" : "CLAIMED") : label;
    }

    private string BuildClaimFeedback(DailyLoginClaimResult result)
    {
        switch (result.rewardType)
        {
            case DailyLoginRewardType.SoftCurrency:
                return "+" + Mathf.Max(0, result.amount) + " coins";

            case DailyLoginRewardType.PremiumCurrency:
                return "+" + Mathf.Max(0, result.amount) + " gems";

            case DailyLoginRewardType.Chest:
                return "Chest received";

            case DailyLoginRewardType.FreeLuckyShot:
                return "+" + Mathf.Max(0, result.amount) + " free lucky shot";

            default:
                return "Reward claimed";
        }
    }

    private void ResolveService()
    {
        if (service != null)
            return;

        service = DailyLoginRewardService.Instance;

#if UNITY_2023_1_OR_NEWER
        if (service == null)
            service = FindFirstObjectByType<DailyLoginRewardService>();
#else
        if (service == null)
            service = FindObjectOfType<DailyLoginRewardService>();
#endif
    }

    private void ResolveOptionalButtonVisuals()
    {
        if (claimButton != null)
        {
            if (claimButtonCanvasGroup == null)
                claimButtonCanvasGroup = claimButton.GetComponent<CanvasGroup>();

            if (claimButtonCanvasGroup == null)
                claimButtonCanvasGroup = claimButton.gameObject.AddComponent<CanvasGroup>();

            if (claimButtonLabel == null)
                claimButtonLabel = claimButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void SubscribeServiceEvents()
    {
        if (service == null)
            return;

        service.PreviewUpdated -= HandlePreviewUpdated;
        service.PreviewUpdated += HandlePreviewUpdated;

        service.ClaimCompleted -= HandleClaimCompleted;
        service.ClaimCompleted += HandleClaimCompleted;
    }

    private void UnsubscribeServiceEvents()
    {
        if (service == null)
            return;

        service.PreviewUpdated -= HandlePreviewUpdated;
        service.ClaimCompleted -= HandleClaimCompleted;
    }

    private void ResetCancellationToken()
    {
        DisposeCancellationToken();
        cts = new CancellationTokenSource();
    }

    private void DisposeCancellationToken()
    {
        if (cts == null)
            return;

        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);
        else
            gameObject.SetActive(visible);
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private string BuildResetText(long nextResetUnix)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long remaining = Math.Max(0L, nextResetUnix - now);

        int hours = (int)(remaining / 3600L);
        int minutes = (int)((remaining % 3600L) / 60L);
        int seconds = (int)(remaining % 60L);

        return "NEXT RESET IN " + hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;

        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] Feedback -> " + message, this);
    }

    private string GetName(UnityEngine.Object target)
    {
        return target == null ? "<null>" : target.name;
    }
}
