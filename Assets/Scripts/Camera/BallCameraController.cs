using UnityEngine;

/// <summary>
/// Camera controller:
/// - overview ortografica con viewport fisso e autofit
/// - aim perspective sui due lati
/// - ritorno alla overview dopo il lancio
/// </summary>
public class BallCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public TurnManager turnManager;
    public BallTurnSpawner ballTurnSpawner;
    public AutoFitOrthographicCamera autoFit;
    public CameraViewportFitter viewportFitter;

    [Header("Camera Positions")]
    public Transform overviewAnchor;
    public Transform leftSideAimAnchor;
    public Transform rightSideAimAnchor;

    [Header("Transition")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    [Header("Overview Orthographic")]
    public float overviewOrthoSizeFallback = 4f;

    [Header("Aim Perspective")]
    public bool usePerspectiveInAim = true;

    [Range(10f, 90f)]
    public float leftAimFieldOfView = 35f;

    [Range(10f, 90f)]
    public float rightAimFieldOfView = 35f;

    public float aimOrthoSize = 3f;

    [Header("Aim Viewport")]
    public bool keepViewportInAim = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool isAiming;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private bool hasCachedOverviewPose;
    private Vector3 cachedOverviewPosition;
    private Quaternion cachedOverviewRotation;
    private float cachedOverviewOrthoSize;
    private Rect cachedOverviewRect;

    public bool IsAiming => isAiming;
    public bool IsOverview => !isAiming;

    void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (cam != null)
        {
            targetPosition = cam.transform.position;
            targetRotation = cam.transform.rotation;
        }

        isAiming = false;
    }

    void Start()
    {
        if (turnManager == null)
            turnManager = TurnManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();

        if (autoFit == null)
            autoFit = FindFirstObjectByType<AutoFitOrthographicCamera>();

        if (viewportFitter == null)
            viewportFitter = FindFirstObjectByType<CameraViewportFitter>();
#else
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();

        if (autoFit == null)
            autoFit = FindObjectOfType<AutoFitOrthographicCamera>();

        if (viewportFitter == null)
            viewportFitter = FindObjectOfType<CameraViewportFitter>();
#endif

        ApplyOverviewAndCache(true);
    }

    void LateUpdate()
    {
        if (cam == null)
            return;

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPosition, t);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRotation, t);
    }

    public void SetAiming(bool aiming)
    {
        if (cam == null)
            return;

        isAiming = aiming;

        if (!aiming)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        SaveCurrentOverviewPose();

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        Transform aimAnchor = GetCurrentAimAnchor(out bool playerIsOnLeft);
        if (aimAnchor == null)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        ApplyAimProjectionAndViewport(playerIsOnLeft);

        targetPosition = aimAnchor.position;
        targetRotation = aimAnchor.rotation;

        if (debugLogs)
            Debug.Log("[BallCameraController] Enter aim.");
    }

    public void OnBallLaunched()
    {
        if (cam == null)
            return;

        isAiming = false;

        if (!hasCachedOverviewPose)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        cam.orthographic = true;
        cam.orthographicSize = cachedOverviewOrthoSize;
        cam.rect = cachedOverviewRect;

        targetPosition = cachedOverviewPosition;
        targetRotation = cachedOverviewRotation;

        if (debugLogs)
            Debug.Log("[BallCameraController] Return to overview after launch.");
    }

    public void RefreshOverviewIfNeeded()
    {
        if (!isAiming)
            ApplyOverviewAndCache(false);
    }

    private void ApplyOverviewAndCache(bool snapToOverviewPose)
    {
        if (cam == null)
            return;

        cam.orthographic = true;

        if (overviewAnchor != null)
        {
            cam.transform.position = overviewAnchor.position;
            cam.transform.rotation = overviewAnchor.rotation;
        }

        if (viewportFitter != null)
            viewportFitter.ApplyViewport();
        else
            cam.rect = new Rect(0f, 0f, 1f, 1f);

        if (autoFit != null)
            autoFit.FitNow();
        else
            cam.orthographicSize = overviewOrthoSizeFallback;

        SaveCurrentOverviewPose();

        if (snapToOverviewPose)
        {
            cam.transform.position = cachedOverviewPosition;
            cam.transform.rotation = cachedOverviewRotation;
        }

        targetPosition = cachedOverviewPosition;
        targetRotation = cachedOverviewRotation;

        if (debugLogs)
        {
            Debug.Log($"[BallCameraController] Overview applied. Rect={cam.rect}, OrthoSize={cam.orthographicSize}");
        }
    }

    private void SaveCurrentOverviewPose()
    {
        if (cam == null)
            return;

        cachedOverviewPosition = cam.transform.position;
        cachedOverviewRotation = cam.transform.rotation;
        cachedOverviewRect = cam.rect;
        cachedOverviewOrthoSize = cam.orthographic ? cam.orthographicSize : overviewOrthoSizeFallback;
        hasCachedOverviewPose = true;
    }

    private void ApplyAimProjectionAndViewport(bool playerIsOnLeft)
    {
        if (cam == null)
            return;

        if (keepViewportInAim)
        {
            if (viewportFitter != null)
                viewportFitter.ApplyViewport();
            else
                cam.rect = new Rect(0f, 0f, 1f, 1f);
        }
        else
        {
            cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        if (usePerspectiveInAim)
        {
            cam.orthographic = false;
            cam.fieldOfView = playerIsOnLeft ? leftAimFieldOfView : rightAimFieldOfView;
        }
        else
        {
            cam.orthographic = true;
            cam.orthographicSize = aimOrthoSize;
        }
    }

    private Transform GetCurrentAimAnchor(out bool playerIsOnLeft)
    {
        playerIsOnLeft = true;

        if (turnManager == null || ballTurnSpawner == null || turnManager.currentPlayer == null)
            return overviewAnchor;

        playerIsOnLeft = ballTurnSpawner.IsPlayerOnLeft(
            turnManager.currentPlayer,
            turnManager.player1,
            turnManager.player2
        );

        if (playerIsOnLeft)
            return leftSideAimAnchor != null ? leftSideAimAnchor : overviewAnchor;

        return rightSideAimAnchor != null ? rightSideAimAnchor : overviewAnchor;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (overviewAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overviewAnchor.position, 0.2f);
            Gizmos.DrawRay(overviewAnchor.position, overviewAnchor.forward * 1.5f);
        }

        if (leftSideAimAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftSideAimAnchor.position, 0.2f);
            Gizmos.DrawRay(leftSideAimAnchor.position, leftSideAimAnchor.forward * 1.5f);
        }

        if (rightSideAimAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightSideAimAnchor.position, 0.2f);
            Gizmos.DrawRay(rightSideAimAnchor.position, rightSideAimAnchor.forward * 1.5f);
        }
    }
#endif
}