using UnityEngine;

/// <summary>
/// Camera controller:
/// - overview ortografica con viewport + autofit
/// - aim principalmente basata su CameraAimP1 / CameraAimP2
/// - piccola compensazione opzionale per viewport stretti
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
    public BallLauncher ballLauncher;

    [Header("Camera Positions")]
    public Transform overviewAnchor;
    public Transform leftSideAimAnchor;
    public Transform rightSideAimAnchor;

    [Header("Transition")]
    [Range(1f, 20f)]
    public float transitionSpeed = 4.9f;

    [Header("Overview Orthographic")]
    public float overviewOrthoSizeFallback = 4f;

    [Header("Aim Perspective")]
    public bool usePerspectiveInAim = true;

    [Range(10f, 90f)]
    public float leftAimFieldOfView = 90f;

    [Range(10f, 90f)]
    public float rightAimFieldOfView = 90f;

    public float aimOrthoSize = 3f;

    [Header("Aim Viewport")]
    public bool keepViewportInAim = true;

    [Header("Aim Anchor Framing")]
    [Tooltip("Se attivo, usa direttamente CameraAimP1 / CameraAimP2 come base della posa aim.")]
    public bool useAimAnchorsAsPrimaryPose = true;

    [Tooltip("Offset locale applicato all'anchor aim sinistro. X = destra/sinistra, Y = alto/basso, Z = avanti/indietro locale.")]
    public Vector3 leftAimLocalOffset = Vector3.zero;

    [Tooltip("Offset locale applicato all'anchor aim destro. X = destra/sinistra, Y = alto/basso, Z = avanti/indietro locale.")]
    public Vector3 rightAimLocalOffset = Vector3.zero;

    [Header("Aim Viewport Compensation")]
    [Tooltip("Aspetto di riferimento per cui gli anchor sono stati calibrati.")]
    public float referenceAimAspect = 0.5625f;

    [Tooltip("Quanto arretrare localmente l'aim camera quando il viewport utile è più stretto del riferimento.")]
    public float aimLocalZCompensation = 0f;

    [Tooltip("Quanto alzare localmente l'aim camera quando il viewport utile è più stretto del riferimento.")]
    public float aimLocalYCompensation = 0f;

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

        if (ballLauncher == null)
            ballLauncher = FindFirstObjectByType<BallLauncher>();
#else
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();

        if (autoFit == null)
            autoFit = FindObjectOfType<AutoFitOrthographicCamera>();

        if (viewportFitter == null)
            viewportFitter = FindObjectOfType<CameraViewportFitter>();

        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>();
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

        Transform aimAnchor = GetCurrentAimAnchor(out bool playerIsOnLeft);
        if (aimAnchor == null)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        ApplyAimProjectionAndViewport(playerIsOnLeft);

        if (useAimAnchorsAsPrimaryPose)
        {
            BuildAimPoseFromAnchor(aimAnchor, playerIsOnLeft, out Vector3 aimPos, out Quaternion aimRot);
            targetPosition = aimPos;
            targetRotation = aimRot;

            if (debugLogs)
                Debug.Log($"[BallCameraController] Aim pose from anchor. Pos={aimPos}, Rot={aimRot.eulerAngles}");
        }
        else
        {
            targetPosition = aimAnchor.position;
            targetRotation = aimAnchor.rotation;

            if (debugLogs)
                Debug.Log("[BallCameraController] Aim pose using raw anchor transform.");
        }
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
            targetPosition = cachedOverviewPosition;
            targetRotation = cachedOverviewRotation;
        }
        else
        {
            targetPosition = cachedOverviewPosition;
            targetRotation = cachedOverviewRotation;
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

    private void BuildAimPoseFromAnchor(Transform aimAnchor, bool playerIsOnLeft, out Vector3 builtPosition, out Quaternion builtRotation)
    {
        Vector3 localOffset = playerIsOnLeft ? leftAimLocalOffset : rightAimLocalOffset;
        Vector3 compensatedLocalOffset = localOffset + GetViewportCompensationLocalOffset();

        builtPosition = aimAnchor.TransformPoint(compensatedLocalOffset);
        builtRotation = aimAnchor.rotation;
    }

    private Vector3 GetViewportCompensationLocalOffset()
    {
        float currentAspect = GetEffectiveCameraAspect();

        if (currentAspect <= 0.0001f || referenceAimAspect <= 0.0001f)
            return Vector3.zero;

        if (currentAspect >= referenceAimAspect)
            return Vector3.zero;

        float t = Mathf.Clamp01((referenceAimAspect - currentAspect) / referenceAimAspect);

        return new Vector3(
            0f,
            aimLocalYCompensation * t,
            -aimLocalZCompensation * t
        );
    }

    private float GetEffectiveCameraAspect()
    {
        if (cam == null)
            return 1f;

        Rect rect = cam.rect;
        float pixelWidth = Mathf.Max(1f, Screen.width * rect.width);
        float pixelHeight = Mathf.Max(1f, Screen.height * rect.height);

        return pixelWidth / pixelHeight;
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

            Vector3 leftPos = leftSideAimAnchor.TransformPoint(leftAimLocalOffset);
            Gizmos.DrawWireSphere(leftPos, 0.12f);
        }

        if (rightSideAimAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightSideAimAnchor.position, 0.2f);
            Gizmos.DrawRay(rightSideAimAnchor.position, rightSideAimAnchor.forward * 1.5f);

            Vector3 rightPos = rightSideAimAnchor.TransformPoint(rightAimLocalOffset);
            Gizmos.DrawWireSphere(rightPos, 0.12f);
        }
    }
#endif
}