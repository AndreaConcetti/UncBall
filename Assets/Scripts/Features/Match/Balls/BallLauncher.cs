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
    public StartEndController startEndController;
    public BallVisualRoll ballVisualRoll;

    [Header("Placement")]
    [Tooltip("Area di placement valida per la ball corrente")]
    public BoxCollider activePlacementArea;

    [Tooltip("Offset verticale opzionale applicato in placement")]
    public float placementYOffset = 0f;

    [Tooltip("Distanza massima tra touch iniziale e centro ball per iniziare il drag di placement")]
    public float placementPickRadiusScreen = 140f;

    [Tooltip("Se attivo, il placement viene smussato leggermente")]
    public bool smoothPlacement = false;

    [Tooltip("Velocità di smoothing del placement")]
    public float placementSmoothSpeed = 20f;

    [Header("Confirm")]
    public bool allowKeyboardConfirm = true;
    public bool allowDoubleTapConfirm = true;
    public float doubleTapMaxInterval = 0.30f;

    [Header("Force Mapping")]
    public float minForce = 8f;
    public float maxForce = 37.5f;
    public float maxSwipePixels = 300f;

    [Tooltip("Soglia minima di swipe in pixel per considerare valido il lancio")]
    public float minLaunchSwipePixels = 20f;

    [Tooltip("Esponente della curva di carica. >1 = più controllo sui tiri bassi/medi")]
    public float chargeCurveExponent = 1.5f;

    [Tooltip("Se attivo, la potenza usa quasi solo la componente forward (Y) dello swipe")]
    public bool useForwardOnlyForPower = true;

    [Header("Direction Constraint")]
    public bool constrainAngle = false;

    [Range(1f, 90f)]
    public float angleLimit = 45f;

    public Vector3 forwardAxis = Vector3.forward;

    [Header("Input")]
    public bool enableTouchSimulationInEditor = true;
    public bool allowMouseInputInEditor = true;

    [Header("Gameplay Input Lock")]
    [Tooltip("Se attivo, blocca placement/aim/launch quando la partita è in pausa")]
    public bool blockGameplayInputWhenPaused = true;

    [Header("Debug")]
    public bool debugLogs = false;

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

    // Placement line data: la ball si muove solo lungo l'asse X locale del BoxCollider
    private float placementPlaneY;
    private float placementLineLocalY;
    private float placementLineLocalZ;

    // Drag state stabile
    private bool hasPlacementDragStart;
    private Vector3 placementDragStartWorldPoint;
    private Vector3 placementDragStartBallWorldPosition;

    // Serve per rilevare il cambio stato verso pausa e pulire input in corso
    private bool wasGameplayInputLockedLastFrame;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (startEndController == null)
            startEndController = FindFirstObjectByType<StartEndController>();
#else
        if (startEndController == null)
            startEndController = FindObjectOfType<StartEndController>();
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
        if (ball == null)
            return;

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        bool gameplayInputLocked = IsGameplayInputLocked();

        if (gameplayInputLocked && !wasGameplayInputLockedLastFrame)
        {
            CancelAllCurrentInputState();

            if (debugLogs)
                Debug.Log("[BallLauncher] Gameplay input locked because match is paused.");
        }

        wasGameplayInputLockedLastFrame = gameplayInputLocked;

        if (gameplayInputLocked)
            return;

        HandleKeyboardConfirm();
        HandleTouchInput();
        HandleMouseInput();
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

        wasGameplayInputLockedLastFrame = IsGameplayInputLocked();

        if (debugLogs)
            Debug.Log("[BallLauncher] Reset -> Placement");
    }

    public void ConfirmPlacementFromUI()
    {
        if (IsGameplayInputLocked())
            return;

        ConfirmPlacement();
    }

    void HandleKeyboardConfirm()
    {
        if (IsGameplayInputLocked())
            return;

        if (!allowKeyboardConfirm || CurrentPhase != LaunchPhase.Placement || hasLaunched)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            ConfirmPlacement();
    }

    void HandleTouchInput()
    {
        if (IsGameplayInputLocked())
            return;

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
        if (IsGameplayInputLocked())
            return;

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

                        if (debugLogs)
                            Debug.Log("[BallLauncher] Mouse double click confirm");
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
        if (ball == null)
            return;

        if (!hasPlacementDragStart)
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
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.isKinematic = true;
            }

            rb.position = targetWorld;
        }
        else
        {
            if (smoothPlacement)
            {
                float t = 1f - Mathf.Exp(-placementSmoothSpeed * Time.deltaTime);
                ball.transform.position = Vector3.Lerp(ball.transform.position, targetWorld, t);
            }
            else
            {
                ball.transform.position = targetWorld;
            }
        }
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

            if (debugLogs)
                Debug.Log("[BallLauncher] Double tap confirm");

            return true;
        }

        lastPlacementTapTime = timeNow;
        lastPlacementTapScreenPos = screenPos;
        return false;
    }

    void ConfirmPlacement()
    {
        if (IsGameplayInputLocked())
            return;

        if (CurrentPhase != LaunchPhase.Placement)
            return;

        CurrentPhase = LaunchPhase.AimReady;
        ChargeRatio = 0f;
        LaunchDirection = GetSafeForward();

        cameraController?.SetAiming(true);

        if (debugLogs)
            Debug.Log("[BallLauncher] Placement confirmed -> AimReady");
    }

    void BeginAimSwipe(Touch touch)
    {
        if (IsGameplayInputLocked())
            return;

        if (CurrentPhase != LaunchPhase.AimReady || hasLaunched)
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
        if (IsGameplayInputLocked())
            return;

        if (hasLaunched || ball == null)
            return;

        hasLaunched = true;
        CurrentPhase = LaunchPhase.Launched;

        RefreshBallVisualRollReference();

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        float force = Mathf.Lerp(minForce, maxForce, ChargeRatio);
        Vector3 impulse = LaunchDirection * force;
        impulse.y = 0f;

        TurnManager.Instance?.NotifyBallLaunched(ball);
        ball.Launch(impulse);

        cameraController?.OnBallLaunched();
        TurnManager.Instance?.PauseTimer();

        if (debugLogs)
            Debug.Log($"[BallLauncher] Launch -> charge={ChargeRatio:F3} force={force:F2} dir={LaunchDirection}");
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

        float rawCharge;

        if (useForwardOnlyForPower)
        {
            float forwardPixels = Mathf.Max(0f, swipe.y);
            rawCharge = Mathf.Clamp01(forwardPixels / Mathf.Max(1f, maxSwipePixels));
        }
        else
        {
            rawCharge = Mathf.Clamp01(swipe.magnitude / Mathf.Max(1f, maxSwipePixels));
        }

        float safeExponent = Mathf.Max(0.01f, chargeCurveExponent);
        ChargeRatio = Mathf.Pow(rawCharge, safeExponent);

        if (debugLogs)
            Debug.Log($"[BallLauncher] Swipe={swipe} rawCharge={rawCharge:F3} curvedCharge={ChargeRatio:F3}");
    }

    bool TryGetPlacementWorldPoint(Vector2 screenPos, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (gameplayCamera == null || ball == null)
            return false;

        Ray ray = gameplayCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, placementPlaneY, 0f));

        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
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

    void CancelAllCurrentInputState()
    {
        activeFinger = null;

        isPlacementDragging = false;
        isAimTracking = false;
        hasPlacementDragStart = false;

        mousePlacementDragging = false;
        mouseAimDragging = false;

        aimStartScreen = Vector2.zero;
        mouseAimStartScreen = Vector2.zero;

        ChargeRatio = 0f;

        if (CurrentPhase == LaunchPhase.AimReady)
            LaunchDirection = GetSafeForward();
        else
            LaunchDirection = Vector3.zero;
    }

    bool IsGameplayInputLocked()
    {
        if (!blockGameplayInputWhenPaused)
            return false;

        if (startEndController == null)
        {
#if UNITY_2023_1_OR_NEWER
            startEndController = FindFirstObjectByType<StartEndController>();
#else
            startEndController = FindObjectOfType<StartEndController>();
#endif
        }

        if (startEndController == null)
            return false;

        return startEndController.IsPaused() || startEndController.IsMatchEnded();
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

    public void Launch(Vector3 direction, float force)
    {
        if (IsGameplayInputLocked())
            return;

        if (ball == null)
            return;

        RefreshBallVisualRollReference();

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        LaunchDirection = direction.normalized;
        LaunchDirection = new Vector3(LaunchDirection.x, 0f, LaunchDirection.z);
        force = Mathf.Clamp(force, minForce, maxForce);

        TurnManager.Instance?.NotifyBallLaunched(ball);

        ball.Launch(LaunchDirection * force);
        hasLaunched = true;
        CurrentPhase = LaunchPhase.Launched;

        cameraController?.OnBallLaunched();
        TurnManager.Instance?.PauseTimer();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (ball == null)
            return;

        Vector3 origin = ball.transform.position;

        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin, GetSafeForward() * 1.5f);

        if (activePlacementArea != null)
        {
            Vector3 placementAxis = activePlacementArea.transform.right;
            placementAxis.y = 0f;

            if (placementAxis.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(origin, placementAxis.normalized * 0.75f);
                Gizmos.DrawRay(origin, -placementAxis.normalized * 0.75f);
            }
        }

        if (constrainAngle)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 left = Quaternion.AngleAxis(-angleLimit, Vector3.up) * GetSafeForward();
            Vector3 right = Quaternion.AngleAxis(angleLimit, Vector3.up) * GetSafeForward();
            Gizmos.DrawRay(origin, left * 1.5f);
            Gizmos.DrawRay(origin, right * 1.5f);
        }

        if (Application.isPlaying && CurrentPhase == LaunchPhase.AimReady)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, LaunchDirection * Mathf.Lerp(minForce, maxForce, ChargeRatio) * 0.1f);
        }
    }
#endif
}