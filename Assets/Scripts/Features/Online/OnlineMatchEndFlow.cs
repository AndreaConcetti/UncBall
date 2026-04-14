using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public struct OnlineMatchRewardResult
{
    public bool CountAsMatchPlayed;
    public bool CountAsWin;
    public bool CountAsLoss;
    public int XpReward;
    public int SoftCurrencyReward;
    public bool GrantChest;
    public ChestType ChestType;
    public int RankedLpDelta;
}

public class OnlineMatchEndFlow : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private FusionOnlineRematchController rematchController;
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private OnlineMatchRewardsConfig rewardsConfig;
    [SerializeField] private PlayerProgressionRules progressionRules;
    [SerializeField] private OnlineMatchPresentationResultStore presentationResultStore;
    [SerializeField] private LevelUpRewardsConfig levelUpRewardsConfig;

    [Header("Progression Policy")]
    [SerializeField] private bool allowChestRewards = false;
    [SerializeField] private bool allowXpRewards = false;
    [SerializeField] private bool allowStatsProgression = false;

    [Header("Rematch Policy")]
    [SerializeField] private bool allowRematchInRanked = false;
    [SerializeField] private bool hideRematchButtonWhenUnavailable = true;

    [Header("Main Endgame Buttons")]
    [SerializeField] private Button requestRematchButton;
    [SerializeField] private Button backHomeButton;

    [Header("Rematch Request Sent Panel")]
    [SerializeField] private GameObject rematchRequestSentPanel;
    [SerializeField] private TMPro.TMP_Text rematchRequestSentOpponentNameText;
    [SerializeField] private TMPro.TMP_Text rematchRequestSentStatusText;
    [SerializeField] private Button cancelRequestButton;

    [Header("Rematch Request Arrived Panel")]
    [SerializeField] private GameObject rematchRequestArrivedPanel;
    [SerializeField] private TMPro.TMP_Text rematchRequestArrivedOpponentNameText;
    [SerializeField] private TMPro.TMP_Text rematchRequestArrivedStatusText;
    [SerializeField] private Button acceptRequestButton;
    [SerializeField] private Button declineRequestButton;

    [Header("Optional Legacy Chest Hooks")]
    [SerializeField] private UnityEvent onNormalChestRewardRequested;
    [SerializeField] private UnityEvent onRankedChestRewardRequested;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private readonly List<LevelRewardEntry> cachedLevelRewards = new List<LevelRewardEntry>();

    private bool rewardsApplied;
    private int lastConsumedRematchNonce = -1;
    private bool exitTriggered;

    private int lastGrantedXp;
    private int lastGrantedSoftCurrency;
    private int lastGrantedChestCount;
    private int lastGrantedRankedLp;
    private ChestType lastGrantedChestType = ChestType.Random;
    private ChestType lastLevelUpBonusChestType = ChestType.Random;

    private void Awake()
    {
        ResolveDependencies();
        ApplyRuntimeRewardFlagsIfAvailable();
        HideAllRematchPanels();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    private void Update()
    {
        ResolveDependencies();

        if (exitTriggered)
        {
            RefreshButtonInteractableState();
            return;
        }

        bool matchEnded = matchController != null && matchController.MatchEnded;

        if (matchEnded)
            ApplyEndMatchRewardsIfNeeded();

        if (matchEnded && CanUseRematch())
            HandleRematchVisualRefresh();
        else
            HideAllRematchPanels();

        RefreshButtonInteractableState();
    }

    public void OnPressReturnHome()
    {
        if (exitTriggered)
            return;

        exitTriggered = true;
        HideAllRematchPanels();
        RefreshButtonInteractableState();

        if (onlineFlowController != null)
            onlineFlowController.ReturnToMenuFromMatch(true);
    }

    public void OnPressRequestRematch()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null || !CanUseRematch())
            return;

        rematchController.RequestLocalRematch();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressCancelRequest()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null || !CanUseRematch())
            return;

        rematchController.CancelLocalRematchRequest();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressAcceptRematch()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null || !CanUseRematch())
            return;

        rematchController.AcceptLocalRematch();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressDeclineRematch()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null || !CanUseRematch())
            return;

        rematchController.DeclineLocalRematch();
        HideAllRematchPanels();
        RefreshButtonInteractableState();

        if (onlineFlowController != null)
        {
            exitTriggered = true;
            onlineFlowController.ReturnToMenuFromMatch(true);
        }
    }

    private bool CanUseRematch()
    {
        if (matchController == null || !matchController.MatchEnded)
            return false;

        QueueType queueType = ResolveQueueType();
        if (queueType == QueueType.Ranked && !allowRematchInRanked)
            return false;

        OnlineRewardCategory category = ResolveRewardCategory();
        return category == OnlineRewardCategory.NormalCompletionWin ||
               category == OnlineRewardCategory.NormalCompletionLoss ||
               category == OnlineRewardCategory.Draw;
    }

    private void HandleRematchVisualRefresh()
    {
        if (rematchController == null || matchController == null || !matchController.MatchEnded)
            return;

        if (lastConsumedRematchNonce != rematchController.RematchNonce)
        {
            lastConsumedRematchNonce = rematchController.RematchNonce;
            RefreshAllVisualState();
        }

        switch (rematchController.State)
        {
            case FusionOnlineRematchController.RematchState.Pending:
                if (rematchController.HasOutgoingRequestFromLocalPlayer())
                    ShowRequestSentPanel(rematchController.OpponentDisplayName, "WAITING FOR OPPONENT...");
                else if (rematchController.HasIncomingRequestForLocalPlayer())
                    ShowIncomingRequestPanel(rematchController.RequesterDisplayName, "REQUESTED TO PLAY AGAIN!");
                break;

            case FusionOnlineRematchController.RematchState.Accepted:
                ShowRequestSentPanel(rematchController.OpponentDisplayName, "REMATCH ACCEPTED!");
                break;

            case FusionOnlineRematchController.RematchState.Declined:
                if (!exitTriggered && onlineFlowController != null)
                {
                    exitTriggered = true;
                    onlineFlowController.ReturnToMenuFromMatch(true);
                }
                break;

            default:
                HideAllRematchPanels();
                break;
        }
    }

    private void RefreshAllVisualState()
    {
        bool matchEnded = matchController != null && matchController.MatchEnded;

        if (!matchEnded || rematchController == null || !CanUseRematch())
        {
            HideAllRematchPanels();
            return;
        }

        switch (rematchController.State)
        {
            case FusionOnlineRematchController.RematchState.Pending:
                if (rematchController.HasOutgoingRequestFromLocalPlayer())
                    ShowRequestSentPanel(rematchController.OpponentDisplayName, "WAITING FOR OPPONENT...");
                else if (rematchController.HasIncomingRequestForLocalPlayer())
                    ShowIncomingRequestPanel(rematchController.RequesterDisplayName, "REQUESTED TO PLAY AGAIN!");
                else
                    HideAllRematchPanels();
                break;

            case FusionOnlineRematchController.RematchState.Accepted:
                ShowRequestSentPanel(rematchController.OpponentDisplayName, "REMATCH ACCEPTED!");
                break;

            default:
                HideAllRematchPanels();
                break;
        }
    }

    private void RefreshButtonInteractableState()
    {
        bool returningToMenu =
            onlineFlowController != null &&
            onlineFlowController.CurrentState == OnlineFlowState.ReturningToMenu;

        bool matchEnded = matchController != null && matchController.MatchEnded;
        bool canUseRematch = CanUseRematch();
        bool canRequest = rematchController != null && matchEnded && canUseRematch && rematchController.CanLocalRequestRematch();

        if (requestRematchButton != null)
        {
            if (hideRematchButtonWhenUnavailable)
                requestRematchButton.gameObject.SetActive(canUseRematch);
            else
                requestRematchButton.gameObject.SetActive(true);

            requestRematchButton.interactable = !exitTriggered && !returningToMenu && canRequest;
        }

        if (cancelRequestButton != null)
            cancelRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                canUseRematch &&
                rematchController != null &&
                rematchController.HasOutgoingRequestFromLocalPlayer();

        if (acceptRequestButton != null)
            acceptRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                canUseRematch &&
                rematchController != null &&
                rematchController.HasIncomingRequestForLocalPlayer();

        if (declineRequestButton != null)
            declineRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                canUseRematch &&
                rematchController != null &&
                rematchController.HasIncomingRequestForLocalPlayer();

        if (backHomeButton != null)
            backHomeButton.interactable = !exitTriggered && !returningToMenu;
    }

    private void ShowRequestSentPanel(string opponentName, string statusText)
    {
        if (rematchRequestSentPanel != null)
            rematchRequestSentPanel.SetActive(true);

        if (rematchRequestArrivedPanel != null)
            rematchRequestArrivedPanel.SetActive(false);

        if (rematchRequestSentOpponentNameText != null)
            rematchRequestSentOpponentNameText.text = string.IsNullOrWhiteSpace(opponentName)
                ? "OPPONENT"
                : opponentName.ToUpperInvariant();

        if (rematchRequestSentStatusText != null)
            rematchRequestSentStatusText.text = statusText;
    }

    private void ShowIncomingRequestPanel(string opponentName, string statusText)
    {
        if (rematchRequestSentPanel != null)
            rematchRequestSentPanel.SetActive(false);

        if (rematchRequestArrivedPanel != null)
            rematchRequestArrivedPanel.SetActive(true);

        if (rematchRequestArrivedOpponentNameText != null)
            rematchRequestArrivedOpponentNameText.text = string.IsNullOrWhiteSpace(opponentName)
                ? "OPPONENT"
                : opponentName.ToUpperInvariant();

        if (rematchRequestArrivedStatusText != null)
            rematchRequestArrivedStatusText.text = statusText;
    }

    private void HideAllRematchPanels()
    {
        if (rematchRequestSentPanel != null)
            rematchRequestSentPanel.SetActive(false);

        if (rematchRequestArrivedPanel != null)
            rematchRequestArrivedPanel.SetActive(false);
    }

    private void ApplyEndMatchRewardsIfNeeded()
    {
        if (rewardsApplied)
            return;

        ResolveDependencies();
        ApplyRuntimeRewardFlagsIfAvailable();

        if (profileManager == null || matchController == null || rewardsConfig == null)
            return;

        string currentMatchId = ResolveCurrentMatchId();

        if (presentationResultStore != null &&
            !string.IsNullOrWhiteSpace(currentMatchId) &&
            presentationResultStore.HasProcessedMatch(currentMatchId))
        {
            rewardsApplied = true;

            if (logDebug)
                Debug.LogWarning("[OnlineMatchEndFlow] Reward application skipped because this match was already processed: " + currentMatchId, this);

            return;
        }

        QueueType queueType = ResolveQueueType();
        MatchMode matchMode = ResolveMatchMode();
        OnlineRewardCategory category = ResolveRewardCategory();
        OnlineRewardRule rule = rewardsConfig.GetRule(category, queueType);

        int startTotalXp = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : 0;
        int startRankedLp = profileManager.ActiveRankedLp;
        int effectiveStartLevel = ComputeLevelFromTotalXp(startTotalXp);
        int startXpIntoLevel = progressionRules != null ? progressionRules.GetXpIntoCurrentLevel(startTotalXp) : 0;
        int startXpNeededForNext = progressionRules != null ? progressionRules.GetXpNeededForNextLevel(startTotalXp) : 0;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Before rewards -> " +
                "MatchId=" + currentMatchId +
                " | EffectiveStartLevel=" + effectiveStartLevel +
                " | StartTotalXp=" + startTotalXp +
                " | StartXpIntoLevel=" + startXpIntoLevel +
                " | StartXpNeededForNext=" + startXpNeededForNext +
                " | StartRankedLp=" + startRankedLp,
                this);
        }

        OnlineMatchRewardResult rewardResult = new OnlineMatchRewardResult
        {
            CountAsMatchPlayed = rule.countAsMatchPlayed,
            CountAsWin = rule.countAsWin,
            CountAsLoss = rule.countAsLoss,
            XpReward = rule.xpReward,
            SoftCurrencyReward = rule.softCurrencyReward,
            GrantChest = rule.grantChest,
            ChestType = rule.chestType,
            RankedLpDelta = queueType == QueueType.Ranked ? rule.rankedLpDelta : 0
        };

        lastGrantedXp = 0;
        lastGrantedSoftCurrency = 0;
        lastGrantedChestCount = 0;
        lastGrantedRankedLp = 0;
        lastGrantedChestType = ChestType.Random;
        lastLevelUpBonusChestType = ChestType.Random;

        if (allowStatsProgression && rewardResult.CountAsMatchPlayed)
        {
            profileManager.RegisterMatchResult(
                PlayerMatchCategory.OnlineMultiplayer,
                matchMode,
                rewardResult.CountAsWin,
                queueType == QueueType.Ranked
            );
        }

        if (queueType == QueueType.Ranked && rewardResult.RankedLpDelta != 0)
        {
            profileManager.AddRankedLp(rewardResult.RankedLpDelta);
            lastGrantedRankedLp = rewardResult.RankedLpDelta;
        }

        if (allowXpRewards && rewardResult.XpReward > 0)
        {
            lastGrantedXp = Mathf.Max(0, rewardResult.XpReward);
            profileManager.AddXp(lastGrantedXp);
        }

        lastGrantedSoftCurrency = Mathf.Max(0, rewardResult.SoftCurrencyReward);
        if (lastGrantedSoftCurrency > 0)
            profileManager.AddSoftCurrency(lastGrantedSoftCurrency);

        if (allowChestRewards && rewardResult.GrantChest)
        {
            int granted = TryGrantChest(rewardResult.ChestType, queueType, category.ToString(), queueType == QueueType.Ranked);
            if (granted > 0)
            {
                lastGrantedChestCount += granted;
                lastGrantedChestType = rewardResult.ChestType;
            }
        }

        int endTotalXpAfterBase = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : startTotalXp;
        int effectiveEndLevelAfterBase = ComputeLevelFromTotalXp(endTotalXpAfterBase);
        int endXpIntoLevelAfterBase = progressionRules != null ? progressionRules.GetXpIntoCurrentLevel(endTotalXpAfterBase) : 0;
        int endXpNeededAfterBase = progressionRules != null ? progressionRules.GetXpNeededForNextLevel(endTotalXpAfterBase) : 0;
        int levelUpCount = Mathf.Max(0, effectiveEndLevelAfterBase - effectiveStartLevel);

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] After base rewards -> " +
                "EffectiveEndLevelAfterBase=" + effectiveEndLevelAfterBase +
                " | EndTotalXpAfterBase=" + endTotalXpAfterBase +
                " | EndXpIntoLevelAfterBase=" + endXpIntoLevelAfterBase +
                " | EndXpNeededAfterBase=" + endXpNeededAfterBase +
                " | GrantedXp=" + lastGrantedXp +
                " | ComputedLevelUpCount=" + levelUpCount,
                this);
        }

        int levelUpBonusSoft = 0;
        int levelUpBonusChestCount = 0;

        if (levelUpCount > 0)
        {
            if (levelUpRewardsConfig == null)
            {
                Debug.LogError("[LEVEL UP] levelUpRewardsConfig NULL", this);
            }
            else
            {
                cachedLevelRewards.Clear();

                levelUpRewardsConfig.GetRewardsBetweenLevels(
                    effectiveStartLevel,
                    effectiveEndLevelAfterBase,
                    cachedLevelRewards
                );

                Debug.Log("[LEVEL UP] Rewards found: " + cachedLevelRewards.Count, this);

                for (int i = 0; i < cachedLevelRewards.Count; i++)
                {
                    LevelRewardEntry entry = cachedLevelRewards[i];

                    Debug.Log(
                        "[LEVEL UP] Entry -> Level=" + entry.targetLevel +
                        " | Soft=" + entry.softCurrencyReward +
                        " | ChestCount=" + entry.chestCount +
                        " | Type=" + entry.chestType,
                        this
                    );

                    if (entry.softCurrencyReward > 0)
                    {
                        profileManager.AddSoftCurrency(entry.softCurrencyReward);

                        levelUpBonusSoft += entry.softCurrencyReward;
                        lastGrantedSoftCurrency += entry.softCurrencyReward;
                    }

                    for (int chestIndex = 0; chestIndex < Mathf.Max(0, entry.chestCount); chestIndex++)
                    {
                        int granted = TryGrantChest(
                            entry.chestType,
                            queueType,
                            "level_up_reward_level_" + entry.targetLevel,
                            false
                        );

                        if (granted > 0)
                        {
                            levelUpBonusChestCount += granted;
                            lastGrantedChestCount += granted;
                            lastLevelUpBonusChestType = entry.chestType;

                            if (lastGrantedChestType == ChestType.Random)
                                lastGrantedChestType = entry.chestType;
                        }
                    }
                }
            }
        }

        int endTotalXp = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : endTotalXpAfterBase;
        int effectiveEndLevel = ComputeLevelFromTotalXp(endTotalXp);
        int endRankedLp = profileManager.ActiveRankedLp;
        int endXpIntoLevel = progressionRules != null ? progressionRules.GetXpIntoCurrentLevel(endTotalXp) : 0;
        int endXpNeededForNext = progressionRules != null ? progressionRules.GetXpNeededForNextLevel(endTotalXp) : 0;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Final progression snapshot -> " +
                "EffectiveEndLevel=" + effectiveEndLevel +
                " | EndTotalXp=" + endTotalXp +
                " | EndXpIntoLevel=" + endXpIntoLevel +
                " | EndXpNeededForNext=" + endXpNeededForNext +
                " | LevelUpCount=" + levelUpCount +
                " | LevelUpBonusSoft=" + levelUpBonusSoft +
                " | LevelUpBonusChestCount=" + levelUpBonusChestCount +
                " | FinalChestType=" + lastGrantedChestType,
                this);
        }

        if (presentationResultStore != null)
        {
            OnlineMatchPresentationResult result = BuildPresentationResult(
                category,
                queueType,
                effectiveStartLevel,
                effectiveEndLevel,
                startTotalXp,
                endTotalXp,
                startRankedLp,
                endRankedLp,
                levelUpCount,
                levelUpBonusSoft,
                levelUpBonusChestCount);

            result.sourceMatchId = currentMatchId;
            presentationResultStore.SetLatest(result);
            presentationResultStore.MarkMatchProcessed(currentMatchId);
        }

        rewardsApplied = true;

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchEnded();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Rewards applied -> " +
                "MatchId=" + currentMatchId +
                " | Category=" + category +
                " | XP=" + lastGrantedXp +
                " | Soft=" + lastGrantedSoftCurrency +
                " | ChestCount=" + lastGrantedChestCount +
                " | LP=" + lastGrantedRankedLp +
                " | LevelUpCount=" + levelUpCount,
                this);
        }
    }

    private int ComputeLevelFromTotalXp(int totalXp)
    {
        int safeTotalXp = Mathf.Max(0, totalXp);

        if (progressionRules == null)
            return 1;

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

    private int TryGrantChest(ChestType chestType, QueueType queueType, string reason, bool invokeLegacyHookIfFailed)
    {
        bool granted = rewardManager != null && rewardManager.GrantChestReward(
            chestType,
            queueType == QueueType.Ranked ? "online_ranked_match_reward" : "online_normal_match_reward",
            reason,
            matchController != null ? matchController.Winner : PlayerID.None
        );

        if (granted)
            return 1;

        if (invokeLegacyHookIfFailed)
        {
            if (queueType == QueueType.Ranked)
                onRankedChestRewardRequested?.Invoke();
            else
                onNormalChestRewardRequested?.Invoke();
        }

        return 0;
    }

    private OnlineMatchPresentationResult BuildPresentationResult(
        OnlineRewardCategory category,
        QueueType queueType,
        int startLevel,
        int endLevel,
        int startTotalXp,
        int endTotalXp,
        int startRankedLp,
        int endRankedLp,
        int levelUpCount,
        int levelUpBonusSoft,
        int levelUpBonusChestCount)
    {
        OnlineMatchPresentationResult result = new OnlineMatchPresentationResult
        {
            hasData = true,
            isDraw = category == OnlineRewardCategory.Draw,
            isVictory =
                category == OnlineRewardCategory.NormalCompletionWin ||
                category == OnlineRewardCategory.DisconnectWin ||
                category == OnlineRewardCategory.SurrenderWin,
            isDefeat =
                category == OnlineRewardCategory.NormalCompletionLoss ||
                category == OnlineRewardCategory.DisconnectLoss ||
                category == OnlineRewardCategory.SurrenderLoss ||
                category == OnlineRewardCategory.ReconnectTimeoutLoss,
            isRanked = queueType == QueueType.Ranked,
            playerName = profileManager != null ? profileManager.ActiveDisplayName : "PLAYER",
            startLevel = Mathf.Max(1, startLevel),
            endLevel = Mathf.Max(1, endLevel),
            levelText = "LV " + Mathf.Max(1, endLevel),
            startTotalXp = Mathf.Max(0, startTotalXp),
            endTotalXp = Mathf.Max(0, endTotalXp),
            grantedXp = Mathf.Max(0, lastGrantedXp),
            startLevelProgress01 = CalculateLevelProgress01(startTotalXp),
            endLevelProgress01 = CalculateLevelProgress01(endTotalXp),
            rankedLpDelta = lastGrantedRankedLp,
            newRankedLpTotal = Mathf.Max(0, endRankedLp),
            totalSoftCurrencyGained = Mathf.Max(0, lastGrantedSoftCurrency),
            totalChestCount = Mathf.Max(0, lastGrantedChestCount),
            totalChestType = lastGrantedChestType,
            leveledUp = levelUpCount > 0,
            levelUpCount = Mathf.Max(0, levelUpCount),
            levelUpBonusSoftCurrency = Mathf.Max(0, levelUpBonusSoft),
            levelUpBonusChestCount = Mathf.Max(0, levelUpBonusChestCount),
            levelUpBonusChestType = lastLevelUpBonusChestType,
            overlayTitleText = "LEVEL UP!"
        };

        result.titleText = result.isDraw ? "DRAW" : (result.isVictory ? "VICTORY" : "DEFEAT");
        result.rewardSummaryText = BuildSummaryText(result, startRankedLp);
        return result;
    }

    private string BuildSummaryText(OnlineMatchPresentationResult result, int startRankedLp)
    {
        string summary = string.Empty;

        if (result.isRanked && result.rankedLpDelta != 0)
            summary += "LP: " + (result.rankedLpDelta > 0 ? "+" : string.Empty) + result.rankedLpDelta;

        if (result.grantedXp > 0)
        {
            if (!string.IsNullOrWhiteSpace(summary))
                summary += " | ";

            summary += "XP: +" + result.grantedXp;
        }

        if (result.leveledUp)
        {
            if (!string.IsNullOrWhiteSpace(summary))
                summary += " | ";

            summary += "LEVEL UP";
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            if (result.isRanked)
                summary = "TOTAL LP: " + startRankedLp + " -> " + result.newRankedLpTotal;
            else if (result.totalSoftCurrencyGained > 0)
                summary = "REWARDS OBTAINED";
            else
                summary = string.Empty;
        }

        return summary;
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

    private OnlineRewardCategory ResolveRewardCategory()
    {
        if (matchController == null)
            return OnlineRewardCategory.None;

        PlayerID localPlayerId = matchController.EffectiveLocalPlayerId;
        PlayerID winner = matchController.Winner;
        bool localWon = winner == localPlayerId;
        bool draw = winner == PlayerID.None;

        switch (matchController.EndReason)
        {
            case FusionOnlineMatchController.MatchEndReason.NormalCompletion:
                if (draw)
                    return OnlineRewardCategory.Draw;
                return localWon ? OnlineRewardCategory.NormalCompletionWin : OnlineRewardCategory.NormalCompletionLoss;

            case FusionOnlineMatchController.MatchEndReason.OpponentDisconnected:
            case FusionOnlineMatchController.MatchEndReason.LocalDisconnected:
                return localWon ? OnlineRewardCategory.DisconnectWin : OnlineRewardCategory.DisconnectLoss;

            case FusionOnlineMatchController.MatchEndReason.OpponentSurrendered:
            case FusionOnlineMatchController.MatchEndReason.LocalSurrendered:
                return localWon ? OnlineRewardCategory.SurrenderWin : OnlineRewardCategory.SurrenderLoss;

            case FusionOnlineMatchController.MatchEndReason.ReconnectTimeout:
                return OnlineRewardCategory.ReconnectTimeoutLoss;

            case FusionOnlineMatchController.MatchEndReason.MatchCancelled:
                return OnlineRewardCategory.MatchCancelled;

            default:
                return OnlineRewardCategory.None;
        }
    }

    private string ResolveCurrentMatchId()
    {
        if (onlineFlowController == null ||
            onlineFlowController.RuntimeContext == null ||
            onlineFlowController.RuntimeContext.currentSession == null)
        {
            return string.Empty;
        }

        MatchSessionContext session = onlineFlowController.RuntimeContext.currentSession;
        return session.matchId ?? string.Empty;
    }

    private void ResolveDependencies()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (rewardManager == null)
            rewardManager = RewardManager.Instance;

        if (progressionRules == null)
            progressionRules = PlayerProgressionRules.Instance;

        if (presentationResultStore == null)
            presentationResultStore = OnlineMatchPresentationResultStore.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchController == null)
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();

        if (rematchController == null)
            rematchController = FindFirstObjectByType<FusionOnlineRematchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindFirstObjectByType<OnlineMatchPresentationResultStore>();
#else
        if (matchController == null)
            matchController = FindObjectOfType<FusionOnlineMatchController>();

        if (rematchController == null)
            rematchController = FindObjectOfType<FusionOnlineRematchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindObjectOfType<OnlineMatchPresentationResultStore>();
#endif
    }

    private void ApplyRuntimeRewardFlagsIfAvailable()
    {
        if (onlineFlowController == null ||
            onlineFlowController.RuntimeContext == null ||
            onlineFlowController.RuntimeContext.currentSession == null)
        {
            return;
        }

        MatchSessionContext session = onlineFlowController.RuntimeContext.currentSession;
        allowChestRewards = session.allowChestRewards;
        allowXpRewards = session.allowXpRewards;
        allowStatsProgression = session.allowStatsProgression;
    }

    private QueueType ResolveQueueType()
    {
        if (onlineFlowController != null && onlineFlowController.RuntimeContext != null)
            return onlineFlowController.RuntimeContext.queueType;

        return QueueType.Normal;
    }

    private MatchMode ResolveMatchMode()
    {
        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return onlineFlowController.RuntimeContext.currentSession.matchMode;
        }

        return MatchMode.ScoreTarget;
    }
}