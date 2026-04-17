using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginPanelUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform daysContainer;
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
    private DailyLoginPreviewState currentPreview;
    private bool isRefreshing;

    private void Awake()
    {
        ResolveService();

        if (claimButton != null)
            claimButton.onClick.AddListener(HandleClaimPressed);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

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

        ResolveService();

        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] OnEnable -> Service=" + (service != null), this);

        if (refreshOnEnable)
            RefreshNow();
    }

    private void Update()
    {
        if (!currentPreview.isReady)
            return;

        if (resetTimerText != null)
            resetTimerText.text = BuildResetText(currentPreview.nextResetUnixSeconds);
    }

    public void RefreshNow()
    {
        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] RefreshNow called.", this);

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (isRefreshing)
            return;

        isRefreshing = true;

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] RefreshAsync start -> " +
                "ServiceBeforeResolve=" + (service != null) +
                " | Container=" + GetName(daysContainer) +
                " | Prefab=" + GetName(dayItemPrefab),
                this);
        }

        ResolveService();

        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] RefreshAsync -> ServiceAfterResolve=" + (service != null), this);

        if (service == null)
        {
            SetFeedback("Daily login service missing.");
            isRefreshing = false;
            return;
        }

        await Task.Yield();

        currentPreview = service.GetPreviewState();
        RebuildItems(currentPreview);
        ApplyPreview(currentPreview);

        isRefreshing = false;
    }

    private void HandleClaimPressed()
    {
        ResolveService();

        if (service == null)
        {
            SetFeedback("Daily login service missing.");
            return;
        }

        DailyLoginClaimResult result = service.ClaimTodayReward();

        if (!result.success)
        {
            SetFeedback(string.IsNullOrWhiteSpace(result.failureReason)
                ? "Reward not claimable."
                : result.failureReason);

            RefreshNow();
            return;
        }

        if (rewardPopup != null)
            rewardPopup.Show(result);

        SetFeedback(string.Empty);
        RefreshNow();
    }

    private void RebuildItems(DailyLoginPreviewState preview)
    {
        if (daysContainer == null || dayItemPrefab == null)
        {
            SetFeedback("Daily login UI references missing.");
            return;
        }

        ClearContainer(daysContainer);

        if (preview.days == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[DailyLoginPanelUI] RebuildItems -> preview.days is null.", this);
            return;
        }

        for (int i = 0; i < preview.days.Length; i++)
        {
            DailyLoginRewardItemUI item = Instantiate(dayItemPrefab, daysContainer);
            item.Bind(preview.days[i]);

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

    private void ApplyPreview(DailyLoginPreviewState preview)
    {
        if (titleText != null)
            titleText.text = "DAILY LOGIN";

        if (streakText != null)
            streakText.text = "DAY " + Mathf.Clamp(preview.currentStreakDay, 0, 7) + "/7";

        if (resetTimerText != null)
            resetTimerText.text = BuildResetText(preview.nextResetUnixSeconds);

        if (claimButton != null)
            claimButton.interactable = preview.canClaimNow;

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyLoginPanelUI] ApplyPreview -> " +
                "Ready=" + preview.isReady +
                " | CurrentStreakDay=" + preview.currentStreakDay +
                " | NextClaimDay=" + preview.nextClaimDayIndex +
                " | SpawnedItems=" + (preview.days != null ? preview.days.Length : 0),
                this);
        }
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshNow();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
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

    private void ClearContainer(RectTransform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private string BuildResetText(long nextResetUnix)
    {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long remaining = Mathf.Max(0, (int)(nextResetUnix - now));

        int hours = (int)(remaining / 3600);
        int minutes = (int)((remaining % 3600) / 60);
        int seconds = (int)(remaining % 60);

        return "NEXT RESET IN " + hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;

        if (verboseLogs)
            Debug.Log("[DailyLoginPanelUI] Feedback -> " + message, this);
    }

    private string GetName(Object target)
    {
        return target == null ? "<null>" : target.name;
    }
}
