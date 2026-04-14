using UnityEngine;

public sealed class PlayerShotDebugRecorder : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [Header("Runtime")]
    [SerializeField] private int currentMatchShotIndex = 0;
    [SerializeField] private bool hasPendingLocalShot = false;
    [SerializeField] private int pendingShotIndex = -1;
    [SerializeField] private PlayerID pendingOwner = PlayerID.None;
    [SerializeField] private Vector3 pendingStartPosition;
    [SerializeField] private Vector2 pendingSwipe;
    [SerializeField] private Vector3 pendingDirection;
    [SerializeField] private float pendingForce;
    [SerializeField] private bool pendingScored = false;
    [SerializeField] private int pendingScoredPlateIndex = -1;
    [SerializeField] private int pendingScoredSlotIndex = -1;

    [Header("Last Resolved Shot")]
    [SerializeField] private int lastResolvedShotIndex = -1;
    [SerializeField] private bool lastResolvedScored = false;
    [SerializeField] private int lastResolvedPlateIndex = -1;
    [SerializeField] private int lastResolvedSlotIndex = -1;

    public int CurrentMatchShotIndex => currentMatchShotIndex;
    public int PendingShotIndex => pendingShotIndex;
    public bool HasPendingLocalShot => hasPendingLocalShot;

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public int RegisterLocalLaunch(PlayerID owner, Vector3 startPosition, Vector2 swipe, Vector3 launchDirection, float force)
    {
        currentMatchShotIndex++;

        hasPendingLocalShot = true;
        pendingShotIndex = currentMatchShotIndex;
        pendingOwner = owner;
        pendingStartPosition = startPosition;
        pendingSwipe = swipe;
        pendingDirection = launchDirection;
        pendingForce = force;
        pendingScored = false;
        pendingScoredPlateIndex = -1;
        pendingScoredSlotIndex = -1;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerShotDebugRecorder] RegisterLocalLaunch -> " +
                "ShotIndex=" + pendingShotIndex +
                " | Owner=" + pendingOwner +
                " | StartPos=" + pendingStartPosition +
                " | Swipe=" + pendingSwipe +
                " | Direction=" + pendingDirection +
                " | Force=" + pendingForce,
                this);
        }

        return pendingShotIndex;
    }

    public void RegisterPendingShotScored(int plateIndex, int slotIndex)
    {
        if (!hasPendingLocalShot)
            return;

        pendingScored = true;
        pendingScoredPlateIndex = plateIndex;
        pendingScoredSlotIndex = slotIndex;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerShotDebugRecorder] PendingShotScored -> " +
                "ShotIndex=" + pendingShotIndex +
                " | PlateIndex=" + pendingScoredPlateIndex +
                " | SlotIndex=" + pendingScoredSlotIndex,
                this);
        }
    }

    public void FinalizePendingShot(string reason)
    {
        if (!hasPendingLocalShot)
            return;

        lastResolvedShotIndex = pendingShotIndex;
        lastResolvedScored = pendingScored;
        lastResolvedPlateIndex = pendingScoredPlateIndex;
        lastResolvedSlotIndex = pendingScoredSlotIndex;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerShotDebugRecorder] FinalizePendingShot -> " +
                "ShotIndex=" + pendingShotIndex +
                " | Reason=" + reason +
                " | Owner=" + pendingOwner +
                " | StartPos=" + pendingStartPosition +
                " | Swipe=" + pendingSwipe +
                " | Direction=" + pendingDirection +
                " | Force=" + pendingForce +
                " | Scored=" + pendingScored +
                " | PlateIndex=" + pendingScoredPlateIndex +
                " | SlotIndex=" + pendingScoredSlotIndex,
                this);
        }

        hasPendingLocalShot = false;
        pendingShotIndex = -1;
        pendingOwner = PlayerID.None;
        pendingStartPosition = Vector3.zero;
        pendingSwipe = Vector2.zero;
        pendingDirection = Vector3.zero;
        pendingForce = 0f;
        pendingScored = false;
        pendingScoredPlateIndex = -1;
        pendingScoredSlotIndex = -1;
    }

    private void OnScoreManagerPointsScored(PlayerID scoringPlayer, int newTotal)
    {
        if (!hasPendingLocalShot)
            return;

        if (scoringPlayer != pendingOwner)
            return;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerShotDebugRecorder] OnScoreManagerPointsScored -> " +
                "PendingShotIndex=" + pendingShotIndex +
                " | ScoringPlayer=" + scoringPlayer +
                " | NewTotal=" + newTotal,
                this);
        }
    }

    private void ResolveReferences()
    {
#if UNITY_2023_1_OR_NEWER
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
#else
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();
#endif
    }

    private void Subscribe()
    {
        if (scoreManager == null)
            return;

        scoreManager.onPointsScored.RemoveListener(OnScoreManagerPointsScored);
        scoreManager.onPointsScored.AddListener(OnScoreManagerPointsScored);
    }

    private void Unsubscribe()
    {
        if (scoreManager == null)
            return;

        scoreManager.onPointsScored.RemoveListener(OnScoreManagerPointsScored);
    }
}
