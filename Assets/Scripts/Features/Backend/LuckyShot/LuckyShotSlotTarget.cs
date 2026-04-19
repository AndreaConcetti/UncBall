using UnityEngine;

public enum LuckyShotBoardTier
{
    Unknown = 0,
    Board1 = 1,
    Board2 = 2,
    Board3 = 3
}

public sealed class LuckyShotSlotTarget : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private LuckyShotBoardTier boardTier = LuckyShotBoardTier.Unknown;
    [SerializeField] private string slotId = "slot_01";
    [SerializeField] private int slotOrderInBoard = 0;

    [Header("Anchors")]
    [SerializeField] private Transform highlightAnchor;
    [SerializeField] private Transform rewardAnchor;

    [Header("Collision")]
    [SerializeField] private Collider hitCollider3D;
    [SerializeField] private Collider2D hitCollider2D;

    [Header("Difficulty / Reward")]
    [SerializeField] private int difficultyScore = 1;
    [SerializeField] private int rewardWeight = 1;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    public LuckyShotBoardTier BoardTier => boardTier;
    public string SlotId => slotId;
    public int SlotOrderInBoard => slotOrderInBoard;
    public Transform HighlightAnchor => highlightAnchor;
    public Transform RewardAnchor => rewardAnchor;
    public Collider HitCollider3D => hitCollider3D;
    public Collider2D HitCollider2D => hitCollider2D;
    public int DifficultyScore => difficultyScore;
    public int RewardWeight => rewardWeight;

    private void Reset()
    {
        if (hitCollider3D == null)
            hitCollider3D = GetComponent<Collider>();

        if (hitCollider2D == null)
            hitCollider2D = GetComponent<Collider2D>();

        if (highlightAnchor == null)
            highlightAnchor = transform;
    }

    private void Awake()
    {
        if (hitCollider3D == null)
            hitCollider3D = GetComponent<Collider>();

        if (hitCollider2D == null)
            hitCollider2D = GetComponent<Collider2D>();

        if (highlightAnchor == null)
            highlightAnchor = transform;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSlotTarget] Awake -> " +
                "BoardTier=" + boardTier +
                " | SlotId=" + slotId +
                " | SlotOrder=" + slotOrderInBoard,
                this);
        }
    }
}
