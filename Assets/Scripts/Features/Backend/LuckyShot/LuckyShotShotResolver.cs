using UnityEngine;
using static LuckyShotSlotRegistry;

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

        if (sessionRuntime == null || slotRegistry == null)
            return;

        if (ball == null)
            return;

        if (hasResolvedCurrentBall)
            return;

        if (activeBall != null && ball != activeBall)
            return;

        hasResolvedCurrentBall = true;

        string fallbackSlotId = "slot_" + (slotIndex + 1).ToString("00");
        LuckyShotRegisteredSlot registeredSlot = slotRegistry.GetSlot(boardNumber, fallbackSlotId);

        if (registeredSlot == null && slotTransform != null)
        {
            for (int i = 0; i < slotRegistry.Slots.Count; i++)
            {
                LuckyShotRegisteredSlot candidate = slotRegistry.Slots[i];
                if (candidate != null && candidate.boardNumber == boardNumber && candidate.slotTransform == slotTransform)
                {
                    registeredSlot = candidate;
                    break;
                }
            }
        }

        string resolvedSlotId = registeredSlot != null ? registeredSlot.slotId : fallbackSlotId;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotShotResolver] NotifySlotEntered -> Board=" + boardNumber +
                " | SlotIndex=" + slotIndex +
                " | SlotId=" + resolvedSlotId,
                this);
        }

        LuckyShotResolvedResult result = await sessionRuntime.ResolveHitAsync(boardNumber, resolvedSlotId);

        if (gameplayController != null)
        {
            gameplayController.NotifyHitResolved(result);
            gameplayController.NotifyHitResolved(ball);
        }
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