using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class FusionOnlineMatchController : NetworkBehaviour
{
    public enum MatchEndReason : byte
    {
        None = 0,
        NormalCompletion = 1,
        OpponentDisconnected = 2,
        LocalDisconnected = 3,
        OpponentSurrendered = 4,
        LocalSurrendered = 5,
        MatchCancelled = 6,
        ReconnectTimeout = 7
    }

    private enum MidMatchBreakReason : byte
    {
        None = 0,
        TimeHalftime = 1,
        ScoreHalfPoint = 2
    }

    private enum RematchState : byte
    {
        None = 0,
        Pending = 1,
        Accepted = 2,
        Declined = 3
    }

    [Header("Scene References")]
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FusionOnlineMatchHUD hud;
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;
    [SerializeField] private ShotScorePopupUI shotScorePopupUI;
    [SerializeField] private OnlineFlowController onlineFlowController;

    [Header("Rules")]
    [SerializeField] private float defaultTurnDuration = 15f;
    [SerializeField] private float defaultBreakDuration = 8f;
    [SerializeField] private float postShotPanelDelay = 1.5f;
    [SerializeField] private bool enableStuckBallCheck = true;
    [SerializeField] private float stuckTimeout = 2.5f;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;

    [Header("Gameplay Watchdogs")]
    [SerializeField] private float authorityHeartbeatSendInterval = 0.25f;
    [SerializeField] private float gameplayDisconnectTimeoutSeconds = 6.0f;
    [SerializeField] private float clientHeartbeatSendInterval = 0.25f;

    [Header("Fallback Match Config")]
    [SerializeField] private MatchMode fallbackMatchMode = MatchMode.TimeLimit;
    [SerializeField] private int fallbackPointsToWin = 16;
    [SerializeField] private float fallbackMatchDurationSeconds = 180f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [Networked] private byte NetMatchModeRaw { get; set; }
    [Networked] private int NetPointsToWin { get; set; }
    [Networked] private float NetConfiguredMatchDuration { get; set; }
    [Networked] private float NetConfiguredTurnDuration { get; set; }

    [Networked] private byte NetCurrentTurnOwnerRaw { get; set; }
    [Networked] private float NetTurnTimeRemaining { get; set; }
    [Networked] private float NetMatchTimeRemaining { get; set; }

    [Networked] private int NetScoreP1 { get; set; }
    [Networked] private int NetScoreP2 { get; set; }

    [Networked] private NetworkBool NetMatchStarted { get; set; }
    [Networked] private NetworkBool NetMatchEnded { get; set; }

    [Networked] private NetworkBool NetBreakTriggered { get; set; }
    [Networked] private NetworkBool NetBreakActive { get; set; }
    [Networked] private float NetBreakTimeRemaining { get; set; }
    [Networked] private byte NetBreakReasonRaw { get; set; }

    [Networked] private byte NetWinnerRaw { get; set; }
    [Networked] private NetworkBool NetShotInFlight { get; set; }
    [Networked] private NetworkId NetActiveBallId { get; set; }

    [Networked] private NetworkBool NetPlayer1OnLeft { get; set; }

    [Networked] private NetworkBool NetPendingBreakAfterShot { get; set; }
    [Networked] private NetworkBool NetPendingEndAfterShot { get; set; }
    [Networked] private float NetPostShotDelayRemaining { get; set; }

    [Networked, Capacity(64)] private NetworkString<_64> NetPlayer1DisplayName { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetPlayer2DisplayName { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetPlayer1SkinId { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetPlayer2SkinId { get; set; }

    [Networked] private NetworkBool NetPlayer1PresentationLocked { get; set; }
    [Networked] private NetworkBool NetPlayer2PresentationLocked { get; set; }

    [Networked] private int NetShotPopupSequence { get; set; }
    [Networked] private int NetShotPopupCombo { get; set; }
    [Networked] private int NetShotPopupPoints { get; set; }
    [Networked] private Vector3 NetShotPopupWorldPosition { get; set; }

    [Networked] private int NetRematchNonce { get; set; }
    [Networked] private byte NetRematchRequesterRaw { get; set; }
    [Networked] private byte NetRematchStateRaw { get; set; }

    [Networked] private byte NetEndReasonRaw { get; set; }
    [Networked] private int NetAuthorityHeartbeat { get; set; }

    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private float stuckTimer;
    private bool currentPlayerScoredThisTurn;
    private bool scoreSubscribed;
    private bool localPresentationSubmitted;
    private int lastConsumedShotPopupSequence = -1;

    private bool hasSpawnedNetworkState;
    private bool hasInitializedAuthorityState;

    private bool localForcedEndActive;
    private MatchEndReason localForcedEndReason = MatchEndReason.None;
    private PlayerID localForcedWinner = PlayerID.None;

    private MatchMode cachedMatchMode = MatchMode.TimeLimit;
    private int cachedPointsToWin;
    private float cachedConfiguredMatchDuration;
    private PlayerID cachedCurrentTurnOwner = PlayerID.Player1;
    private float cachedTurnTimeRemaining;
    private float cachedMatchTimeRemaining;
    private int cachedScoreP1;
    private int cachedScoreP2;
    private bool cachedMatchStarted;
    private bool cachedMatchEnded;
    private bool cachedMidBreakActive;
    private bool cachedIsTimeHalftime;
    private bool cachedIsHalfPoint;
    private float cachedBreakTimeRemaining;
    private string cachedPlayer1Name = "Player 1";
    private string cachedPlayer2Name = "Player 2";
    private PlayerID cachedWinner = PlayerID.None;
    private bool cachedPlayer1OnLeft = true;

    private float authorityHeartbeatSendTimer;
    private int lastObservedAuthorityHeartbeat = int.MinValue;
    private float authorityHeartbeatStaleTimer;

    private float clientHeartbeatSendTimer;
    private float remoteClientHeartbeatStaleTimer;
    private bool authorityRemoteWatchdogStarted;
    private bool clientSentInitialHeartbeat;

    private bool localAuthorityConnectionLostVisible;
    private bool localRemoteClientConnectionLostVisible;
    private bool localTransportConnectionIssueVisible;

    private string localAuthorityConnectionLostPlayerName = string.Empty;
    private string localRemoteClientConnectionLostPlayerName = string.Empty;

    private bool localGameplayHardLocked;

    private bool authorityRemoteDisconnectPending;
    private PlayerID authorityRemoteDisconnectPendingPlayer = PlayerID.None;
    private float authorityRemoteDisconnectPendingElapsed;

    public bool HasSpawnedNetworkState => hasSpawnedNetworkState;

    public bool IsNetworkStateReadable =>
        hasSpawnedNetworkState &&
        Object != null &&
        Object.IsValid &&
        Runner != null;

    public bool IsTerminalOutcomeLocked =>
        localForcedEndActive ||
        cachedMatchEnded ||
        (IsNetworkStateReadable && NetMatchEnded);

    public bool IsAuthorityConnectionLostOverlayVisible =>
        localAuthorityConnectionLostVisible ||
        localRemoteClientConnectionLostVisible ||
        localTransportConnectionIssueVisible ||
        authorityRemoteDisconnectPending;

    public bool IsGameplayHardLocked => localGameplayHardLocked;

    public MatchMode CurrentMatchMode => IsNetworkStateReadable ? (MatchMode)NetMatchModeRaw : cachedMatchMode;
    public int PointsToWin => IsNetworkStateReadable ? NetPointsToWin : cachedPointsToWin;
    public float ConfiguredMatchDuration => IsNetworkStateReadable ? NetConfiguredMatchDuration : cachedConfiguredMatchDuration;
    public float ConfiguredTurnDuration => IsNetworkStateReadable ? NetConfiguredTurnDuration : defaultTurnDuration;

    public PlayerID CurrentTurnOwner => IsNetworkStateReadable ? (PlayerID)NetCurrentTurnOwnerRaw : cachedCurrentTurnOwner;
    public float CurrentTurnTimeRemaining => IsNetworkStateReadable ? NetTurnTimeRemaining : cachedTurnTimeRemaining;
    public float CurrentMatchTimeRemaining => IsNetworkStateReadable ? NetMatchTimeRemaining : cachedMatchTimeRemaining;

    public bool MatchStarted
    {
        get
        {
            if (localForcedEndActive || localGameplayHardLocked)
                return false;

            if (IsNetworkStateReadable)
                return NetMatchStarted;

            return cachedMatchStarted;
        }
    }

    public bool MatchEnded
    {
        get
        {
            if (localForcedEndActive || localGameplayHardLocked)
                return true;

            if (IsNetworkStateReadable)
                return NetMatchEnded;

            return cachedMatchEnded;
        }
    }

    public bool MidMatchBreakActive => IsNetworkStateReadable ? NetBreakActive : cachedMidBreakActive;
    public float CurrentBreakTimeRemaining => IsNetworkStateReadable ? NetBreakTimeRemaining : cachedBreakTimeRemaining;

    public bool IsTimeHalftimeActive =>
        IsNetworkStateReadable
            ? NetBreakActive && (MidMatchBreakReason)NetBreakReasonRaw == MidMatchBreakReason.TimeHalftime
            : cachedIsTimeHalftime;

    public bool IsHalfPointActive =>
        IsNetworkStateReadable
            ? NetBreakActive && (MidMatchBreakReason)NetBreakReasonRaw == MidMatchBreakReason.ScoreHalfPoint
            : cachedIsHalfPoint;

    public int ScoreP1 => IsNetworkStateReadable ? NetScoreP1 : cachedScoreP1;
    public int ScoreP2 => IsNetworkStateReadable ? NetScoreP2 : cachedScoreP2;

    public PlayerID Winner
    {
        get
        {
            if (localForcedEndActive)
                return localForcedWinner;

            if (IsNetworkStateReadable)
                return (PlayerID)NetWinnerRaw;

            return cachedWinner;
        }
    }

    public MatchEndReason EndReason
    {
        get
        {
            if (localForcedEndActive)
                return localForcedEndReason;

            if (IsNetworkStateReadable)
                return (MatchEndReason)NetEndReasonRaw;

            return MatchEndReason.None;
        }
    }

    public string Player1DisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return string.IsNullOrWhiteSpace(cachedPlayer1Name) ? "Player 1" : cachedPlayer1Name;

            string value = NetPlayer1DisplayName.ToString();
            return string.IsNullOrWhiteSpace(value) ? "Player 1" : value;
        }
    }

    public string Player2DisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return string.IsNullOrWhiteSpace(cachedPlayer2Name) ? "Player 2" : cachedPlayer2Name;

            string value = NetPlayer2DisplayName.ToString();
            return string.IsNullOrWhiteSpace(value) ? "Player 2" : value;
        }
    }

    public string Player1SkinId => IsNetworkStateReadable ? NetPlayer1SkinId.ToString() : string.Empty;
    public string Player2SkinId => IsNetworkStateReadable ? NetPlayer2SkinId.ToString() : string.Empty;

    public PlayerID EffectiveLocalPlayerId
    {
        get
        {
            ResolveReferences();

            if (onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession)
                return onlineGameplayAuthority.LocalPlayerId;

            return Runner != null && Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;
        }
    }

    public PlayerID EffectiveRemotePlayerId
    {
        get
        {
            ResolveReferences();

            if (onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession)
                return onlineGameplayAuthority.RemotePlayerId;

            return EffectiveLocalPlayerId == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        }
    }

    public int RematchNonce => IsNetworkStateReadable ? NetRematchNonce : 0;
    public PlayerID RematchRequester => IsNetworkStateReadable ? (PlayerID)NetRematchRequesterRaw : PlayerID.None;
    public bool HasPendingRematchRequest => IsNetworkStateReadable && (RematchState)NetRematchStateRaw == RematchState.Pending;
    public bool RematchAccepted => IsNetworkStateReadable && (RematchState)NetRematchStateRaw == RematchState.Accepted;
    public bool RematchDeclined => IsNetworkStateReadable && (RematchState)NetRematchStateRaw == RematchState.Declined;

    public BallPhysics CurrentBall
    {
        get
        {
            if (!IsNetworkStateReadable || NetActiveBallId == default)
                return null;

            if (!Runner.TryFindObject(NetActiveBallId, out NetworkObject obj))
                return null;

            return obj != null ? obj.GetComponent<BallPhysics>() : null;
        }
    }

    public override void Spawned()
    {
        hasSpawnedNetworkState = true;

        ResolveReferences();
        RegisterAuthorityLocally();
        SubscribeToScoreIfNeeded();

        if (Object.HasStateAuthority && !hasInitializedAuthorityState)
        {
            InitializeAuthorityState();
            hasInitializedAuthorityState = true;
        }

        lastObservedAuthorityHeartbeat = IsNetworkStateReadable ? NetAuthorityHeartbeat : int.MinValue;
        authorityHeartbeatStaleTimer = 0f;

        clientHeartbeatSendTimer = 0f;
        remoteClientHeartbeatStaleTimer = 0f;
        authorityRemoteWatchdogStarted = false;
        clientSentInitialHeartbeat = false;

        localAuthorityConnectionLostVisible = false;
        localRemoteClientConnectionLostVisible = false;
        localTransportConnectionIssueVisible = false;

        localAuthorityConnectionLostPlayerName = string.Empty;
        localRemoteClientConnectionLostPlayerName = string.Empty;

        localGameplayHardLocked = false;

        authorityRemoteDisconnectPending = false;
        authorityRemoteDisconnectPendingPlayer = PlayerID.None;
        authorityRemoteDisconnectPendingElapsed = 0f;

        SubmitLocalPresentationIfNeeded();
        ApplyReplicaToLocalSystems();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawnedNetworkState = false;
        UnsubscribeFromScore();
    }

    private void Update()
    {
        ResolveReferences();

        if (localForcedEndActive || localGameplayHardLocked)
        {
            ApplyLocalOverrideToHud();
            return;
        }

        TickLocalAuthorityHeartbeatWatchdog();
        TickAuthorityRemoteClientHeartbeatWatchdog();
        TickAuthorityRemoteDisconnectPending();
    }

    public override void FixedUpdateNetwork()
    {
        ResolveReferences();

        if (!IsNetworkStateReadable)
            return;

        if (Object.HasStateAuthority)
            RefreshAuthoritativePresentationFromSession();

        RegisterAuthorityLocally();

        if (Object.HasStateAuthority)
        {
            TickAuthorityHeartbeat();
            TickAuthority();
        }
        else
        {
            TickClientHeartbeatSender();
        }

        SubmitLocalPresentationIfNeeded();
    }

    public override void Render()
    {
        ResolveReferences();

        if (!IsNetworkStateReadable)
            return;

        RegisterAuthorityLocally();
        ApplyReplicaToLocalSystems();
        ConsumeReplicatedShotPopupIfNeeded();
    }

    public void RequestLocalRematch()
    {
        if (!IsTerminalOutcomeLocked)
            return;

        if (!MatchEnded || EndReason != MatchEndReason.NormalCompletion)
            return;

        if (Object.HasStateAuthority)
        {
            AuthorityStartRematchRequest(EffectiveLocalPlayerId);
            return;
        }

        RPC_RequestRematch((byte)EffectiveLocalPlayerId);
    }

    public void AcceptLocalRematch()
    {
        if (!MatchEnded || EndReason != MatchEndReason.NormalCompletion)
            return;

        if (Object.HasStateAuthority)
        {
            AuthorityAcceptRematch(EffectiveLocalPlayerId);
            return;
        }

        RPC_AcceptRematch((byte)EffectiveLocalPlayerId);
    }

    public void DeclineLocalRematch()
    {
        if (!MatchEnded)
            return;

        if (Object.HasStateAuthority)
        {
            AuthorityDeclineRematch(EffectiveLocalPlayerId);
            return;
        }

        RPC_DeclineRematch((byte)EffectiveLocalPlayerId);
    }

    public void RequestLocalSurrender()
    {
        if (!MatchStarted || MatchEnded || IsTerminalOutcomeLocked)
            return;

        if (Object.HasStateAuthority)
        {
            AuthorityResolveSurrender(EffectiveLocalPlayerId);
            return;
        }

        RPC_RequestSurrender((byte)EffectiveLocalPlayerId);
    }

    public void RequestSetCurrentBallPlacement(Vector3 targetWorldPosition)
    {
        if (!IsNetworkStateReadable || IsTerminalOutcomeLocked)
            return;

        if (localGameplayHardLocked || IsAuthorityConnectionLostOverlayVisible)
            return;

        if (!MatchStarted || MatchEnded || MidMatchBreakActive || NetShotInFlight || NetPostShotDelayRemaining > 0f)
            return;

        PlayerID requester = EffectiveLocalPlayerId;
        BallPhysics currentBall = CurrentBall;
        if (currentBall == null)
            return;

        BallOwnership ownership = currentBall.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != requester)
            return;

        Vector3 sanitizedTarget = SanitizePlacementTarget(targetWorldPosition);

        if (Object.HasStateAuthority)
        {
            ApplyPlacementAuthority(requester, sanitizedTarget);
            return;
        }

        RPC_RequestSetPlacement(sanitizedTarget);
    }

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!IsNetworkStateReadable || IsTerminalOutcomeLocked)
            return;

        if (localGameplayHardLocked || IsAuthorityConnectionLostOverlayVisible)
            return;

        if (!NetMatchStarted || NetMatchEnded || NetBreakActive || NetPostShotDelayRemaining > 0f)
            return;

        if (Object.HasStateAuthority)
        {
            ApplyLaunchAuthority(CurrentTurnOwner, direction, force);
            return;
        }

        RPC_RequestLaunch(direction, force);
    }

    public void RequestResumeAfterHalftime()
    {
        if (!MidMatchBreakActive || MatchEnded || IsTerminalOutcomeLocked)
            return;

        if (Object.HasStateAuthority)
        {
            ResumeAfterMidMatchBreakAuthority();
            return;
        }

        RPC_RequestResumeAfterHalftime();
    }

    public void NotifyRunnerPlayerLeft(PlayerRef player)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        if (!Object.HasStateAuthority)
            return;

        PlayerID leaver = ResolvePlayerIdFromPlayerRef(player);
        if (leaver == PlayerID.None)
            leaver = EffectiveRemotePlayerId;

        if (leaver == EffectiveRemotePlayerId)
        {
            BeginAuthorityRemoteDisconnectPending(leaver, "OnPlayerLeft");
            return;
        }

        AuthorityResolveDisconnectForPlayer(leaver);
    }

    public void NotifyLocalDisconnectedFromSession()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!MatchStarted && !cachedMatchStarted)
            return;

        HideAllConnectionIssueOverlays();
        ForceLocalEnd(MatchEndReason.LocalDisconnected, EffectiveRemotePlayerId);
    }

    public void NotifyLocalAppBackgroundForfeit()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!MatchStarted && !cachedMatchStarted)
            return;

        HideAllConnectionIssueOverlays();

        bool localHasAuthority = Object != null && Object.HasStateAuthority;

        if (localHasAuthority)
        {
            AuthorityResolveDisconnectForPlayer(EffectiveLocalPlayerId);

            if (!NetMatchEnded)
                ForceLocalEnd(MatchEndReason.LocalDisconnected, EffectiveRemotePlayerId);
            else
                LockLocalGameplayAfterLoss();

            return;
        }

        RPC_ReportClientBackgroundForfeit((byte)EffectiveLocalPlayerId);
        ForceLocalEnd(MatchEndReason.LocalDisconnected, EffectiveRemotePlayerId);
    }

    public void NotifyRunnerShutdown(ShutdownReason reason)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!MatchStarted && !cachedMatchStarted)
            return;

        HideAllConnectionIssueOverlays();
        ForceLocalEnd(MatchEndReason.LocalDisconnected, EffectiveRemotePlayerId);
    }

    public void NotifyRemoteAuthorityLostAsClient()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        if (Object != null && Object.HasStateAuthority)
            return;

        ShowAuthorityConnectionLostOverlay();
        ForceLocalEnd(MatchEndReason.OpponentDisconnected, EffectiveLocalPlayerId);
    }

    public void NotifyLocalReconnectedToSession()
    {
        if (localForcedEndActive || localGameplayHardLocked)
            return;

        HideAllConnectionIssueOverlays();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetPlacement(Vector3 targetWorldPosition, RpcInfo info = default)
    {
        if (IsTerminalOutcomeLocked)
            return;

        PlayerID requester = ResolvePlayerIdFromRpc(info);
        ApplyPlacementAuthority(requester, SanitizePlacementTarget(targetWorldPosition));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRematch(byte requesterRaw)
    {
        if (localForcedEndActive)
            return;

        AuthorityStartRematchRequest((PlayerID)requesterRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AcceptRematch(byte accepterRaw)
    {
        if (localForcedEndActive)
            return;

        AuthorityAcceptRematch((PlayerID)accepterRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_DeclineRematch(byte declinerRaw)
    {
        if (localForcedEndActive)
            return;

        AuthorityDeclineRematch((PlayerID)declinerRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitLocalPresentation(byte playerRaw, string displayName, string skinId)
    {
        if (IsTerminalOutcomeLocked)
            return;

        ApplyPresentationForPlayer((PlayerID)playerRaw, displayName, skinId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLaunch(Vector3 direction, float force, RpcInfo info = default)
    {
        if (IsTerminalOutcomeLocked)
            return;

        PlayerID requester = ResolvePlayerIdFromRpc(info);
        ApplyLaunchAuthority(requester, direction, force);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestResumeAfterHalftime(RpcInfo info = default)
    {
        if (IsTerminalOutcomeLocked)
            return;

        ResumeAfterMidMatchBreakAuthority();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSurrender(byte surrenderingPlayerRaw)
    {
        if (IsTerminalOutcomeLocked)
            return;

        AuthorityResolveSurrender((PlayerID)surrenderingPlayerRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientHeartbeat(RpcInfo info = default)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority)
            return;

        if (!NetMatchStarted || NetMatchEnded)
            return;

        PlayerID sender = ResolvePlayerIdFromRpc(info);
        if (sender != EffectiveRemotePlayerId)
            return;

        authorityRemoteWatchdogStarted = true;
        remoteClientHeartbeatStaleTimer = 0f;
        HideRemoteClientConnectionLostOverlay();
        CancelAuthorityRemoteDisconnectPending(sender);

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] RPC_ClientHeartbeat received from remote client.", this);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportClientBackgroundForfeit(byte playerRaw)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority)
            return;

        PlayerID player = (PlayerID)playerRaw;
        if (player != EffectiveRemotePlayerId)
            return;

        BeginAuthorityRemoteDisconnectPending(player, "RPC_ReportClientBackgroundForfeit");
    }

    private void AuthorityStartRematchRequest(PlayerID requester)
    {
        if (localForcedEndActive)
            return;

        if (!Object.HasStateAuthority || !MatchEnded || EndReason != MatchEndReason.NormalCompletion)
            return;

        if ((RematchState)NetRematchStateRaw == RematchState.Pending ||
            (RematchState)NetRematchStateRaw == RematchState.Accepted)
            return;

        NetRematchNonce += 1;
        NetRematchRequesterRaw = (byte)requester;
        NetRematchStateRaw = (byte)RematchState.Pending;
    }

    private void AuthorityAcceptRematch(PlayerID accepter)
    {
        if (localForcedEndActive)
            return;

        if (!Object.HasStateAuthority || !MatchEnded || EndReason != MatchEndReason.NormalCompletion)
            return;

        if ((RematchState)NetRematchStateRaw != RematchState.Pending)
            return;

        if (accepter == (PlayerID)NetRematchRequesterRaw)
            return;

        NetRematchStateRaw = (byte)RematchState.Accepted;
    }

    private void AuthorityDeclineRematch(PlayerID decliner)
    {
        if (localForcedEndActive)
            return;

        if (!Object.HasStateAuthority || !MatchEnded)
            return;

        if ((RematchState)NetRematchStateRaw == RematchState.Declined)
            return;

        NetRematchStateRaw = (byte)RematchState.Declined;
    }

    private void AuthorityResolveSurrender(PlayerID surrenderingPlayer)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || !NetMatchStarted || NetMatchEnded)
            return;

        PlayerID winner = surrenderingPlayer == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        MatchEndReason reason = surrenderingPlayer == EffectiveLocalPlayerId
            ? MatchEndReason.LocalSurrendered
            : MatchEndReason.OpponentSurrendered;

        EndMatchAuthority(reason, winner);
    }

    private void AuthorityResolveDisconnectForPlayer(PlayerID disconnectedPlayer)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || !NetMatchStarted || NetMatchEnded)
            return;

        if (disconnectedPlayer != PlayerID.Player1 && disconnectedPlayer != PlayerID.Player2)
            return;

        CancelAuthorityRemoteDisconnectPending(disconnectedPlayer);

        PlayerID winner = disconnectedPlayer == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        MatchEndReason reason = disconnectedPlayer == EffectiveLocalPlayerId
            ? MatchEndReason.LocalDisconnected
            : MatchEndReason.OpponentDisconnected;

        EndMatchAuthority(reason, winner);

        if (disconnectedPlayer == EffectiveLocalPlayerId)
            LockLocalGameplayAfterLoss();
    }

    private void BeginAuthorityRemoteDisconnectPending(PlayerID player, string source)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        if (player != EffectiveRemotePlayerId)
            return;

        authorityRemoteDisconnectPending = true;
        authorityRemoteDisconnectPendingPlayer = player;
        authorityRemoteDisconnectPendingElapsed = 0f;
        authorityRemoteWatchdogStarted = true;
        remoteClientHeartbeatStaleTimer = 0f;

        ShowRemoteClientConnectionLostOverlay();

        if (logDebug)
        {
            Debug.LogWarning(
                "[FusionOnlineMatchController] BeginAuthorityRemoteDisconnectPending -> Source=" + source +
                " | Player=" + player,
                this);
        }
    }

    private void CancelAuthorityRemoteDisconnectPending(PlayerID player)
    {
        if (!authorityRemoteDisconnectPending)
            return;

        if (authorityRemoteDisconnectPendingPlayer != player)
            return;

        authorityRemoteDisconnectPending = false;
        authorityRemoteDisconnectPendingPlayer = PlayerID.None;
        authorityRemoteDisconnectPendingElapsed = 0f;
        HideRemoteClientConnectionLostOverlay();

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] CancelAuthorityRemoteDisconnectPending -> Player=" + player, this);
    }

    private void TickAuthorityRemoteDisconnectPending()
    {
        if (!authorityRemoteDisconnectPending)
            return;

        if (IsTerminalOutcomeLocked)
            return;

        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        authorityRemoteDisconnectPendingElapsed += Time.unscaledDeltaTime;

        float timeout = Mathf.Max(0.25f, gameplayDisconnectTimeoutSeconds);
        if (authorityRemoteDisconnectPendingElapsed < timeout)
            return;

        PlayerID pendingPlayer = authorityRemoteDisconnectPendingPlayer;
        authorityRemoteDisconnectPending = false;
        authorityRemoteDisconnectPendingPlayer = PlayerID.None;
        authorityRemoteDisconnectPendingElapsed = 0f;

        AuthorityResolveDisconnectForPlayer(pendingPlayer);
    }

    private void ForceLocalEnd(MatchEndReason reason, PlayerID winner)
    {
        if (IsTerminalOutcomeLocked)
            return;

        localForcedEndActive = true;
        localForcedEndReason = reason;
        localForcedWinner = winner;

        cachedMatchStarted = false;
        cachedMatchEnded = true;
        cachedWinner = winner;

        LockLocalGameplayAfterLoss();
        ResolveReferences();

        string disconnectedPlayerName = ResolveDisconnectedPlayerNameForReason(reason, winner);

        if (hud != null)
        {
            hud.ForceShowPostGame(
                cachedMatchMode,
                cachedPointsToWin,
                cachedPlayer1Name,
                cachedPlayer2Name,
                cachedScoreP1,
                cachedScoreP2,
                winner,
                ConvertToOnlineMatchEndReason(reason),
                reason == MatchEndReason.OpponentDisconnected,
                disconnectedPlayerName
            );
        }

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchEnded();
    }

    private void LockLocalGameplayAfterLoss()
    {
        localGameplayHardLocked = true;

        if (ballTurnSpawner != null && ballTurnSpawner.Launcher != null)
        {
            ballTurnSpawner.Launcher.ball = null;
            ballTurnSpawner.Launcher.SetActivePlacementArea(null);
        }
    }

    private void TickAuthorityHeartbeat()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority)
            return;

        if (!NetMatchStarted || NetMatchEnded)
            return;

        authorityHeartbeatSendTimer += Runner.DeltaTime;
        if (authorityHeartbeatSendTimer < Mathf.Max(0.01f, authorityHeartbeatSendInterval))
            return;

        authorityHeartbeatSendTimer = 0f;
        NetAuthorityHeartbeat += 1;
    }

    private void TickClientHeartbeatSender()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (Object == null || Object.HasStateAuthority)
            return;

        if (!NetMatchStarted || NetMatchEnded)
            return;

        if (!clientSentInitialHeartbeat)
        {
            clientSentInitialHeartbeat = true;
            RPC_ClientHeartbeat();

            if (logDebug)
                Debug.Log("[FusionOnlineMatchController] Initial client heartbeat sent.", this);
        }

        clientHeartbeatSendTimer += Time.unscaledDeltaTime;
        if (clientHeartbeatSendTimer < Mathf.Max(0.01f, clientHeartbeatSendInterval))
            return;

        clientHeartbeatSendTimer = 0f;
        RPC_ClientHeartbeat();

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] Periodic client heartbeat sent.", this);
    }

    private void TickLocalAuthorityHeartbeatWatchdog()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!IsNetworkStateReadable)
            return;

        if (Object != null && Object.HasStateAuthority)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        int currentHeartbeat = NetAuthorityHeartbeat;

        if (lastObservedAuthorityHeartbeat == int.MinValue)
        {
            lastObservedAuthorityHeartbeat = currentHeartbeat;
            authorityHeartbeatStaleTimer = 0f;
            HideAuthorityConnectionLostOverlay();
            HideLocalTransportConnectionIssueOverlay();
            return;
        }

        if (currentHeartbeat != lastObservedAuthorityHeartbeat)
        {
            lastObservedAuthorityHeartbeat = currentHeartbeat;
            authorityHeartbeatStaleTimer = 0f;
            HideAuthorityConnectionLostOverlay();
            HideLocalTransportConnectionIssueOverlay();
            return;
        }

        authorityHeartbeatStaleTimer += Time.unscaledDeltaTime;

        float timeout = Mathf.Max(0.25f, gameplayDisconnectTimeoutSeconds);
        float overlayThreshold = Mathf.Min(timeout, Mathf.Max(0.15f, timeout * 0.35f));
        bool localNetworkUnavailable = IsLocalNetworkLikelyUnavailable();

        if (authorityHeartbeatStaleTimer >= overlayThreshold)
        {
            if (localNetworkUnavailable)
            {
                ShowLocalTransportConnectionIssueOverlay();
                HideAuthorityConnectionLostOverlay();
            }
            else
            {
                ShowAuthorityConnectionLostOverlay();
                HideLocalTransportConnectionIssueOverlay();
            }
        }

        if (authorityHeartbeatStaleTimer < timeout)
            return;

        if (localNetworkUnavailable || localTransportConnectionIssueVisible)
        {
            ForceLocalEnd(MatchEndReason.LocalDisconnected, EffectiveRemotePlayerId);
            return;
        }

        NotifyRemoteAuthorityLostAsClient();
    }

    private void TickAuthorityRemoteClientHeartbeatWatchdog()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!IsNetworkStateReadable)
            return;

        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!MatchStarted || MatchEnded)
            return;

        if (!authorityRemoteWatchdogStarted)
            return;

        if (authorityRemoteDisconnectPending)
            return;

        remoteClientHeartbeatStaleTimer += Time.unscaledDeltaTime;

        float timeout = Mathf.Max(0.25f, gameplayDisconnectTimeoutSeconds);
        float overlayThreshold = Mathf.Min(timeout, Mathf.Max(0.15f, timeout * 0.35f));

        if (remoteClientHeartbeatStaleTimer >= overlayThreshold)
            BeginAuthorityRemoteDisconnectPending(EffectiveRemotePlayerId, "HeartbeatTimeoutOverlay");

        if (remoteClientHeartbeatStaleTimer < timeout)
            return;
    }

    private bool IsLocalNetworkLikelyUnavailable()
    {
        return Application.internetReachability == NetworkReachability.NotReachable;
    }

    private void ShowAuthorityConnectionLostOverlay()
    {
        if (localAuthorityConnectionLostVisible)
            return;

        localAuthorityConnectionLostVisible = true;
        localAuthorityConnectionLostPlayerName = Player1DisplayName;
    }

    private void HideAuthorityConnectionLostOverlay()
    {
        if (!localAuthorityConnectionLostVisible)
            return;

        localAuthorityConnectionLostVisible = false;
        localAuthorityConnectionLostPlayerName = string.Empty;
    }

    private void ShowRemoteClientConnectionLostOverlay()
    {
        if (localRemoteClientConnectionLostVisible)
            return;

        localRemoteClientConnectionLostVisible = true;
        localRemoteClientConnectionLostPlayerName =
            EffectiveRemotePlayerId == PlayerID.Player1 ? Player1DisplayName : Player2DisplayName;
    }

    private void HideRemoteClientConnectionLostOverlay()
    {
        if (!localRemoteClientConnectionLostVisible)
            return;

        localRemoteClientConnectionLostVisible = false;
        localRemoteClientConnectionLostPlayerName = string.Empty;
    }

    private void ShowLocalTransportConnectionIssueOverlay()
    {
        if (localTransportConnectionIssueVisible)
            return;

        localTransportConnectionIssueVisible = true;
    }

    private void HideLocalTransportConnectionIssueOverlay()
    {
        if (!localTransportConnectionIssueVisible)
            return;

        localTransportConnectionIssueVisible = false;
    }

    private void HideAllConnectionIssueOverlays()
    {
        HideAuthorityConnectionLostOverlay();
        HideRemoteClientConnectionLostOverlay();
        HideLocalTransportConnectionIssueOverlay();
        authorityRemoteDisconnectPending = false;
        authorityRemoteDisconnectPendingPlayer = PlayerID.None;
        authorityRemoteDisconnectPendingElapsed = 0f;
    }

    private void ResolveReferences()
    {
        if (ballTurnSpawner == null)
        {
#if UNITY_2023_1_OR_NEWER
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();
#else
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();
#endif
        }

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (hud == null)
        {
#if UNITY_2023_1_OR_NEWER
            hud = FindFirstObjectByType<FusionOnlineMatchHUD>();
#else
            hud = FindObjectOfType<FusionOnlineMatchHUD>();
#endif
        }

        if (bottomBarOrderSwapper == null)
        {
#if UNITY_2023_1_OR_NEWER
            bottomBarOrderSwapper = FindFirstObjectByType<BottomBarOrderSwapper>();
#else
            bottomBarOrderSwapper = FindObjectOfType<BottomBarOrderSwapper>();
#endif
        }

        if (shotScorePopupUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            shotScorePopupUI = FindFirstObjectByType<ShotScorePopupUI>();
#else
            shotScorePopupUI = FindObjectOfType<ShotScorePopupUI>();
#endif
        }
    }

    private void RefreshAuthoritativePresentationFromSession()
    {
        if (IsTerminalOutcomeLocked)
            return;

        MatchSessionContext session = GetCurrentSessionContext();
        if (session == null)
            return;

        if (!NetPlayer1PresentationLocked)
        {
            if (!string.IsNullOrWhiteSpace(session.player1DisplayName))
                NetPlayer1DisplayName = session.player1DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(session.player1SkinUniqueId))
                NetPlayer1SkinId = session.player1SkinUniqueId.Trim();
        }

        if (!NetPlayer2PresentationLocked)
        {
            if (!string.IsNullOrWhiteSpace(session.player2DisplayName))
                NetPlayer2DisplayName = session.player2DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(session.player2SkinUniqueId))
                NetPlayer2SkinId = session.player2SkinUniqueId.Trim();
        }
    }

    private void RegisterAuthorityLocally()
    {
        if (onlineGameplayAuthority == null)
            return;

        string localName = EffectiveLocalPlayerId == PlayerID.Player1 ? Player1DisplayName : Player2DisplayName;
        string remoteName = EffectiveLocalPlayerId == PlayerID.Player1 ? Player2DisplayName : Player1DisplayName;

        onlineGameplayAuthority.ConfigureOnline(
            EffectiveLocalPlayerId,
            EffectiveRemotePlayerId,
            Object != null && Object.HasStateAuthority,
            localName,
            remoteName,
            CurrentTurnOwner == PlayerID.None ? PlayerID.Player1 : CurrentTurnOwner
        );

        onlineGameplayAuthority.SetOnlineMatchController(this);
        onlineGameplayAuthority.SetStateAuthority(Object != null && Object.HasStateAuthority);
        onlineGameplayAuthority.SetCurrentTurnOwner(CurrentTurnOwner);
    }

    private void SubscribeToScoreIfNeeded()
    {
        if (scoreSubscribed || scoreManager == null)
            return;

        scoreManager.onPointsScored.AddListener(OnScoreManagerPointsScored);
        scoreManager.onShotScoreDetailed.AddListener(OnShotScoreDetailed);
        scoreSubscribed = true;
    }

    private void UnsubscribeFromScore()
    {
        if (!scoreSubscribed || scoreManager == null)
            return;

        scoreManager.onPointsScored.RemoveListener(OnScoreManagerPointsScored);
        scoreManager.onShotScoreDetailed.RemoveListener(OnShotScoreDetailed);
        scoreSubscribed = false;
    }

    private void InitializeAuthorityState()
    {
        MatchSessionContext session = GetCurrentSessionContext();

        MatchMode resolvedMatchMode = session != null ? session.matchMode : fallbackMatchMode;
        int resolvedPointsToWin = session != null ? Mathf.Max(1, session.pointsToWin) : Mathf.Max(1, fallbackPointsToWin);
        float resolvedMatchDuration = session != null
            ? Mathf.Max(1f, session.matchDurationSeconds)
            : Mathf.Max(1f, fallbackMatchDurationSeconds);

        float resolvedTurnDuration = session != null
            ? Mathf.Max(1f, session.turnDurationSeconds)
            : Mathf.Max(1f, defaultTurnDuration);

        NetMatchModeRaw = (byte)resolvedMatchMode;
        NetPointsToWin = resolvedPointsToWin;
        NetConfiguredMatchDuration = resolvedMatchDuration;
        NetConfiguredTurnDuration = resolvedTurnDuration;

        NetCurrentTurnOwnerRaw = (byte)PlayerID.Player1;
        NetTurnTimeRemaining = resolvedTurnDuration;
        NetMatchTimeRemaining = resolvedMatchDuration;

        NetScoreP1 = 0;
        NetScoreP2 = 0;

        NetMatchStarted = true;
        NetMatchEnded = false;

        NetBreakTriggered = false;
        NetBreakActive = false;
        NetBreakTimeRemaining = defaultBreakDuration;
        NetBreakReasonRaw = (byte)MidMatchBreakReason.None;

        NetWinnerRaw = (byte)PlayerID.None;
        NetShotInFlight = false;
        NetActiveBallId = default;

        NetPlayer1OnLeft = true;

        NetPendingBreakAfterShot = false;
        NetPendingEndAfterShot = false;
        NetPostShotDelayRemaining = 0f;

        NetPlayer1DisplayName = ResolveDisplayNameForPlayer(PlayerID.Player1);
        NetPlayer2DisplayName = ResolveDisplayNameForPlayer(PlayerID.Player2);

        NetPlayer1SkinId = ResolveInitialSkinIdForPlayer(PlayerID.Player1);
        NetPlayer2SkinId = ResolveInitialSkinIdForPlayer(PlayerID.Player2);

        if (string.IsNullOrWhiteSpace(NetPlayer1DisplayName.ToString()))
            NetPlayer1DisplayName = "Player 1";

        if (string.IsNullOrWhiteSpace(NetPlayer2DisplayName.ToString()))
            NetPlayer2DisplayName = "Player 2";

        if (Runner != null && Runner.IsServer)
        {
            string localServerName = ResolveLocalDisplayName();
            string localServerSkinId = ResolveLocalEquippedSkinId();

            if (!string.IsNullOrWhiteSpace(localServerName))
                NetPlayer1DisplayName = localServerName.Trim();

            if (!string.IsNullOrWhiteSpace(localServerSkinId))
                NetPlayer1SkinId = localServerSkinId.Trim();
        }

        NetPlayer1PresentationLocked = false;
        NetPlayer2PresentationLocked = false;

        NetShotPopupSequence = 0;
        NetShotPopupCombo = 0;
        NetShotPopupPoints = 0;
        NetShotPopupWorldPosition = Vector3.zero;

        NetRematchNonce = 0;
        NetRematchRequesterRaw = (byte)PlayerID.None;
        NetRematchStateRaw = (byte)RematchState.None;

        NetEndReasonRaw = (byte)MatchEndReason.None;
        NetAuthorityHeartbeat = 0;
        authorityHeartbeatSendTimer = 0f;

        clientHeartbeatSendTimer = 0f;
        remoteClientHeartbeatStaleTimer = 0f;
        authorityRemoteWatchdogStarted = false;
        clientSentInitialHeartbeat = false;

        authorityRemoteDisconnectPending = false;
        authorityRemoteDisconnectPendingPlayer = PlayerID.None;
        authorityRemoteDisconnectPendingElapsed = 0f;

        if (scoreManager != null)
            scoreManager.StartMatch();

        if (ballTurnSpawner != null)
            ballTurnSpawner.SetPlayer1Side(true);

        if (ballTurnSpawner != null)
            SpawnNewActiveBallAuthority(PlayerID.Player1);

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);
    }

    private void SubmitLocalPresentationIfNeeded()
    {
        if (localPresentationSubmitted || !IsNetworkStateReadable || IsTerminalOutcomeLocked)
            return;

        string localName = ResolveLocalDisplayName();
        string localSkinId = ResolveLocalEquippedSkinId();

        if (Object.HasStateAuthority)
        {
            ApplyPresentationForPlayer(EffectiveLocalPlayerId, localName, localSkinId);
            localPresentationSubmitted = true;
            return;
        }

        RPC_SubmitLocalPresentation((byte)EffectiveLocalPlayerId, localName, localSkinId);
        localPresentationSubmitted = true;
    }

    private void ApplyPresentationForPlayer(PlayerID player, string displayName, string skinId)
    {
        if (IsTerminalOutcomeLocked)
            return;

        string safeName = string.IsNullOrWhiteSpace(displayName)
            ? (player == PlayerID.Player1 ? "Player 1" : "Player 2")
            : displayName.Trim();

        string safeSkin = string.IsNullOrWhiteSpace(skinId)
            ? string.Empty
            : skinId.Trim();

        if (player == PlayerID.Player1)
        {
            NetPlayer1DisplayName = safeName;

            if (!string.IsNullOrWhiteSpace(safeSkin))
                NetPlayer1SkinId = safeSkin;

            NetPlayer1PresentationLocked = true;
        }
        else if (player == PlayerID.Player2)
        {
            NetPlayer2DisplayName = safeName;

            if (!string.IsNullOrWhiteSpace(safeSkin))
                NetPlayer2SkinId = safeSkin;

            NetPlayer2PresentationLocked = true;
        }
    }

    private void TickAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!NetMatchStarted || NetMatchEnded)
            return;

        if (NetPostShotDelayRemaining > 0f)
        {
            NetPostShotDelayRemaining -= Runner.DeltaTime;
            if (NetPostShotDelayRemaining < 0f)
                NetPostShotDelayRemaining = 0f;

            if (NetPostShotDelayRemaining <= 0f)
            {
                if (NetPendingBreakAfterShot)
                {
                    NetPendingBreakAfterShot = false;
                    StartMidMatchBreakAuthority();
                    return;
                }

                if (NetPendingEndAfterShot)
                {
                    NetPendingEndAfterShot = false;
                    EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
                    return;
                }
            }
        }

        if (NetBreakActive)
        {
            NetBreakTimeRemaining -= Runner.DeltaTime;
            if (NetBreakTimeRemaining < 0f)
                NetBreakTimeRemaining = 0f;

            if (NetBreakTimeRemaining <= 0f)
                ResumeAfterMidMatchBreakAuthority();

            return;
        }

        float halftimeThreshold = NetConfiguredMatchDuration * 0.5f;

        if (CurrentMatchMode == MatchMode.TimeLimit)
        {
            if (NetShotInFlight)
            {
                if (!NetBreakTriggered)
                {
                    float nextTime = NetMatchTimeRemaining - Runner.DeltaTime;

                    if (nextTime <= halftimeThreshold)
                    {
                        NetMatchTimeRemaining = halftimeThreshold;
                        NetPendingBreakAfterShot = true;
                    }
                    else
                    {
                        NetMatchTimeRemaining = nextTime;
                    }
                }
                else
                {
                    float nextTime = NetMatchTimeRemaining - Runner.DeltaTime;

                    if (nextTime <= 0f)
                    {
                        NetMatchTimeRemaining = 0f;
                        NetPendingEndAfterShot = true;
                    }
                    else
                    {
                        NetMatchTimeRemaining = nextTime;
                    }
                }
            }
            else
            {
                NetMatchTimeRemaining -= Runner.DeltaTime;
                if (NetMatchTimeRemaining < 0f)
                    NetMatchTimeRemaining = 0f;
            }
        }

        if (!NetShotInFlight)
        {
            NetTurnTimeRemaining -= Runner.DeltaTime;
            if (NetTurnTimeRemaining < 0f)
                NetTurnTimeRemaining = 0f;
        }

        RefreshWatchedBallCache();

        if (!NetShotInFlight)
        {
            if (CurrentMatchMode == MatchMode.TimeLimit)
            {
                if (!NetBreakTriggered && NetMatchTimeRemaining <= halftimeThreshold)
                {
                    NetMatchTimeRemaining = halftimeThreshold;
                    StartMidMatchBreakAuthority();
                    return;
                }

                if (NetBreakTriggered && NetMatchTimeRemaining <= 0f)
                {
                    NetMatchTimeRemaining = 0f;
                    EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
                    return;
                }
            }
            else
            {
                if (ShouldEndMatchNow())
                {
                    EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
                    return;
                }

                if (!NetBreakTriggered && ShouldTriggerMidMatchBreak())
                {
                    StartMidMatchBreakAuthority();
                    return;
                }
            }

            if (NetTurnTimeRemaining <= 0f)
                ResolveIdleTimeoutAuthority();

            return;
        }

        if (enableStuckBallCheck && watchedBall != null && watchedBallRb != null && !watchedBallRb.isKinematic)
        {
            if (watchedBallRb.linearVelocity.magnitude <= stuckVelocityThreshold)
            {
                stuckTimer += Runner.DeltaTime;

                if (stuckTimer >= stuckTimeout)
                {
                    if (currentPlayerScoredThisTurn)
                        ResolveScoringTurnCompletedAuthority();
                    else
                        ResolveMissTurnCompletedAuthority();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
    }

    private bool ShouldEndMatchNow()
    {
        if (CurrentMatchMode == MatchMode.TimeLimit)
            return NetBreakTriggered && NetMatchTimeRemaining <= 0f;

        if (NetScoreP1 >= NetPointsToWin || NetScoreP2 >= NetPointsToWin)
            return true;

        if (scoreManager != null && scoreManager.AreAllBoardsFull())
            return !NetShotInFlight;

        return false;
    }

    private bool ShouldTriggerMidMatchBreak()
    {
        if (NetBreakTriggered)
            return false;

        if (NetShotInFlight)
            return false;

        if (CurrentMatchMode == MatchMode.TimeLimit)
        {
            float threshold = NetConfiguredMatchDuration * 0.5f;
            return NetMatchTimeRemaining <= threshold;
        }

        int halfTarget = Mathf.Max(1, Mathf.CeilToInt(NetPointsToWin * 0.5f));
        return NetScoreP1 >= halfTarget || NetScoreP2 >= halfTarget;
    }

    private void StartMidMatchBreakAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded || NetBreakTriggered)
            return;

        NetBreakTriggered = true;
        NetBreakActive = true;
        NetBreakTimeRemaining = defaultBreakDuration;
        NetBreakReasonRaw = (byte)(CurrentMatchMode == MatchMode.TimeLimit
            ? MidMatchBreakReason.TimeHalftime
            : MidMatchBreakReason.ScoreHalfPoint);

        NetShotInFlight = false;
        NetPendingBreakAfterShot = false;
        NetPostShotDelayRemaining = 0f;

        DespawnActiveBallAuthority();

        if (scoreManager != null)
            scoreManager.BeginHalftime();

        NetPlayer1OnLeft = !NetPlayer1OnLeft;

        if (ballTurnSpawner != null)
            ballTurnSpawner.SetPlayer1Side(NetPlayer1OnLeft);

        if (ballTurnSpawner != null)
            ballTurnSpawner.ClearAllBallsInScene();

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(NetPlayer1OnLeft);
    }

    private void ResumeAfterMidMatchBreakAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || !NetBreakActive || NetMatchEnded)
            return;

        NetBreakActive = false;
        NetBreakTimeRemaining = defaultBreakDuration;
        NetBreakReasonRaw = (byte)MidMatchBreakReason.None;
        NetTurnTimeRemaining = ConfiguredTurnDuration;
        NetCurrentTurnOwnerRaw = (byte)PlayerID.Player2;
        NetShotInFlight = false;

        if (scoreManager != null)
            scoreManager.EndHalftime();

        SpawnNewActiveBallAuthority(PlayerID.Player2);

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(NetPlayer1OnLeft);
    }

    private void ResolveIdleTimeoutAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        PlayerID nextOwner = CurrentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        DespawnActiveBallAuthority();
        NetShotInFlight = false;
        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void ResolveScoringTurnCompletedAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        ReleaseCurrentBallFromTurnTrackingAuthority();

        if (NetPendingEndAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (NetPendingBreakAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        PlayerID sameOwner = CurrentTurnOwner;

        if (CurrentMatchMode == MatchMode.ScoreTarget)
        {
            if (ShouldEndMatchNow())
            {
                EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
                return;
            }

            if (!NetBreakTriggered && ShouldTriggerMidMatchBreak())
            {
                StartMidMatchBreakAuthority();
                return;
            }
        }

        SpawnNewActiveBallAuthority(sameOwner);
    }

    private void ResolveMissTurnCompletedAuthority()
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        ReleaseCurrentBallFromTurnTrackingAuthority();

        if (NetPendingEndAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (NetPendingBreakAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        PlayerID nextOwner = CurrentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        if (CurrentMatchMode == MatchMode.ScoreTarget)
        {
            if (ShouldEndMatchNow())
            {
                EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
                return;
            }

            if (!NetBreakTriggered && ShouldTriggerMidMatchBreak())
            {
                StartMidMatchBreakAuthority();
                return;
            }
        }

        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void ReleaseCurrentBallFromTurnTrackingAuthority()
    {
        NetShotInFlight = false;
        NetTurnTimeRemaining = ConfiguredTurnDuration;
        watchedBall = null;
        watchedBallRb = null;
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;
        NetActiveBallId = default;
    }

    private PlayerID ResolvePlayerIdFromRpc(RpcInfo info)
    {
        if (Runner == null)
            return PlayerID.None;

        if (info.Source == Runner.LocalPlayer)
            return Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

        return Runner.IsServer ? PlayerID.Player2 : PlayerID.Player1;
    }

    private PlayerID ResolvePlayerIdFromPlayerRef(PlayerRef player)
    {
        if (Runner == null)
            return PlayerID.None;

        if (player == Runner.LocalPlayer)
            return Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

        return Runner.IsServer ? PlayerID.Player2 : PlayerID.Player1;
    }

    private void ApplyPlacementAuthority(PlayerID requester, Vector3 targetWorldPosition)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || !IsNetworkStateReadable)
            return;

        if (!MatchStarted || MatchEnded || MidMatchBreakActive || NetShotInFlight || NetPostShotDelayRemaining > 0f)
            return;

        if (requester != CurrentTurnOwner)
            return;

        BallPhysics ball = CurrentBall;
        if (ball == null)
            return;

        BallOwnership ownership = ball.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != requester)
            return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        Vector3 finalTarget = SanitizePlacementTarget(targetWorldPosition);

        if (rb != null)
        {
            if (!rb.isKinematic)
                rb.isKinematic = true;

            if (rb.constraints != RigidbodyConstraints.FreezeAll)
                rb.constraints = RigidbodyConstraints.FreezeAll;

            if (Vector3.SqrMagnitude(rb.position - finalTarget) > 0.0000001f)
                rb.position = finalTarget;
        }
        else
        {
            if (Vector3.SqrMagnitude(ball.transform.position - finalTarget) > 0.0000001f)
                ball.transform.position = finalTarget;
        }
    }

    private Vector3 SanitizePlacementTarget(Vector3 targetWorldPosition)
    {
        BallPhysics ball = CurrentBall;
        if (ball == null)
            return targetWorldPosition;

        Vector3 safe = targetWorldPosition;
        safe.y = ball.transform.position.y;

        if (ballTurnSpawner == null)
            return safe;

        BoxCollider placementArea = ballTurnSpawner.GetPlacementAreaForOwner(CurrentTurnOwner);
        if (placementArea == null)
            return safe;

        Vector3 local = placementArea.transform.InverseTransformPoint(safe);
        Vector3 half = placementArea.size * 0.5f;
        Vector3 center = placementArea.center;

        local.x = Mathf.Clamp(local.x, center.x - half.x, center.x + half.x);

        Vector3 currentLocal = placementArea.transform.InverseTransformPoint(ball.transform.position);
        local.y = currentLocal.y;
        local.z = currentLocal.z;

        return placementArea.transform.TransformPoint(local);
    }

    private void ApplyLaunchAuthority(PlayerID requester, Vector3 direction, float force)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority)
            return;

        if (requester != CurrentTurnOwner)
            return;

        BallPhysics ball = CurrentBall;
        if (ball == null)
            return;

        BallOwnership ownership = ball.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != requester)
            return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        Vector3 launchDir = direction.normalized;
        launchDir.y = 0f;

        ball.Launch(launchDir * Mathf.Max(0f, force));

        watchedBall = ball;
        watchedBallRb = rb;
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;
        NetShotInFlight = true;
    }

    public void NotifyAuthoritativeBallLost(BallPhysics ball)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        if (ball == null)
            return;

        bool wasActiveBall = ball == CurrentBall;

        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        if (netObj != null && Runner != null && Runner.IsServer)
            Runner.Despawn(netObj);

        if (!wasActiveBall)
            return;

        PlayerID nextOwner = CurrentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        NetShotInFlight = false;
        NetTurnTimeRemaining = ConfiguredTurnDuration;
        watchedBall = null;
        watchedBallRb = null;
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;
        NetActiveBallId = default;

        if (NetPendingEndAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (NetPendingBreakAfterShot)
        {
            NetPostShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (CurrentMatchMode == MatchMode.ScoreTarget && ShouldEndMatchNow())
        {
            EndMatchAuthority(MatchEndReason.NormalCompletion, PlayerID.None);
            return;
        }

        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void ApplyReplicaToLocalSystems()
    {
        if (!IsNetworkStateReadable)
            return;

        cachedMatchMode = CurrentMatchMode;
        cachedPointsToWin = PointsToWin;
        cachedConfiguredMatchDuration = ConfiguredMatchDuration;
        cachedCurrentTurnOwner = CurrentTurnOwner;
        cachedTurnTimeRemaining = NetTurnTimeRemaining;
        cachedMatchTimeRemaining = NetMatchTimeRemaining;
        cachedScoreP1 = NetScoreP1;
        cachedScoreP2 = NetScoreP2;
        cachedMatchStarted = NetMatchStarted;
        cachedMatchEnded = NetMatchEnded;
        cachedMidBreakActive = NetBreakActive;
        cachedIsTimeHalftime = IsTimeHalftimeActive;
        cachedIsHalfPoint = IsHalfPointActive;
        cachedBreakTimeRemaining = NetBreakTimeRemaining;
        cachedPlayer1Name = Player1DisplayName;
        cachedPlayer2Name = Player2DisplayName;
        cachedWinner = (PlayerID)NetWinnerRaw;
        cachedPlayer1OnLeft = NetPlayer1OnLeft;

        if (ballTurnSpawner != null)
            ballTurnSpawner.SetPlayer1Side(NetPlayer1OnLeft);

        if (onlineGameplayAuthority != null)
        {
            onlineGameplayAuthority.SetOnlineMatchController(this);
            onlineGameplayAuthority.SetStateAuthority(Object != null && Object.HasStateAuthority);
            onlineGameplayAuthority.SetCurrentTurnOwner(CurrentTurnOwner);
        }

        if (scoreManager != null)
            scoreManager.SetReplicatedScores(NetScoreP1, NetScoreP2);

        if (!localGameplayHardLocked && ballTurnSpawner != null)
        {
            ballTurnSpawner.TryBindCurrentOnlineBallForLocalControl(CurrentBall, CurrentTurnOwner);
            ballTurnSpawner.ClearOnlineLauncherBindingIfNeeded(CurrentTurnOwner);
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(NetPlayer1OnLeft);

        bool reconnectPending = false;
        PlayerID reconnectMissingPlayer = PlayerID.None;
        float reconnectElapsed = 0f;

        if (localTransportConnectionIssueVisible)
        {
            reconnectPending = true;
            reconnectMissingPlayer = PlayerID.None;
            reconnectElapsed = Mathf.Max(0f, authorityHeartbeatStaleTimer);
        }
        else if (localAuthorityConnectionLostVisible)
        {
            reconnectPending = true;
            reconnectMissingPlayer = PlayerID.Player1;
            reconnectElapsed = Mathf.Max(0f, authorityHeartbeatStaleTimer);
        }
        else if (authorityRemoteDisconnectPending)
        {
            reconnectPending = true;
            reconnectMissingPlayer = authorityRemoteDisconnectPendingPlayer;
            reconnectElapsed = Mathf.Max(0f, authorityRemoteDisconnectPendingElapsed);
        }
        else if (localRemoteClientConnectionLostVisible)
        {
            reconnectPending = true;
            reconnectMissingPlayer = EffectiveRemotePlayerId;
            reconnectElapsed = Mathf.Max(0f, remoteClientHeartbeatStaleTimer);
        }

        if (hud != null)
        {
            hud.ApplyState(
                CurrentMatchMode,
                PointsToWin,
                ConfiguredMatchDuration,
                CurrentTurnOwner,
                NetTurnTimeRemaining,
                NetMatchTimeRemaining,
                NetScoreP1,
                NetScoreP2,
                localGameplayHardLocked ? false : NetMatchStarted,
                localGameplayHardLocked ? true : NetMatchEnded,
                NetBreakActive,
                IsTimeHalftimeActive,
                IsHalfPointActive,
                NetBreakTimeRemaining,
                Player1DisplayName,
                Player2DisplayName,
                Winner,
                NetPlayer1OnLeft,
                reconnectPending,
                reconnectMissingPlayer,
                reconnectElapsed,
                ConvertToOnlineMatchEndReason((MatchEndReason)NetEndReasonRaw)
            );
        }
    }

    private void ApplyLocalOverrideToHud()
    {
        if (hud == null)
            return;

        hud.ApplyState(
            cachedMatchMode,
            cachedPointsToWin,
            cachedConfiguredMatchDuration,
            cachedCurrentTurnOwner,
            cachedTurnTimeRemaining,
            cachedMatchTimeRemaining,
            cachedScoreP1,
            cachedScoreP2,
            false,
            true,
            false,
            false,
            false,
            0f,
            cachedPlayer1Name,
            cachedPlayer2Name,
            localForcedWinner,
            cachedPlayer1OnLeft,
            false,
            PlayerID.None,
            0f,
            ConvertToOnlineMatchEndReason(localForcedEndReason)
        );
    }

    private void ConsumeReplicatedShotPopupIfNeeded()
    {
        if (shotScorePopupUI == null || !IsNetworkStateReadable)
            return;

        if (NetShotPopupSequence <= 0)
            return;

        if (lastConsumedShotPopupSequence == NetShotPopupSequence)
            return;

        lastConsumedShotPopupSequence = NetShotPopupSequence;
        shotScorePopupUI.PlayReplicatedPopup(
            NetShotPopupCombo,
            NetShotPopupPoints,
            NetShotPopupWorldPosition
        );
    }

    private void SpawnNewActiveBallAuthority(PlayerID owner)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || ballTurnSpawner == null)
            return;

        string skinId = owner == PlayerID.Player1 ? Player1SkinId : Player2SkinId;

        BallPhysics spawned = ballTurnSpawner.SpawnOnlineBallForOwner(owner, skinId);
        if (spawned == null)
            return;

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        NetActiveBallId = netObj != null ? netObj.Id : default;

        watchedBall = spawned;
        watchedBallRb = spawned.GetComponent<Rigidbody>();
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;
        NetCurrentTurnOwnerRaw = (byte)owner;
        NetTurnTimeRemaining = ConfiguredTurnDuration;
        NetShotInFlight = false;
    }

    private void DespawnActiveBallAuthority()
    {
        BallPhysics current = CurrentBall;
        if (current == null)
        {
            NetActiveBallId = default;
            return;
        }

        NetworkObject netObj = current.GetComponent<NetworkObject>();
        if (netObj != null && Runner != null && Runner.IsServer)
            Runner.Despawn(netObj);

        NetActiveBallId = default;
        watchedBall = null;
        watchedBallRb = null;
        stuckTimer = 0f;
    }

    private void RefreshWatchedBallCache()
    {
        watchedBall = CurrentBall;
        watchedBallRb = watchedBall != null ? watchedBall.GetComponent<Rigidbody>() : null;
    }

    private void EndMatchAuthority(MatchEndReason reason, PlayerID forcedWinner)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        NetMatchEnded = true;
        NetMatchStarted = false;
        NetBreakActive = false;
        NetShotInFlight = false;
        NetPendingEndAfterShot = false;
        NetPendingBreakAfterShot = false;
        NetPostShotDelayRemaining = 0f;
        NetBreakReasonRaw = (byte)MidMatchBreakReason.None;
        NetEndReasonRaw = (byte)reason;

        HideAllConnectionIssueOverlays();

        if (forcedWinner != PlayerID.None)
        {
            NetWinnerRaw = (byte)forcedWinner;
        }
        else
        {
            if (NetScoreP1 > NetScoreP2)
                NetWinnerRaw = (byte)PlayerID.Player1;
            else if (NetScoreP2 > NetScoreP1)
                NetWinnerRaw = (byte)PlayerID.Player2;
            else
                NetWinnerRaw = (byte)PlayerID.None;
        }

        ResolveReferences();

        string disconnectedPlayerName = ResolveDisconnectedPlayerNameForReason(reason, (PlayerID)NetWinnerRaw);

        if (hud != null)
        {
            hud.ForceShowPostGame(
                CurrentMatchMode,
                PointsToWin,
                Player1DisplayName,
                Player2DisplayName,
                NetScoreP1,
                NetScoreP2,
                (PlayerID)NetWinnerRaw,
                ConvertToOnlineMatchEndReason(reason),
                reason == MatchEndReason.OpponentDisconnected,
                disconnectedPlayerName
            );
        }

        if (scoreManager != null)
            scoreManager.EndMatch((PlayerID)NetWinnerRaw);

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchEnded();
    }

    private void OnScoreManagerPointsScored(PlayerID player, int newTotal)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || scoreManager == null)
            return;

        NetScoreP1 = scoreManager.ScoreP1;
        NetScoreP2 = scoreManager.ScoreP2;

        if (player == CurrentTurnOwner)
            currentPlayerScoredThisTurn = true;
    }

    private void OnShotScoreDetailed(ShotScoreData data)
    {
        if (IsTerminalOutcomeLocked)
            return;

        if (!Object.HasStateAuthority || data == null)
            return;

        NetShotPopupCombo = Mathf.Max(0, data.comboStreak);
        NetShotPopupPoints = Mathf.Max(0, data.shotPoints);
        NetShotPopupWorldPosition = data.slotWorldPosition;
        NetShotPopupSequence += 1;
    }

    private string ResolveDisplayNameForPlayer(PlayerID player)
    {
        MatchSessionContext session = GetCurrentSessionContext();
        if (session != null)
        {
            if (player == PlayerID.Player1 && !string.IsNullOrWhiteSpace(session.player1DisplayName))
                return session.player1DisplayName.Trim();

            if (player == PlayerID.Player2 && !string.IsNullOrWhiteSpace(session.player2DisplayName))
                return session.player2DisplayName.Trim();
        }

        return player == PlayerID.Player1 ? "Player 1" : "Player 2";
    }

    private string ResolveInitialSkinIdForPlayer(PlayerID player)
    {
        MatchSessionContext session = GetCurrentSessionContext();
        if (session != null)
        {
            string sessionSkin = player == PlayerID.Player1
                ? session.player1SkinUniqueId
                : session.player2SkinUniqueId;

            if (!string.IsNullOrWhiteSpace(sessionSkin))
                return sessionSkin.Trim();
        }

        return string.Empty;
    }

    private string ResolveLocalDisplayName()
    {
        MatchSessionContext session = GetCurrentSessionContext();
        if (session != null && session.localPlayer != null && !string.IsNullOrWhiteSpace(session.localPlayer.displayName))
            return session.localPlayer.displayName.Trim();

        if (onlineFlowController != null)
        {
            string flowLocalName = onlineFlowController.GetResolvedLocalDisplayName();
            if (!string.IsNullOrWhiteSpace(flowLocalName))
                return flowLocalName.Trim();
        }

        return EffectiveLocalPlayerId == PlayerID.Player1 ? "Player 1" : "Player 2";
    }

    private string ResolveLocalEquippedSkinId()
    {
        if (PlayerSkinLoadout.Instance == null)
            return string.Empty;

        BallSkinData skin = PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer1();
        if (skin == null || string.IsNullOrWhiteSpace(skin.skinUniqueId))
            return string.Empty;

        return skin.skinUniqueId.Trim();
    }

    private MatchSessionContext GetCurrentSessionContext()
    {
        ResolveReferences();

        if (onlineFlowController == null ||
            onlineFlowController.RuntimeContext == null ||
            onlineFlowController.RuntimeContext.currentSession == null)
        {
            return null;
        }

        return onlineFlowController.RuntimeContext.currentSession;
    }

    private OnlineMatchEndReason ConvertToOnlineMatchEndReason(MatchEndReason reason)
    {
        switch (reason)
        {
            case MatchEndReason.NormalCompletion:
                return OnlineMatchEndReason.NormalCompletion;
            case MatchEndReason.OpponentDisconnected:
                return OnlineMatchEndReason.DisconnectWin;
            case MatchEndReason.LocalDisconnected:
                return OnlineMatchEndReason.DisconnectLoss;
            case MatchEndReason.OpponentSurrendered:
                return OnlineMatchEndReason.SurrenderWin;
            case MatchEndReason.LocalSurrendered:
                return OnlineMatchEndReason.SurrenderLoss;
            case MatchEndReason.MatchCancelled:
                return OnlineMatchEndReason.MatchCancelled;
            case MatchEndReason.ReconnectTimeout:
                return OnlineMatchEndReason.ReconnectTimeout;
            default:
                return OnlineMatchEndReason.None;
        }
    }

    private string ResolveDisconnectedPlayerNameForReason(MatchEndReason reason, PlayerID winner)
    {
        if (reason == MatchEndReason.LocalDisconnected || reason == MatchEndReason.LocalSurrendered)
            return EffectiveLocalPlayerId == PlayerID.Player1 ? Player1DisplayName : Player2DisplayName;

        if (reason == MatchEndReason.OpponentDisconnected || reason == MatchEndReason.OpponentSurrendered)
            return EffectiveRemotePlayerId == PlayerID.Player1 ? Player1DisplayName : Player2DisplayName;

        if (winner == PlayerID.Player1)
            return Player2DisplayName;

        if (winner == PlayerID.Player2)
            return Player1DisplayName;

        return string.Empty;
    }
}