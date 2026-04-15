using System.Collections.Generic;
using UnityEngine;

public sealed class OfflineBotMatchEndFlow : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OfflineBotMatchController offlineBotMatchController;
    [SerializeField] private OnlineMatchPresentationResultStore presentationResultStore;
    [SerializeField] private OnlinePostMatchPanelsController postMatchPanelsController;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private PlayerProgressionRules progressionRules;
    [SerializeField] private LevelUpRewardsConfig levelUpRewardsConfig;
    [SerializeField] private OfflineBotMatchRewardsConfig offlineBotMatchRewardsConfig;

    [Header("Masked Bot Queue Rewards")]
    [SerializeField] private OnlineMatchRewardsConfig onlineMatchRewardsConfig;

    [Header("Policy")]
    [SerializeField] private bool applyRewards = true;
    [SerializeField] private bool applyProfileStats = true;
    [SerializeField] private bool autoOpenRewardsPanel = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private readonly List<LevelRewardEntry> cachedLevelRewards = new List<LevelRewardEntry>();

    private bool resultApplied;
    private string lastProcessedOfflineRequestId = string.Empty;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        ResolveDependencies();

        if (offlineBotMatchController == null)
            return;

        if (!offlineBotMatchController.IsOfflineBotSessionActive)
        {
            resultApplied = false;
            return;
        }

        BotOfflineMatchRequest request = offlineBotMatchController.ActiveRequest;
        string requestId = request != null ? request.RequestId : string.Empty;

        if (!offlineBotMatchController.MatchEnded)
        {
            if (!string.IsNullOrWhiteSpace(requestId) && requestId != lastProcessedOfflineRequestId)
                resultApplied = false;

            return;
        }

        if (resultApplied)
            return;

        ApplyOfflineMatchEnd();
    }

    private void ApplyOfflineMatchEnd()
    {
        if (offlineBotMatchController == null || !offlineBotMatchController.MatchEnded)
            return;

        BotOfflineMatchRequest request = offlineBotMatchController.ActiveRequest;
        if (request == null)
            return;

        ResolveDependencies();

        string requestId = request.RequestId ?? string.Empty;
        lastProcessedOfflineRequestId = requestId;

        PlayerID localPlayerId = offlineBotMatchController.LocalPlayerId;
        PlayerID winner = ResolveWinner();
        OnlineMatchEndReason endReason = offlineBotMatchController.MatchEndReason;

        bool isDraw = winner == PlayerID.None;
        bool localWon = !isDraw && winner == localPlayerId;
        bool localLost = !isDraw && winner != localPlayerId;
        bool surrenderLoss = endReason == OnlineMatchEndReason.SurrenderLoss;

        bool maskedBotQueueMatch = IsMaskedBotQueueRuntime(request);
        QueueType rewardQueueType = ResolveRewardQueueType(maskedBotQueueMatch);

        int startTotalXp = profileManager != null && profileManager.ActiveProfile != null
            ? Mathf.Max(0, profileManager.ActiveProfile.xp)
            : 0;

        int startLevel = ComputeLevelFromTotalXp(startTotalXp);

        int grantedXp = 0;
        int grantedSoftCurrency = 0;
        int grantedChestCount = 0;
        ChestType grantedChestType = ChestType.Random;
        int rankedLpDelta = 0;

        if (applyProfileStats && profileManager != null)
        {
            if (maskedBotQueueMatch)
            {
                profileManager.RegisterMatchResult(
                    PlayerMatchCategory.Bot,
                    request.MatchMode,
                    localWon,
                    false
                );
            }
            else
            {
                OfflineBotRewardRule rewardRuleForStats = ResolveOfflineRewardRule(request.Difficulty, localWon, surrenderLoss);
                if (rewardRuleForStats.countAsMatchPlayed)
                {
                    profileManager.RegisterMatchResult(
                        PlayerMatchCategory.Bot,
                        request.MatchMode,
                        localWon,
                        false
                    );
                }
            }
        }

        if (applyRewards && profileManager != null)
        {
            if (maskedBotQueueMatch)
            {
                if (onlineMatchRewardsConfig == null)
                {
                    Debug.LogError(
                        "[OfflineBotMatchEndFlow] onlineMatchRewardsConfig is NULL during masked bot queue match. " +
                        "Assign OnlineMatchRewardsConfig on OnlineFlowController and/or this component.",
                        this);
                }

                OnlineRewardCategory category = ResolveOnlineRewardCategory(localWon, localLost, isDraw, endReason);
                OnlineRewardRule onlineRule = onlineMatchRewardsConfig != null
                    ? onlineMatchRewardsConfig.GetRule(category, rewardQueueType)
                    : default;

                if (onlineRule.xpReward > 0)
                {
                    grantedXp = Mathf.Max(0, onlineRule.xpReward);
                    profileManager.AddXp(grantedXp);
                }

                if (onlineRule.softCurrencyReward > 0)
                {
                    grantedSoftCurrency = Mathf.Max(0, onlineRule.softCurrencyReward);
                    profileManager.AddSoftCurrency(grantedSoftCurrency);
                }

                if (rewardQueueType == QueueType.Ranked && onlineRule.rankedLpDelta != 0)
                {
                    rankedLpDelta = onlineRule.rankedLpDelta;
                    profileManager.AddRankedLp(rankedLpDelta);
                }

                if (onlineRule.grantChest && rewardManager != null)
                {
                    bool granted = rewardManager.GrantChestReward(
                        onlineRule.chestType,
                        rewardQueueType == QueueType.Ranked ? "ranked_masked_bot_match_reward" : "normal_masked_bot_match_reward",
                        category.ToString(),
                        winner
                    );

                    if (granted)
                    {
                        grantedChestCount = 1;
                        grantedChestType = onlineRule.chestType;
                    }
                }
            }
            else
            {
                OfflineBotRewardRule rewardRule = ResolveOfflineRewardRule(request.Difficulty, localWon, surrenderLoss);

                if (rewardRule.xpReward > 0)
                {
                    grantedXp = Mathf.Max(0, rewardRule.xpReward);
                    profileManager.AddXp(grantedXp);
                }

                if (rewardRule.softCurrencyReward > 0)
                {
                    grantedSoftCurrency = Mathf.Max(0, rewardRule.softCurrencyReward);
                    profileManager.AddSoftCurrency(grantedSoftCurrency);
                }

                if (rewardRule.grantChest && rewardManager != null)
                {
                    bool granted = rewardManager.GrantChestReward(
                        rewardRule.chestType,
                        "offline_bot_match_reward",
                        localWon ? "offline_bot_win" : (surrenderLoss ? "offline_bot_surrender_loss" : "offline_bot_loss"),
                        winner
                    );

                    if (granted)
                    {
                        grantedChestCount = 1;
                        grantedChestType = rewardRule.chestType;
                    }
                }
            }
        }

        int endTotalXpAfterBase = profileManager != null && profileManager.ActiveProfile != null
            ? Mathf.Max(0, profileManager.ActiveProfile.xp)
            : startTotalXp;

        int endLevelAfterBase = ComputeLevelFromTotalXp(endTotalXpAfterBase);
        int levelUpCount = Mathf.Max(0, endLevelAfterBase - startLevel);

        int levelUpBonusSoft = 0;
        int levelUpBonusChestCount = 0;
        ChestType levelUpBonusChestType = ChestType.Random;

        if (levelUpCount > 0 && levelUpRewardsConfig != null && profileManager != null)
        {
            cachedLevelRewards.Clear();
            levelUpRewardsConfig.GetRewardsBetweenLevels(startLevel, endLevelAfterBase, cachedLevelRewards);

            for (int i = 0; i < cachedLevelRewards.Count; i++)
            {
                LevelRewardEntry entry = cachedLevelRewards[i];

                if (entry.softCurrencyReward > 0)
                {
                    profileManager.AddSoftCurrency(entry.softCurrencyReward);
                    grantedSoftCurrency += entry.softCurrencyReward;
                    levelUpBonusSoft += entry.softCurrencyReward;
                }

                int chestCount = Mathf.Max(0, entry.chestCount);
                for (int chestIndex = 0; chestIndex < chestCount; chestIndex++)
                {
                    bool granted = rewardManager != null && rewardManager.GrantChestReward(
                        entry.chestType,
                        maskedBotQueueMatch
                            ? (rewardQueueType == QueueType.Ranked ? "ranked_masked_bot_level_up_reward" : "normal_masked_bot_level_up_reward")
                            : "offline_bot_level_up_reward",
                        "level_up_reward_level_" + entry.targetLevel,
                        winner
                    );

                    if (granted)
                    {
                        grantedChestCount += 1;
                        levelUpBonusChestCount += 1;
                        levelUpBonusChestType = entry.chestType;

                        if (grantedChestType == ChestType.Random)
                            grantedChestType = entry.chestType;
                    }
                }
            }
        }

        int endTotalXp = profileManager != null && profileManager.ActiveProfile != null
            ? Mathf.Max(0, profileManager.ActiveProfile.xp)
            : endTotalXpAfterBase;

        int endLevel = ComputeLevelFromTotalXp(endTotalXp);
        int newRankedLpTotal = profileManager != null ? profileManager.ActiveRankedLp : 0;

        OnlineMatchPresentationResult result = BuildPresentationResult(
            request,
            winner,
            endReason,
            localWon,
            localLost,
            isDraw,
            maskedBotQueueMatch,
            rewardQueueType,
            startLevel,
            endLevel,
            startTotalXp,
            endTotalXp,
            grantedXp,
            grantedSoftCurrency,
            grantedChestCount,
            grantedChestType,
            levelUpCount,
            levelUpBonusSoft,
            levelUpBonusChestCount,
            levelUpBonusChestType,
            rankedLpDelta,
            newRankedLpTotal
        );

        if (presentationResultStore != null)
        {
            presentationResultStore.SetLatest(result);
            presentationResultStore.MarkMatchProcessed(requestId);
        }

        resultApplied = true;

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchEndFlow] Result applied -> " +
                "RequestId=" + requestId +
                " | MaskedBotQueueMatch=" + maskedBotQueueMatch +
                " | RewardQueueType=" + rewardQueueType +
                " | Difficulty=" + request.Difficulty +
                " | Winner=" + winner +
                " | EndReason=" + endReason +
                " | LocalWon=" + localWon +
                " | XP=" + grantedXp +
                " | Soft=" + grantedSoftCurrency +
                " | LPDelta=" + rankedLpDelta +
                " | ChestCount=" + grantedChestCount +
                " | RewardsConfigNull=" + (onlineMatchRewardsConfig == null),
                this
            );
        }

        if (autoOpenRewardsPanel && postMatchPanelsController != null)
        {
            if (logDebug)
                Debug.Log("[OfflineBotMatchEndFlow] autoOpenRewardsPanel attivo, ma il flow consigliato è aprire RewardsObtained solo da bottone PostGame.", this);
        }
    }

    private OnlineMatchPresentationResult BuildPresentationResult(
        BotOfflineMatchRequest request,
        PlayerID winner,
        OnlineMatchEndReason endReason,
        bool localWon,
        bool localLost,
        bool isDraw,
        bool maskedBotQueueMatch,
        QueueType rewardQueueType,
        int startLevel,
        int endLevel,
        int startTotalXp,
        int endTotalXp,
        int grantedXp,
        int grantedSoftCurrency,
        int grantedChestCount,
        ChestType grantedChestType,
        int levelUpCount,
        int levelUpBonusSoft,
        int levelUpBonusChestCount,
        ChestType levelUpBonusChestType,
        int rankedLpDelta,
        int newRankedLpTotal)
    {
        string titleText;
        if (isDraw)
            titleText = "DRAW";
        else if (localWon)
            titleText = "VICTORY";
        else
            titleText = "DEFEAT";

        OnlineMatchPresentationResult result = new OnlineMatchPresentationResult
        {
            hasData = true,
            isVictory = localWon,
            isDefeat = localLost,
            isDraw = isDraw,
            isRanked = maskedBotQueueMatch && rewardQueueType == QueueType.Ranked,

            titleText = titleText,
            playerName = profileManager != null ? profileManager.ActiveDisplayName : request.LocalDisplayName,
            levelText = "LV " + Mathf.Max(1, endLevel),

            startLevel = Mathf.Max(1, startLevel),
            endLevel = Mathf.Max(1, endLevel),

            startTotalXp = Mathf.Max(0, startTotalXp),
            endTotalXp = Mathf.Max(0, endTotalXp),
            grantedXp = Mathf.Max(0, grantedXp),

            startLevelProgress01 = CalculateLevelProgress01(startTotalXp),
            endLevelProgress01 = CalculateLevelProgress01(endTotalXp),

            rankedLpDelta = (maskedBotQueueMatch && rewardQueueType == QueueType.Ranked) ? rankedLpDelta : 0,
            newRankedLpTotal = (maskedBotQueueMatch && rewardQueueType == QueueType.Ranked)
                ? newRankedLpTotal
                : (profileManager != null ? profileManager.ActiveRankedLp : 0),

            totalSoftCurrencyGained = Mathf.Max(0, grantedSoftCurrency),
            totalChestCount = Mathf.Max(0, grantedChestCount),
            totalChestType = grantedChestType,

            leveledUp = levelUpCount > 0,
            levelUpCount = Mathf.Max(0, levelUpCount),
            levelUpBonusSoftCurrency = Mathf.Max(0, levelUpBonusSoft),
            levelUpBonusChestCount = Mathf.Max(0, levelUpBonusChestCount),
            levelUpBonusChestType = levelUpBonusChestType,

            overlayTitleText = "LEVEL UP!",
            sourceMatchId = request.RequestId
        };

        result.rewardSummaryText = BuildSummaryText(
            request.Difficulty,
            localWon,
            endReason,
            result,
            maskedBotQueueMatch,
            rewardQueueType
        );

        return result;
    }

    private string BuildSummaryText(
        BotDifficulty difficulty,
        bool localWon,
        OnlineMatchEndReason endReason,
        OnlineMatchPresentationResult result,
        bool maskedBotQueueMatch,
        QueueType rewardQueueType)
    {
        string difficultyText = difficulty.ToString().ToUpperInvariant();
        string outcomeText;

        if (endReason == OnlineMatchEndReason.SurrenderLoss)
            outcomeText = "SURRENDER";
        else if (result.isDraw)
            outcomeText = "DRAW";
        else
            outcomeText = localWon ? "MATCH WON" : "MATCH LOST";

        string summary;
        if (maskedBotQueueMatch)
            summary = (rewardQueueType == QueueType.Ranked ? "RANKED" : "NORMAL") + " | " + outcomeText;
        else
            summary = difficultyText + " | " + outcomeText;

        if (result.grantedXp > 0)
            summary += " | XP +" + result.grantedXp;

        if (result.isRanked)
            summary += " | LP " + (result.rankedLpDelta > 0 ? "+" : "") + result.rankedLpDelta;

        if (result.totalSoftCurrencyGained > 0)
            summary += " | COINS +" + result.totalSoftCurrencyGained;

        if (result.totalChestCount > 0)
            summary += " | CHEST X" + result.totalChestCount;

        return summary;
    }

    private bool IsMaskedBotQueueRuntime(BotOfflineMatchRequest request)
    {
        OnlineFlowController flow = OnlineFlowController.Instance;
        if (flow != null && flow.RuntimeContext != null)
        {
            if (flow.RuntimeContext.currentSession != null)
            {
                MatchRuntimeType sessionRuntime = flow.RuntimeContext.currentSession.runtimeType;
                return sessionRuntime == MatchRuntimeType.RankedMaskedBot ||
                       sessionRuntime == MatchRuntimeType.NormalMaskedBot;
            }

            if (flow.RuntimeContext.currentAssignment != null)
            {
                MatchRuntimeType assignmentRuntime = flow.RuntimeContext.currentAssignment.runtimeType;
                return assignmentRuntime == MatchRuntimeType.RankedMaskedBot ||
                       assignmentRuntime == MatchRuntimeType.NormalMaskedBot;
            }
        }

        return request != null && request.UseDisguisedBotIdentity;
    }

    private QueueType ResolveRewardQueueType(bool maskedBotQueueMatch)
    {
        if (!maskedBotQueueMatch)
            return QueueType.Normal;

        OnlineFlowController flow = OnlineFlowController.Instance;
        if (flow != null && flow.RuntimeContext != null)
            return flow.RuntimeContext.queueType;

        return QueueType.Normal;
    }

    private OnlineRewardCategory ResolveOnlineRewardCategory(
        bool localWon,
        bool localLost,
        bool isDraw,
        OnlineMatchEndReason endReason)
    {
        if (endReason == OnlineMatchEndReason.SurrenderLoss)
            return localWon ? OnlineRewardCategory.SurrenderWin : OnlineRewardCategory.SurrenderLoss;

        if (isDraw)
            return OnlineRewardCategory.Draw;

        if (localWon)
            return OnlineRewardCategory.NormalCompletionWin;

        if (localLost)
            return OnlineRewardCategory.NormalCompletionLoss;

        return OnlineRewardCategory.Draw;
    }

    private OfflineBotRewardRule ResolveOfflineRewardRule(BotDifficulty difficulty, bool localWon, bool surrenderLoss)
    {
        if (offlineBotMatchRewardsConfig == null)
            return default;

        if (surrenderLoss)
            return offlineBotMatchRewardsConfig.GetSurrenderLossRule(difficulty);

        return localWon
            ? offlineBotMatchRewardsConfig.GetWinRule(difficulty)
            : offlineBotMatchRewardsConfig.GetLossRule(difficulty);
    }

    private PlayerID ResolveWinner()
    {
        if (offlineBotMatchController == null)
            return PlayerID.None;

        return offlineBotMatchController.Winner;
    }

    private int ComputeLevelFromTotalXp(int totalXp)
    {
        int safeTotalXp = Mathf.Max(0, totalXp);

        if (progressionRules == null)
            return profileManager != null && profileManager.ActiveProfile != null
                ? Mathf.Max(1, profileManager.ActiveProfile.level)
                : 1;

        int level = 1;
        int remainingXp = safeTotalXp;
        int safety = 0;

        while (safety < 10000)
        {
            int required = Mathf.Max(1, progressionRules.GetXpRequiredToAdvanceFromLevel(level));
            if (remainingXp < required)
                break;

            remainingXp -= required;
            level++;
            safety++;
        }

        return Mathf.Max(1, level);
    }

    private float CalculateLevelProgress01(int totalXp)
    {
        if (progressionRules == null)
            return 0f;

        int safeXp = Mathf.Max(0, totalXp);
        int intoLevel = progressionRules.GetXpIntoCurrentLevel(safeXp);
        int needed = Mathf.Max(1, progressionRules.GetXpNeededForNextLevel(safeXp));
        return Mathf.Clamp01((float)intoLevel / needed);
    }

    private void ResolveDependencies()
    {
#if UNITY_2023_1_OR_NEWER
        if (offlineBotMatchController == null)
            offlineBotMatchController = FindFirstObjectByType<OfflineBotMatchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindFirstObjectByType<OnlineMatchPresentationResultStore>();

        if (postMatchPanelsController == null)
            postMatchPanelsController = FindFirstObjectByType<OnlinePostMatchPanelsController>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();

        if (rewardManager == null)
            rewardManager = FindFirstObjectByType<RewardManager>();

        if (progressionRules == null)
            progressionRules = FindFirstObjectByType<PlayerProgressionRules>();
#else
        if (offlineBotMatchController == null)
            offlineBotMatchController = FindObjectOfType<OfflineBotMatchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindObjectOfType<OnlineMatchPresentationResultStore>();

        if (postMatchPanelsController == null)
            postMatchPanelsController = FindObjectOfType<OnlinePostMatchPanelsController>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();

        if (rewardManager == null)
            rewardManager = FindObjectOfType<RewardManager>();

        if (progressionRules == null)
            progressionRules = FindObjectOfType<PlayerProgressionRules>();
#endif

        if (presentationResultStore == null)
            presentationResultStore = OnlineMatchPresentationResultStore.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (onlineMatchRewardsConfig == null && OnlineFlowController.Instance != null)
            onlineMatchRewardsConfig = OnlineFlowController.Instance.RewardsConfig;

        if (offlineBotMatchController != null && offlineBotMatchController.ActiveRequest != null)
        {
            string requestId = offlineBotMatchController.ActiveRequest.RequestId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(requestId) &&
                requestId != lastProcessedOfflineRequestId &&
                !offlineBotMatchController.MatchEnded)
            {
                resultApplied = false;
            }
        }
    }
}