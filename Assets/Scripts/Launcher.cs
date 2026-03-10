using UnityEngine;

/// <summary>
/// Reads a swipe/drag gesture (touch or mouse) to determine throw direction and force.
///
/// Gesture flow:
///   1. Finger/mouse down  → record start point
///   2. Drag               → show live direction + charge (swipe length drives force)
///   3. Finger/mouse up    → launch in the swipe direction with swipe-length force
///
/// The swipe direction on screen is mapped to the X/Z play surface:
///   screen right  → world +X
///   screen up     → world +Z  (toward the islands)
/// </summary>
public class BallLauncher : MonoBehaviour
{
    [Header("References")]
    public BallPhysics ball;

    [Tooltip("Optional — assign to trigger camera transitions on charge/release")]
    public BallCameraController cameraController;

    [Header("Force Mapping")]
    [Tooltip("Minimum launch force (short swipe)")]
    public float minForce = 3f;

    [Tooltip("Maximum launch force (long swipe)")]
    public float maxForce = 14f;

    [Tooltip("Screen swipe length in pixels that maps to maxForce")]
    public float maxSwipePixels = 300f;

    [Header("Direction Constraint (optional)")]
    [Tooltip("If true, only allow throws within ±angleLimit degrees of forwardAxis")]
    public bool constrainAngle = false;

    [Tooltip("Max angle (degrees) from forwardAxis allowed")]
    [Range(1f, 90f)]
    public float angleLimit = 45f;

    [Tooltip("Forward axis on the play surface (world space)")]
    public Vector3 forwardAxis = Vector3.forward;

    // ── Runtime State ──────────────────────────────────────────

    /// <summary>Swipe charge in [0,1] — use to drive a UI power bar.</summary>
    public float ChargeRatio { get; private set; }

    /// <summary>Current swipe direction in world space (X/Z plane).</summary>
    public Vector3 LaunchDirection { get; private set; }

    private bool _tracking;
    private Vector2 _startScreen;

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#else
        HandleTouch();
#endif
    }

    // ── Input Handlers ─────────────────────────────────────────

    private void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
            BeginTracking(Input.mousePosition);

        if (_tracking)
            UpdateTracking(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            EndTracking(Input.mousePosition);
    }

    private void HandleTouch()
    {
        if (Input.touchCount == 0) return;
        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                BeginTracking(touch.position);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (_tracking) UpdateTracking(touch.position);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndTracking(touch.position);
                break;
        }
    }

    // ── Gesture Logic ──────────────────────────────────────────

    private void BeginTracking(Vector2 screenPos)
    {
        //if (!ball.IsActive) return;
        _tracking = true;
        _startScreen = screenPos;
        ChargeRatio = 0f;
        LaunchDirection = forwardAxis.normalized;
        cameraController?.SetAiming(true);
    }

    private void UpdateTracking(Vector2 screenPos)
    {
        Vector2 swipe = screenPos - _startScreen;
        ApplySwipe(swipe);
    }

    private void EndTracking(Vector2 screenPos)
    {
        if (!_tracking) return;
        _tracking = false;

        Vector2 swipe = screenPos - _startScreen;
        ApplySwipe(swipe);

        // Only launch if there was a meaningful gesture
        if (swipe.magnitude > 5f)
            DoLaunch();
        else
            Debug.Log("[BallLauncher] Swipe too short — no launch.");

        ChargeRatio = 0f;
        LaunchDirection = Vector3.zero;
        cameraController?.SetAiming(false);
    }

    private void ApplySwipe(Vector2 swipe)
    {
        // Map screen swipe → world X/Z direction
        // Screen +X = world +X, Screen +Y = world +Z
        Vector3 worldSwipe = new Vector3(swipe.x, 0f, swipe.y);

        if (worldSwipe.sqrMagnitude < 0.001f) return;

        Vector3 dir = worldSwipe.normalized;

        // Optional angle constraint
        if (constrainAngle)
        {
            float angle = Vector3.Angle(forwardAxis, dir);
            if (angle > angleLimit)
                dir = Vector3.RotateTowards(forwardAxis.normalized, dir,
                          angleLimit * Mathf.Deg2Rad, 0f);
        }

        LaunchDirection = dir;
        ChargeRatio = Mathf.Clamp01(swipe.magnitude / maxSwipePixels);

        Debug.Log($"[BallLauncher] Dir: {LaunchDirection:F2}  " +
                  $"Charge: {ChargeRatio * 100f:F0}%  " +
                  $"Force: {Mathf.Lerp(minForce, maxForce, ChargeRatio):F1}");
    }

    // ── Launch ─────────────────────────────────────────────────

    private void DoLaunch()
    {
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        // sblocca la fisica
        rb.constraints = RigidbodyConstraints.None;
        float force = Mathf.Lerp(minForce, maxForce, ChargeRatio);
        Vector3 impulse = LaunchDirection * force;
        impulse.y = 0f;

        Debug.Log($"[BallLauncher] LAUNCH → dir: {LaunchDirection:F2}  force: {force:F1}  impulse: {impulse:F2}");
        ball.Launch(impulse);
        TurnManager.Instance.PauseTimer();
    }

    /// <summary>Scripted / UI launch in a fixed direction at a given force [minForce–maxForce].</summary>
    public void Launch(Vector3 direction, float force)
    {
        LaunchDirection = direction.normalized;
        LaunchDirection = new Vector3(LaunchDirection.x, 0f, LaunchDirection.z);
        force = Mathf.Clamp(force, minForce, maxForce);
        Debug.Log($"[BallLauncher] Scripted LAUNCH → dir: {LaunchDirection:F2}  force: {force:F1}");
        ball.Launch(LaunchDirection * force);
    }

    // ── Gizmos ─────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (ball == null) return;
        Vector3 origin = ball.transform.position;

        // Forward axis
        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin, forwardAxis.normalized * maxForce * 0.1f);

        if (constrainAngle)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 left  = Quaternion.AngleAxis(-angleLimit, Vector3.up) * forwardAxis.normalized;
            Vector3 right = Quaternion.AngleAxis( angleLimit, Vector3.up) * forwardAxis.normalized;
            Gizmos.DrawRay(origin, left  * maxForce * 0.1f);
            Gizmos.DrawRay(origin, right * maxForce * 0.1f);
        }

        if (!Application.isPlaying || !_tracking) return;

        // Live swipe preview
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, LaunchDirection * Mathf.Lerp(minForce, maxForce, ChargeRatio) * 0.1f);
    }
#endif
}