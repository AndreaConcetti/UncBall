using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraViewportFitter : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;

    [Tooltip("RectTransform che definisce esattamente la finestra gameplay visibile")]
    public RectTransform gameplayViewportFrame;

    [Tooltip("Canvas root a cui appartiene il frame gameplay")]
    public Canvas referenceCanvas;

    [Header("Refresh")]
    public bool applyOnStart = true;
    public bool applyContinuously = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private Rect lastAppliedRect = new Rect(-1f, -1f, -1f, -1f);
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (referenceCanvas == null && gameplayViewportFrame != null)
            referenceCanvas = gameplayViewportFrame.GetComponentInParent<Canvas>();
    }

    void Start()
    {
        if (applyOnStart)
            ApplyViewport(true);
    }

    void LateUpdate()
    {
        if (!applyContinuously)
            return;

        ApplyViewport(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (referenceCanvas == null && gameplayViewportFrame != null)
            referenceCanvas = gameplayViewportFrame.GetComponentInParent<Canvas>();

        if (!Application.isPlaying)
            ApplyViewport(true);
    }
#endif

    public void ApplyViewport()
    {
        ApplyViewport(true);
    }

    private void ApplyViewport(bool force)
    {
        if (targetCamera == null || gameplayViewportFrame == null)
            return;

        if (!TryGetNormalizedScreenRect(gameplayViewportFrame, out Rect normalizedRect))
            return;

        bool changed =
            force ||
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            !ApproximatelyRect(lastAppliedRect, normalizedRect);

        if (!changed)
            return;

        targetCamera.rect = normalizedRect;

        lastAppliedRect = normalizedRect;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (debugLogs)
        {
            Debug.Log($"[CameraViewportFitter] Applied viewport rect = {normalizedRect}");
        }
    }

    private bool TryGetNormalizedScreenRect(RectTransform rectTransform, out Rect normalizedRect)
    {
        normalizedRect = default;

        if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
            return false;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Camera uiCamera = GetCanvasEventCamera();

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);

            if (screenPoint.x < min.x) min.x = screenPoint.x;
            if (screenPoint.y < min.y) min.y = screenPoint.y;
            if (screenPoint.x > max.x) max.x = screenPoint.x;
            if (screenPoint.y > max.y) max.y = screenPoint.y;
        }

        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);

        float xMin = Mathf.Clamp01(min.x / screenWidth);
        float yMin = Mathf.Clamp01(min.y / screenHeight);
        float xMax = Mathf.Clamp01(max.x / screenWidth);
        float yMax = Mathf.Clamp01(max.y / screenHeight);

        normalizedRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }

    private Camera GetCanvasEventCamera()
    {
        if (referenceCanvas == null)
            return null;

        if (referenceCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (referenceCanvas.worldCamera != null)
            return referenceCanvas.worldCamera;

        return Camera.main;
    }

    private bool ApproximatelyRect(Rect a, Rect b)
    {
        const float eps = 0.0001f;

        return Mathf.Abs(a.x - b.x) < eps &&
               Mathf.Abs(a.y - b.y) < eps &&
               Mathf.Abs(a.width - b.width) < eps &&
               Mathf.Abs(a.height - b.height) < eps;
    }

    public Rect GetCurrentViewportRect()
    {
        return targetCamera != null ? targetCamera.rect : new Rect(0f, 0f, 1f, 1f);
    }
}