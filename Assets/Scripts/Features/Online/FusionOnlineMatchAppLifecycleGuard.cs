using UnityEngine;

public class FusionOnlineMatchAppLifecycleGuard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FusionOnlineMatchController matchController;

    [Header("Timing")]
    [SerializeField] private float suspendForfeitDelaySeconds = 6f;

    [Header("Behavior")]
    [SerializeField] private bool useFocusEventsOnMobile = true;
    [SerializeField] private bool ignoreEditorFocusEvents = true;
    [SerializeField] private bool ignoreDesktopFocusEvents = true;
    [SerializeField] private bool logDebug = true;

    private bool suspendTrackingActive;
    private bool backgroundEnteredSent;
    private bool localSuspendForfeitTriggered;
    private float suspendStartedRealtime;
    private string suspendSource = string.Empty;

    private bool IsMobileRuntime
    {
        get { return Application.isMobilePlatform; }
    }

    private void Awake()
    {
        ResolveReferences();
        ResetState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        if (!suspendTrackingActive)
            return;

        if (localSuspendForfeitTriggered)
            return;

        if (!CanTrackSuspendRightNow())
        {
            ResetState();
            return;
        }

        float elapsed = Time.realtimeSinceStartup - suspendStartedRealtime;

        if (!backgroundEnteredSent)
        {
            backgroundEnteredSent = true;

            if (matchController != null)
                matchController.NotifyLocalBackgroundEntered();

            if (logDebug)
            {
                Debug.Log(
                    "[FusionOnlineMatchAppLifecycleGuard] Background entered sent.",
                    this);
            }
        }

        if (elapsed < suspendForfeitDelaySeconds)
            return;

        TriggerLocalSuspendForfeit("UpdateElapsed");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            BeginSuspendTracking("OnApplicationPause(true)");
            return;
        }

        EvaluateResume("OnApplicationPause(false)");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_EDITOR
        if (ignoreEditorFocusEvents)
            return;
#endif

        if (ignoreDesktopFocusEvents && !IsMobileRuntime)
            return;

        if (!useFocusEventsOnMobile && IsMobileRuntime)
            return;

        if (!hasFocus)
        {
            BeginSuspendTracking("OnApplicationFocus(false)");
            return;
        }

        EvaluateResume("OnApplicationFocus(true)");
    }

    private void BeginSuspendTracking(string source)
    {
        ResolveReferences();

        if (!CanTrackSuspendRightNow())
            return;

        if (suspendTrackingActive)
        {
            if (logDebug)
            {
                Debug.Log(
                    "[FusionOnlineMatchAppLifecycleGuard] Suspend tracking already active -> Source=" + source,
                    this);
            }

            return;
        }

        suspendTrackingActive = true;
        backgroundEnteredSent = false;
        localSuspendForfeitTriggered = false;
        suspendStartedRealtime = Time.realtimeSinceStartup;
        suspendSource = source;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchAppLifecycleGuard] Suspend tracking started -> Source=" + source +
                " | Delay=" + suspendForfeitDelaySeconds.ToString("F3"),
                this);
        }
    }

    private void EvaluateResume(string source)
    {
        ResolveReferences();

        if (!suspendTrackingActive && !localSuspendForfeitTriggered)
            return;

        float elapsed = 0f;

        if (suspendStartedRealtime > 0f)
            elapsed = Time.realtimeSinceStartup - suspendStartedRealtime;

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchAppLifecycleGuard] Resume detected -> Source=" + source +
                " | PreviousSuspendSource=" + suspendSource +
                " | Elapsed=" + elapsed.ToString("F3") +
                " | Threshold=" + suspendForfeitDelaySeconds.ToString("F3") +
                " | ForfeitTriggered=" + localSuspendForfeitTriggered,
                this);
        }

        bool wasTracking = suspendTrackingActive;
        bool exceededThreshold = elapsed >= suspendForfeitDelaySeconds;

        suspendTrackingActive = false;

        if (matchController == null)
        {
            ResetState();
            return;
        }

        if (localSuspendForfeitTriggered)
        {
            matchController.NotifyLocalReconnectedToSession();
            ResetState();
            return;
        }

        if (!wasTracking)
        {
            ResetState();
            return;
        }

        if (exceededThreshold)
        {
            TriggerLocalSuspendForfeit("ResumeAfterThreshold");
            matchController.NotifyLocalReconnectedToSession();
            ResetState();
            return;
        }

        if (backgroundEnteredSent)
            matchController.NotifyLocalBackgroundRecoveredBeforeTimeout();

        ResetState();
    }

    private void TriggerLocalSuspendForfeit(string source)
    {
        ResolveReferences();

        if (localSuspendForfeitTriggered)
            return;

        localSuspendForfeitTriggered = true;
        suspendTrackingActive = false;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchAppLifecycleGuard] TriggerLocalSuspendForfeit -> Source=" + source,
                this);
        }

        if (matchController != null)
            matchController.NotifyLocalAppBackgroundForfeit();
    }

    private bool CanTrackSuspendRightNow()
    {
        ResolveReferences();

        if (matchController == null)
            return false;

        if (!matchController.HasSpawnedNetworkState)
            return false;

        if (!matchController.MatchStarted)
            return false;

        if (matchController.MatchEnded)
            return false;

        if (matchController.IsTerminalOutcomeLocked)
            return false;

        return true;
    }

    private void ResolveReferences()
    {
        if (matchController == null)
        {
#if UNITY_2023_1_OR_NEWER
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
            matchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
        }
    }

    private void ResetState()
    {
        suspendTrackingActive = false;
        backgroundEnteredSent = false;
        localSuspendForfeitTriggered = false;
        suspendStartedRealtime = 0f;
        suspendSource = string.Empty;
    }
}
