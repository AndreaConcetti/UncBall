using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraViewportFitter : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;

    [Header("UI Occlusion")]
    public RectTransform topUI;
    public RectTransform bottomUI;
    public RectTransform leftUI;
    public RectTransform rightUI;

    [Header("Canvas")]
    public Canvas rootCanvas;

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
        ApplyViewport();
    }

    void LateUpdate()
    {
        ApplyViewport();
    }

    public void ApplyViewport()
    {
        if (targetCamera == null)
            return;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        if (screenWidth <= 0f || screenHeight <= 0f)
            return;

        float topPx = GetHeightOnScreen(topUI);
        float bottomPx = GetHeightOnScreen(bottomUI);
        float leftPx = GetWidthOnScreen(leftUI);
        float rightPx = GetWidthOnScreen(rightUI);

        float x = leftPx / screenWidth;
        float y = bottomPx / screenHeight;
        float width = 1f - ((leftPx + rightPx) / screenWidth);
        float height = 1f - ((topPx + bottomPx) / screenHeight);

        width = Mathf.Clamp01(width);
        height = Mathf.Clamp01(height);

        targetCamera.rect = new Rect(x, y, width, height);
    }

    float GetHeightOnScreen(RectTransform rt)
    {
        if (rt == null)
            return 0f;

        return rt.rect.height * rt.lossyScale.y;
    }

    float GetWidthOnScreen(RectTransform rt)
    {
        if (rt == null)
            return 0f;

        return rt.rect.width * rt.lossyScale.x;
    }
}