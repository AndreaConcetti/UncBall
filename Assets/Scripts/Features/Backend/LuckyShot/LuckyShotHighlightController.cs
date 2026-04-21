using UnityEngine;

public sealed class LuckyShotHighlightController : MonoBehaviour
{
    public enum HighlightPlacementMode
    {
        KeepCurrentPosition = 0,
        SnapToSlotAnchorXZOnly = 1,
        SnapToSlotAnchorXYZ = 2
    }

    public enum HighlightRotationMode
    {
        PreserveEditorRotation = 0,
        LookAtTarget = 1,
        MatchTargetRotation = 2
    }

    [Header("Scene References")]
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;

    [Header("Winning Highlights")]
    [SerializeField] private Transform board1HighlightRoot;
    [SerializeField] private Transform board2HighlightRoot;
    [SerializeField] private Transform board3HighlightRoot;

    [Header("Highlight Behaviour")]
    [SerializeField] private HighlightPlacementMode placementMode = HighlightPlacementMode.SnapToSlotAnchorXYZ;
    [SerializeField] private HighlightRotationMode rotationMode = HighlightRotationMode.PreserveEditorRotation;
    [SerializeField] private bool forceExactSlotAnchorPosition = true;
    [SerializeField] private bool hideHighlightIfSlotMissing = true;

    [Header("Placement Offsets")]
    [SerializeField] private Vector3 board1LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board2LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board3LocalOffset = Vector3.zero;

    [Header("LookAt Settings")]
    [SerializeField] private Vector3 board1WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 board2WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 board3WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 lookAtUp = Vector3.up;

    [Header("Optional Side Markers")]
    [SerializeField] private GameObject leftLaunchMarker;
    [SerializeField] private GameObject rightLaunchMarker;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private Quaternion board1InitialRotation;
    private Quaternion board2InitialRotation;
    private Quaternion board3InitialRotation;
    private bool cachedInitialRotations;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialRotations();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheInitialRotations();
        Subscribe();

        if (sessionRuntime != null && sessionRuntime.HasActiveSession)
            ApplySession(sessionRuntime.CurrentSession);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void ApplySession(LuckyShotActiveSession session)
    {
        ResolveReferences();
        CacheInitialRotations();

        if (slotRegistry == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotHighlightController] ApplySession -> LuckyShotSlotRegistry missing.", this);

            return;
        }

        ApplyBoardHighlight(
            boardNumber: 1,
            slotId: session.board1WinningSlotId,
            highlightRoot: board1HighlightRoot,
            cachedInitialRotation: board1InitialRotation,
            localOffset: board1LocalOffset,
            worldAimOffset: board1WorldAimOffset);

        ApplyBoardHighlight(
            boardNumber: 2,
            slotId: session.board2WinningSlotId,
            highlightRoot: board2HighlightRoot,
            cachedInitialRotation: board2InitialRotation,
            localOffset: board2LocalOffset,
            worldAimOffset: board2WorldAimOffset);

        ApplyBoardHighlight(
            boardNumber: 3,
            slotId: session.board3WinningSlotId,
            highlightRoot: board3HighlightRoot,
            cachedInitialRotation: board3InitialRotation,
            localOffset: board3LocalOffset,
            worldAimOffset: board3WorldAimOffset);

        bool launchLeft = session.launchSide == LuckyShotLaunchSide.Left;

        if (leftLaunchMarker != null)
            leftLaunchMarker.SetActive(launchLeft);

        if (rightLaunchMarker != null)
            rightLaunchMarker.SetActive(!launchLeft);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] ApplySession -> " +
                $"LaunchSide={session.launchSide} | " +
                $"B1={session.board1WinningSlotId} | " +
                $"B2={session.board2WinningSlotId} | " +
                $"B3={session.board3WinningSlotId}",
                this);
        }
    }

    public void ClearHighlights()
    {
        SetHighlightVisible(board1HighlightRoot, false);
        SetHighlightVisible(board2HighlightRoot, false);
        SetHighlightVisible(board3HighlightRoot, false);

        if (leftLaunchMarker != null)
            leftLaunchMarker.SetActive(false);

        if (rightLaunchMarker != null)
            rightLaunchMarker.SetActive(false);

        if (verboseLogs)
            Debug.Log("[LuckyShotHighlightController] ClearHighlights -> all highlight roots disabled.", this);
    }

    private void ApplyBoardHighlight(
        int boardNumber,
        string slotId,
        Transform highlightRoot,
        Quaternion cachedInitialRotation,
        Vector3 localOffset,
        Vector3 worldAimOffset)
    {
        if (highlightRoot == null)
        {
            if (verboseLogs)
            {
                Debug.LogWarning(
                    $"[LuckyShotHighlightController] ApplyBoardHighlight -> highlight root missing. Board={boardNumber}",
                    this);
            }

            return;
        }

        LuckyShotSlotRegistry.LuckyShotRegisteredSlot slot = slotRegistry.GetSlot(boardNumber, slotId);
        if (slot == null || slot.highlightAnchor == null)
        {
            if (hideHighlightIfSlotMissing)
                SetHighlightVisible(highlightRoot, false);

            if (verboseLogs)
            {
                Debug.LogWarning(
                    $"[LuckyShotHighlightController] ApplyBoardHighlight -> slot not found. Board={boardNumber} | SlotId={slotId}",
                    this);
            }

            return;
        }

        Transform targetAnchor = slot.highlightAnchor;
        Vector3 targetWorldPosition = targetAnchor.position + targetAnchor.TransformVector(localOffset);

        HighlightPlacementMode effectivePlacementMode = forceExactSlotAnchorPosition
            ? HighlightPlacementMode.SnapToSlotAnchorXYZ
            : placementMode;

        switch (effectivePlacementMode)
        {
            case HighlightPlacementMode.KeepCurrentPosition:
                break;

            case HighlightPlacementMode.SnapToSlotAnchorXZOnly:
                {
                    Vector3 current = highlightRoot.position;
                    highlightRoot.position = new Vector3(
                        targetWorldPosition.x,
                        current.y,
                        targetWorldPosition.z);
                    break;
                }

            case HighlightPlacementMode.SnapToSlotAnchorXYZ:
                highlightRoot.position = targetWorldPosition;
                break;
        }

        switch (rotationMode)
        {
            case HighlightRotationMode.PreserveEditorRotation:
                highlightRoot.rotation = cachedInitialRotation;
                break;

            case HighlightRotationMode.LookAtTarget:
                {
                    Vector3 lookTarget = targetAnchor.position + worldAimOffset;
                    Vector3 lookDirection = lookTarget - highlightRoot.position;

                    if (lookDirection.sqrMagnitude > 0.0001f)
                        highlightRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, lookAtUp);

                    break;
                }

            case HighlightRotationMode.MatchTargetRotation:
                highlightRoot.rotation = targetAnchor.rotation;
                break;
        }

        SetHighlightVisible(highlightRoot, true);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] Highlight applied -> " +
                $"Board={boardNumber} | SlotId={slotId} | Anchor={targetAnchor.name} | " +
                $"WorldPos={highlightRoot.position} | ForceExact={forceExactSlotAnchorPosition}",
                this);
        }
    }

    private void SetHighlightVisible(Transform root, bool visible)
    {
        if (root == null)
            return;

        if (root.gameObject.activeSelf != visible)
            root.gameObject.SetActive(visible);
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        ApplySession(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
        {
            ClearHighlights();
            return;
        }

        LuckyShotActiveSession session = sessionRuntime != null ? sessionRuntime.CurrentSession : default;
        if (session.IsValid())
            ApplySession(session);
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (result.sessionAfterResolve.hasActiveSession)
        {
            ApplySession(result.sessionAfterResolve);
            return;
        }

        ClearHighlights();
    }

    private void ResolveReferences()
    {
        if (slotRegistry == null)
        {
#if UNITY_2023_1_OR_NEWER
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
#else
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();
#endif
        }

        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

        if (sessionRuntime == null)
        {
#if UNITY_2023_1_OR_NEWER
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();
#else
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();
#endif
        }
    }

    private void CacheInitialRotations()
    {
        if (cachedInitialRotations)
            return;

        if (board1HighlightRoot != null)
            board1InitialRotation = board1HighlightRoot.rotation;

        if (board2HighlightRoot != null)
            board2InitialRotation = board2HighlightRoot.rotation;

        if (board3HighlightRoot != null)
            board3InitialRotation = board3HighlightRoot.rotation;

        cachedInitialRotations = true;
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
    }
}