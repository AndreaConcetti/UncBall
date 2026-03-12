using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identificatore del player proprietario di una ball o di uno slot.
/// </summary>
public enum PlayerID
{
    None,
    Player1,
    Player2
}

/// <summary>
/// Gestisce una singola board / star plate.
/// Questa classe:
/// - memorizza quale player occupa ogni slot
/// - calcola il punteggio del nuovo inserimento
/// - applica la regola combo stile Uncball:
///   il valore del tiro dipende dalla lunghezza della catena contigua
///   di palline dello stesso player che contiene lo slot appena inserito
/// - applica eventualmente il bonus Full Star
///
/// NOTA IMPORTANTE:
/// la combo qui è spaziale, non temporale.
/// Non conta quanti tiri consecutivi hai fatto in passato.
/// Conta solo quanti slot adiacenti dello stesso player sono collegati
/// alla nuova ball senza interruzioni.
/// </summary>
public class StarPlate : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("1 = board da 1 punto, 2 = board da 2 punti, 3 = board da 5 punti")]
    [Range(1, 3)]
    public int plateNumber = 1;

    [Header("Slot Setup")]
    [Tooltip("Numero totale di slot presenti su questa board")]
    public int totalSlots = 6;

    [Header("Full Board Bonus")]
    [Tooltip("Bonus minimo fisso assegnato quando un player completa tutta la board")]
    public int fullBoardFlatBonus = 10;

    [Tooltip("Se attivo, il bonus finale Full Board sarà il maggiore tra fullBoardFlatBonus e i punti già accumulati su questa board")]
    public bool useBestBetweenFlatAndDouble = true;

    // Proprietario di ogni slot della board
    private PlayerID[] _slotOwners;

    // Somma dei punti accumulati da ciascun player su questa board.
    // Serve per il calcolo del bonus Full Star / Full Board.
    private Dictionary<PlayerID, int> _platePoints = new Dictionary<PlayerID, int>
    {
        { PlayerID.Player1, 0 },
        { PlayerID.Player2, 0 }
    };

    // Valore base della board in funzione del suo numero
    private static readonly Dictionary<int, int> BasePoints = new Dictionary<int, int>
    {
        { 1, 1 },
        { 2, 2 },
        { 3, 5 }
    };

    void Awake()
    {
        _slotOwners = new PlayerID[totalSlots];

        for (int i = 0; i < totalSlots; i++)
            _slotOwners[i] = PlayerID.None;
    }

    /// <summary>
    /// Chiamato da SlotScorer quando una ball entra in uno slot valido.
    /// </summary>
    public void OnBallEntered(int slotIndex, PlayerID owner, BallPhysics ball)
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

        if (_slotOwners[slotIndex] != PlayerID.None)
        {
            Debug.Log($"[StarPlate {plateNumber}] Slot {slotIndex} già occupato.");
            return;
        }

        _slotOwners[slotIndex] = owner;

        int basePoints = BasePoints[plateNumber];

        // Combo spaziale: conta la catena contigua che contiene lo slot appena inserito
        int chainLength = GetContiguousChainLength(slotIndex, owner);

        // Regola Uncball:
        // valore del tiro = valore base board * lunghezza catena contigua
        int shotPoints = basePoints * chainLength;

        // Aggiorna i punti accumulati su questa board per quel player
        _platePoints[owner] += shotPoints;

        int fullStarExtra = 0;
        bool didFullStar = false;

        // Se tutti gli slot della board appartengono allo stesso player,
        // assegna il bonus Full Board
        if (IsFullStar(owner))
        {
            didFullStar = true;

            if (useBestBetweenFlatAndDouble)
            {
                int doubleBonus = _platePoints[owner];
                fullStarExtra = Mathf.Max(fullBoardFlatBonus, doubleBonus);
            }
            else
            {
                fullStarExtra = fullBoardFlatBonus;
            }

            _platePoints[owner] += fullStarExtra;
        }

        int totalThisShot = shotPoints + fullStarExtra;

        ScoreManagerNew scoreManager = ScoreManagerNew.Instance;
        if (scoreManager != null)
        {
            scoreManager.AddPoints(
                owner,
                totalThisShot,
                plateNumber,
                chainLength,
                didFullStar
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

    /// <summary>
    /// Calcola la lunghezza della sequenza contigua di slot dello stesso owner
    /// che include slotIndex.
    /// </summary>
    int GetContiguousChainLength(int slotIndex, PlayerID owner)
    {
        int count = 1;

        int left = slotIndex - 1;
        while (left >= 0 && _slotOwners[left] == owner)
        {
            count++;
            left--;
        }

        int right = slotIndex + 1;
        while (right < totalSlots && _slotOwners[right] == owner)
        {
            count++;
            right++;
        }

        return count;
    }

    /// <summary>
    /// True se tutti gli slot della board appartengono allo stesso owner.
    /// </summary>
    bool IsFullStar(PlayerID owner)
    {
        for (int i = 0; i < totalSlots; i++)
        {
            if (_slotOwners[i] != owner)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Azzera completamente la board.
    /// Da chiamare quando resetti il match o il round.
    /// </summary>
    public void ResetPlate()
    {
        for (int i = 0; i < totalSlots; i++)
            _slotOwners[i] = PlayerID.None;

        _platePoints[PlayerID.Player1] = 0;
        _platePoints[PlayerID.Player2] = 0;
    }

    /// <summary>
    /// Restituisce il proprietario dello slot.
    /// Utile per debug o UI.
    /// </summary>
    public PlayerID GetSlotOwner(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= totalSlots)
            return PlayerID.None;

        return _slotOwners[slotIndex];
    }
}