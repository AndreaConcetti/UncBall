using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class AutoFitOrthographicCamera : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public Transform targetRoot;

    [Header("Optional explicit bounds objects")]
    public Renderer[] renderersToFit;
    public Collider[] collidersToFit;

    [Header("Fit Settings")]
    [Min(0f)] public float worldMargin = 0.15f;
    public bool fitOnStart = true;
    public bool fitContinuously = true;
    public bool recenterCamera = false;

    [Header("Center Offset")]
    public Vector3 worldCenterOffset = Vector3.zero;

    private int lastScreenWidth;
    private int lastScreenHeight;

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

        CacheScreenSize();
    }

    void LateUpdate()
    {
        if (!fitContinuously)
            return;

        if (ScreenSizeChanged())
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

        Bounds bounds;
        if (!TryGetTargetBounds(out bounds))
            return;

        bounds.Expand(worldMargin * 2f);

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

        float halfWidth = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
        float halfHeight = Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY));

        float requiredSizeByHeight = halfHeight;
        float requiredSizeByWidth = halfWidth / targetCamera.aspect;

        float requiredSize = Mathf.Max(requiredSizeByHeight, requiredSizeByWidth);
        targetCamera.orthographicSize = requiredSize;

        if (recenterCamera)
        {
            Vector3 targetCenter = bounds.center + worldCenterOffset;

            Vector3 camPos = targetCamera.transform.position;
            Vector3 camForward = targetCamera.transform.forward;

            float distanceAlongForward = Vector3.Dot(camPos - targetCenter, camForward);
            targetCamera.transform.position = targetCenter + camForward * distanceAlongForward;
        }

        CacheScreenSize();
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

    private bool ScreenSizeChanged()
    {
        return Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
    }

    private void CacheScreenSize()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
}