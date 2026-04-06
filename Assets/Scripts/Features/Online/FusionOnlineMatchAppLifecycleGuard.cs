using UnityEngine;

public class FusionOnlineMatchAppLifecycleGuard : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;

    [Header("Suspend Policy")]
    [SerializeField] private bool monitorApplicationPause = true;
    [SerializeField] private bool monitorApplicationFocusLoss = true;
    [SerializeField] private float backgroundForfeitDelaySeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool suspendPending;
    private bool localTerminalForfeitTriggered;
    private float suspendCountdownRemaining;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CancelPendingSuspend();
        localTerminalForfeitTriggered = false;
    }

    private void Update()
    {
        if (!suspendPending)
            return;

        ResolveReferences();

        if (matchController == null)
        {
            CancelPendingSuspend();
            return;
        }

        if (!matchController.MatchStarted || matchController.MatchEnded || matchController.IsTerminalOutcomeLocked)
        {
            CancelPendingSuspend();
            return;
        }

        suspendCountdownRemaining -= Time.unscaledDeltaTime;
        if (suspendCountdownRemaining > 0f)
            return;

        TriggerLocalSuspendForfeit("CountdownExpired");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!monitorApplicationPause)
            return;

        if (pauseStatus)
            BeginSuspendCountdown("OnApplicationPause(true)");
        else
            HandleResumeSignal("OnApplicationPause(false)");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!monitorApplicationFocusLoss)
            return;

        if (!hasFocus)
            BeginSuspendCountdown("OnApplicationFocus(false)");
        else
            HandleResumeSignal("OnApplicationFocus(true)");
    }

    private void BeginSuspendCountdown(string source)
    {
        ResolveReferences();

        if (localTerminalForfeitTriggered)
            return;

        if (matchController == null)
            return;

        if (!matchController.MatchStarted || matchController.MatchEnded || matchController.IsTerminalOutcomeLocked)
            return;

        float delay = Mathf.Max(0f, backgroundForfeitDelaySeconds);

        if (delay <= 0f)
        {
            TriggerLocalSuspendForfeit(source);
            return;
        }

        suspendPending = true;
        suspendCountdownRemaining = delay;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchAppLifecycleGuard] Suspend detected -> Source=" + source +
                " | Delay=" + delay,
                this);
        }
    }

    private void HandleResumeSignal(string source)
    {
        if (!suspendPending)
            return;

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchAppLifecycleGuard] Resume detected before terminal forfeit -> Source=" + source,
                this);
        }

        CancelPendingSuspend();
    }

    private void TriggerLocalSuspendForfeit(string source)
    {
        ResolveReferences();

        if (localTerminalForfeitTriggered)
            return;

        suspendPending = false;
        suspendCountdownRemaining = 0f;
        localTerminalForfeitTriggered = true;

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

        matchController.NotifyLocalDisconnectedFromSession();
    }

    private void CancelPendingSuspend()
    {
        suspendPending = false;
        suspendCountdownRemaining = 0f;
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