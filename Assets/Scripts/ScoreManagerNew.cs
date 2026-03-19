using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class ShotScoreData
{
    public PlayerID owner;
    public int shotPoints;
    public int newTotalScore;
    public int plateNumber;
    public int comboStreak;
    public bool isFullStar;
    public Vector3 slotWorldPosition;
}

[Serializable]
public class ShotScoreDataEvent : UnityEvent<ShotScoreData> { }

public class ScoreManagerNew : MonoBehaviour
{
    public static ScoreManagerNew Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Header("Star Plates")]
    public StarPlate[] starPlates = new StarPlate[3];

    [Header("Events")]
    public UnityEvent<PlayerID, int> onPointsScored;
    public ShotScoreDataEvent onShotScoreDetailed;
    public UnityEvent onHalftime;
    public UnityEvent<PlayerID> onMatchEnd;
    public UnityEvent onOvertimeStart;

    public int ScoreP1 { get; private set; }
    public int ScoreP2 { get; private set; }

    public bool IsHalftime { get; private set; }
    public bool IsOvertime { get; private set; }
    public bool MatchActive { get; private set; }

    public void StartMatch()
    {
        ScoreP1 = 0;
        ScoreP2 = 0;

        IsHalftime = false;
        IsOvertime = false;
        MatchActive = true;

        foreach (StarPlate plate in starPlates)
            plate?.ResetPlate();

        Debug.Log("[ScoreManager] Match started.");
    }

    public void BeginHalftime()
    {
        if (!MatchActive || IsHalftime)
            return;

        IsHalftime = true;
        Debug.Log("[ScoreManager] Halftime started.");
        onHalftime?.Invoke();
    }

    public void EndHalftime()
    {
        if (!MatchActive || !IsHalftime)
            return;

        IsHalftime = false;

        foreach (StarPlate plate in starPlates)
            plate?.ResetPlate();

        Debug.Log("[ScoreManager] Halftime ended.");
    }

    public void BeginOvertime()
    {
        if (!MatchActive || IsOvertime)
            return;

        IsOvertime = true;
        Debug.Log("[ScoreManager] Overtime started.");
        onOvertimeStart?.Invoke();
    }

    public void EndMatch(PlayerID winner)
    {
        if (!MatchActive)
            return;

        MatchActive = false;

        Debug.Log($"[ScoreManager] Match over. Winner: {winner} | P1: {ScoreP1} | P2: {ScoreP2}");
        onMatchEnd?.Invoke(winner);
    }

    public void AddPoints(PlayerID owner, int points, int plateNumber, int comboStreak, bool isFullStar, Vector3 slotWorldPosition)
    {
        if (!MatchActive)
            return;

        if (owner == PlayerID.Player1)
            ScoreP1 += points;
        else if (owner == PlayerID.Player2)
            ScoreP2 += points;
        else
            return;

        int newTotal = owner == PlayerID.Player1 ? ScoreP1 : ScoreP2;

        Debug.Log($"[ScoreManager] {owner} +{points} pts (plate {plateNumber}, streak x{comboStreak}{(isFullStar ? ", FULL STAR" : "")}) -> total {newTotal} | P1:{ScoreP1} | P2:{ScoreP2}");

        onPointsScored?.Invoke(owner, newTotal);

        ShotScoreData data = new ShotScoreData
        {
            owner = owner,
            shotPoints = points,
            newTotalScore = newTotal,
            plateNumber = plateNumber,
            comboStreak = comboStreak,
            isFullStar = isFullStar,
            slotWorldPosition = slotWorldPosition
        };

        onShotScoreDetailed?.Invoke(data);

        if (IsOvertime && points > 0)
            EndMatch(owner);
    }
}