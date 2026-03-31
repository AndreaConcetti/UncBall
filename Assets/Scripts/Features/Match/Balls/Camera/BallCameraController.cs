using UnityEngine;

public class BallCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private AutoFitOrthographicCamera autoFit;
    [SerializeField] private CameraViewportFitter viewportFitter;
    [SerializeField] private BallLauncher ballLauncher;
    [SerializeField] private OnlineGameplayAuthority onlineAuthority;

    [Header("Strategic Camera")]
    [SerializeField] private Transform overviewAnchor;
    [SerializeField] private float overviewOrthoSizeFallback = 4f;

    [Header("Aim Anchors")]
    [SerializeField] private Transform leftSideAimAnchor;
    [SerializeField] private Transform rightSideAimAnchor;

    [Header("Transition")]
    [SerializeField, Range(1f, 20f)] private float transitionSpeed = 5f;

    [Header("Aim Projection")]
    [SerializeField] private bool usePerspectiveInAim = true;
    [SerializeField, Range(10f, 90f)] private float leftAimFieldOfView = 90f;
    [SerializeField, Range(10f, 90f)] private float rightAimFieldOfView = 90f;
    [SerializeField] private float aimOrthoSize = 3f;

    [Header("Aim Pose")]
    [SerializeField] private bool useAimAnchorsAsPrimaryPose = true;
    [SerializeField] private Vector3 leftAimLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 rightAimLocalOffset = Vector3.zero;

    [Header("Viewport Compensation")]
    [SerializeField] private bool keepViewportInAim = true;
    [SerializeField] private float referenceAimAspect = 0.5625f;
    [SerializeField] private float aimLocalZCompensation = 0f;
    [SerializeField] private float aimLocalYCompensation = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

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

    private void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (cam != null)
        {
            targetPosition = cam.transform.position;
            targetRotation = cam.transform.rotation;
        }
    }

    private void Start()
    {
        ResolveDependencies();
        ApplyOverviewAndCache(true);
    }

    private void LateUpdate()
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

        ResolveDependencies();
        isAiming = aiming;

        if (!aiming)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        SaveCurrentOverviewPose();

        Transform aimAnchor = GetCurrentAimAnchor(out bool localAimSideIsLeft);
        if (aimAnchor == null)
        {
            ApplyOverviewAndCache(false);
            return;
        }

        ApplyAimProjectionAndViewport(localAimSideIsLeft);

        if (useAimAnchorsAsPrimaryPose)
        {
            BuildAimPoseFromAnchor(aimAnchor, localAimSideIsLeft, out Vector3 aimPos, out Quaternion aimRot);
            targetPosition = aimPos;
            targetRotation = aimRot;
        }
        else
        {
            targetPosition = aimAnchor.position;
            targetRotation = aimAnchor.rotation;
        }

        if (debugLogs)
        {
            Debug.Log(
                "[BallCameraController] SetAiming -> " +
                "LocalAimSideIsLeft=" + localAimSideIsLeft,
                this
            );
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
        }

        targetPosition = cachedOverviewPosition;
        targetRotation = cachedOverviewRotation;
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

    private void ApplyAimProjectionAndViewport(bool isLeftSide)
    {
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
            cam.fieldOfView = isLeftSide ? leftAimFieldOfView : rightAimFieldOfView;
        }
        else
        {
            cam.orthographic = true;
            cam.orthographicSize = aimOrthoSize;
        }
    }

    private void BuildAimPoseFromAnchor(Transform aimAnchor, bool isLeftSide, out Vector3 builtPosition, out Quaternion builtRotation)
    {
        Vector3 localOffset = isLeftSide ? leftAimLocalOffset : rightAimLocalOffset;
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
        return new Vector3(0f, aimLocalYCompensation * t, -aimLocalZCompensation * t);
    }

    private float GetEffectiveCameraAspect()
    {
        Rect rect = cam.rect;
        float pixelWidth = Mathf.Max(1f, Screen.width * rect.width);
        float pixelHeight = Mathf.Max(1f, Screen.height * rect.height);
        return pixelWidth / pixelHeight;
    }

    private Transform GetCurrentAimAnchor(out bool localAimSideIsLeft)
    {
        localAimSideIsLeft = true;

        if (ballTurnSpawner == null || onlineAuthority == null)
            return overviewAnchor;

        PlayerID playerToFrame = onlineAuthority.LocalPlayerId;
        localAimSideIsLeft = ballTurnSpawner.IsPlayerIdOnLeft(playerToFrame);

        return localAimSideIsLeft
            ? (leftSideAimAnchor != null ? leftSideAimAnchor : overviewAnchor)
            : (rightSideAimAnchor != null ? rightSideAimAnchor : overviewAnchor);
    }

    private void ResolveDependencies()
    {
        if (onlineAuthority == null)
            onlineAuthority = OnlineGameplayAuthority.Instance;

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
    }
}