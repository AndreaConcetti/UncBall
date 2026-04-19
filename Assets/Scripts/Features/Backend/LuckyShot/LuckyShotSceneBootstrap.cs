using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public sealed class LuckyShotSceneBootstrap : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;
    [SerializeField] private LuckyShotHighlightController highlightController;
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text shotsLeftText;
    [SerializeField] private TMP_Text launchSideText;
    [SerializeField] private TMP_Text sessionDateText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Optional side state roots")]
    [SerializeField] private GameObject leftSideRoot;
    [SerializeField] private GameObject rightSideRoot;

    [Header("Startup")]
    [SerializeField] private bool bootstrapOnEnable = true;
    [SerializeField] private bool clearFeedbackOnSuccess = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private bool isBootstrapping;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (bootstrapOnEnable)
            BootstrapNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void BootstrapNow()
    {
        if (isBootstrapping)
            return;

        _ = BootstrapAsync();
    }

    private async Task BootstrapAsync()
    {
        ResolveReferences();

        if (sessionRuntime == null)
        {
            SetFeedback("Lucky Shot session runtime missing.");
            return;
        }

        if (slotRegistry == null)
        {
            SetFeedback("Lucky Shot slot registry missing.");
            return;
        }

        isBootstrapping = true;
        SetFeedback("Loading Lucky Shot session...");

        try
        {
            LuckyShotActiveSession session = await sessionRuntime.EnsureSessionLoadedAsync();

            if (!session.IsValid())
            {
                SetFeedback("Lucky Shot session invalid.");
                return;
            }

            if (highlightController != null)
                highlightController.ApplySession(session);

            ApplySideVisuals(session.launchSide);
            ApplySessionTexts(session);

            if (clearFeedbackOnSuccess)
                SetFeedback(string.Empty);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSceneBootstrap] BootstrapAsync -> " +
                    "SessionId=" + session.sessionId +
                    " | Side=" + session.launchSide +
                    " | RemainingShots=" + session.remainingShots,
                    this);
            }
        }
        finally
        {
            isBootstrapping = false;
        }
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        ApplySideVisuals(session.launchSide);
        ApplySessionTexts(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (shotsLeftText != null)
            shotsLeftText.text = "SHOTS LEFT: " + Mathf.Max(0, preview.remainingShots);

        if (launchSideText != null)
            launchSideText.text = preview.launchSide == LuckyShotLaunchSide.Left ? "SHOOT FROM LEFT" : "SHOOT FROM RIGHT";

        if (sessionDateText != null)
            sessionDateText.text = preview.sessionDateUtc;
    }

    private void HandleFeedbackRaised(string message)
    {
        SetFeedback(message);
    }

    private void ApplySideVisuals(LuckyShotLaunchSide side)
    {
        bool isLeft = side == LuckyShotLaunchSide.Left;

        if (leftSideRoot != null)
            leftSideRoot.SetActive(isLeft);

        if (rightSideRoot != null)
            rightSideRoot.SetActive(!isLeft);
    }

    private void ApplySessionTexts(LuckyShotActiveSession session)
    {
        if (shotsLeftText != null)
            shotsLeftText.text = "SHOTS LEFT: " + Mathf.Max(0, session.remainingShots);

        if (launchSideText != null)
            launchSideText.text = session.launchSide == LuckyShotLaunchSide.Left ? "SHOOT FROM LEFT" : "SHOOT FROM RIGHT";

        if (sessionDateText != null)
            sessionDateText.text = session.sessionDateUtc ?? string.Empty;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;

        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.Log("[LuckyShotSceneBootstrap] Feedback -> " + message, this);
    }

    private void ResolveReferences()
    {
        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();

        if (highlightController == null)
            highlightController = FindFirstObjectByType<LuckyShotHighlightController>();

        if (slotRegistry == null)
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (highlightController == null)
            highlightController = FindObjectOfType<LuckyShotHighlightController>();

        if (slotRegistry == null)
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();
#endif
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
        sessionRuntime.FeedbackRaised += HandleFeedbackRaised;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
    }
}
