using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform target;

    [Header("Apply Edges")]
    [SerializeField] private bool applyLeft = true;
    [SerializeField] private bool applyRight = true;
    [SerializeField] private bool applyTop = true;
    [SerializeField] private bool applyBottom = true;

    [Header("Extra Padding In Pixels")]
    [SerializeField] private Vector2 extraPaddingMin = Vector2.zero;
    [SerializeField] private Vector2 extraPaddingMax = Vector2.zero;

    [Header("Behavior")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyContinuouslyInPlayMode = true;
    [SerializeField] private bool resetOffsetsToZero = true;
    [SerializeField] private bool logDebug = false;

    private Rect lastSafeArea = new Rect(0f, 0f, 0f, 0f);
    private Vector2Int lastScreenSize = Vector2Int.zero;
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        ResolveTarget();

        if (applyOnAwake)
            ApplySafeArea();
    }

    private void OnEnable()
    {
        ResolveTarget();
        ApplySafeArea();
    }

    private void Update()
    {
        ResolveTarget();
        ApplySafeAreaIfChanged();
    }

    public void ApplySafeArea()
    {
        ResolveTarget();

        if (target == null)
            return;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;

        if (safeArea.width <= 0f || safeArea.height <= 0f)
            return;

        Rect adjustedSafeArea = safeArea;

        adjustedSafeArea.xMin += extraPaddingMin.x;
        adjustedSafeArea.yMin += extraPaddingMin.y;
        adjustedSafeArea.xMax -= extraPaddingMax.x;
        adjustedSafeArea.yMax -= extraPaddingMax.y;

        adjustedSafeArea.xMin = Mathf.Clamp(adjustedSafeArea.xMin, 0f, Screen.width);
        adjustedSafeArea.yMin = Mathf.Clamp(adjustedSafeArea.yMin, 0f, Screen.height);
        adjustedSafeArea.xMax = Mathf.Clamp(adjustedSafeArea.xMax, 0f, Screen.width);
        adjustedSafeArea.yMax = Mathf.Clamp(adjustedSafeArea.yMax, 0f, Screen.height);

        Vector2 newAnchorMin = new Vector2(
            adjustedSafeArea.xMin / Screen.width,
            adjustedSafeArea.yMin / Screen.height);

        Vector2 newAnchorMax = new Vector2(
            adjustedSafeArea.xMax / Screen.width,
            adjustedSafeArea.yMax / Screen.height);

        Vector2 currentAnchorMin = target.anchorMin;
        Vector2 currentAnchorMax = target.anchorMax;

        if (!applyLeft)
            newAnchorMin.x = currentAnchorMin.x;

        if (!applyBottom)
            newAnchorMin.y = currentAnchorMin.y;

        if (!applyRight)
            newAnchorMax.x = currentAnchorMax.x;

        if (!applyTop)
            newAnchorMax.y = currentAnchorMax.y;

        target.anchorMin = newAnchorMin;
        target.anchorMax = newAnchorMax;

        if (resetOffsetsToZero)
        {
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        if (logDebug)
        {
            Debug.Log(
                "[SafeAreaFitter] Applied -> " +
                "Target=" + target.name +
                " | SafeArea=" + Screen.safeArea +
                " | AdjustedSafeArea=" + adjustedSafeArea +
                " | AnchorMin=" + target.anchorMin +
                " | AnchorMax=" + target.anchorMax,
                this
            );
        }
    }

    private void ApplySafeAreaIfChanged()
    {
        if (target == null)
            return;

        bool changed =
            lastSafeArea != Screen.safeArea ||
            lastScreenSize.x != Screen.width ||
            lastScreenSize.y != Screen.height ||
            lastOrientation != Screen.orientation;

        if (changed)
            ApplySafeArea();
    }

    private void ResolveTarget()
    {
        if (target == null)
            target = GetComponent<RectTransform>();
    }
}