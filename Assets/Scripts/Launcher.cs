using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Gestisce il controllo della ball corrente in 3 fasi:
/// 1. Placement
/// 2. Confirm
/// 3. Launch
///
/// Mobile:
/// - drag touch per placement
/// - tap per conferma
/// - swipe per launch
///
/// PC / Editor:
/// - drag mouse per placement
/// - Enter per conferma
/// - drag mouse per launch
/// </summary>
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

    [Header("Placement")]
    public BoxCollider activePlacementArea;
    public bool lockPlacementY = true;
    public float placementYOffset = 0f;
    public LayerMask placementRaycastMask = ~0;
    public float placementRayDistance = 500f;
    public float placementPickRadiusScreen = 140f;

    [Header("Confirm")]
    public float confirmTapRadiusScreen = 140f;
    public float confirmTapMaxDuration = 0.25f;
    public bool allowKeyboardConfirm = true;

    [Header("Force Mapping")]
    public float minForce = 3f;
    public float maxForce = 14f;
    public float maxSwipePixels = 300f;

    [Header("Direction Constraint")]
    public bool constrainAngle = false;

    [Range(1f, 90f)]
    public float angleLimit = 45f;

    public Vector3 forwardAxis = Vector3.forward;

    [Header("Input")]
    public bool enableTouchSimulationInEditor = true;
    public bool allowMouseInputInEditor = true;

    [Header("Debug")]
    public bool debugLogs = false;

    public float ChargeRatio { get; private set; }
    public Vector3 LaunchDirection { get; private set; }
    public LaunchPhase CurrentPhase { get; private set; } = LaunchPhase.None;

    private Finger activeFinger;
    private bool isPlacementDragging;
    private bool isAimTracking;
    private Vector2 aimStartScreen;
    private float touchStartTime;
    private float fixedPlacementY;
    private bool hasLaunched;

    private bool mousePlacementDragging;
    private bool mouseAimDragging;
    private Vector2 mouseAimStartScreen;

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

        CurrentPhase = LaunchPhase.Placement;

        if (ball != null)
            fixedPlacementY = ball.transform.position.y + placementYOffset;

        cameraController?.SetAiming(false);

        if (debugLogs)
            Debug.Log("[BallLauncher] Reset -> Placement");
    }

    void HandleKeyboardConfirm()
    {
        if (!allowKeyboardConfirm)
            return;

        if (CurrentPhase != LaunchPhase.Placement)
            return;

        if (hasLaunched)
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
                    TryBeginPlacement(touch);
                    if (activeFinger != null)
                        return;

                    TryPrepareConfirmTap(touch);
                    if (activeFinger != null)
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

    void LateUpdate()
    {
        if (activeFinger == null || isPlacementDragging || isAimTracking || CurrentPhase != LaunchPhase.Placement)
            return;

        var touch = activeFinger.currentTouch;
        if (!touch.valid)
        {
            ClearActiveFinger();
            return;
        }

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
        {
            float duration = Time.time - touchStartTime;

            if (duration <= confirmTapMaxDuration)
                ConfirmPlacement();

            ClearActiveFinger();
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            ClearActiveFinger();
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

                if (Vector2.Distance(mousePos, ballScreen) <= placementPickRadiusScreen)
                {
                    mousePlacementDragging = true;

                    if (lockPlacementY)
                        fixedPlacementY = ball.transform.position.y + placementYOffset;
                }
            }

            if (mousePlacementDragging && Mouse.current.leftButton.isPressed)
            {
                UpdatePlacement(mousePos);
            }

            if (mousePlacementDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                mousePlacementDragging = false;
            }
        }
        else if (CurrentPhase == LaunchPhase.AimReady)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                mouseAimDragging = true;
                mouseAimStartScreen = mousePos;
                ChargeRatio = 0f;
                LaunchDirection = forwardAxis.normalized;
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

                if (swipe.magnitude > 5f)
                    DoLaunch();
                else
                    CancelAimState();
            }
        }
#endif
    }

    void TryBeginPlacement(Touch touch)
    {
        if (ball == null || CurrentPhase != LaunchPhase.Placement)
            return;

        Vector2 screenPos = touch.screenPosition;
        Vector2 ballScreen = gameplayCamera.WorldToScreenPoint(ball.transform.position);

        if (Vector2.Distance(screenPos, ballScreen) > placementPickRadiusScreen)
            return;

        activeFinger = touch.finger;
        isPlacementDragging = true;
        touchStartTime = Time.time;

        if (lockPlacementY)
            fixedPlacementY = ball.transform.position.y + placementYOffset;
    }

    void UpdatePlacement(Vector2 screenPos)
    {
        if (ball == null)
            return;

        if (TryGetPlacementWorldPoint(screenPos, out Vector3 worldPoint))
        {
            worldPoint = ClampPointInsidePlacementArea(worldPoint);

            if (lockPlacementY)
                worldPoint.y = fixedPlacementY;

            ball.transform.position = worldPoint;
        }
    }

    void EndPlacementDrag()
    {
        isPlacementDragging = false;
        ClearActiveFinger();
    }

    void TryPrepareConfirmTap(Touch touch)
    {
        if (ball == null || CurrentPhase != LaunchPhase.Placement)
            return;

        Vector2 screenPos = touch.screenPosition;
        Vector2 ballScreen = gameplayCamera.WorldToScreenPoint(ball.transform.position);

        if (Vector2.Distance(screenPos, ballScreen) > confirmTapRadiusScreen)
            return;

        activeFinger = touch.finger;
        touchStartTime = Time.time;
    }

    void ConfirmPlacement()
    {
        if (CurrentPhase != LaunchPhase.Placement)
            return;

        CurrentPhase = LaunchPhase.AimReady;
        ChargeRatio = 0f;
        LaunchDirection = forwardAxis.normalized;

        cameraController?.SetAiming(true);

        if (debugLogs)
            Debug.Log("[BallLauncher] Placement confirmed -> AimReady");
    }

    void BeginAimSwipe(Touch touch)
    {
        if (CurrentPhase != LaunchPhase.AimReady || hasLaunched)
            return;

        activeFinger = touch.finger;
        isAimTracking = true;
        aimStartScreen = touch.screenPosition;
        ChargeRatio = 0f;
        LaunchDirection = forwardAxis.normalized;
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

        if (swipe.magnitude > 5f)
            DoLaunch();
        else
            CancelAimState();
    }

    void CancelAimState()
    {
        ChargeRatio = 0f;
        LaunchDirection = Vector3.zero;
    }

    void DoLaunch()
    {
        if (hasLaunched || ball == null)
            return;

        hasLaunched = true;
        CurrentPhase = LaunchPhase.Launched;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
            rb.constraints = RigidbodyConstraints.None;

        float force = Mathf.Lerp(minForce, maxForce, ChargeRatio);
        Vector3 impulse = LaunchDirection * force;
        impulse.y = 0f;

        TurnManager.Instance?.NotifyBallLaunched(ball);

        ball.Launch(impulse);

        cameraController?.SetAiming(false);
        TurnManager.Instance?.PauseTimer();

        if (debugLogs)
            Debug.Log($"[BallLauncher] Launch -> force={force:F2}");
    }

    void ApplySwipe(Vector2 swipe)
    {
        Vector3 worldSwipe = new Vector3(swipe.x, 0f, swipe.y);

        if (worldSwipe.sqrMagnitude < 0.001f)
            return;

        Vector3 dir = worldSwipe.normalized;

        if (constrainAngle)
        {
            float angle = Vector3.Angle(forwardAxis, dir);
            if (angle > angleLimit)
            {
                dir = Vector3.RotateTowards(
                    forwardAxis.normalized,
                    dir,
                    angleLimit * Mathf.Deg2Rad,
                    0f
                );
            }
        }

        LaunchDirection = dir;
        ChargeRatio = Mathf.Clamp01(swipe.magnitude / maxSwipePixels);
    }

    bool TryGetPlacementWorldPoint(Vector2 screenPos, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (gameplayCamera == null)
            return false;

        Ray ray = gameplayCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, placementRayDistance, placementRaycastMask))
        {
            worldPoint = hit.point;
            return true;
        }

        float planeY = lockPlacementY ? fixedPlacementY : ball.transform.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        if (plane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    Vector3 ClampPointInsidePlacementArea(Vector3 worldPoint)
    {
        if (activePlacementArea == null)
            return worldPoint;

        Vector3 local = activePlacementArea.transform.InverseTransformPoint(worldPoint);
        Vector3 half = activePlacementArea.size * 0.5f;
        Vector3 center = activePlacementArea.center;

        local.x = Mathf.Clamp(local.x, center.x - half.x, center.x + half.x);
        local.y = Mathf.Clamp(local.y, center.y - half.y, center.y + half.y);
        local.z = Mathf.Clamp(local.z, center.z - half.z, center.z + half.z);

        return activePlacementArea.transform.TransformPoint(local);
    }

    void ClearActiveFinger()
    {
        activeFinger = null;
    }

    public void Launch(Vector3 direction, float force)
    {
        if (ball == null)
            return;

        LaunchDirection = direction.normalized;
        LaunchDirection = new Vector3(LaunchDirection.x, 0f, LaunchDirection.z);
        force = Mathf.Clamp(force, minForce, maxForce);

        TurnManager.Instance?.NotifyBallLaunched(ball);

        ball.Launch(LaunchDirection * force);
        hasLaunched = true;
        CurrentPhase = LaunchPhase.Launched;

        cameraController?.SetAiming(false);
        TurnManager.Instance?.PauseTimer();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (ball == null)
            return;

        Vector3 origin = ball.transform.position;

        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin, forwardAxis.normalized * 1.5f);

        if (constrainAngle)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 left = Quaternion.AngleAxis(-angleLimit, Vector3.up) * forwardAxis.normalized;
            Vector3 right = Quaternion.AngleAxis(angleLimit, Vector3.up) * forwardAxis.normalized;
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