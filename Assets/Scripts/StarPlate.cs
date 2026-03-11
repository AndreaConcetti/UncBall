using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which player owns a ball or scored a point.
/// Extend this enum if you add more players.
/// </summary>
public enum PlayerID { None, Player1, Player2 }

/// <summary>
/// One of the three Star Plates on the table.
/// Tracks slot occupancy, manages combo streaks per player, and computes Full Star bonuses.
/// Reports final scored points to ScoreManager.
/// </summary>
public class StarPlate : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("1 = 7-pt zone, 2 = 5-pt zone, 3 = 3-pt zone")]
    [Range(1, 3)]
    public int plateNumber = 1;

    [Header("Slot Setup")]
    [Tooltip("Total number of slots on this plate")]
    public int totalSlots = 6;

    // ── Scoring Tables (from rulebook) ─────────────────────────

    // Base points per ball per plate
    private static readonly Dictionary<int, int> BasePoints = new Dictionary<int, int>
    {
        { 1, 1 },   // Star #1: 7-pt zone → 1 pt per ball
        { 2, 2 },   // Star #2: 5-pt zone → 2 pts per ball
        { 3, 5 },   // Star #3: 3-pt zone → 5 pts per ball
    };

    // Combo increment multiplier per plate (applied as +multiplier * n for nth consecutive ball)
    private static readonly Dictionary<int, int> ComboMultiplier = new Dictionary<int, int>
    {
        { 1, 1 },   // Star #1: +n pts for nth consecutive ball
        { 2, 2 },   // Star #2: +2n pts
        { 3, 5 },   // Star #3: +5n pts
    };

    // Full Star bonus points per plate
    private static readonly Dictionary<int, int> FullStarBonus = new Dictionary<int, int>
    {
        { 1, 10 },
        { 2, 20 },
        { 3, 30 },
    };

    // ── Runtime State ──────────────────────────────────────────

    // slotOwners[slotIndex] = which player's ball is in that slot (None = empty)
    private PlayerID[] _slotOwners;

    // How many consecutive balls each player has landed on THIS plate without interruption
    private Dictionary<PlayerID, int> _consecutiveCount = new Dictionary<PlayerID, int>
    {
        { PlayerID.Player1, 0 },
        { PlayerID.Player2, 0 },
    };

    // Points each player has accumulated in this plate so far (used for Full Star doubling)
    private Dictionary<PlayerID, int> _platePoints = new Dictionary<PlayerID, int>
    {
        { PlayerID.Player1, 0 },
        { PlayerID.Player2, 0 },
    };

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        _slotOwners = new PlayerID[totalSlots];
        for (int i = 0; i < totalSlots; i++)
            _slotOwners[i] = PlayerID.None;
    }

    // ── Called by SlotScorer ───────────────────────────────────

    /// <summary>
    /// Called when a ball enters a slot on this plate.
    /// Computes the score for this ball including combo, reports to ScoreManager.
    /// </summary>
    public void OnBallEntered(int slotIndex, PlayerID owner, BallPhysics ball)
    {
        if (slotIndex < 0 || slotIndex >= totalSlots)
        {
            Debug.LogWarning($"[StarPlate {plateNumber}] Invalid slotIndex {slotIndex}");
            return;
        }

        if (_slotOwners[slotIndex] != PlayerID.None)
        {
            Debug.Log($"[StarPlate {plateNumber}] Slot {slotIndex} already occupied — ignoring.");
            return;
        }

        // Mark slot as occupied
        _slotOwners[slotIndex] = owner;

        // ── Step 1: Base points ────────────────────────────────
        int basePoints = BasePoints[plateNumber];

        // ── Step 2: Combo ──────────────────────────────────────
        // Increment this player's consecutive count on this plate
        _consecutiveCount[owner]++;
        int n = _consecutiveCount[owner];

        // Reset opponent's streak — any ball on this plate breaks theirs
        PlayerID opponent = owner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        _consecutiveCount[opponent] = 0;

        // Combo bonus only applies from the 2nd consecutive ball onward
        int comboBonus = n > 1 ? ComboMultiplier[plateNumber] * n : 0;

        int totalThisShot = basePoints + comboBonus;

        // Accumulate plate points for this player (used by Full Star calc)
        _platePoints[owner] += totalThisShot;

        Debug.Log($"[StarPlate {plateNumber}] {owner} scored slot {slotIndex} | " +
                  $"base={basePoints}  combo streak={n}  comboBonus={comboBonus}  shot total={totalThisShot}");

        // ── Step 3: Full Star check ────────────────────────────
        int fullStarExtra = 0;
        if (IsFullStar(owner))
        {
            // Rulebook: +X points OR double total in zone — take whichever is higher
            int flatBonus = FullStarBonus[plateNumber];
            int doubleBonus = _platePoints[owner]; // doubling means adding the same amount again
            fullStarExtra = Mathf.Max(flatBonus, doubleBonus);

            Debug.Log($"[StarPlate {plateNumber}] FULL STAR for {owner}! " +
                      $"flat={flatBonus}  double={doubleBonus}  awarded={fullStarExtra}");
        }

        // ── Step 4: Report to ScoreManager ────────────────────
        int finalPoints = totalThisShot + fullStarExtra;
        ScoreManagerNew.Instance?.AddPoints(owner, finalPoints, plateNumber, n, fullStarExtra > 0);
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>Returns true if every slot on this plate is owned by the given player.</summary>
    private bool IsFullStar(PlayerID owner)
    {
        foreach (PlayerID slot in _slotOwners)
            if (slot != owner) return false;
        return true;
    }

    /// <summary>Clear all slot state (e.g. halftime reset).</summary>
    public void ResetPlate()
    {
        for (int i = 0; i < totalSlots; i++)
            _slotOwners[i] = PlayerID.None;

        _consecutiveCount[PlayerID.Player1] = 0;
        _consecutiveCount[PlayerID.Player2] = 0;
        _platePoints[PlayerID.Player1] = 0;
        _platePoints[PlayerID.Player2] = 0;

        Debug.Log($"[StarPlate {plateNumber}] Reset.");
    }

    /// <summary>How many slots are occupied by the given player on this plate.</summary>
    public int GetSlotCount(PlayerID owner)
    {
        int count = 0;
        foreach (PlayerID slot in _slotOwners)
            if (slot == owner) count++;
        return count;
    }
}