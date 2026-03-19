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

    private PlayerID[] slotOwners;

    private Dictionary<PlayerID, int> platePoints = new Dictionary<PlayerID, int>
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
        slotOwners = new PlayerID[totalSlots];

        for (int i = 0; i < totalSlots; i++)
            slotOwners[i] = PlayerID.None;
    }

    /// <summary>
    /// Chiamato dallo SlotScorer quando una ball entra in uno slot valido.
    /// </summary>
    public void OnBallEntered(int slotIndex, PlayerID owner, BallPhysics ball, Transform slotTransform)
    {
        if (owner == PlayerID.None)
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] Owner None non valido.");
            return;
        }

        if (slotIndex < 0 || slotIndex >= totalSlots)
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] slotIndex {slotIndex} non valido.");
            return;
        }

        if (slotOwners[slotIndex] != PlayerID.None)
        {
            Debug.Log($"[StarPlate {plateNumber}] Slot {slotIndex} già occupato.");
            return;
        }

        slotOwners[slotIndex] = owner;

        int basePoints = BasePoints.ContainsKey(plateNumber) ? BasePoints[plateNumber] : 1;
        int chainLength = GetContiguousChainLength(slotIndex, owner);
        int shotPoints = basePoints * chainLength;

        platePoints[owner] += shotPoints;

        bool didFullStar = false;
        int fullStarExtra = 0;

        if (IsFullStar(owner))
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

        ScoreManagerNew scoreManager = ScoreManagerNew.Instance;
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
            Debug.LogWarning($"[StarPlate {plateNumber}] ScoreManagerNew.Instance nullo.");
        }

        Debug.Log(
            $"[StarPlate {plateNumber}] {owner} scored slot {slotIndex} | " +
            $"base={basePoints} chainLength={chainLength} shotPoints={shotPoints} " +
            $"fullStarExtra={fullStarExtra} shotTotal={totalThisShot}"
        );
    }

    int GetContiguousChainLength(int slotIndex, PlayerID owner)
    {
        int count = 1;

        int left = slotIndex - 1;
        while (left >= 0 && slotOwners[left] == owner)
        {
            count++;
            left--;
        }

        int right = slotIndex + 1;
        while (right < totalSlots && slotOwners[right] == owner)
        {
            count++;
            right++;
        }

        return count;
    }

    bool IsFullStar(PlayerID owner)
    {
        for (int i = 0; i < totalSlots; i++)
        {
            if (slotOwners[i] != owner)
                return false;
        }

        return true;
    }

    public void ResetPlate()
    {
        for (int i = 0; i < totalSlots; i++)
            slotOwners[i] = PlayerID.None;

        platePoints[PlayerID.Player1] = 0;
        platePoints[PlayerID.Player2] = 0;
    }

    public PlayerID GetSlotOwner(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSlots)
            return PlayerID.None;

        return slotOwners[slotIndex];
    }
}