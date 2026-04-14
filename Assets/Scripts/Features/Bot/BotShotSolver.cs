using System.Collections.Generic;
using UnityEngine;

public sealed class BotShotSolver : MonoBehaviour
{
    [Header("Seed Library")]
    [SerializeField] private BotHumanShotSeedLibrary seedLibrary;
    [SerializeField] private bool useHumanSeedLibrary = true;
    [SerializeField] private bool allowFallbackBruteforce = true;

    [Header("Seed Matching Debug")]
    [SerializeField] private bool logSeedMatchingDebug = false;

    [Header("Simulation")]
    [SerializeField] private float simulationDuration = 3.0f;
    [SerializeField] private float slotBoundsPadding = 0.01f;
    [SerializeField] private float blockingBoundsPadding = 0.02f;
    [SerializeField] private float descendingDotThreshold = 0.10f;

    [Header("Fallback Bruteforce Search")]
    [SerializeField] private float fallbackEasySwipeYMin = 280f;
    [SerializeField] private float fallbackEasySwipeYMax = 420f;
    [SerializeField] private float fallbackMediumSwipeYMin = 380f;
    [SerializeField] private float fallbackMediumSwipeYMax = 620f;
    [SerializeField] private float fallbackHardSwipeYMin = 520f;
    [SerializeField] private float fallbackHardSwipeYMax = 900f;

    [SerializeField] private int fallbackEasyVerticalSamples = 10;
    [SerializeField] private int fallbackMediumVerticalSamples = 14;
    [SerializeField] private int fallbackHardVerticalSamples = 18;

    [SerializeField] private float fallbackEasyLateralWindow = 100f;
    [SerializeField] private float fallbackMediumLateralWindow = 80f;
    [SerializeField] private float fallbackHardLateralWindow = 60f;

    [SerializeField] private int fallbackEasyLateralSamples = 9;
    [SerializeField] private int fallbackMediumLateralSamples = 11;
    [SerializeField] private int fallbackHardLateralSamples = 13;

    [Header("Adaptive Retry")]
    [SerializeField] private int maxMissStacksApplied = 6;
    [SerializeField] private float extraSwipeYPerMiss = 45f;
    [SerializeField] private float extraLateralWindowPerMiss = 8f;

    [Header("Target Policy")]
    [SerializeField] private bool preferHigherPlatesFirst = true;
    [SerializeField] private bool requireDescendingEntry = true;
    [SerializeField] private bool requireTargetTriggerEntry = true;

    [Header("Scoring")]
    [SerializeField] private float enteredTargetReward = 2000f;
    [SerializeField] private float descendingEntryReward = 7000f;
    [SerializeField] private float nonDescendingEntryPenalty = 6000f;
    [SerializeField] private float blockingPenalty = 5000f;
    [SerializeField] private float distancePenaltyMultiplier = 120f;
    [SerializeField] private float higherPlateRewardMultiplier = 900f;
    [SerializeField] private float noEntryPenalty = 2500f;

    [Header("Trajectory Gizmos")]
    [SerializeField] private bool drawBestTrajectory = true;
    [SerializeField] private bool drawAllTrajectoryPoints = true;
    [SerializeField] private float gizmoPointRadius = 0.02f;
    [SerializeField] private Color trajectoryColor = Color.green;
    [SerializeField] private Color targetColor = Color.yellow;
    [SerializeField] private Color bestSampleColor = Color.cyan;
    [SerializeField] private Color entryColor = Color.magenta;

    [Header("Debug")]
    [SerializeField] private bool logSolver = true;

    [Header("Last Solve Debug")]
    [SerializeField] private bool lastSolveHasSolution;
    [SerializeField] private int lastEvaluatedCandidates;
    [SerializeField] private Vector3 lastTargetSlotCenter;
    [SerializeField] private Vector3 lastBestSamplePosition;
    [SerializeField] private Vector3 lastEntryPoint;
    [SerializeField] private bool lastEntryPointValid;
    [SerializeField] private bool lastDescendingAtEntry;
    [SerializeField] private bool lastHitBlockingBoardBeforeEntry;
    [SerializeField] private float lastCandidateScore;
    [SerializeField] private string lastSeedIdUsed = "";
    [SerializeField] private bool lastChosenSeedStartPositionValid;
    [SerializeField] private Vector3 lastChosenSeedStartPosition;
    [SerializeField] private float lastSimulatedMass = 1f;
    [SerializeField] private float lastSimulatedFixedDeltaTime = 0.02f;

    private readonly List<Vector3> lastBestTrajectoryPoints = new List<Vector3>();

    public bool TryGetLastChosenSeedStartPosition(out Vector3 startPosition)
    {
        startPosition = lastChosenSeedStartPosition;
        return lastChosenSeedStartPositionValid;
    }

    public string GetLastSeedId()
    {
        return lastSeedIdUsed;
    }

    public bool TryGetSeedById(string seedId, out BotHumanShotSeed seed)
    {
        seed = null;
        return seedLibrary != null && seedLibrary.TryGetSeedById(seedId, out seed);
    }

    public void OnDrawGizmosSelected()
    {
        if (!drawBestTrajectory)
            return;

        if (!lastSolveHasSolution || lastBestTrajectoryPoints.Count < 2)
            return;

        Gizmos.color = trajectoryColor;
        for (int i = 1; i < lastBestTrajectoryPoints.Count; i++)
        {
            Gizmos.DrawLine(lastBestTrajectoryPoints[i - 1], lastBestTrajectoryPoints[i]);

            if (drawAllTrajectoryPoints)
                Gizmos.DrawSphere(lastBestTrajectoryPoints[i], gizmoPointRadius);
        }

        Gizmos.color = targetColor;
        Gizmos.DrawSphere(lastTargetSlotCenter, gizmoPointRadius * 1.8f);

        Gizmos.color = bestSampleColor;
        Gizmos.DrawSphere(lastBestSamplePosition, gizmoPointRadius * 1.6f);

        if (lastEntryPointValid)
        {
            Gizmos.color = entryColor;
            Gizmos.DrawSphere(lastEntryPoint, gizmoPointRadius * 2.0f);
        }
    }

    public BotShotSolution SolveBestShot(
        BallPhysics sourceBall,
        BallLauncher launcher,
        ScoreManager scoreManager,
        BotDifficulty difficulty,
        int missStacks)
    {
        ClearLastTrajectoryDebug();

        BotShotSolution solution = new BotShotSolution
        {
            hasSolution = false,
            evaluatedCandidates = 0,
            bestCandidate = default
        };

        if (sourceBall == null || launcher == null || scoreManager == null || scoreManager.starPlates == null || scoreManager.starPlates.Length == 0)
            return solution;

        List<PlateInfo> plates = CollectPlatesAndSlots(scoreManager);
        if (plates.Count == 0)
            return solution;

        float bestScore = float.NegativeInfinity;
        BotShotCandidate bestCandidate = default;
        List<Vector3> bestPath = null;
        CandidateEval bestEval = default;
        string bestSeedId = string.Empty;
        Vector3 bestSeedStartPosition = Vector3.zero;
        bool bestSeedStartPositionValid = false;

        IEnumerable<PlateInfo> orderedPlates = GetOrderedPlates(plates);

        foreach (PlateInfo plate in orderedPlates)
        {
            for (int slotIdx = 0; slotIdx < plate.slots.Count; slotIdx++)
            {
                SlotInfo slot = plate.slots[slotIdx];

                if (slot.isOccupied)
                {
                    if (logSeedMatchingDebug)
                    {
                        Debug.Log(
                            "[BotShotSolver] SlotSkippedOccupied -> " +
                            "PlateIndex=" + plate.index +
                            " | PlateName=" + plate.name +
                            " | SlotIndex=" + slot.slotIndex +
                            " | SlotName=" + slot.name,
                            this);
                    }

                    continue;
                }

                if (useHumanSeedLibrary && seedLibrary != null)
                {
                    if (seedLibrary.TryGetBestSeed(plate.index, slot.slotIndex, sourceBall.transform.position, out BotHumanShotSeed seed))
                    {
                        EvaluateSeedCluster(sourceBall, launcher, plate, slot, plates, seed, missStacks,
                            ref solution, ref bestScore, ref bestCandidate, ref bestPath, ref bestEval, ref bestSeedId,
                            ref bestSeedStartPosition, ref bestSeedStartPositionValid);
                    }
                }

                if (!allowFallbackBruteforce)
                    continue;

                EvaluateFallbackCluster(sourceBall, launcher, difficulty, plate, slot, plates, missStacks,
                    ref solution, ref bestScore, ref bestCandidate, ref bestPath, ref bestEval, ref bestSeedId,
                    ref bestSeedStartPosition, ref bestSeedStartPositionValid);
            }
        }

        solution.bestCandidate = bestCandidate;

        if (solution.hasSolution)
            StoreLastTrajectoryDebug(solution, bestPath, bestEval, bestSeedId, bestSeedStartPosition, bestSeedStartPositionValid);

        if (logSolver)
            Debug.Log("[BotShotSolver] " + solution + " | Seed=" + bestSeedId + " | SeedStartPos=" + bestSeedStartPosition, this);

        return solution;
    }

    private void EvaluateSeedCluster(
        BallPhysics sourceBall,
        BallLauncher launcher,
        PlateInfo targetPlate,
        SlotInfo targetSlot,
        List<PlateInfo> allPlates,
        BotHumanShotSeed seed,
        int missStacks,
        ref BotShotSolution solution,
        ref float bestScore,
        ref BotShotCandidate bestCandidate,
        ref List<Vector3> bestPath,
        ref CandidateEval bestEval,
        ref string bestSeedId,
        ref Vector3 bestSeedStartPosition,
        ref bool bestSeedStartPositionValid)
    {
        int appliedMissStacks = Mathf.Clamp(missStacks, 0, Mathf.Max(0, maxMissStacksApplied));
        float extraY = appliedMissStacks * 8f;

        float minX = seed.swipe.x - seed.swipeXVariation;
        float maxX = seed.swipe.x + seed.swipeXVariation;
        float minY = seed.swipe.y - seed.swipeYVariation;
        float maxY = seed.swipe.y + seed.swipeYVariation + extraY;

        int lateralSamples = Mathf.Max(3, seed.lateralSamples);
        int verticalSamples = Mathf.Max(3, seed.verticalSamples);

        for (int yIndex = 0; yIndex < verticalSamples; yIndex++)
        {
            float yT = verticalSamples <= 1 ? 0.5f : (float)yIndex / (verticalSamples - 1);
            float swipeY = Mathf.Lerp(minY, maxY, yT);

            for (int xIndex = 0; xIndex < lateralSamples; xIndex++)
            {
                float xT = lateralSamples <= 1 ? 0.5f : (float)xIndex / (lateralSamples - 1);
                float swipeX = Mathf.Lerp(minX, maxX, xT);
                Vector2 swipe = new Vector2(swipeX, swipeY);

                if (!launcher.TryBuildLaunchFromSwipe(swipe, out Vector3 dir, out float force))
                    continue;

                solution.evaluatedCandidates++;

                CandidateEval eval = EvaluateCandidate(sourceBall, swipe, dir, force, targetPlate, targetSlot, allPlates);

                if (requireTargetTriggerEntry && !eval.candidate.enteredTargetTrigger)
                    continue;

                if (requireDescendingEntry && !eval.candidate.descendingAtEntry)
                    continue;

                if (eval.candidate.score > bestScore)
                {
                    bestScore = eval.candidate.score;
                    bestCandidate = eval.candidate;
                    bestPath = eval.path;
                    bestEval = eval;
                    bestSeedId = seed.seedId;
                    bestSeedStartPosition = seed.referenceStartPosition;
                    bestSeedStartPositionValid = true;
                    solution.hasSolution = true;
                }
            }
        }
    }

    private void EvaluateFallbackCluster(
        BallPhysics sourceBall,
        BallLauncher launcher,
        BotDifficulty difficulty,
        PlateInfo targetPlate,
        SlotInfo targetSlot,
        List<PlateInfo> allPlates,
        int missStacks,
        ref BotShotSolution solution,
        ref float bestScore,
        ref BotShotCandidate bestCandidate,
        ref List<Vector3> bestPath,
        ref CandidateEval bestEval,
        ref string bestSeedId,
        ref Vector3 bestSeedStartPosition,
        ref bool bestSeedStartPositionValid)
    {
        int appliedMissStacks = Mathf.Clamp(missStacks, 0, Mathf.Max(0, maxMissStacksApplied));
        float swipeYMin = GetFallbackSwipeYMin(difficulty) + appliedMissStacks * extraSwipeYPerMiss;
        float swipeYMax = GetFallbackSwipeYMax(difficulty) + appliedMissStacks * extraSwipeYPerMiss;
        float lateralWindow = GetFallbackLateralWindow(difficulty) + appliedMissStacks * extraLateralWindowPerMiss;
        int verticalSamples = Mathf.Max(2, GetFallbackVerticalSamples(difficulty));
        int lateralSamples = Mathf.Max(3, GetFallbackLateralSamples(difficulty));

        float targetDx = targetSlot.center.x - sourceBall.transform.position.x;
        float targetDz = Mathf.Max(0.10f, targetSlot.center.z - sourceBall.transform.position.z);

        for (int yIndex = 0; yIndex < verticalSamples; yIndex++)
        {
            float yT = verticalSamples <= 1 ? 0.5f : (float)yIndex / (verticalSamples - 1);
            float swipeY = Mathf.Lerp(swipeYMin, swipeYMax, yT);

            float estimatedSwipeX = (targetDx / targetDz) * swipeY;

            for (int xIndex = 0; xIndex < lateralSamples; xIndex++)
            {
                float xT = lateralSamples <= 1 ? 0.5f : (float)xIndex / (lateralSamples - 1);
                float swipeX = Mathf.Lerp(estimatedSwipeX - lateralWindow, estimatedSwipeX + lateralWindow, xT);
                Vector2 swipe = new Vector2(swipeX, swipeY);

                if (!launcher.TryBuildLaunchFromSwipe(swipe, out Vector3 dir, out float force))
                    continue;

                solution.evaluatedCandidates++;

                CandidateEval eval = EvaluateCandidate(sourceBall, swipe, dir, force, targetPlate, targetSlot, allPlates);

                if (requireTargetTriggerEntry && !eval.candidate.enteredTargetTrigger)
                    continue;

                if (requireDescendingEntry && !eval.candidate.descendingAtEntry)
                    continue;

                if (eval.candidate.score > bestScore)
                {
                    bestScore = eval.candidate.score;
                    bestCandidate = eval.candidate;
                    bestPath = eval.path;
                    bestEval = eval;
                    bestSeedId = "fallback_bruteforce";
                    bestSeedStartPosition = Vector3.zero;
                    bestSeedStartPositionValid = false;
                    solution.hasSolution = true;
                }
            }
        }
    }

    private CandidateEval EvaluateCandidate(
        BallPhysics sourceBall,
        Vector2 swipe,
        Vector3 launchDirection,
        float launchForce,
        PlateInfo targetPlate,
        SlotInfo targetSlot,
        List<PlateInfo> allPlates)
    {
        BotShotCandidate candidate = new BotShotCandidate
        {
            targetPlateIndex = targetPlate.index,
            targetPlateName = targetPlate.name,
            targetSlotIndex = targetSlot.slotIndex,
            targetSlotName = targetSlot.name,
            swipeDelta = swipe,
            launchDirection = launchDirection,
            launchForce = launchForce,
            targetSlotCenter = targetSlot.center,
            bestSamplePosition = sourceBall.transform.position,
            bestDistanceToTarget = float.PositiveInfinity,
            enteredTargetTrigger = false,
            descendingAtEntry = false,
            hitBlockingBoardBeforeEntry = false,
            score = float.NegativeInfinity
        };

        List<Vector3> path = new List<Vector3>();

        Rigidbody rb = sourceBall.GetComponent<Rigidbody>();
        float mass = rb != null ? Mathf.Max(0.0001f, rb.mass) : 1f;
        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;

        Vector3 position = sourceBall.transform.position;
        Vector3 velocity = launchDirection.normalized * (launchForce / mass);
        Vector3 gravityDir = sourceBall.gravityDirection.normalized;
        Vector3 gravity = gravityDir * sourceBall.gravityStrength;
        float maxSpeed = Mathf.Max(1f, sourceBall.maxSpeed);

        path.Add(position);

        bool entryPointValid = false;
        Vector3 entryPoint = Vector3.zero;

        for (float t = 0f; t <= simulationDuration; t += dt)
        {
            velocity += gravity * dt;
            velocity *= Mathf.Clamp01(1f - sourceBall.linearDrag * dt);

            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
                velocity = velocity.normalized * maxSpeed;

            position += velocity * dt;
            path.Add(position);

            float distance = Vector3.Distance(position, targetSlot.center);
            if (distance < candidate.bestDistanceToTarget)
            {
                candidate.bestDistanceToTarget = distance;
                candidate.bestSamplePosition = position;
            }

            if (ContainsPoint(targetSlot.bounds, position, slotBoundsPadding))
            {
                candidate.enteredTargetTrigger = true;
                candidate.descendingAtEntry = Vector3.Dot(velocity.normalized, gravityDir) > descendingDotThreshold;
                entryPoint = position;
                entryPointValid = true;
                break;
            }

            if (HitsBlockingBoards(position, targetPlate.index, allPlates))
            {
                candidate.hitBlockingBoardBeforeEntry = true;
                break;
            }

            if (ContainsPoint(targetPlate.blockingBounds, position, blockingBoundsPadding))
            {
                candidate.hitBlockingBoardBeforeEntry = true;
                break;
            }
        }

        float score = 0f;
        score -= candidate.bestDistanceToTarget * distancePenaltyMultiplier;
        score += targetPlate.index * higherPlateRewardMultiplier;

        if (candidate.enteredTargetTrigger)
            score += enteredTargetReward;
        else
            score -= noEntryPenalty;

        if (candidate.descendingAtEntry)
            score += descendingEntryReward;
        else if (candidate.enteredTargetTrigger)
            score -= nonDescendingEntryPenalty;

        if (candidate.hitBlockingBoardBeforeEntry)
            score -= blockingPenalty;

        candidate.score = score;

        return new CandidateEval
        {
            candidate = candidate,
            path = path,
            entryPoint = entryPoint,
            entryPointValid = entryPointValid,
            mass = mass,
            fixedDeltaTime = dt
        };
    }

    private bool HitsBlockingBoards(Vector3 position, int targetPlateIndex, List<PlateInfo> allPlates)
    {
        for (int i = 0; i < allPlates.Count; i++)
        {
            if (allPlates[i].index == targetPlateIndex)
                continue;

            if (ContainsPoint(allPlates[i].blockingBounds, position, blockingBoundsPadding))
                return true;
        }

        return false;
    }


    private bool IsSlotOccupied(Collider slotCollider)
    {
        if (slotCollider == null)
            return false;

        Bounds b = slotCollider.bounds;
        Vector3 halfExtents = b.extents;
        if (halfExtents.x <= 0f) halfExtents.x = 0.01f;
        if (halfExtents.y <= 0f) halfExtents.y = 0.01f;
        if (halfExtents.z <= 0f) halfExtents.z = 0.01f;

        Collider[] hits = Physics.OverlapBox(
            b.center,
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
            if (ball == null)
                continue;

            return true;
        }

        return false;
    }

    private bool ContainsPoint(Bounds bounds, Vector3 point, float padding)
    {
        Vector3 min = bounds.min - Vector3.one * padding;
        Vector3 max = bounds.max + Vector3.one * padding;

        return point.x >= min.x && point.x <= max.x &&
               point.y >= min.y && point.y <= max.y &&
               point.z >= min.z && point.z <= max.z;
    }

    private List<PlateInfo> CollectPlatesAndSlots(ScoreManager scoreManager)
    {
        List<PlateInfo> result = new List<PlateInfo>();

        for (int i = 0; i < scoreManager.starPlates.Length; i++)
        {
            StarPlate plate = scoreManager.starPlates[i];
            if (plate == null)
                continue;

            SlotScorer[] slotScorers = plate.GetComponentsInChildren<SlotScorer>(true);
            if (slotScorers == null || slotScorers.Length == 0)
                continue;

            PlateInfo plateInfo = new PlateInfo
            {
                index = i,
                name = plate.name,
                blockingBounds = BuildBlockingBounds(plate),
                slots = new List<SlotInfo>()
            };

            for (int s = 0; s < slotScorers.Length; s++)
            {
                SlotScorer slot = slotScorers[s];
                if (slot == null)
                    continue;

                Collider slotCollider = slot.GetComponent<Collider>();
                if (slotCollider == null)
                    continue;

                plateInfo.slots.Add(new SlotInfo
                {
                    slotIndex = slot.slotIndex,
                    name = slot.name,
                    center = slotCollider.bounds.center,
                    bounds = slotCollider.bounds,
                    isOccupied = IsSlotOccupied(slotCollider)
                });
            }

            if (plateInfo.slots.Count > 0)
                result.Add(plateInfo);
        }

        return result;
    }

    private Bounds BuildBlockingBounds(StarPlate plate)
    {
        Collider col = plate.GetComponent<Collider>();
        if (col != null)
            return col.bounds;

        Renderer rnd = plate.GetComponent<Renderer>();
        if (rnd != null)
            return rnd.bounds;

        Renderer childRenderer = plate.GetComponentInChildren<Renderer>(true);
        if (childRenderer != null)
            return childRenderer.bounds;

        return new Bounds(plate.transform.position, Vector3.one * 0.5f);
    }

    private IEnumerable<PlateInfo> GetOrderedPlates(List<PlateInfo> plates)
    {
        List<PlateInfo> sorted = new List<PlateInfo>(plates);
        sorted.Sort((a, b) => preferHigherPlatesFirst ? b.index.CompareTo(a.index) : a.index.CompareTo(b.index));
        return sorted;
    }

    private void ClearLastTrajectoryDebug()
    {
        lastSolveHasSolution = false;
        lastEvaluatedCandidates = 0;
        lastTargetSlotCenter = Vector3.zero;
        lastBestSamplePosition = Vector3.zero;
        lastEntryPoint = Vector3.zero;
        lastEntryPointValid = false;
        lastDescendingAtEntry = false;
        lastHitBlockingBoardBeforeEntry = false;
        lastCandidateScore = 0f;
        lastSeedIdUsed = string.Empty;
        lastChosenSeedStartPositionValid = false;
        lastChosenSeedStartPosition = Vector3.zero;
        lastSimulatedMass = 1f;
        lastSimulatedFixedDeltaTime = 0.02f;
        lastBestTrajectoryPoints.Clear();
    }

    private void StoreLastTrajectoryDebug(BotShotSolution solution, List<Vector3> bestPath, CandidateEval bestEval, string seedId, Vector3 seedStartPosition, bool seedStartPositionValid)
    {
        lastSolveHasSolution = solution.hasSolution;
        lastEvaluatedCandidates = solution.evaluatedCandidates;
        lastTargetSlotCenter = solution.bestCandidate.targetSlotCenter;
        lastBestSamplePosition = solution.bestCandidate.bestSamplePosition;
        lastEntryPoint = bestEval.entryPoint;
        lastEntryPointValid = bestEval.entryPointValid;
        lastDescendingAtEntry = solution.bestCandidate.descendingAtEntry;
        lastHitBlockingBoardBeforeEntry = solution.bestCandidate.hitBlockingBoardBeforeEntry;
        lastCandidateScore = solution.bestCandidate.score;
        lastSeedIdUsed = seedId;
        lastChosenSeedStartPosition = seedStartPosition;
        lastChosenSeedStartPositionValid = seedStartPositionValid;
        lastSimulatedMass = bestEval.mass;
        lastSimulatedFixedDeltaTime = bestEval.fixedDeltaTime;

        lastBestTrajectoryPoints.Clear();
        if (bestPath != null)
            lastBestTrajectoryPoints.AddRange(bestPath);
    }

    private float GetFallbackSwipeYMin(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy: return fallbackEasySwipeYMin;
            case BotDifficulty.Medium: return fallbackMediumSwipeYMin;
            case BotDifficulty.Hard: return fallbackHardSwipeYMin;
            default: return fallbackMediumSwipeYMin;
        }
    }

    private float GetFallbackSwipeYMax(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy: return fallbackEasySwipeYMax;
            case BotDifficulty.Medium: return fallbackMediumSwipeYMax;
            case BotDifficulty.Hard: return fallbackHardSwipeYMax;
            default: return fallbackMediumSwipeYMax;
        }
    }

    private int GetFallbackVerticalSamples(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy: return fallbackEasyVerticalSamples;
            case BotDifficulty.Medium: return fallbackMediumVerticalSamples;
            case BotDifficulty.Hard: return fallbackHardVerticalSamples;
            default: return fallbackMediumVerticalSamples;
        }
    }

    private float GetFallbackLateralWindow(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy: return fallbackEasyLateralWindow;
            case BotDifficulty.Medium: return fallbackMediumLateralWindow;
            case BotDifficulty.Hard: return fallbackHardLateralWindow;
            default: return fallbackMediumLateralWindow;
        }
    }

    private int GetFallbackLateralSamples(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy: return fallbackEasyLateralSamples;
            case BotDifficulty.Medium: return fallbackMediumLateralSamples;
            case BotDifficulty.Hard: return fallbackHardLateralSamples;
            default: return fallbackMediumLateralSamples;
        }
    }

    private struct PlateInfo
    {
        public int index;
        public string name;
        public Bounds blockingBounds;
        public List<SlotInfo> slots;
    }

    private struct SlotInfo
    {
        public int slotIndex;
        public string name;
        public Vector3 center;
        public Bounds bounds;
        public bool isOccupied;
    }

    private struct CandidateEval
    {
        public BotShotCandidate candidate;
        public List<Vector3> path;
        public Vector3 entryPoint;
        public bool entryPointValid;
        public float mass;
        public float fixedDeltaTime;
    }
}
