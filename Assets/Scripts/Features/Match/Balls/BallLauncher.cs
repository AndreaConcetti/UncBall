using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class BallLauncher : MonoBehaviour
{
    public enum LaunchPhase
    {
        None,
        Placement,
        AimReady,
        Launched
    }

    [Header("References")]
    public BallPhysics ball;
    public BallCameraController cameraController;
    public Camera gameplayCamera;
    public BallVisualRoll ballVisualRoll;
    public OnlineGameplayAuthority onlineAuthority;

    [Header("Optional UI Lock")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;

    [Header("Placement")]
    public BoxCollider activePlacementArea;
    public float placementYOffset = 0f;
    public float placementPickRadiusScreen = 140f;
    public float placementSmoothSpeed = 20f;

    [Header("Confirm")]
    public bool allowKeyboardConfirm = true;
    public bool allowDoubleTapConfirm = true;
    public float doubleTapMaxInterval = 0.30f;

    [Header("Force Mapping")]
    public float minForce = 8f;
    public float maxForce = 37.5f;
    public float maxSwipePixels = 300f;
    public float minLaunchSwipePixels = 20f;
    public float chargeCurveExponent = 1.5f;
    public bool useForwardOnlyForPower = true;

    [Header("Direction Constraint")]
    public bool constrainAngle = false;

    [Range(1f, 90f)]
    public float angleLimit = 45f;

    public Vector3 forwardAxis = Vector3.forward;

    [Header("Input")]
    public bool enableTouchSimulationInEditor = true;
    public bool allowMouseInputInEditor = true;

    [Header("Debug")]
    public bool debugLogs = true;

    public float ChargeRatio { get; private set; }
    public Vector3 LaunchDirection { get; private set; }
    public LaunchPhase CurrentPhase { get; private set; } = LaunchPhase.None;

    private Finger activeFinger;
    private bool isPlacementDragging;
    private bool isAimTracking;
    private Vector2 aimStartScreen;
    private bool hasLaunched;

    private bool mousePlacementDragging;
    private bool mouseAimDragging;
    private Vector2 mouseAimStartScreen;

    private float lastPlacementTapTime = -999f;
    private Vector2 lastPlacementTapScreenPos;

    private float placementPlaneY;
    private float placementLineLocalY;
    private float placementLineLocalZ;

    private bool hasPlacementDragStart;
    private Vector3 placementDragStartWorldPoint;
    private Vector3 placementDragStartBallWorldPosition;

    private FusionOnlineMatchController cachedController;
    private BallTurnSpawner cachedSpawner;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (onlineAuthority == null)
            onlineAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();

        cachedSpawner = FindFirstObjectByType<BallTurnSpawner>();

        if (settingsPanelUI == null)
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>();
#else
        if (onlineAuthority == null)
            onlineAuthority = FindObjectOfType<OnlineGameplayAuthority>();

        cachedSpawner = FindObjectOfType<BallTurnSpawner>();

        if (settingsPanelUI == null)
            settingsPanelUI = FindObjectOfType<SettingsPanelUI>();
#endif

        RefreshBallVisualRollReference();
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();

#if UNITY_EDITOR
        if (enableTouchSimulationInEditor)
            TouchSimulation.Enable();
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (enableTouchSimulationInEditor)
            TouchSimulation.Disable();
#endif

        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        ResolveOnlineController();
        ResolveSpawner();
        ResolveSettingsPanel();
        SyncOnlineCurrentBallBinding();
        AbortLocalPresentationIfStateInvalid();

        if (ball == null)
            return;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (IsGameplayInputLocked())
            return;

        HandleKeyboardConfirm();
        HandleTouchInput();
        HandleMouseInput();
    }

    private void ResolveSpawner()
    {
        if (cachedSpawner != null)
            return;

#if UNITY_2023_1_OR_NEWER
        cachedSpawner = FindFirstObjectByType<BallTurnSpawner>();
#else
        cachedSpawner = FindObjectOfType<BallTurnSpawner>();
#endif
    }

    private void ResolveOnlineController()
    {
        if (cachedController != null && cachedController.IsNetworkStateReadable)
            return;

        if (onlineAuthority != null && onlineAuthority.OnlineMatchController != null)
        {
            cachedController = onlineAuthority.OnlineMatchController;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        cachedController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        cachedController = FindObjectOfType<FusionOnlineMatchController>();
#endif
    }

    private void ResolveSettingsPanel()
    {
        if (settingsPanelUI != null)
            return;

#if UNITY_2023_1_OR_NEWER
        settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>();
#else
        settingsPanelUI = FindObjectOfType<SettingsPanelUI>();
#endif
    }

    private PlayerID ResolveEffectiveLocalPlayerId()
    {
        ResolveOnlineController();

        if (cachedController != null)
            return cachedController.EffectiveLocalPlayerId;

        if (onlineAuthority != null)
            return onlineAuthority.LocalPlayerId;

        return PlayerID.Player1;
    }

    private void SyncOnlineCurrentBallBinding()
    {
        if (onlineAuthority == null || !onlineAuthority.IsOnlineSession)
            return;

        ResolveOnlineController();
        ResolveSpawner();

        if (cachedController == null || !cachedController.IsNetworkStateReadable)
            return;

        PlayerID localPlayer = ResolveEffectiveLocalPlayerId();
        BallPhysics currentBall = cachedController.CurrentBall;

        if (currentBall == null)
        {
            if (ball != null)
            {
                ForceAbortCurrentInteraction(false);
                ball = null;
                activePlacementArea = null;
            }

            return;
        }

        PlayerID owner = GetOwnerFromBall(currentBall);
        if (owner != localPlayer)
        {
            if (ball != null && ball == currentBall)
            {
                ForceAbortCurrentInteraction(false);
                ball = null;
                activePlacementArea = null;
            }

            return;
        }

        if (ball != currentBall)
        {
            ball = currentBall;

            if (cachedSpawner != null)
                activePlacementArea = cachedSpawner.GetPlacementAreaForOwner(owner);
            else
                activePlacementArea = null;

            ResetLaunch();

            if (debugLogs)
            {
                Debug.Log(
                    "[BallLauncher] SyncOnlineCurrentBallBinding -> bound local ball for owner " + owner,
                    this
                );
            }
        }
    }

    public void SetActivePlacementArea(BoxCollider placementArea)
    {
        activePlacementArea = placementArea;
    }

    public void ResetLaunch()
    {
        hasLaunched = false;
        ChargeRatio = 0f;
        LaunchDirection = Vector3.zero;

        activeFinger = null;
        isPlacementDragging = false;
        isAimTracking = false;

        mousePlacementDragging = false;
        mouseAimDragging = false;

        hasPlacementDragStart = false;
        placementDragStartWorldPoint = Vector3.zero;
        placementDragStartBallWorldPosition = Vector3.zero;

        CurrentPhase = LaunchPhase.Placement;

        RefreshBallVisualRollReference();

        if (ball != null)
        {
            CachePlacementLineFromCurrentBall();

            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.isKinematic = true;
            }
        }

        if (ballVisualRoll != null)
            ballVisualRoll.ResetVisualRotation();

        lastPlacementTapTime = -999f;
        lastPlacementTapScreenPos = Vector2.zero;

        cameraController?.SetAiming(false);
    }

    private void AbortLocalPresentationIfStateInvalid()
    {
        if (onlineAuthority == null || !onlineAuthority.IsOnlineSession)
            return;

        ResolveOnlineController();
        if (cachedController == null || !cachedController.IsNetworkStateReadable)
        {
            if (CurrentPhase == LaunchPhase.AimReady || CurrentPhase == LaunchPhase.Placement)
                ForceAbortCurrentInteraction(false);

            return;
        }

        PlayerID localPlayer = ResolveEffectiveLocalPlayerId();

        bool localLostTurn = cachedController.CurrentTurnOwner != localPlayer;
        bool matchBlocked = cachedController.MatchEnded || cachedController.MidMatchBreakActive;
        bool ballMissing = ball == null;
        bool launchedAlready = CurrentPhase == LaunchPhase.Launched;

        if (launchedAlready)
            return;

        if (localLostTurn || matchBlocked || ballMissing)
            ForceAbortCurrentInteraction(false);
    }

    private void ForceAbortCurrentInteraction(bool keepBoundBall)
    {
        isPlacementDragging = false;
        isAimTracking = false;
        mousePlacementDragging = false;
        mouseAimDragging = false;
        hasPlacementDragStart = false;
        activeFinger = null;

        ChargeRatio = 0f;
        LaunchDirection = Vector3.zero;
        hasLaunched = false;

        if (keepBoundBall && ball != null)
            CurrentPhase = LaunchPhase.Placement;
        else
            CurrentPhase = LaunchPhase.None;

        cameraController?.SetAiming(false);
    }

    void HandleKeyboardConfirm()
    {
        if (!allowKeyboardConfirm || CurrentPhase != LaunchPhase.Placement || hasLaunched)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            ConfirmPlacement();
    }

    void HandleTouchInput()
    {
        if (Touch.activeTouches.Count == 0)
            return;

        if (activeFinger == null)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
                    continue;

                if (CurrentPhase == LaunchPhase.Placement)
                {
                    if (TryRegisterPlacementTap(touch))
                        return;

                    if (TryBeginPlacement(touch))
                        return;

                    return;
                }
                else if (CurrentPhase == LaunchPhase.AimReady)
                {
                    BeginAimSwipe(touch);
                    if (activeFinger != null)
                        return;
                }
            }

            return;
        }

        var currentTouch = activeFinger.currentTouch;
        if (!currentTouch.valid)
        {
            ClearActiveFinger();
            return;
        }

        if (CurrentPhase == LaunchPhase.Placement && isPlacementDragging)
        {
            if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                UpdatePlacement(currentTouch.screenPosition);
            }
            else if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                     currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                EndPlacementDrag();
            }
        }
        else if (CurrentPhase == LaunchPhase.AimReady && isAimTracking)
        {
            if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                UpdateAimSwipe(currentTouch.screenPosition);
            }
            else if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                     currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                EndAimSwipe(currentTouch.screenPosition);
            }
        }
    }

    void HandleMouseInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!allowMouseInputInEditor)
            return;

        if (Mouse.current == null || gameplayCamera == null || ball == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (CurrentPhase == LaunchPhase.Placement)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 ballScreen = gameplayCamera.WorldToScreenPoint(ball.transform.position);

                bool confirmedByDoubleClick = false;

                if (allowDoubleTapConfirm)
                {
                    float timeNow = Time.time;
                    bool sameAreaAsPreviousClick = Vector2.Distance(mousePos, lastPlacementTapScreenPos) <= 120f;
                    bool quickEnough = (timeNow - lastPlacementTapTime) <= doubleTapMaxInterval;

                    if (quickEnough && sameAreaAsPreviousClick)
                    {
                        ConfirmPlacement();
                        lastPlacementTapTime = -999f;
                        lastPlacementTapScreenPos = Vector2.zero;
                        confirmedByDoubleClick = true;
                    }
                    else
                    {
                        lastPlacementTapTime = timeNow;
                        lastPlacementTapScreenPos = mousePos;
                    }
                }

                if (confirmedByDoubleClick)
                    return;

                if (Vector2.Distance(mousePos, ballScreen) <= placementPickRadiusScreen)
                {
                    mousePlacementDragging = true;
                    CachePlacementLineFromCurrentBall();
                    CachePlacementDragStart(mousePos);
                }
            }

            if (mousePlacementDragging && Mouse.current.leftButton.isPressed)
                UpdatePlacement(mousePos);

            if (mousePlacementDragging && Mouse.current.leftButton.wasReleasedThisFrame)
                EndMousePlacementDrag();
        }
        else if (CurrentPhase == LaunchPhase.AimReady)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                mouseAimDragging = true;
                mouseAimStartScreen = mousePos;
                ChargeRatio = 0f;
                LaunchDirection = GetSafeForward();
            }

            if (mouseAimDragging && Mouse.current.leftButton.isPressed)
            {
                Vector2 swipe = mousePos - mouseAimStartScreen;
                ApplySwipe(swipe);
            }

            if (mouseAimDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                Vector2 swipe = mousePos - mouseAimStartScreen;
                ApplySwipe(swipe);

                mouseAimDragging = false;

                if (IsSwipeValidForLaunch(swipe))
                    DoLaunch();
                else
                    CancelAimState();
            }
        }
#endif
    }

    bool TryBeginPlacement(Touch touch)
    {
        if (ball == null || CurrentPhase != LaunchPhase.Placement || gameplayCamera == null)
            return false;

        if (!IsCurrentBallOwnedByLocalPlayer())
            return false;

        Vector2 screenPos = touch.screenPosition;
        Vector2 ballScreen = gameplayCamera.WorldToScreenPoint(ball.transform.position);

        if (Vector2.Distance(screenPos, ballScreen) > placementPickRadiusScreen)
            return false;

        activeFinger = touch.finger;
        isPlacementDragging = true;

        CachePlacementLineFromCurrentBall();
        CachePlacementDragStart(screenPos);
        return true;
    }

    void CachePlacementLineFromCurrentBall()
    {
        if (ball == null)
            return;

        placementPlaneY = ball.transform.position.y + placementYOffset;

        if (activePlacementArea != null)
        {
            Vector3 local = activePlacementArea.transform.InverseTransformPoint(ball.transform.position);
            placementLineLocalY = local.y;
            placementLineLocalZ = local.z;
        }
        else
        {
            placementLineLocalY = ball.transform.position.y;
            placementLineLocalZ = ball.transform.position.z;
        }
    }

    void CachePlacementDragStart(Vector2 screenPos)
    {
        if (ball == null)
            return;

        if (TryGetPlacementWorldPoint(screenPos, out Vector3 worldPoint))
        {
            placementDragStartWorldPoint = worldPoint;
            placementDragStartBallWorldPosition = ball.transform.position;
            hasPlacementDragStart = true;
        }
        else
        {
            placementDragStartWorldPoint = Vector3.zero;
            placementDragStartBallWorldPosition = ball.transform.position;
            hasPlacementDragStart = false;
        }
    }

    void UpdatePlacement(Vector2 screenPos)
    {
        if (ball == null || !hasPlacementDragStart)
            return;

        if (!TryGetPlacementWorldPoint(screenPos, out Vector3 currentWorldPoint))
            return;

        Vector3 placementAxisWorld = GetPlacementAxisWorld();
        Vector3 worldDelta = currentWorldPoint - placementDragStartWorldPoint;
        float deltaAlongAxis = Vector3.Dot(worldDelta, placementAxisWorld);

        Vector3 targetWorld = placementDragStartBallWorldPosition + placementAxisWorld * deltaAlongAxis;
        targetWorld = ClampToPlacementLineAndArea(targetWorld);

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
            rb.position = targetWorld;
        else
            ball.transform.position = targetWorld;

        if (cachedController != null && cachedController.IsNetworkStateReadable)
            cachedController.RequestSetCurrentBallPlacement(targetWorld);
    }

    void EndPlacementDrag()
    {
        isPlacementDragging = false;
        hasPlacementDragStart = false;
        ClearActiveFinger();
    }

    void EndMousePlacementDrag()
    {
        mousePlacementDragging = false;
        hasPlacementDragStart = false;
    }

    bool TryRegisterPlacementTap(Touch touch)
    {
        if (!allowDoubleTapConfirm || CurrentPhase != LaunchPhase.Placement)
            return false;

        Vector2 screenPos = touch.screenPosition;
        float timeNow = Time.time;

        bool sameAreaAsPreviousTap = Vector2.Distance(screenPos, lastPlacementTapScreenPos) <= 120f;
        bool quickEnough = (timeNow - lastPlacementTapTime) <= doubleTapMaxInterval;

        if (quickEnough && sameAreaAsPreviousTap)
        {
            ConfirmPlacement();
            lastPlacementTapTime = -999f;
            lastPlacementTapScreenPos = Vector2.zero;
            activeFinger = null;
            isPlacementDragging = false;
            hasPlacementDragStart = false;
            return true;
        }

        lastPlacementTapTime = timeNow;
        lastPlacementTapScreenPos = screenPos;
        return false;
    }

    void ConfirmPlacement()
    {
        if (CurrentPhase != LaunchPhase.Placement)
            return;

        if (!IsCurrentBallOwnedByLocalPlayer())
            return;

        CurrentPhase = LaunchPhase.AimReady;
        ChargeRatio = 0f;
        LaunchDirection = GetSafeForward();

        cameraController?.SetAiming(true);
    }

    void BeginAimSwipe(Touch touch)
    {
        if (CurrentPhase != LaunchPhase.AimReady || hasLaunched)
            return;

        if (!IsCurrentBallOwnedByLocalPlayer())
            return;

        activeFinger = touch.finger;
        isAimTracking = true;
        aimStartScreen = touch.screenPosition;
        ChargeRatio = 0f;
        LaunchDirection = GetSafeForward();
    }

    void UpdateAimSwipe(Vector2 screenPos)
    {
        Vector2 swipe = screenPos - aimStartScreen;
        ApplySwipe(swipe);
    }

    void EndAimSwipe(Vector2 screenPos)
    {
        if (!isAimTracking)
        {
            ClearActiveFinger();
            return;
        }

        Vector2 swipe = screenPos - aimStartScreen;
        ApplySwipe(swipe);

        isAimTracking = false;
        ClearActiveFinger();

        if (IsSwipeValidForLaunch(swipe))
            DoLaunch();
        else
            CancelAimState();
    }

    void CancelAimState()
    {
        ChargeRatio = 0f;
        LaunchDirection = Vector3.zero;
        cameraController?.SetAiming(false);

        if (ball != null)
            CurrentPhase = LaunchPhase.Placement;
        else
            CurrentPhase = LaunchPhase.None;
    }

    bool IsSwipeValidForLaunch(Vector2 swipe)
    {
        float forwardPixels = Mathf.Max(0f, swipe.y);

        if (useForwardOnlyForPower)
            return forwardPixels >= minLaunchSwipePixels;

        return swipe.magnitude >= minLaunchSwipePixels;
    }

    void DoLaunch()
    {
        if (hasLaunched || ball == null)
            return;

        if (!IsCurrentBallOwnedByLocalPlayer())
            return;

        ResolveOnlineController();

        if (onlineAuthority == null || !onlineAuthority.IsOnlineSession || cachedController == null || !cachedController.IsNetworkStateReadable)
            return;

        hasLaunched = true;
        CurrentPhase = LaunchPhase.Launched;

        RefreshBallVisualRollReference();

        float force = Mathf.Lerp(minForce, maxForce, ChargeRatio);
        Vector3 impulseDir = LaunchDirection.normalized;
        impulseDir.y = 0f;

        cachedController.RequestLaunchCurrentBall(impulseDir, force);

        cameraController?.OnBallLaunched();
    }

    void ApplySwipe(Vector2 swipe)
    {
        Vector3 worldSwipe = new Vector3(swipe.x, 0f, swipe.y);

        if (worldSwipe.sqrMagnitude < 0.001f)
            return;

        Vector3 dir = worldSwipe.normalized;
        Vector3 forward = GetSafeForward();

        if (constrainAngle)
        {
            float signedAngle = Vector3.SignedAngle(forward, dir, Vector3.up);
            float clampedAngle = Mathf.Clamp(signedAngle, -angleLimit, angleLimit);
            dir = Quaternion.AngleAxis(clampedAngle, Vector3.up) * forward;
        }

        dir.y = 0f;
        dir.Normalize();

        LaunchDirection = dir;

        float rawCharge = useForwardOnlyForPower
            ? Mathf.Clamp01(Mathf.Max(0f, swipe.y) / Mathf.Max(1f, maxSwipePixels))
            : Mathf.Clamp01(swipe.magnitude / Mathf.Max(1f, maxSwipePixels));

        ChargeRatio = Mathf.Pow(rawCharge, Mathf.Max(0.01f, chargeCurveExponent));
    }

    bool TryGetPlacementWorldPoint(Vector2 screenPos, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (gameplayCamera == null || ball == null)
            return false;

        Ray ray = gameplayCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, placementPlaneY, 0f));

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        return true;
    }

    Vector3 GetPlacementAxisWorld()
    {
        if (activePlacementArea != null)
        {
            Vector3 axis = activePlacementArea.transform.right;
            axis.y = 0f;

            if (axis.sqrMagnitude > 0.0001f)
                return axis.normalized;
        }

        return Vector3.right;
    }

    Vector3 ClampToPlacementLineAndArea(Vector3 targetWorld)
    {
        if (activePlacementArea == null)
        {
            targetWorld.y = placementPlaneY;
            targetWorld.z = placementDragStartBallWorldPosition.z;
            return targetWorld;
        }

        Vector3 local = activePlacementArea.transform.InverseTransformPoint(targetWorld);
        Vector3 half = activePlacementArea.size * 0.5f;
        Vector3 center = activePlacementArea.center;

        local.x = Mathf.Clamp(local.x, center.x - half.x, center.x + half.x);
        local.y = placementLineLocalY;
        local.z = placementLineLocalZ;

        return activePlacementArea.transform.TransformPoint(local);
    }

    Vector3 GetSafeForward()
    {
        Vector3 forward = forwardAxis;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    void ClearActiveFinger()
    {
        activeFinger = null;
    }

    bool IsGameplayInputLocked()
    {
        ResolveSettingsPanel();

        if (settingsPanelUI != null && settingsPanelUI.IsPanelOpen)
            return true;

        if (onlineAuthority == null || !onlineAuthority.IsOnlineSession)
            return true;

        ResolveOnlineController();

        if (cachedController == null || !cachedController.IsNetworkStateReadable)
            return true;

        if (cachedController.MatchEnded || cachedController.MidMatchBreakActive)
            return true;

        if (cachedController.CurrentTurnOwner != ResolveEffectiveLocalPlayerId())
            return true;

        if (!IsCurrentBallOwnedByLocalPlayer())
            return true;

        return false;
    }

    bool IsCurrentBallOwnedByLocalPlayer()
    {
        if (ball == null)
            return false;

        PlayerID owner = GetOwnerFromBall(ball);
        if (owner == PlayerID.None)
            return false;

        return owner == ResolveEffectiveLocalPlayerId();
    }

    PlayerID GetOwnerFromBall(BallPhysics targetBall)
    {
        if (targetBall == null)
            return PlayerID.None;

        FusionNetworkBall fusionBall = targetBall.GetComponent<FusionNetworkBall>();
        if (fusionBall != null)
            return fusionBall.OwnerPlayerId;

        BallOwnership ownership = targetBall.GetComponent<BallOwnership>();
        if (ownership != null)
            return ownership.Owner;

        return PlayerID.None;
    }

    void RefreshBallVisualRollReference()
    {
        if (ball == null)
        {
            ballVisualRoll = null;
            return;
        }

        ballVisualRoll = ball.GetComponent<BallVisualRoll>();
    }
}