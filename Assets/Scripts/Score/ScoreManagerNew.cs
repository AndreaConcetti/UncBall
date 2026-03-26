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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void StartMatch()
    {
        ScoreP1 = 0;
        ScoreP2 = 0;

        IsHalftime = false;
        IsOvertime = false;
        MatchActive = true;

        foreach (StarPlate plate in starPlates)
            plate?.ResetPlate();
    }

    public void BeginHalftime()
    {
        if (!MatchActive || IsHalftime)
            return;

        IsHalftime = true;
        onHalftime?.Invoke();
    }

    public void EndHalftime()
    {
        if (!MatchActive || !IsHalftime)
            return;

        IsHalftime = false;

        foreach (StarPlate plate in starPlates)
            plate?.ResetPlate();
    }

    public void BeginOvertime()
    {
        if (!MatchActive || IsOvertime)
            return;

        IsOvertime = true;
        onOvertimeStart?.Invoke();
    }

    public void EndMatch(PlayerID winner)
    {
        if (!MatchActive)
            return;

        MatchActive = false;
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

    public bool AreAllBoardsFull()
    {
        if (starPlates == null || starPlates.Length == 0)
            return false;

        for (int i = 0; i < starPlates.Length; i++)
        {
            if (starPlates[i] == null)
                return false;

            if (!starPlates[i].IsPlateFull())
                return false;
        }

        return true;
    }

    public int GetMaxAdditionalPointsAvailable(PlayerID player)
    {
        if (player == PlayerID.None)
            return 0;

        if (starPlates == null || starPlates.Length == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < starPlates.Length; i++)
        {
            if (starPlates[i] == null)
                continue;

            total += starPlates[i].GetMaxAdditionalPointsForPlayer(player);
        }

        return total;
    }

    public int GetCurrentScore(PlayerID player)
    {
        switch (player)
        {
            case PlayerID.Player1:
                return ScoreP1;
            case PlayerID.Player2:
                return ScoreP2;
            default:
                return 0;
        }
    }

    public PlayerID GetWinnerOrNone()
    {
        if (ScoreP1 > ScoreP2)
            return PlayerID.Player1;

        if (ScoreP2 > ScoreP1)
            return PlayerID.Player2;

        return PlayerID.None;
    }
}