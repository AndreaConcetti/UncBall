using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestisce solo punteggi ed eventi legati allo score.
/// Non possiede più il clock del match.
/// Lo stato globale del match è orchestrato da StartEndController.
/// </summary>
public class ScoreManagerNew : MonoBehaviour
{
    public static ScoreManagerNew Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Header("Star Plates (assign all 3)")]
    public StarPlate[] starPlates = new StarPlate[3];

    [Header("Events")]
    public UnityEvent<PlayerID, int> onPointsScored;
    public UnityEvent onHalftime;
    public UnityEvent<PlayerID> onMatchEnd;
    public UnityEvent onOvertimeStart;

    public int ScoreP1 { get; private set; }
    public int ScoreP2 { get; private set; }

    public bool IsHalftime { get; private set; }
    public bool IsOvertime { get; private set; }
    public bool MatchActive { get; private set; }

    /// <summary>
    /// Resetta i punteggi e lo stato score-related del match.
    /// Il clock non viene gestito qui.
    /// </summary>
    public void StartMatch()
    {
        ScoreP1 = 0;
        ScoreP2 = 0;

        IsHalftime = false;
        IsOvertime = false;
        MatchActive = true;

        foreach (var plate in starPlates)
            plate?.ResetPlate();

        Debug.Log("[ScoreManager] Match started.");
    }

    /// <summary>
    /// Entra in halftime. La gestione della pausa e della UI è esterna.
    /// </summary>
    public void BeginHalftime()
    {
        if (!MatchActive || IsHalftime)
            return;

        IsHalftime = true;
        Debug.Log("[ScoreManager] Halftime started.");
        onHalftime?.Invoke();
    }

    /// <summary>
    /// Chiude l'halftime e resetta le board per il secondo tempo.
    /// </summary>
    public void EndHalftime()
    {
        if (!MatchActive || !IsHalftime)
            return;

        IsHalftime = false;

        foreach (var plate in starPlates)
            plate?.ResetPlate();

        Debug.Log("[ScoreManager] Halftime ended.");
    }

    /// <summary>
    /// Entra in overtime. Da qui in poi il primo punto può chiudere il match.
    /// </summary>
    public void BeginOvertime()
    {
        if (!MatchActive || IsOvertime)
            return;

        IsOvertime = true;
        Debug.Log("[ScoreManager] Overtime started.");
        onOvertimeStart?.Invoke();
    }

    /// <summary>
    /// Chiude ufficialmente il match e notifica il vincitore.
    /// </summary>
    public void EndMatch(PlayerID winner)
    {
        if (!MatchActive)
            return;

        MatchActive = false;

        Debug.Log($"[ScoreManager] Match over. Winner: {winner} | P1: {ScoreP1}  P2: {ScoreP2}");
        onMatchEnd?.Invoke(winner);
    }

    /// <summary>
    /// Chiamato da StarPlate dopo il calcolo del tiro.
    /// </summary>
    public void AddPoints(PlayerID owner, int points, int plateNumber, int comboStreak, bool isFullStar)
    {
        if (!MatchActive)
            return;

        if (owner == PlayerID.Player1)
            ScoreP1 += points;
        else if (owner == PlayerID.Player2)
            ScoreP2 += points;

        int newTotal = owner == PlayerID.Player1 ? ScoreP1 : ScoreP2;

        Debug.Log($"[ScoreManager] {owner} +{points} pts (plate {plateNumber}, " +
                  $"streak x{comboStreak}{(isFullStar ? ", FULL STAR" : "")}) " +
                  $"→ total {newTotal}  |  P1:{ScoreP1}  P2:{ScoreP2}");

        onPointsScored?.Invoke(owner, newTotal);

        // Sudden death in overtime: il primo punto chiude il match.
        if (IsOvertime && points > 0)
            EndMatch(owner);
    }
}