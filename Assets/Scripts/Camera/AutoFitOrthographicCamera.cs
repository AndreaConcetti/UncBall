using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class AutoFitOrthographicCamera : MonoBehaviour
{
    public enum FitMode
    {
        FitEntireBounds = 0,
        MaximizeWidth = 1,
        WidthOnlyUnsafe = 2
    }

    [Header("References")]
    public Camera targetCamera;
    public Transform targetRoot;

    [Header("Optional explicit bounds objects")]
    public Renderer[] renderersToFit;
    public Collider[] collidersToFit;

    [Header("Fit Settings")]
    public FitMode fitMode = FitMode.MaximizeWidth;

    [Min(0f)] public float worldMarginHorizontal = 0.02f;
    [Min(0f)] public float worldMarginVertical = 0.02f;

    public bool fitOnStart = true;
    public bool fitContinuously = true;
    public bool recenterCamera = false;

    [Header("Center Offset")]
    public Vector3 worldCenterOffset = Vector3.zero;

    [Header("Debug")]
    public bool debugLogs = false;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Rect lastCameraRect = new Rect(-1f, -1f, -1f, -1f);

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
        if (fitOnStart)
            FitNow();

        CacheState();
    }

    void LateUpdate()
    {
        if (!fitContinuously)
            return;

        if (NeedsRefit())
            FitNow();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (!Application.isPlaying)
            FitNow();
    }
#endif

    public void FitNow()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null || !targetCamera.orthographic)
            return;

        if (!TryGetTargetBounds(out Bounds bounds))
            return;

        Vector3[] corners = GetBoundsCorners(bounds);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = targetCamera.transform.InverseTransformPoint(corners[i]);

            if (local.x < minX) minX = local.x;
            if (local.x > maxX) maxX = local.x;
            if (local.y < minY) minY = local.y;
            if (local.y > maxY) maxY = local.y;
        }

        float contentWidth = (maxX - minX) + (worldMarginHorizontal * 2f);
        float contentHeight = (maxY - minY) + (worldMarginVertical * 2f);

        float halfWidth = contentWidth * 0.5f;
        float halfHeight = contentHeight * 0.5f;

        float effectiveAspect = GetEffectiveCameraAspect(targetCamera);
        if (effectiveAspect <= 0f)
            effectiveAspect = 1f;

        float sizeByWidth = halfWidth / effectiveAspect;
        float sizeByHeight = halfHeight;

        float finalSize = sizeByHeight;

        switch (fitMode)
        {
            case FitMode.FitEntireBounds:
                finalSize = Mathf.Max(sizeByWidth, sizeByHeight);
                break;

            case FitMode.MaximizeWidth:
                {
                    float widthPreferredSize = sizeByWidth;

                    if (widthPreferredSize >= sizeByHeight)
                        finalSize = widthPreferredSize;
                    else
                        finalSize = sizeByHeight;

                    break;
                }

            case FitMode.WidthOnlyUnsafe:
                finalSize = sizeByWidth;
                break;
        }

        targetCamera.orthographicSize = finalSize;

        if (recenterCamera)
        {
            Vector3 targetCenter = bounds.center + worldCenterOffset;

            Vector3 camPos = targetCamera.transform.position;
            Vector3 camForward = targetCamera.transform.forward;

            float distanceAlongForward = Vector3.Dot(camPos - targetCenter, camForward);
            targetCamera.transform.position = targetCenter + camForward * distanceAlongForward;
        }

        CacheState();

        if (debugLogs)
        {
            Debug.Log(
                $"[AutoFitOrthographicCamera] FitNow -> mode={fitMode}, orthoSize={finalSize:F3}, " +
                $"contentWidth={contentWidth:F3}, contentHeight={contentHeight:F3}, aspect={effectiveAspect:F3}"
            );
        }
    }

    private float GetEffectiveCameraAspect(Camera cam)
    {
        if (cam == null)
            return 1f;

        Rect rect = cam.rect;

        float pixelWidth = Mathf.Max(1f, Screen.width * rect.width);
        float pixelHeight = Mathf.Max(1f, Screen.height * rect.height);

        return pixelWidth / pixelHeight;
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bool found = false;
        bounds = new Bounds();

        if (renderersToFit != null && renderersToFit.Length > 0)
        {
            for (int i = 0; i < renderersToFit.Length; i++)
            {
                Renderer r = renderersToFit[i];
                if (r == null) continue;

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        if (collidersToFit != null && collidersToFit.Length > 0)
        {
            for (int i = 0; i < collidersToFit.Length; i++)
            {
                Collider c = collidersToFit[i];
                if (c == null) continue;

                if (!found)
                {
                    bounds = c.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
        }

        if (!found && targetRoot != null)
        {
            Renderer[] childRenderers = targetRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer r = childRenderers[i];
                if (r == null) continue;

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            Collider[] childColliders = targetRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider c = childColliders[i];
                if (c == null) continue;

                if (!found)
                {
                    bounds = c.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
        }

        return found;
    }

    private Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private bool NeedsRefit()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            return true;

        if (!ApproximatelyRect(targetCamera.rect, lastCameraRect))
            return true;

        return false;
    }

    private void CacheState()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraRect = targetCamera != null ? targetCamera.rect : new Rect(0f, 0f, 1f, 1f);
    }

    private bool ApproximatelyRect(Rect a, Rect b)
    {
        const float eps = 0.0001f;

        return Mathf.Abs(a.x - b.x) < eps &&
               Mathf.Abs(a.y - b.y) < eps &&
               Mathf.Abs(a.width - b.width) < eps &&
               Mathf.Abs(a.height - b.height) < eps;
    }
}