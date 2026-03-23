using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraViewportFitter : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("Reference Resolution")]
    public Vector2 referenceResolution = new Vector2(1080f, 1920f);

    [Header("Reserved Design Space (pixels on reference resolution)")]
    [Tooltip("Spazio riservato in alto nel layout di riferimento")]
    public float topReservedPixels = 180f;

    [Tooltip("Spazio riservato in basso nel layout di riferimento")]
    public float bottomReservedPixels = 270f;

    [Tooltip("Spazio riservato a sinistra nel layout di riferimento")]
    public float leftReservedPixels = 0f;

    [Tooltip("Spazio riservato a destra nel layout di riferimento")]
    public float rightReservedPixels = 0f;

    [Header("Safe Area")]
    [Tooltip("Se attivo, interseca anche la viewport con la safe area reale del device")]
    public bool respectSafeArea = true;

    [Header("Refresh")]
    public bool applyOnStart = true;
    public bool applyContinuously = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private Rect lastAppliedRect = new Rect(-1f, -1f, -1f, -1f);
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
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

        ClampValues();

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
        if (targetCamera == null)
            return;

        ClampValues();

        float refWidth = Mathf.Max(1f, referenceResolution.x);
        float refHeight = Mathf.Max(1f, referenceResolution.y);

        float designX = leftReservedPixels / refWidth;
        float designY = bottomReservedPixels / refHeight;
        float designWidth = 1f - ((leftReservedPixels + rightReservedPixels) / refWidth);
        float designHeight = 1f - ((topReservedPixels + bottomReservedPixels) / refHeight);

        Rect designRect = new Rect(
            Mathf.Clamp01(designX),
            Mathf.Clamp01(designY),
            Mathf.Clamp01(designWidth),
            Mathf.Clamp01(designHeight)
        );

        Rect finalRect = designRect;

        if (respectSafeArea)
        {
            Rect safeArea = Screen.safeArea;
            Rect safeAreaNormalized = new Rect(
                safeArea.x / Mathf.Max(1f, Screen.width),
                safeArea.y / Mathf.Max(1f, Screen.height),
                safeArea.width / Mathf.Max(1f, Screen.width),
                safeArea.height / Mathf.Max(1f, Screen.height)
            );

            finalRect = IntersectRects(designRect, safeAreaNormalized);
        }

        bool changed =
            force ||
            Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            Screen.safeArea != lastSafeArea ||
            !ApproximatelyRect(lastAppliedRect, finalRect);

        if (!changed)
            return;

        targetCamera.rect = finalRect;

        lastAppliedRect = finalRect;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;

        if (debugLogs)
        {
            Debug.Log(
                $"[CameraViewportFitter] Applied viewport rect = {finalRect} | " +
                $"Screen = {Screen.width}x{Screen.height} | SafeArea = {Screen.safeArea}"
            );
        }
    }

    private void ClampValues()
    {
        referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
        referenceResolution.y = Mathf.Max(1f, referenceResolution.y);

        topReservedPixels = Mathf.Max(0f, topReservedPixels);
        bottomReservedPixels = Mathf.Max(0f, bottomReservedPixels);
        leftReservedPixels = Mathf.Max(0f, leftReservedPixels);
        rightReservedPixels = Mathf.Max(0f, rightReservedPixels);

        float maxVertical = referenceResolution.y - 1f;
        float maxHorizontal = referenceResolution.x - 1f;

        if (topReservedPixels + bottomReservedPixels >= referenceResolution.y)
        {
            float scale = maxVertical / Mathf.Max(1f, topReservedPixels + bottomReservedPixels);
            topReservedPixels *= scale;
            bottomReservedPixels *= scale;
        }

        if (leftReservedPixels + rightReservedPixels >= referenceResolution.x)
        {
            float scale = maxHorizontal / Mathf.Max(1f, leftReservedPixels + rightReservedPixels);
            leftReservedPixels *= scale;
            rightReservedPixels *= scale;
        }
    }

    private Rect IntersectRects(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMax < xMin || yMax < yMin)
            return new Rect(0f, 0f, 1f, 1f);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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