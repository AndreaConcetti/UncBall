using System.Collections.Generic;
using UnityEngine;

public enum PlayerID
{
    None,
    Player1,
    Player2
}

public class StarPlate : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("1 = board da 1 punto, 2 = board da 2 punti, 3 = board da 5 punti")]
    [Range(1, 3)]
    public int plateNumber = 1;

    [Header("Slot Setup")]
    [Tooltip("Numero totale di slot su questa board")]
    public int totalSlots = 6;

    [Header("Full Board Bonus")]
    [Tooltip("Bonus piatto quando un player completa tutta la board")]
    public int fullBoardFlatBonus = 10;

    [Tooltip("Se attivo, il bonus full board è il maggiore tra bonus piatto e punti già accumulati su questa board")]
    public bool useBestBetweenFlatAndDouble = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private PlayerID[] slotOwners;

    private readonly Dictionary<PlayerID, int> platePoints = new Dictionary<PlayerID, int>
    {
        { PlayerID.Player1, 0 },
        { PlayerID.Player2, 0 }
    };

    private static readonly Dictionary<int, int> BasePoints = new Dictionary<int, int>
    {
        { 1, 1 },
        { 2, 2 },
        { 3, 5 }
    };

    void Awake()
    {
        EnsureInitialized();
    }

    private void OnValidate()
    {
        totalSlots = Mathf.Max(1, totalSlots);
        fullBoardFlatBonus = Mathf.Max(0, fullBoardFlatBonus);
    }

    private void EnsureInitialized()
    {
        int safeSlots = Mathf.Max(1, totalSlots);

        if (slotOwners == null || slotOwners.Length != safeSlots)
        {
            slotOwners = new PlayerID[safeSlots];

            for (int i = 0; i < safeSlots; i++)
                slotOwners[i] = PlayerID.None;

            if (logDebug)
                Debug.Log($"[StarPlate {plateNumber}] EnsureInitialized -> slotOwners created. Slots={safeSlots}", this);
        }

        if (!platePoints.ContainsKey(PlayerID.Player1))
            platePoints[PlayerID.Player1] = 0;

        if (!platePoints.ContainsKey(PlayerID.Player2))
            platePoints[PlayerID.Player2] = 0;
    }

    /// <summary>
    /// Chiamato dallo SlotScorer quando una ball entra in uno slot valido.
    /// </summary>
    public void OnBallEntered(int slotIndex, PlayerID owner, BallPhysics ball, Transform slotTransform)
    {
        EnsureInitialized();

        if (owner == PlayerID.None)
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] Owner None non valido.", this);
            return;
        }

        if (slotIndex < 0 || slotIndex >= totalSlots)
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] slotIndex {slotIndex} non valido.", this);
            return;
        }

        if (slotOwners[slotIndex] != PlayerID.None)
        {
            if (logDebug)
                Debug.Log($"[StarPlate {plateNumber}] Slot {slotIndex} già occupato.", this);
            return;
        }

        slotOwners[slotIndex] = owner;

        int basePoints = GetBasePoints();
        int chainLength = GetContiguousChainLength(slotOwners, slotIndex, owner);
        int shotPoints = basePoints * chainLength;

        platePoints[owner] += shotPoints;

        bool didFullStar = false;
        int fullStarExtra = 0;

        if (IsFullStar(slotOwners, owner))
        {
            didFullStar = true;

            if (useBestBetweenFlatAndDouble)
            {
                int doubleBonus = platePoints[owner];
                fullStarExtra = Mathf.Max(fullBoardFlatBonus, doubleBonus);
            }
            else
            {
                fullStarExtra = fullBoardFlatBonus;
            }

            platePoints[owner] += fullStarExtra;
        }

        int totalThisShot = shotPoints + fullStarExtra;
        Vector3 slotWorldPosition = slotTransform != null ? slotTransform.position : transform.position;

        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager != null)
        {
            scoreManager.AddPoints(
                owner,
                totalThisShot,
                plateNumber,
                chainLength,
                didFullStar,
                slotWorldPosition
            );
        }
        else
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] ScoreManager.Instance nullo.", this);
        }
    }

    public void ResetPlate()
    {
        EnsureInitialized();

        for (int i = 0; i < slotOwners.Length; i++)
            slotOwners[i] = PlayerID.None;

        platePoints[PlayerID.Player1] = 0;
        platePoints[PlayerID.Player2] = 0;

        if (logDebug)
            Debug.Log($"[StarPlate {plateNumber}] ResetPlate completed. Slots={slotOwners.Length}", this);
    }

    public PlayerID GetSlotOwner(int slotIndex)
    {
        EnsureInitialized();

        if (slotIndex < 0 || slotIndex >= slotOwners.Length)
            return PlayerID.None;

        return slotOwners[slotIndex];
    }

    public bool IsPlateFull()
    {
        EnsureInitialized();

        for (int i = 0; i < slotOwners.Length; i++)
        {
            if (slotOwners[i] == PlayerID.None)
                return false;
        }

        return true;
    }

    public int GetOccupiedSlotsCount()
    {
        EnsureInitialized();

        int count = 0;

        for (int i = 0; i < slotOwners.Length; i++)
        {
            if (slotOwners[i] != PlayerID.None)
                count++;
        }

        return count;
    }

    public int GetCurrentPlatePoints(PlayerID player)
    {
        EnsureInitialized();

        if (player == PlayerID.None)
            return 0;

        if (!platePoints.ContainsKey(player))
            return 0;

        return platePoints[player];
    }

    /// <summary>
    /// Calcolo ESATTO del massimo punteggio addizionale ancora ottenibile da questo player
    /// su questa board, assumendo che da ora in poi occupi lui tutti gli slot vuoti rimasti
    /// nell'ordine migliore possibile.
    /// Include chain/combo e full board bonus.
    /// </summary>
    public int GetMaxAdditionalPointsForPlayer(PlayerID player)
    {
        EnsureInitialized();

        if (player == PlayerID.None)
            return 0;

        List<int> emptySlots = new List<int>();

        for (int i = 0; i < slotOwners.Length; i++)
        {
            if (slotOwners[i] == PlayerID.None)
                emptySlots.Add(i);
        }

        if (emptySlots.Count == 0)
            return 0;

        PlayerID[] ownersCopy = (PlayerID[])slotOwners.Clone();
        int currentPlateScore = GetCurrentPlatePoints(player);

        return EvaluateBestAdditionalScoreRecursive(ownersCopy, emptySlots, currentPlateScore, player);
    }

    private int EvaluateBestAdditionalScoreRecursive(
        PlayerID[] ownersState,
        List<int> emptySlots,
        int runningPlatePoints,
        PlayerID player)
    {
        if (emptySlots == null || emptySlots.Count == 0)
            return 0;

        int bestTotalAdditional = 0;
        int basePoints = GetBasePoints();

        for (int i = 0; i < emptySlots.Count; i++)
        {
            int slotIndex = emptySlots[i];

            ownersState[slotIndex] = player;

            int chainLength = GetContiguousChainLength(ownersState, slotIndex, player);
            int shotPoints = basePoints * chainLength;

            int newRunningPlatePoints = runningPlatePoints + shotPoints;
            int additionalThisMove = shotPoints;

            if (IsFullStar(ownersState, player))
            {
                int fullStarExtra = useBestBetweenFlatAndDouble
                    ? Mathf.Max(fullBoardFlatBonus, newRunningPlatePoints)
                    : fullBoardFlatBonus;

                newRunningPlatePoints += fullStarExtra;
                additionalThisMove += fullStarExtra;
            }

            List<int> nextEmptySlots = new List<int>(emptySlots);
            nextEmptySlots.RemoveAt(i);

            int recursiveAdditional = EvaluateBestAdditionalScoreRecursive(
                ownersState,
                nextEmptySlots,
                newRunningPlatePoints,
                player
            );

            int totalAdditional = additionalThisMove + recursiveAdditional;

            if (totalAdditional > bestTotalAdditional)
                bestTotalAdditional = totalAdditional;

            ownersState[slotIndex] = PlayerID.None;
        }

        return bestTotalAdditional;
    }

    private int GetBasePoints()
    {
        return BasePoints.ContainsKey(plateNumber) ? BasePoints[plateNumber] : 1;
    }

    private int GetContiguousChainLength(PlayerID[] ownersState, int slotIndex, PlayerID owner)
    {
        int count = 1;

        int left = slotIndex - 1;
        while (left >= 0 && ownersState[left] == owner)
        {
            count++;
            left--;
        }

        int right = slotIndex + 1;
        while (right < ownersState.Length && ownersState[right] == owner)
        {
            count++;
            right++;
        }

        return count;
    }

    private bool IsFullStar(PlayerID[] ownersState, PlayerID owner)
    {
        for (int i = 0; i < ownersState.Length; i++)
        {
            if (ownersState[i] != owner)
                return false;
        }

        return true;
    }
}
