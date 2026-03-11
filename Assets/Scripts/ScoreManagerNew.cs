using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Singleton supervisor for the entire match.
/// Owns the clock, both player scores, halftime, overtime, and sudden death.
/// StarPlate reports to this via AddPoints().
/// </summary>
public class ScoreManagerNew : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────

    public static ScoreManagerNew Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Configuration ──────────────────────────────────────────

    [Header("Match Settings")]
    public float halfDuration = 360f;  // 6 minutes
    public float overtimeDuration = 180f;  // 3 minutes

    [Header("Star Plates (assign all 3)")]
    public StarPlate[] starPlates = new StarPlate[3];

    // ── Events (hook up UI here) ───────────────────────────────

    [Header("Events")]
    public UnityEvent<PlayerID, int> onPointsScored;   // (who, new total)
    public UnityEvent onHalftime;
    public UnityEvent<PlayerID> onMatchEnd;        // winner (None = should not happen)
    public UnityEvent onOvertimeStart;

    // ── Runtime State ──────────────────────────────────────────

    public int ScoreP1 { get; private set; }
    public int ScoreP2 { get; private set; }

    public float TimeRemaining { get; private set; }
    public bool IsHalftime { get; private set; }
    public bool IsOvertime { get; private set; }
    public bool MatchActive { get; private set; }

    // ── Match Control ──────────────────────────────────────────

    public void StartMatch()
    {
        ScoreP1 = 0;
        ScoreP2 = 0;
        IsHalftime = false;
        IsOvertime = false;
        MatchActive = true;
        TimeRemaining = halfDuration;

        foreach (var plate in starPlates)
            plate?.ResetPlate();

        StartCoroutine(MatchClock());
        Debug.Log("[ScoreManager] Match started.");
    }

    private IEnumerator MatchClock()
    {
        // ── First half ────────────────────────────────────────
        while (TimeRemaining > 0f && MatchActive)
        {
            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // ── Halftime ──────────────────────────────────────────
        IsHalftime = true;
        TimeRemaining = halfDuration;
        Debug.Log("[ScoreManager] Halftime.");
        onHalftime?.Invoke();

        // Brief pause — actual halftime flow controlled externally
        yield return new WaitUntil(() => !IsHalftime);

        // ── Second half ───────────────────────────────────────
        while (TimeRemaining > 0f && MatchActive)
        {
            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // ── End of regulation ─────────────────────────────────
        if (ScoreP1 != ScoreP2)
        {
            EndMatch(ScoreP1 > ScoreP2 ? PlayerID.Player1 : PlayerID.Player2);
            yield break;
        }

        // ── Overtime / Sudden Death ────────────────────────────
        IsOvertime = true;
        TimeRemaining = overtimeDuration;
        Debug.Log("[ScoreManager] Tied — overtime (sudden death) started.");
        onOvertimeStart?.Invoke();

        while (TimeRemaining > 0f && MatchActive)
        {
            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // Overtime expired still tied — extremely unlikely given sudden death,
        // but handle gracefully
        Debug.Log("[ScoreManager] Overtime expired — match still tied.");
        EndMatch(PlayerID.None);
    }

    /// <summary>Call this to dismiss halftime and start the second half.</summary>
    public void EndHalftime()
    {
        IsHalftime = false;
        foreach (var plate in starPlates)
            plate?.ResetPlate();
    }

    private void EndMatch(PlayerID winner)
    {
        MatchActive = false;
        Debug.Log($"[ScoreManager] Match over. Winner: {winner} | P1: {ScoreP1}  P2: {ScoreP2}");
        onMatchEnd?.Invoke(winner);
    }

    // ── Called by StarPlate ────────────────────────────────────

    /// <summary>
    /// Record points for a player. Called by StarPlate after computing base + combo + full star.
    /// </summary>
    /// <param name="owner">Which player scored.</param>
    /// <param name="points">Total points for this shot (already includes combo and bonuses).</param>
    /// <param name="plateNumber">Which Star Plate (1/2/3) — for logging/UI.</param>
    /// <param name="comboStreak">Current consecutive streak on that plate — for logging/UI.</param>
    /// <param name="isFullStar">Whether this shot completed a Full Star.</param>
    public void AddPoints(PlayerID owner, int points, int plateNumber,
                          int comboStreak, bool isFullStar)
    {
        if (!MatchActive) return;

        if (owner == PlayerID.Player1) ScoreP1 += points;
        else if (owner == PlayerID.Player2) ScoreP2 += points;

        int newTotal = owner == PlayerID.Player1 ? ScoreP1 : ScoreP2;

        Debug.Log($"[ScoreManager] {owner} +{points} pts (plate {plateNumber}, " +
                  $"streak x{comboStreak}{(isFullStar ? ", FULL STAR" : "")}) " +
                  $"→ total {newTotal}  |  P1:{ScoreP1}  P2:{ScoreP2}");

        onPointsScored?.Invoke(owner, newTotal);

        // Sudden death: first score in overtime wins immediately
        if (IsOvertime && points > 0)
            EndMatch(owner);
    }
}