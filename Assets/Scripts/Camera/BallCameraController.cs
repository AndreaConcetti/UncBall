using UnityEngine;

/// <summary>
/// Camera controller con:
/// - overview strategica ortografica + AutoFit
/// - aim lato sinistro/destro in perspective
/// - ritorno dopo il lancio verso la posa overview salvata prima dell'aim
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
    [Tooltip("Anchor base overview. Serve come punto iniziale per il fit strategic.")]
    public Transform overviewAnchor;

    [Tooltip("Anchor aim per il lato sinistro del tavolo")]
    public Transform leftSideAimAnchor;

    [Tooltip("Anchor aim per il lato destro del tavolo")]
    public Transform rightSideAimAnchor;

    [Header("Transition")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    [Header("Overview Orthographic")]
    [Tooltip("Fallback se AutoFitOrthographicCamera non è assegnato")]
    public float overviewOrthoSizeFallback = 4f;

    [Header("Aim Perspective")]
    [Tooltip("Se true, in aim la camera passa a perspective")]
    public bool usePerspectiveInAim = true;

    [Tooltip("FOV usato in aim quando il player di turno è sul lato sinistro")]
    [Range(10f, 90f)]
    public float leftAimFieldOfView = 35f;

    [Tooltip("FOV usato in aim quando il player di turno è sul lato destro")]
    [Range(10f, 90f)]
    public float rightAimFieldOfView = 35f;

    [Tooltip("Se false, in aim resta orthographic e usa aimOrthoSize")]
    public float aimOrthoSize = 3f;

    [Header("Aim Viewport")]
    [Tooltip("Se true, anche durante l'aim mantiene il viewport ristretto dalla UI")]
    public bool keepViewportInAim = true;

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

    /// <summary>
    /// aiming = true  -> entra in aim e salva prima la posa overview attuale
    /// aiming = false -> torna in overview ricalcolando il fit e aggiornando la cache
    /// </summary>
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
    }

    /// <summary>
    /// Da chiamare nel momento esatto in cui la ball parte.
    /// Ritorna alla posa overview salvata prima dell'aim.
    /// </summary>
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

    /// <summary>
    /// Da richiamare se cambia layout UI / safe area / risoluzione
    /// mentre sei già in overview.
    /// </summary>
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