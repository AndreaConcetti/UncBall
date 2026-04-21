using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public sealed class LuckyShotSceneBootstrap : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;
    [SerializeField] private LuckyShotHighlightController highlightController;
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private BallLauncher ballLauncher;
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;

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
            ApplySkinToCurrentLuckyShotBall();

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

        if (highlightController != null)
            highlightController.ApplySession(session);

        ApplySkinToCurrentLuckyShotBall();
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (shotsLeftText != null)
            shotsLeftText.text = "SHOTS LEFT: " + Mathf.Max(0, preview.remainingShots);

        if (launchSideText != null)
            launchSideText.text = preview.launchSide == LuckyShotLaunchSide.Left ? "SHOOT FROM LEFT" : "SHOOT FROM RIGHT";

        if (sessionDateText != null)
            sessionDateText.text = preview.sessionDateUtc;

        ApplySideVisuals(preview.launchSide);
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

    public void ApplySkinToCurrentLuckyShotBall()
    {
        ResolveReferences();

        if (playerSkinLoadout == null || ballLauncher == null || ballLauncher.ball == null)
            return;

        BallPhysics targetBall = ballLauncher.ball;
        BallSkinData skin = playerSkinLoadout.GetEquippedSkinForPlayer1();

        if (skin == null)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSceneBootstrap] ApplySkinToCurrentLuckyShotBall -> Ball=" + targetBall.name +
                    " | Applied=False | Skin=none",
                    this);
            }

            return;
        }

        BallSkinApplier applier = targetBall.GetComponent<BallSkinApplier>();
        if (applier == null)
            applier = targetBall.GetComponentInChildren<BallSkinApplier>(true);

        if (applier == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotSceneBootstrap] BallSkinApplier missing on Lucky Shot ball.", targetBall);

            return;
        }

        if (playerSkinLoadout.Database == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotSceneBootstrap] PlayerSkinLoadout database missing.", this);

            return;
        }

        bool applied = applier.ApplySkinData(playerSkinLoadout.Database, skin);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSceneBootstrap] ApplySkinToCurrentLuckyShotBall -> Ball=" + targetBall.name +
                " | Applied=" + applied +
                " | SkinId=" + (skin.skinUniqueId ?? "null"),
                this);
        }
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

        if (ballLauncher == null)
            ballLauncher = FindFirstObjectByType<BallLauncher>();

        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance != null
                ? PlayerSkinLoadout.Instance
                : FindFirstObjectByType<PlayerSkinLoadout>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (highlightController == null)
            highlightController = FindObjectOfType<LuckyShotHighlightController>();

        if (slotRegistry == null)
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();

        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>();

        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance != null
                ? PlayerSkinLoadout.Instance
                : FindObjectOfType<PlayerSkinLoadout>();
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