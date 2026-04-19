using UnityEngine;

public sealed class LuckyShotShotResolver : MonoBehaviour
{
    public static LuckyShotShotResolver Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;
    [SerializeField] private LuckyShotGameplayController gameplayController;
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;

    [Header("Runtime State")]
    [SerializeField] private BallPhysics activeBall;
    [SerializeField] private bool hasResolvedCurrentBall;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PrepareForNewBall(BallPhysics ball)
    {
        activeBall = ball;
        hasResolvedCurrentBall = false;

        if (verboseLogs)
            Debug.Log("[LuckyShotShotResolver] PrepareForNewBall -> " + (ball != null ? ball.name : "<null>"), this);
    }

    public async void NotifySlotEntered(int boardNumber, int slotIndex, BallPhysics ball, Transform slotTransform)
    {
        ResolveReferences();

        if (sessionRuntime == null || slotRegistry == null || ball == null)
            return;

        if (hasResolvedCurrentBall)
            return;

        if (activeBall != null && ball != activeBall)
            return;

        hasResolvedCurrentBall = true;

        string slotId = LuckyShotSlotRegistry.BuildSlotId(boardNumber, slotIndex);
        LuckyShotSlotRegistry.LuckyShotRegisteredSlot registeredSlot = slotRegistry.GetSlot(boardNumber, slotId);
        string resolvedSlotId = registeredSlot != null ? registeredSlot.slotId : slotId;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotShotResolver] NotifySlotEntered -> " +
                "Board=" + boardNumber +
                " | SlotIndex=" + slotIndex +
                " | SlotId=" + resolvedSlotId,
                this);
        }

        await sessionRuntime.ResolveHitAsync(boardNumber, resolvedSlotId);

        if (gameplayController != null)
            gameplayController.NotifyHitResolved(ball);

        if (ball != null)
            Destroy(ball.gameObject);
    }

    private void ResolveReferences()
    {
        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();

        if (gameplayController == null)
            gameplayController = FindFirstObjectByType<LuckyShotGameplayController>();

        if (slotRegistry == null)
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (gameplayController == null)
            gameplayController = FindObjectOfType<LuckyShotGameplayController>();

        if (slotRegistry == null)
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();
#endif
    }
}
