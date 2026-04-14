using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class BotTargetDecisionService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Debug")]
    [SerializeField] private bool logDecision = true;
    [SerializeField] private bool logAllCandidates = false;

    private static readonly string[] OwnerMemberNames =
    {
        "Owner",
        "owner",
        "ownerId",
        "OwnerId",
        "ownerID",
        "OwnerID",
        "playerOwner",
        "PlayerOwner",
        "playerId",
        "PlayerId",
        "playerID",
        "PlayerID"
    };

    [Serializable]
    public struct BotTargetDecision
    {
        public bool hasTarget;
        public int targetPlateIndex;
        public string targetPlateName;
        public int targetSlotIndex;
        public string targetSlotName;
        public float score;
        public string reason;

        public override string ToString()
        {
            return "HasTarget=" + hasTarget +
                   " | PlateIndex=" + targetPlateIndex +
                   " | PlateName=" + targetPlateName +
                   " | SlotIndex=" + targetSlotIndex +
                   " | SlotName=" + targetSlotName +
                   " | Score=" + score +
                   " | Reason=" + reason;
        }
    }

    private struct SlotContext
    {
        public int plateIndex;
        public string plateName;
        public int slotIndex;
        public string slotName;
        public bool isOccupied;
        public bool occupiedByBot;
        public bool occupiedByEnemy;
        public bool boardHasBotPresence;
        public bool boardHasEnemyPresence;
        public int boardBotCount;
        public int boardEnemyCount;
        public bool hasAdjacentFriendly;
        public bool hasAdjacentEnemy;
        public int adjacentFriendlyCount;
        public int adjacentEnemyCount;
        public int friendlyChainLengthIfPlaced;
        public int enemyChainBlockedIfPlaced;
        public int futureExpansionPotential;
        public int centerDistance;
        public float score;
        public string reason;
    }

    private struct BoardContext
    {
        public int plateIndex;
        public string plateName;
        public List<SlotContext> slots;
        public int botCount;
        public int enemyCount;
    }

    public BotTargetDecision DecideBestTarget(PlayerID botOwner, PlayerID enemyOwner, BotDifficulty difficulty)
    {
#if UNITY_2023_1_OR_NEWER
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
#else
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();
#endif

        BotTargetDecision empty = default;
        empty.hasTarget = false;
        empty.targetPlateIndex = -1;
        empty.targetSlotIndex = -1;
        empty.score = float.NegativeInfinity;
        empty.reason = "NoTarget";

        if (scoreManager == null || scoreManager.starPlates == null || scoreManager.starPlates.Length == 0)
            return empty;

        List<BoardContext> boards = BuildBoardContexts(botOwner, enemyOwner);
        if (boards.Count == 0)
            return empty;

        BotDecisionWeights weights = BotDifficultyProfile.GetDecisionWeights(difficulty);
        int totalPlacedBalls = GetTotalPlacedBalls(boards);
        bool openingPhase = totalPlacedBalls < weights.openingTurnThreshold;

        List<SlotContext> candidates = new List<SlotContext>();

        for (int boardIndex = 0; boardIndex < boards.Count; boardIndex++)
        {
            BoardContext board = boards[boardIndex];

            for (int slotIndex = 0; slotIndex < board.slots.Count; slotIndex++)
            {
                SlotContext slot = board.slots[slotIndex];
                if (slot.isOccupied)
                    continue;

                slot.score = EvaluateSlotScore(slot, weights, openingPhase);
                slot.reason = BuildReason(slot, openingPhase);
                candidates.Add(slot);

                if (logAllCandidates)
                {
                    Debug.Log(
                        "[BotTargetDecisionService] Candidate -> " +
                        "PlateIndex=" + slot.plateIndex +
                        " | PlateName=" + slot.plateName +
                        " | SlotIndex=" + slot.slotIndex +
                        " | SlotName=" + slot.slotName +
                        " | Score=" + slot.score +
                        " | Reason=" + slot.reason,
                        this);
                }
            }
        }

        if (candidates.Count == 0)
            return empty;

        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        SlotContext chosen;
        if (weights.alwaysChooseBestTarget)
        {
            chosen = candidates[0];
        }
        else
        {
            int takeCount = Mathf.Min(weights.GetSafeTopCandidateCount(), candidates.Count);
            chosen = candidates[UnityEngine.Random.Range(0, takeCount)];
        }

        BotTargetDecision result = new BotTargetDecision
        {
            hasTarget = true,
            targetPlateIndex = chosen.plateIndex,
            targetPlateName = chosen.plateName,
            targetSlotIndex = chosen.slotIndex,
            targetSlotName = chosen.slotName,
            score = chosen.score,
            reason = chosen.reason
        };

        if (logDecision)
        {
            Debug.Log(
                "[BotTargetDecisionService] Decision -> Difficulty=" + difficulty +
                " | OpeningPhase=" + openingPhase +
                " | TotalPlacedBalls=" + totalPlacedBalls +
                " | " + result,
                this);
        }

        return result;
    }

    private List<BoardContext> BuildBoardContexts(PlayerID botOwner, PlayerID enemyOwner)
    {
        List<BoardContext> result = new List<BoardContext>();

        for (int plateIndex = 0; plateIndex < scoreManager.starPlates.Length; plateIndex++)
        {
            StarPlate plate = scoreManager.starPlates[plateIndex];
            if (plate == null)
                continue;

            SlotScorer[] slotScorers = plate.GetComponentsInChildren<SlotScorer>(true);
            if (slotScorers == null || slotScorers.Length == 0)
                continue;

            Array.Sort(slotScorers, (a, b) => a.slotIndex.CompareTo(b.slotIndex));

            List<SlotContext> slots = new List<SlotContext>(slotScorers.Length);
            int botCount = 0;
            int enemyCount = 0;

            for (int i = 0; i < slotScorers.Length; i++)
            {
                SlotScorer slotScorer = slotScorers[i];
                Collider slotCollider = slotScorer.GetComponent<Collider>();

                bool isOccupied = false;
                bool occupiedByBot = false;
                bool occupiedByEnemy = false;

                if (slotCollider != null)
                {
                    BallPhysics occupyingBall = FindBallInsideSlot(slotCollider);
                    if (occupyingBall != null)
                    {
                        isOccupied = true;

                        if (TryResolveBallOwner(occupyingBall, out PlayerID owner))
                        {
                            occupiedByBot = EqualityComparer<PlayerID>.Default.Equals(owner, botOwner);
                            occupiedByEnemy = EqualityComparer<PlayerID>.Default.Equals(owner, enemyOwner);
                        }
                    }
                }

                if (occupiedByBot) botCount++;
                if (occupiedByEnemy) enemyCount++;

                slots.Add(new SlotContext
                {
                    plateIndex = plateIndex,
                    plateName = plate.name,
                    slotIndex = slotScorer.slotIndex,
                    slotName = slotScorer.name,
                    isOccupied = isOccupied,
                    occupiedByBot = occupiedByBot,
                    occupiedByEnemy = occupiedByEnemy
                });
            }

            for (int i = 0; i < slots.Count; i++)
            {
                SlotContext slot = slots[i];
                slot.boardBotCount = botCount;
                slot.boardEnemyCount = enemyCount;
                slot.boardHasBotPresence = botCount > 0;
                slot.boardHasEnemyPresence = enemyCount > 0;
                slot.hasAdjacentFriendly = HasAdjacentOwned(slots, i, true);
                slot.hasAdjacentEnemy = HasAdjacentOwned(slots, i, false);
                slot.adjacentFriendlyCount = CountAdjacentOwned(slots, i, true);
                slot.adjacentEnemyCount = CountAdjacentOwned(slots, i, false);
                slot.friendlyChainLengthIfPlaced = ComputeChainLengthIfPlaced(slots, i, true);
                slot.enemyChainBlockedIfPlaced = ComputeChainLengthIfPlaced(slots, i, false);
                slot.futureExpansionPotential = ComputeFutureExpansionPotential(slots, i);
                slot.centerDistance = GetCenterDistance(slots.Count, slot.slotIndex);
                slots[i] = slot;
            }

            result.Add(new BoardContext
            {
                plateIndex = plateIndex,
                plateName = plate.name,
                slots = slots,
                botCount = botCount,
                enemyCount = enemyCount
            });
        }

        return result;
    }

    private float EvaluateSlotScore(SlotContext slot, BotDecisionWeights weights, bool openingPhase)
    {
        float score = 0f;

        if (slot.boardHasEnemyPresence && !slot.boardHasBotPresence)
            score += weights.contestEnemyBoardWeight;

        if (!slot.boardHasBotPresence)
            score += weights.boardCoverageWeight;

        if (slot.hasAdjacentFriendly)
            score += weights.adjacentFriendlyWeight;

        if (slot.hasAdjacentEnemy)
            score += weights.adjacentEnemyWeight;

        score += slot.friendlyChainLengthIfPlaced * weights.friendlyChainExtensionWeight;
        score += slot.enemyChainBlockedIfPlaced * weights.enemyChainBlockWeight;
        score += slot.futureExpansionPotential * weights.futureExpansionWeight;
        score -= slot.centerDistance * weights.centerPreferenceWeight;

        if (openingPhase && !slot.boardHasBotPresence)
            score += weights.openingBoardSpreadWeight;

        score += (slot.plateIndex + 1) * weights.boardValueWeight;
        score -= weights.GetRiskPenaltyForPlate(slot.plateIndex);

        if (weights.decisionNoise > 0f)
            score += UnityEngine.Random.Range(-weights.decisionNoise, weights.decisionNoise);

        return score;
    }

    private string BuildReason(SlotContext slot, bool openingPhase)
    {
        List<string> reasons = new List<string>();

        if (slot.boardHasEnemyPresence && !slot.boardHasBotPresence)
            reasons.Add("ContestEnemyBoard");

        if (!slot.boardHasBotPresence)
            reasons.Add("BoardCoverage");

        if (slot.hasAdjacentFriendly)
            reasons.Add("AdjacentFriendly");

        if (slot.hasAdjacentEnemy)
            reasons.Add("AdjacentEnemy");

        if (slot.friendlyChainLengthIfPlaced > 1)
            reasons.Add("FriendlyChain+" + slot.friendlyChainLengthIfPlaced);

        if (slot.enemyChainBlockedIfPlaced > 1)
            reasons.Add("EnemyBlock+" + slot.enemyChainBlockedIfPlaced);

        if (slot.futureExpansionPotential > 0)
            reasons.Add("FutureExpand+" + slot.futureExpansionPotential);

        if (openingPhase && !slot.boardHasBotPresence)
            reasons.Add("OpeningSpread");

        reasons.Add("BoardValueP" + slot.plateIndex);

        return string.Join(" + ", reasons.ToArray());
    }

    private int GetTotalPlacedBalls(List<BoardContext> boards)
    {
        int total = 0;
        for (int i = 0; i < boards.Count; i++)
            total += boards[i].botCount + boards[i].enemyCount;
        return total;
    }

    private bool HasAdjacentOwned(List<SlotContext> slots, int index, bool friendly)
    {
        if (index - 1 >= 0)
        {
            if (friendly && slots[index - 1].occupiedByBot) return true;
            if (!friendly && slots[index - 1].occupiedByEnemy) return true;
        }

        if (index + 1 < slots.Count)
        {
            if (friendly && slots[index + 1].occupiedByBot) return true;
            if (!friendly && slots[index + 1].occupiedByEnemy) return true;
        }

        return false;
    }

    private int CountAdjacentOwned(List<SlotContext> slots, int index, bool friendly)
    {
        int count = 0;

        if (index - 1 >= 0)
        {
            if (friendly && slots[index - 1].occupiedByBot) count++;
            if (!friendly && slots[index - 1].occupiedByEnemy) count++;
        }

        if (index + 1 < slots.Count)
        {
            if (friendly && slots[index + 1].occupiedByBot) count++;
            if (!friendly && slots[index + 1].occupiedByEnemy) count++;
        }

        return count;
    }

    private int ComputeChainLengthIfPlaced(List<SlotContext> slots, int index, bool friendly)
    {
        int chain = 1;

        for (int i = index - 1; i >= 0; i--)
        {
            bool occupied = friendly ? slots[i].occupiedByBot : slots[i].occupiedByEnemy;
            if (!occupied) break;
            chain++;
        }

        for (int i = index + 1; i < slots.Count; i++)
        {
            bool occupied = friendly ? slots[i].occupiedByBot : slots[i].occupiedByEnemy;
            if (!occupied) break;
            chain++;
        }

        return chain;
    }

    private int ComputeFutureExpansionPotential(List<SlotContext> slots, int index)
    {
        int potential = 0;

        if (index - 1 >= 0 && !slots[index - 1].isOccupied)
            potential++;

        if (index + 1 < slots.Count && !slots[index + 1].isOccupied)
            potential++;

        return potential;
    }

    private int GetCenterDistance(int slotCount, int slotIndex)
    {
        int centerLeft = (slotCount - 1) / 2;
        int centerRight = slotCount / 2;
        int distanceLeft = Mathf.Abs(slotIndex - centerLeft);
        int distanceRight = Mathf.Abs(slotIndex - centerRight);
        return Mathf.Min(distanceLeft, distanceRight);
    }

    private BallPhysics FindBallInsideSlot(Collider slotCollider)
    {
        Bounds bounds = slotCollider.bounds;
        Vector3 halfExtents = bounds.extents;

        if (halfExtents.x <= 0f) halfExtents.x = 0.01f;
        if (halfExtents.y <= 0f) halfExtents.y = 0.01f;
        if (halfExtents.z <= 0f) halfExtents.z = 0.01f;

        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            halfExtents * 0.9f,
            slotCollider.transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit == slotCollider)
                continue;

            BallPhysics ball = hit.GetComponentInParent<BallPhysics>();
            if (ball != null)
                return ball;
        }

        return null;
    }

    private bool TryResolveBallOwner(BallPhysics ball, out PlayerID owner)
    {
        owner = default;

        if (ball == null)
            return false;

        BallOwnership ownership = ball.GetComponent<BallOwnership>();
        if (ownership == null)
            ownership = ball.GetComponentInParent<BallOwnership>();

        if (ownership != null)
        {
            owner = ownership.Owner;
            return true;
        }

        Type type = ball.GetType();

        for (int i = 0; i < OwnerMemberNames.Length; i++)
        {
            string memberName = OwnerMemberNames[i];

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(PlayerID))
            {
                owner = (PlayerID)field.GetValue(ball);
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(PlayerID) && property.CanRead)
            {
                owner = (PlayerID)property.GetValue(ball, null);
                return true;
            }
        }

        return false;
    }
}
