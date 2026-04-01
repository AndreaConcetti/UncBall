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
    [SerializeField] private PhotonFusionSessionController fusionSessionController;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int lastConsumedAcceptedNonce = -1;

    public bool IsNetworkStateReadable =>
        matchController != null &&
        matchController.IsNetworkStateReadable;

    public int RematchNonce => IsNetworkStateReadable ? matchController.RematchNonce : 0;

    public RematchState State
    {
        get
        {
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

        if (matchController != null && matchController.Object != null && matchController.Object.HasStateAuthority)
        {
            if (fusionSessionController != null)
            {
                bool loaded = fusionSessionController.LoadGameplaySceneOnActiveRunner();

                if (logDebug)
                {
                    Debug.Log(
                        "[FusionOnlineRematchController] Accepted rematch -> reload requested by scene authority. Loaded=" + loaded,
                        this
                    );
                }
            }
        }
    }

    public bool CanLocalRequestRematch()
    {
        if (!IsNetworkStateReadable)
            return false;

        if (!matchController.MatchEnded)
            return false;

        if (fusionSessionController != null && fusionSessionController.IsShuttingDown)
            return false;

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

        if (matchController.Object != null && matchController.Object.HasStateAuthority)
        {
            ForceLocalCancelledStateOnlyForUi();
            return;
        }

        ForceLocalCancelledStateOnlyForUi();
    }

    public void AcceptLocalRematch()
    {
        ResolveDependencies();

        if (!IsNetworkStateReadable)
            return;

        if (!HasIncomingRequestForLocalPlayer())
            return;

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

        matchController.DeclineLocalRematch();

        if (logDebug)
            Debug.Log("[FusionOnlineRematchController] DeclineLocalRematch", this);
    }

    private void ForceLocalCancelledStateOnlyForUi()
    {
        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineRematchController] CancelLocalRematchRequest -> local UI cancellation only. " +
                "Current match controller does not expose a replicated cancel API yet.",
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (matchController == null)
            matchController = GetComponent<FusionOnlineMatchController>();

        if (fusionSessionController == null)
            fusionSessionController = PhotonFusionSessionController.Instance;

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