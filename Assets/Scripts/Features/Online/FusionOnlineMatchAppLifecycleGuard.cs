using UnityEngine;

public class FusionOnlineMatchAppLifecycleGuard : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;

    [Header("Suspend Policy")]
    [SerializeField] private bool monitorApplicationPause = true;
    [SerializeField] private bool monitorApplicationFocusLoss = true;
    [SerializeField] private float backgroundForfeitDelaySeconds = 6f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool suspendTrackingActive;
    private bool localTerminalForfeitTriggered;
    private double suspendStartedRealtime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetSuspendTracking();
        localTerminalForfeitTriggered = false;
    }

    private void Update()
    {
        if (!suspendTrackingActive)
            return;

        ResolveReferences();

        if (matchController == null)
        {
            ResetSuspendTracking();
            return;
        }

        if (!matchController.MatchStarted || matchController.MatchEnded || matchController.IsTerminalOutcomeLocked)
        {
            ResetSuspendTracking();
            return;
        }

        double elapsed = Time.realtimeSinceStartupAsDouble - suspendStartedRealtime;
        if (elapsed < Mathf.Max(0f, backgroundForfeitDelaySeconds))
            return;

        TriggerLocalSuspendForfeit("UpdateElapsed");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!monitorApplicationPause)
            return;

        if (pauseStatus)
            BeginSuspendTracking("OnApplicationPause(true)");
        else
            EvaluateResume("OnApplicationPause(false)");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!monitorApplicationFocusLoss)
            return;

        if (!hasFocus)
            BeginSuspendTracking("OnApplicationFocus(false)");
        else
            EvaluateResume("OnApplicationFocus(true)");
    }

    private void BeginSuspendTracking(string source)
    {
        ResolveReferences();

        if (localTerminalForfeitTriggered)
            return;

        if (matchController == null)
            return;

        if (!matchController.MatchStarted || matchController.MatchEnded || matchController.IsTerminalOutcomeLocked)
            return;

        if (suspendTrackingActive)
            return;

        suspendTrackingActive = true;
        suspendStartedRealtime = Time.realtimeSinceStartupAsDouble;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchAppLifecycleGuard] Suspend tracking started -> Source=" + source +
                " | Delay=" + Mathf.Max(0f, backgroundForfeitDelaySeconds),
                this);
        }
    }

    private void EvaluateResume(string source)
    {
        if (!suspendTrackingActive)
            return;

        ResolveReferences();

        if (matchController == null)
        {
            ResetSuspendTracking();
            return;
        }

        double elapsed = Time.realtimeSinceStartupAsDouble - suspendStartedRealtime;
        float threshold = Mathf.Max(0f, backgroundForfeitDelaySeconds);

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchAppLifecycleGuard] Resume detected -> Source=" + source +
                " | Elapsed=" + elapsed.ToString("F3") +
                " | Threshold=" + threshold.ToString("F3"),
                this);
        }

        if (elapsed >= threshold &&
            matchController.MatchStarted &&
            !matchController.MatchEnded &&
            !matchController.IsTerminalOutcomeLocked)
        {
            TriggerLocalSuspendForfeit(source);
            return;
        }

        ResetSuspendTracking();
    }

    private void TriggerLocalSuspendForfeit(string source)
    {
        ResolveReferences();

        if (localTerminalForfeitTriggered)
            return;

        localTerminalForfeitTriggered = true;
        suspendTrackingActive = false;

        if (matchController == null)
            return;

        if (!matchController.MatchStarted || matchController.MatchEnded || matchController.IsTerminalOutcomeLocked)
            return;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchAppLifecycleGuard] TriggerLocalSuspendForfeit -> Source=" + source,
                this);
        }

        matchController.NotifyLocalAppBackgroundForfeit();
    }

    private void ResetSuspendTracking()
    {
        suspendTrackingActive = false;
        suspendStartedRealtime = 0d;
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
}