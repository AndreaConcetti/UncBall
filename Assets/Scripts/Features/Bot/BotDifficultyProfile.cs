using UnityEngine;

[System.Serializable]
public struct BotDecisionWeights
{
    [Header("Core Priorities")]
    public float contestEnemyBoardWeight;
    public float adjacentFriendlyWeight;
    public float adjacentEnemyWeight;
    public float boardCoverageWeight;
    public float boardValueWeight;

    [Header("Opening")]
    public int openingTurnThreshold;
    public float openingBoardSpreadWeight;

    [Header("Execution / Risk")]
    public float board1RiskPenalty;
    public float board2RiskPenalty;
    public float board3RiskPenalty;

    [Header("Noise")]
    public float decisionNoise;

    [Header("Mode Flags")]
    public bool preferRandomBoardSelection;
    public bool preferRandomSlotSelection;
    public bool alwaysChooseBestTarget;

    public float GetRiskPenaltyForPlate(int plateIndex)
    {
        switch (plateIndex)
        {
            case 0: return board1RiskPenalty;
            case 1: return board2RiskPenalty;
            case 2: return board3RiskPenalty;
            default: return board3RiskPenalty + ((plateIndex - 2) * 25f);
        }
    }
}

public static class BotDifficultyProfile
{
    public static BotDecisionWeights GetDecisionWeights(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 120f,
                    adjacentFriendlyWeight = 40f,
                    adjacentEnemyWeight = 20f,
                    boardCoverageWeight = 45f,
                    boardValueWeight = 25f,
                    openingTurnThreshold = 6,
                    openingBoardSpreadWeight = 60f,
                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 20f,
                    board3RiskPenalty = 70f,
                    decisionNoise = 180f,
                    preferRandomBoardSelection = true,
                    preferRandomSlotSelection = true,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Medium:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 520f,
                    adjacentFriendlyWeight = 150f,
                    adjacentEnemyWeight = 75f,
                    boardCoverageWeight = 100f,
                    boardValueWeight = 55f,
                    openingTurnThreshold = 5,
                    openingBoardSpreadWeight = 125f,
                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 35f,
                    board3RiskPenalty = 110f,
                    decisionNoise = 60f,
                    preferRandomBoardSelection = false,
                    preferRandomSlotSelection = false,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Hard:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 900f,
                    adjacentFriendlyWeight = 260f,
                    adjacentEnemyWeight = 110f,
                    boardCoverageWeight = 145f,
                    boardValueWeight = 80f,
                    openingTurnThreshold = 4,
                    openingBoardSpreadWeight = 180f,
                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 55f,
                    board3RiskPenalty = 160f,
                    decisionNoise = 12f,
                    preferRandomBoardSelection = false,
                    preferRandomSlotSelection = false,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Unbeatable:
            default:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 1200f,
                    adjacentFriendlyWeight = 340f,
                    adjacentEnemyWeight = 140f,
                    boardCoverageWeight = 170f,
                    boardValueWeight = 95f,
                    openingTurnThreshold = 4,
                    openingBoardSpreadWeight = 220f,
                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 70f,
                    board3RiskPenalty = 190f,
                    decisionNoise = 0f,
                    preferRandomBoardSelection = false,
                    preferRandomSlotSelection = false,
                    alwaysChooseBestTarget = true
                };
        }
    }
}