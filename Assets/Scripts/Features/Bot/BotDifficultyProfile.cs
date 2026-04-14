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

    [Header("Combo / Pressure")]
    public float friendlyChainExtensionWeight;
    public float enemyChainBlockWeight;
    public float futureExpansionWeight;
    public float centerPreferenceWeight;

    [Header("Opening")]
    public int openingTurnThreshold;
    public float openingBoardSpreadWeight;

    [Header("Execution / Risk")]
    public float board1RiskPenalty;
    public float board2RiskPenalty;
    public float board3RiskPenalty;

    [Header("Noise")]
    public float decisionNoise;

    [Header("Candidate Pool")]
    public int chooseFromTopCandidates;

    [Header("Mode Flags")]
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

    public int GetSafeTopCandidateCount()
    {
        return Mathf.Max(1, chooseFromTopCandidates);
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
                    adjacentFriendlyWeight = 35f,
                    adjacentEnemyWeight = 20f,
                    boardCoverageWeight = 35f,
                    boardValueWeight = 18f,

                    friendlyChainExtensionWeight = 45f,
                    enemyChainBlockWeight = 35f,
                    futureExpansionWeight = 18f,
                    centerPreferenceWeight = 12f,

                    openingTurnThreshold = 6,
                    openingBoardSpreadWeight = 55f,

                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 35f,
                    board3RiskPenalty = 90f,

                    decisionNoise = 180f,
                    chooseFromTopCandidates = 5,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Medium:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 380f,
                    adjacentFriendlyWeight = 120f,
                    adjacentEnemyWeight = 70f,
                    boardCoverageWeight = 80f,
                    boardValueWeight = 42f,

                    friendlyChainExtensionWeight = 150f,
                    enemyChainBlockWeight = 110f,
                    futureExpansionWeight = 65f,
                    centerPreferenceWeight = 25f,

                    openingTurnThreshold = 5,
                    openingBoardSpreadWeight = 105f,

                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 25f,
                    board3RiskPenalty = 85f,

                    decisionNoise = 70f,
                    chooseFromTopCandidates = 3,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Hard:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 780f,
                    adjacentFriendlyWeight = 220f,
                    adjacentEnemyWeight = 110f,
                    boardCoverageWeight = 120f,
                    boardValueWeight = 60f,

                    friendlyChainExtensionWeight = 260f,
                    enemyChainBlockWeight = 220f,
                    futureExpansionWeight = 130f,
                    centerPreferenceWeight = 35f,

                    openingTurnThreshold = 4,
                    openingBoardSpreadWeight = 145f,

                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 18f,
                    board3RiskPenalty = 55f,

                    decisionNoise = 18f,
                    chooseFromTopCandidates = 2,
                    alwaysChooseBestTarget = false
                };

            case BotDifficulty.Unbeatable:
            default:
                return new BotDecisionWeights
                {
                    contestEnemyBoardWeight = 1050f,
                    adjacentFriendlyWeight = 300f,
                    adjacentEnemyWeight = 140f,
                    boardCoverageWeight = 135f,
                    boardValueWeight = 70f,

                    friendlyChainExtensionWeight = 340f,
                    enemyChainBlockWeight = 260f,
                    futureExpansionWeight = 170f,
                    centerPreferenceWeight = 40f,

                    openingTurnThreshold = 4,
                    openingBoardSpreadWeight = 170f,

                    board1RiskPenalty = 0f,
                    board2RiskPenalty = 8f,
                    board3RiskPenalty = 20f,

                    decisionNoise = 0f,
                    chooseFromTopCandidates = 1,
                    alwaysChooseBestTarget = true
                };
        }
    }
}
