using UnityEngine;

public class FusionOnlineRematchController : MonoBehaviour
{
    public enum RematchState : byte
    {
        None = 0,
        Pending = 1,
        Accepted = 2,
        Declined = 3,
        Cancelled = 4
    }

    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineFlowController onlineFlowController;

    [Header("Config")]
    [SerializeField] private string gameplaySceneName = "Gameplay";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int lastConsumedAcceptedNonce = -1;
    private bool localUiCancelled;

    public bool IsNetworkStateReadable =>
        matchController != null &&
        matchController.IsNetworkStateReadable;

    public int RematchNonce => IsNetworkStateReadable ? matchController.RematchNonce : 0;

    public RematchState State
    {
        get
        {
            if (localUiCancelled)
                return RematchState.Cancelled;

            if (!IsNetworkStateReadable)
                return RematchState.None;

            if (matchController.RematchAccepted)
                return RematchState.Accepted;

            if (matchController.RematchDeclined)
                return RematchState.Declined;

            if (matchController.HasPendingRematchRequest)
                return RematchState.Pending;

            return RematchState.None;
        }
    }

    public bool HasPendingRequest => State == RematchState.Pending;

    public string OpponentDisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return "OPPONENT";

            return matchController.EffectiveLocalPlayerId == PlayerID.Player1
                ? SafeDisplayName(matchController.Player2DisplayName)
                : SafeDisplayName(matchController.Player1DisplayName);
        }
    }

    public string RequesterDisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return "OPPONENT";

            PlayerID requester = matchController.RematchRequester;

            if (requester == PlayerID.Player1)
                return SafeDisplayName(matchController.Player1DisplayName);

            if (requester == PlayerID.Player2)
                return SafeDisplayName(matchController.Player2DisplayName);

            return "OPPONENT";
        }
    }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        ResolveDependencies();

        if (!IsNetworkStateReadable)
            return;

        if (State != RematchState.Accepted)
            return;

        if (lastConsumedAcceptedNonce == RematchNonce)
            return;

        lastConsumedAcceptedNonce = RematchNonce;
        localUiCancelled = false;

        if (matchController != null &&
            matchController.Object != null &&
            matchController.Object.HasStateAuthority &&
            runnerManager != null &&
            runnerManager.IsRunning)
        {
            bool loaded = runnerManager.LoadNetworkScene(gameplaySceneName);

            if (logDebug)
            {
                Debug.Log(
                    "[FusionOnlineRematchController] Accepted rematch -> reload requested by scene authority. Loaded=" + loaded,
                    this
                );
            }
        }
    }

    public bool CanLocalRequestRematch()
    {
        if (!IsNetworkStateReadable)
            return false;

        if (!matchController.MatchEnded)
            return false;

        if (onlineFlowController != null &&
            onlineFlowController.CurrentState == OnlineFlowState.ReturningToMenu)
        {
            return false;
        }

        return State == RematchState.None || State == RematchState.Cancelled;
    }

    public bool HasOutgoingRequestFromLocalPlayer()
    {
        if (!IsNetworkStateReadable)
            return false;

        if (State != RematchState.Pending)
            return false;

        return matchController.RematchRequester == matchController.EffectiveLocalPlayerId;
    }

    public bool HasIncomingRequestForLocalPlayer()
    {
        if (!IsNetworkStateReadable)
            return false;

        if (State != RematchState.Pending)
            return false;

        return matchController.RematchRequester != matchController.EffectiveLocalPlayerId;
    }

    public void RequestLocalRematch()
    {
        ResolveDependencies();

        if (!CanLocalRequestRematch())
            return;

        localUiCancelled = false;
        matchController.RequestLocalRematch();

        if (logDebug)
            Debug.Log("[FusionOnlineRematchController] RequestLocalRematch", this);
    }

    public void CancelLocalRematchRequest()
    {
        ResolveDependencies();

        if (!IsNetworkStateReadable)
            return;

        if (!HasOutgoingRequestFromLocalPlayer())
            return;

        localUiCancelled = true;

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineRematchController] CancelLocalRematchRequest -> local UI cancellation only. " +
                "Replicated cancel API not implemented yet.",
                this
            );
        }
    }

    public void AcceptLocalRematch()
    {
        ResolveDependencies();

        if (!IsNetworkStateReadable)
            return;

        if (!HasIncomingRequestForLocalPlayer())
            return;

        localUiCancelled = false;
        matchController.AcceptLocalRematch();

        if (logDebug)
            Debug.Log("[FusionOnlineRematchController] AcceptLocalRematch", this);
    }

    public void DeclineLocalRematch()
    {
        ResolveDependencies();

        if (!IsNetworkStateReadable)
            return;

        if (State != RematchState.Pending)
            return;

        localUiCancelled = false;
        matchController.DeclineLocalRematch();

        if (logDebug)
            Debug.Log("[FusionOnlineRematchController] DeclineLocalRematch", this);
    }

    private void ResolveDependencies()
    {
        if (matchController == null)
            matchController = GetComponent<FusionOnlineMatchController>();

        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchController == null)
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        if (matchController == null)
            matchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
    }

    private string SafeDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "OPPONENT" : value.Trim();
    }
}