using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class FusionOnlineMatchController : NetworkBehaviour
{
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

    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private float stuckTimer;
    private bool currentPlayerScoredThisTurn;
    private bool scoreSubscribed;
    private bool localPresentationSubmitted;
    private int lastConsumedShotPopupSequence = -1;

    private bool hasSpawnedNetworkState;
    private bool hasInitializedAuthorityState;

    public bool HasSpawnedNetworkState => hasSpawnedNetworkState;
    public bool IsNetworkStateReadable =>
        hasSpawnedNetworkState &&
        Object != null &&
        Object.IsValid &&
        Runner != null;

    public MatchMode CurrentMatchMode => IsNetworkStateReadable ? (MatchMode)NetMatchModeRaw : fallbackMatchMode;
    public int PointsToWin => IsNetworkStateReadable ? NetPointsToWin : fallbackPointsToWin;
    public float ConfiguredMatchDuration => IsNetworkStateReadable ? NetConfiguredMatchDuration : fallbackMatchDurationSeconds;
    public float ConfiguredTurnDuration => IsNetworkStateReadable ? NetConfiguredTurnDuration : defaultTurnDuration;

    public PlayerID CurrentTurnOwner => IsNetworkStateReadable ? (PlayerID)NetCurrentTurnOwnerRaw : PlayerID.None;
    public float CurrentTurnTimeRemaining => IsNetworkStateReadable ? NetTurnTimeRemaining : 0f;
    public float CurrentMatchTimeRemaining => IsNetworkStateReadable ? NetMatchTimeRemaining : 0f;

    public bool MatchStarted => IsNetworkStateReadable && NetMatchStarted;
    public bool MatchEnded => IsNetworkStateReadable && NetMatchEnded;

    public bool MidMatchBreakActive => IsNetworkStateReadable && NetBreakActive;
    public float CurrentBreakTimeRemaining => IsNetworkStateReadable ? NetBreakTimeRemaining : 0f;
    public bool IsTimeHalftimeActive =>
        IsNetworkStateReadable &&
        NetBreakActive &&
        (MidMatchBreakReason)NetBreakReasonRaw == MidMatchBreakReason.TimeHalftime;

    public bool IsHalfPointActive =>
        IsNetworkStateReadable &&
        NetBreakActive &&
        (MidMatchBreakReason)NetBreakReasonRaw == MidMatchBreakReason.ScoreHalfPoint;

    public int ScoreP1 => IsNetworkStateReadable ? NetScoreP1 : 0;
    public int ScoreP2 => IsNetworkStateReadable ? NetScoreP2 : 0;

    public PlayerID Winner => IsNetworkStateReadable ? (PlayerID)NetWinnerRaw : PlayerID.None;

    public string Player1DisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return "Player 1";

            string value = NetPlayer1DisplayName.ToString();
            return string.IsNullOrWhiteSpace(value) ? "Player 1" : value;
        }
    }

    public string Player2DisplayName
    {
        get
        {
            if (!IsNetworkStateReadable)
                return "Player 2";

            string value = NetPlayer2DisplayName.ToString();
            return string.IsNullOrWhiteSpace(value) ? "Player 2" : value;
        }
    }

    public string Player1SkinId => IsNetworkStateReadable ? NetPlayer1SkinId.ToString() : string.Empty;
    public string Player2SkinId => IsNetworkStateReadable ? NetPlayer2SkinId.ToString() : string.Empty;

    public PlayerID EffectiveLocalPlayerId => Runner != null && Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;
    public PlayerID EffectiveRemotePlayerId => EffectiveLocalPlayerId == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

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

        SubmitLocalPresentationIfNeeded();
        ApplyReplicaToLocalSystems();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawnedNetworkState = false;
        UnsubscribeFromScore();
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
            TickAuthority();

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
        if (!MatchEnded)
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
        if (!MatchEnded)
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

    public void RequestSetCurrentBallPlacement(Vector3 targetWorldPosition)
    {
        if (!IsNetworkStateReadable)
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetPlacement(Vector3 targetWorldPosition, RpcInfo info = default)
    {
        PlayerID requester = ResolvePlayerIdFromRpc(info);
        ApplyPlacementAuthority(requester, SanitizePlacementTarget(targetWorldPosition));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRematch(byte requesterRaw)
    {
        AuthorityStartRematchRequest((PlayerID)requesterRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AcceptRematch(byte accepterRaw)
    {
        AuthorityAcceptRematch((PlayerID)accepterRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_DeclineRematch(byte declinerRaw)
    {
        AuthorityDeclineRematch((PlayerID)declinerRaw);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitLocalPresentation(byte playerRaw, string displayName, string skinId)
    {
        ApplyPresentationForPlayer((PlayerID)playerRaw, displayName, skinId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLaunch(Vector3 direction, float force, RpcInfo info = default)
    {
        PlayerID requester = ResolvePlayerIdFromRpc(info);
        ApplyLaunchAuthority(requester, direction, force);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestResumeAfterHalftime()
    {
        ResumeAfterMidMatchBreakAuthority();
    }

    private void AuthorityStartRematchRequest(PlayerID requester)
    {
        if (!Object.HasStateAuthority || !MatchEnded)
            return;

        if ((RematchState)NetRematchStateRaw == RematchState.Pending ||
            (RematchState)NetRematchStateRaw == RematchState.Accepted)
            return;

        NetRematchNonce += 1;
        NetRematchRequesterRaw = (byte)requester;
        NetRematchStateRaw = (byte)RematchState.Pending;

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] Rematch requested by " + requester, this);
    }

    private void AuthorityAcceptRematch(PlayerID accepter)
    {
        if (!Object.HasStateAuthority || !MatchEnded)
            return;

        if ((RematchState)NetRematchStateRaw != RematchState.Pending)
            return;

        if (accepter == (PlayerID)NetRematchRequesterRaw)
            return;

        NetRematchStateRaw = (byte)RematchState.Accepted;

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] Rematch accepted by " + accepter, this);
    }

    private void AuthorityDeclineRematch(PlayerID decliner)
    {
        if (!Object.HasStateAuthority || !MatchEnded)
            return;

        if ((RematchState)NetRematchStateRaw == RematchState.Declined)
            return;

        NetRematchStateRaw = (byte)RematchState.Declined;

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] Rematch declined by " + decliner, this);
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

        if (scoreManager != null)
            scoreManager.StartMatch();

        if (ballTurnSpawner != null)
            ballTurnSpawner.SetPlayer1Side(true);

        if (ballTurnSpawner != null)
            SpawnNewActiveBallAuthority(PlayerID.Player1);

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(true);

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] InitializeAuthorityState -> " +
                "P1Name=" + NetPlayer1DisplayName +
                " | P1Skin=" + NetPlayer1SkinId +
                " | P2Name=" + NetPlayer2DisplayName +
                " | P2Skin=" + NetPlayer2SkinId,
                this
            );
        }
    }

    private void SubmitLocalPresentationIfNeeded()
    {
        if (localPresentationSubmitted || !IsNetworkStateReadable)
            return;

        string localName = ResolveLocalDisplayName();
        string localSkinId = ResolveLocalEquippedSkinId();

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] SubmitLocalPresentationIfNeeded -> " +
                "EffectiveLocalPlayerId=" + EffectiveLocalPlayerId +
                " | LocalName=" + localName +
                " | LocalSkinId=" + localSkinId,
                this
            );
        }

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

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] ApplyPresentationForPlayer -> " +
                "Player=" + player +
                " | Name=" + safeName +
                " | SkinId=" + safeSkin,
                this
            );
        }
    }

    private void TickAuthority()
    {
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
                    EndMatchAuthority();
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
                    EndMatchAuthority();
                    return;
                }
            }
            else
            {
                if (ShouldEndMatchNow())
                {
                    EndMatchAuthority();
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

    public void RequestResumeAfterHalftime()
    {
        if (!MidMatchBreakActive || MatchEnded)
            return;

        if (Object.HasStateAuthority)
        {
            ResumeAfterMidMatchBreakAuthority();
            return;
        }

        RPC_RequestResumeAfterHalftime();
    }

    private void ResumeAfterMidMatchBreakAuthority()
    {
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
        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        PlayerID nextOwner = CurrentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        DespawnActiveBallAuthority();
        NetShotInFlight = false;
        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void ResolveScoringTurnCompletedAuthority()
    {
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
                EndMatchAuthority();
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
                EndMatchAuthority();
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

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!IsNetworkStateReadable)
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

    private PlayerID ResolvePlayerIdFromRpc(RpcInfo info)
    {
        if (Runner == null)
            return PlayerID.None;

        if (info.Source == Runner.LocalPlayer)
            return Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

        return Runner.IsServer ? PlayerID.Player2 : PlayerID.Player1;
    }

    private void ApplyPlacementAuthority(PlayerID requester, Vector3 targetWorldPosition)
    {
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
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.position = finalTarget;
        }
        else
        {
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
            EndMatchAuthority();
            return;
        }

        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void ApplyReplicaToLocalSystems()
    {
        if (!IsNetworkStateReadable)
            return;

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

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.TryBindCurrentOnlineBallForLocalControl(CurrentBall, CurrentTurnOwner);
            ballTurnSpawner.ClearOnlineLauncherBindingIfNeeded(CurrentTurnOwner);
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(NetPlayer1OnLeft);

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
                NetMatchStarted,
                NetMatchEnded,
                NetBreakActive,
                IsTimeHalftimeActive,
                IsHalfPointActive,
                NetBreakTimeRemaining,
                Player1DisplayName,
                Player2DisplayName,
                Winner,
                NetPlayer1OnLeft
            );
        }
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
        if (!Object.HasStateAuthority || ballTurnSpawner == null)
            return;

        string skinId = owner == PlayerID.Player1 ? Player1SkinId : Player2SkinId;

        BallPhysics spawned = ballTurnSpawner.SpawnOnlineBallForOwner(owner, skinId);
        if (spawned == null)
        {
            Debug.LogError("[FusionOnlineMatchController] Failed to spawn online ball.", this);
            return;
        }

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        NetActiveBallId = netObj != null ? netObj.Id : default;

        watchedBall = spawned;
        watchedBallRb = spawned.GetComponent<Rigidbody>();
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;
        NetCurrentTurnOwnerRaw = (byte)owner;
        NetTurnTimeRemaining = ConfiguredTurnDuration;
        NetShotInFlight = false;

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] SpawnNewActiveBallAuthority -> " +
                "Owner=" + owner +
                " | SkinId=" + skinId,
                this
            );
        }
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

    private void EndMatchAuthority()
    {
        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        NetMatchEnded = true;
        NetMatchStarted = false;
        NetBreakActive = false;
        NetShotInFlight = false;
        NetPendingEndAfterShot = false;
        NetPostShotDelayRemaining = 0f;
        NetBreakReasonRaw = (byte)MidMatchBreakReason.None;

        if (NetScoreP1 > NetScoreP2)
            NetWinnerRaw = (byte)PlayerID.Player1;
        else if (NetScoreP2 > NetScoreP1)
            NetWinnerRaw = (byte)PlayerID.Player2;
        else
            NetWinnerRaw = (byte)PlayerID.None;

        if (scoreManager != null)
            scoreManager.EndMatch((PlayerID)NetWinnerRaw);
    }

    private void OnScoreManagerPointsScored(PlayerID player, int newTotal)
    {
        if (!Object.HasStateAuthority || scoreManager == null)
            return;

        NetScoreP1 = scoreManager.ScoreP1;
        NetScoreP2 = scoreManager.ScoreP2;

        if (player == CurrentTurnOwner)
            currentPlayerScoredThisTurn = true;
    }

    private void OnShotScoreDetailed(ShotScoreData data)
    {
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

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] ResolveLocalEquippedSkinId -> Using local primary loadout skin = " + skin.skinUniqueId,
                this
            );
        }

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
}