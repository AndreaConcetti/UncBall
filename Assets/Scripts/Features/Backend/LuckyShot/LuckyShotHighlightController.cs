using UnityEngine;

public sealed class LuckyShotHighlightController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;

    [Header("Winning Highlights")]
    [SerializeField] private Transform board1HighlightRoot;
    [SerializeField] private Transform board2HighlightRoot;
    [SerializeField] private Transform board3HighlightRoot;

    [Header("Optional side markers")]
    [SerializeField] private GameObject leftLaunchMarker;
    [SerializeField] private GameObject rightLaunchMarker;

    [Header("Placement")]
    [SerializeField] private Vector3 board1LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board2LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board3LocalOffset = Vector3.zero;
    [SerializeField] private bool matchRotation = false;
    [SerializeField] private bool hideHighlightIfSlotMissing = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
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

        if (slotRegistry == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotHighlightController] ApplySession -> LuckyShotSlotRegistry missing.", this);

            return;
        }

        ApplyBoardHighlight(1, session.board1WinningSlotId, board1HighlightRoot, board1LocalOffset);
        ApplyBoardHighlight(2, session.board2WinningSlotId, board2HighlightRoot, board2LocalOffset);
        ApplyBoardHighlight(3, session.board3WinningSlotId, board3HighlightRoot, board3LocalOffset);

        bool isLeft = session.launchSide == LuckyShotLaunchSide.Left;

        if (leftLaunchMarker != null)
            leftLaunchMarker.SetActive(isLeft);

        if (rightLaunchMarker != null)
            rightLaunchMarker.SetActive(!isLeft);
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
    }

    private void ApplyBoardHighlight(int boardNumber, string slotId, Transform highlightRoot, Vector3 localOffset)
    {
        if (highlightRoot == null)
            return;

        LuckyShotSlotRegistry.LuckyShotRegisteredSlot slot = slotRegistry.GetSlot(boardNumber, slotId);
        if (slot == null || slot.highlightAnchor == null)
        {
            if (hideHighlightIfSlotMissing)
                SetHighlightVisible(highlightRoot, false);

            if (verboseLogs)
            {
                Debug.LogWarning(
                    "[LuckyShotHighlightController] ApplyBoardHighlight -> slot not found. " +
                    "Board=" + boardNumber +
                    " | SlotId=" + slotId,
                    this);
            }

            return;
        }

        highlightRoot.position = slot.highlightAnchor.position + slot.highlightAnchor.TransformVector(localOffset);

        if (matchRotation)
            highlightRoot.rotation = slot.highlightAnchor.rotation;

        SetHighlightVisible(highlightRoot, true);
    }

    private void SetHighlightVisible(Transform root, bool value)
    {
        if (root != null && root.gameObject.activeSelf != value)
            root.gameObject.SetActive(value);
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        ApplySession(session);
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (result.sessionAfterResolve.hasActiveSession)
            ApplySession(result.sessionAfterResolve);
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

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();
#endif
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
    }
}
