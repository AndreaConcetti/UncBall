
using TMPro;
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

    [Header("Progression Policy")]
    [SerializeField] private bool allowChestRewards = false;
    [SerializeField] private bool allowXpRewards = false;
    [SerializeField] private bool allowStatsProgression = false;

    [Header("Level Up Bonus")]
    [SerializeField] private int levelUpBonusSoftCurrencyPerLevel = 120;
    [SerializeField] private bool grantBonusChestOnLevelUp = true;
    [SerializeField] private int levelUpBonusChestCountPerLevel = 1;
    [SerializeField] private ChestType levelUpBonusChestType = ChestType.Random;

    [Header("Optional Reward UI")]
    [SerializeField] private TMP_Text rewardXpText;
    [SerializeField] private TMP_Text rewardSoftCurrencyText;
    [SerializeField] private TMP_Text rewardChestText;
    [SerializeField] private TMP_Text rewardRankedLpText;

    [Header("Main Endgame Buttons")]
    [SerializeField] private Button requestRematchButton;
    [SerializeField] private Button backHomeButton;

    [Header("Rematch Request Sent Panel")]
    [SerializeField] private GameObject rematchRequestSentPanel;
    [SerializeField] private TMP_Text rematchRequestSentOpponentNameText;
    [SerializeField] private TMP_Text rematchRequestSentStatusText;
    [SerializeField] private Button cancelRequestButton;

    [Header("Rematch Request Arrived Panel")]
    [SerializeField] private GameObject rematchRequestArrivedPanel;
    [SerializeField] private TMP_Text rematchRequestArrivedOpponentNameText;
    [SerializeField] private TMP_Text rematchRequestArrivedStatusText;
    [SerializeField] private Button acceptRequestButton;
    [SerializeField] private Button declineRequestButton;

    [Header("Optional Legacy Chest Hooks")]
    [SerializeField] private UnityEvent onNormalChestRewardRequested;
    [SerializeField] private UnityEvent onRankedChestRewardRequested;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool rewardsApplied;
    private int lastConsumedRematchNonce = -1;
    private bool exitTriggered;

    private int lastGrantedXp;
    private int lastGrantedSoftCurrency;
    private int lastGrantedChestCount;
    private int lastGrantedRankedLp;

    private void Awake()
    {
        ResolveDependencies();
        ApplyRuntimeRewardFlagsIfAvailable();
        HideAllRematchPanels();
        ClearRewardTexts();
        RefreshAllVisualState();
        RefreshButtonInteractableState();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Awake -> " +
                "MatchController=" + SafeName(matchController) +
                " | OnlineFlowController=" + SafeName(onlineFlowController) +
                " | ProfileManager=" + SafeName(profileManager) +
                " | RewardManager=" + SafeName(rewardManager) +
                " | RewardsConfig=" + SafeName(rewardsConfig) +
                " | ProgressionRules=" + SafeName(progressionRules) +
                " | PresentationStore=" + SafeName(presentationResultStore),
                this
            );
        }
    }

    private void Update()
    {
        ResolveDependencies();

        if (exitTriggered)
        {
            RefreshButtonInteractableState();
            return;
        }

        bool matchEnded = (matchController != null) && matchController.MatchEnded;

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
        bool canRequest = rematchController != null && matchEnded && CanUseRematch() && rematchController.CanLocalRequestRematch();

        if (requestRematchButton != null)
            requestRematchButton.interactable = !exitTriggered && !returningToMenu && canRequest;

        if (cancelRequestButton != null)
            cancelRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                CanUseRematch() &&
                rematchController != null &&
                rematchController.HasOutgoingRequestFromLocalPlayer();

        if (acceptRequestButton != null)
            acceptRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                CanUseRematch() &&
                rematchController != null &&
                rematchController.HasIncomingRequestForLocalPlayer();

        if (declineRequestButton != null)
            declineRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                CanUseRematch() &&
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
        {
            Debug.LogWarning(
                "[OnlineMatchEndFlow] ApplyEndMatchRewardsIfNeeded aborted -> " +
                "ProfileManager=" + (profileManager != null) +
                " | MatchController=" + (matchController != null) +
                " | RewardsConfig=" + (rewardsConfig != null),
                this
            );
            return;
        }

        QueueType queueType = ResolveQueueType();
        MatchMode matchMode = ResolveMatchMode();
        OnlineRewardCategory category = ResolveRewardCategory();
        OnlineRewardRule rule = rewardsConfig.GetRule(category, queueType);

        int startLevel = profileManager.ActiveProfile != null ? Mathf.Max(1, profileManager.ActiveProfile.level) : 1;
        int startTotalXp = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : 0;
        int startRankedLp = profileManager.ActiveRankedLp;

        bool localWon = category == OnlineRewardCategory.NormalCompletionWin ||
                        category == OnlineRewardCategory.DisconnectWin ||
                        category == OnlineRewardCategory.SurrenderWin;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Preparing rewards -> " +
                "Category=" + category +
                " | QueueType=" + queueType +
                " | MatchMode=" + matchMode +
                " | StartLevel=" + startLevel +
                " | StartXp=" + startTotalXp +
                " | StartRankedLp=" + startRankedLp +
                " | LocalWon=" + localWon +
                " | Rule.MatchPlayed=" + rule.countAsMatchPlayed +
                " | Rule.Win=" + rule.countAsWin +
                " | Rule.Loss=" + rule.countAsLoss +
                " | Rule.Xp=" + rule.xpReward +
                " | Rule.Soft=" + rule.softCurrencyReward +
                " | Rule.GrantChest=" + rule.grantChest +
                " | Rule.RankedLpDelta=" + rule.rankedLpDelta,
                this
            );
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

        if (allowStatsProgression && rewardResult.CountAsMatchPlayed)
        {
            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] RegisterMatchResult -> " +
                    "CountAsWin=" + rewardResult.CountAsWin +
                    " | Ranked=" + (queueType == QueueType.Ranked),
                    this
                );
            }

            profileManager.RegisterMatchResult(
                PlayerMatchCategory.OnlineMultiplayer,
                matchMode,
                rewardResult.CountAsWin,
                queueType == QueueType.Ranked
            );

            if (logDebug && profileManager.ActiveProfile != null)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Profile after RegisterMatchResult -> " +
                    "TotalMatches=" + profileManager.ActiveProfile.totalMatchesPlayed +
                    " | TotalWins=" + profileManager.ActiveProfile.totalWins +
                    " | MultiplayerMatches=" + profileManager.ActiveProfile.multiplayerMatchesPlayed +
                    " | MultiplayerWins=" + profileManager.ActiveProfile.multiplayerWins +
                    " | RankedMatches=" + profileManager.ActiveProfile.rankedMatchesPlayed +
                    " | RankedWins=" + profileManager.ActiveProfile.rankedWins,
                    this
                );
            }
        }
        else if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] RegisterMatchResult skipped -> " +
                "AllowStatsProgression=" + allowStatsProgression +
                " | CountAsMatchPlayed=" + rewardResult.CountAsMatchPlayed,
                this
            );
        }

        if (queueType == QueueType.Ranked && rewardResult.RankedLpDelta != 0)
        {
            profileManager.AddRankedLp(rewardResult.RankedLpDelta);
            lastGrantedRankedLp = rewardResult.RankedLpDelta;

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Ranked LP applied -> " +
                    "Delta=" + rewardResult.RankedLpDelta +
                    " | NewRankedLp=" + profileManager.ActiveRankedLp,
                    this
                );
            }
        }
        else if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Ranked LP skipped -> " +
                "QueueType=" + queueType +
                " | LpDelta=" + rewardResult.RankedLpDelta,
                this
            );
        }

        if (allowXpRewards && rewardResult.XpReward > 0)
        {
            lastGrantedXp = Mathf.Max(0, rewardResult.XpReward);
            profileManager.AddXp(lastGrantedXp);

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] XP applied -> " +
                    "Granted=" + lastGrantedXp +
                    " | NewXp=" + (profileManager.ActiveProfile != null ? profileManager.ActiveProfile.xp : -1) +
                    " | NewLevel=" + (profileManager.ActiveProfile != null ? profileManager.ActiveProfile.level : -1),
                    this
                );
            }
        }
        else if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] XP skipped -> " +
                "AllowXpRewards=" + allowXpRewards +
                " | RuleXp=" + rewardResult.XpReward,
                this
            );
        }

        lastGrantedSoftCurrency = Mathf.Max(0, rewardResult.SoftCurrencyReward);
        if (lastGrantedSoftCurrency > 0)
        {
            profileManager.AddSoftCurrency(lastGrantedSoftCurrency);

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Soft currency applied -> " +
                    "Granted=" + lastGrantedSoftCurrency,
                    this
                );
            }
        }

        if (allowChestRewards && rewardResult.GrantChest)
        {
            bool granted = false;

            if (rewardManager != null)
            {
                granted = rewardManager.GrantChestReward(
                    rewardResult.ChestType,
                    queueType == QueueType.Ranked ? "online_ranked_match_reward" : "online_normal_match_reward",
                    category.ToString(),
                    matchController.Winner
                );
            }

            if (granted)
            {
                lastGrantedChestCount += 1;
            }
            else
            {
                if (queueType == QueueType.Ranked)
                    onRankedChestRewardRequested?.Invoke();
                else
                    onNormalChestRewardRequested?.Invoke();
            }

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Chest reward processed -> " +
                    "Requested=" + rewardResult.GrantChest +
                    " | Granted=" + granted +
                    " | ChestType=" + rewardResult.ChestType +
                    " | TotalChestCount=" + lastGrantedChestCount,
                    this
                );
            }
        }
        else if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Chest reward skipped -> " +
                "AllowChestRewards=" + allowChestRewards +
                " | RuleGrantChest=" + rewardResult.GrantChest,
                this
            );
        }

        int endLevelAfterBase = profileManager.ActiveProfile != null ? Mathf.Max(1, profileManager.ActiveProfile.level) : startLevel;
        int endTotalXpAfterBase = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : startTotalXp;

        int levelUpCount = Mathf.Max(0, endLevelAfterBase - startLevel);
        int levelUpBonusSoft = 0;
        int levelUpBonusChestCount = 0;

        if (levelUpCount > 0)
        {
            levelUpBonusSoft = Mathf.Max(0, levelUpBonusSoftCurrencyPerLevel) * levelUpCount;

            if (levelUpBonusSoft > 0)
            {
                profileManager.AddSoftCurrency(levelUpBonusSoft);
                lastGrantedSoftCurrency += levelUpBonusSoft;
            }

            if (grantBonusChestOnLevelUp)
            {
                int requestedCount = Mathf.Max(0, levelUpBonusChestCountPerLevel) * levelUpCount;

                for (int i = 0; i < requestedCount; i++)
                {
                    bool granted = rewardManager != null && rewardManager.GrantChestReward(
                        levelUpBonusChestType,
                        "level_up_bonus",
                        "match_rewards_level_up_bonus",
                        matchController.Winner
                    );

                    if (granted)
                    {
                        levelUpBonusChestCount += 1;
                        lastGrantedChestCount += 1;
                    }
                }
            }

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Level up bonus applied -> " +
                    "LevelUpCount=" + levelUpCount +
                    " | BonusSoft=" + levelUpBonusSoft +
                    " | BonusChestCount=" + levelUpBonusChestCount,
                    this
                );
            }
        }
        else if (logDebug)
        {
            Debug.Log("[OnlineMatchEndFlow] No level up detected.", this);
        }

        int endLevel = profileManager.ActiveProfile != null ? Mathf.Max(1, profileManager.ActiveProfile.level) : endLevelAfterBase;
        int endTotalXp = profileManager.ActiveProfile != null ? Mathf.Max(0, profileManager.ActiveProfile.xp) : endTotalXpAfterBase;
        int endRankedLp = profileManager.ActiveRankedLp;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Final profile snapshot -> " +
                "EndLevel=" + endLevel +
                " | EndXp=" + endTotalXp +
                " | EndRankedLp=" + endRankedLp +
                " | LastGrantedXp=" + lastGrantedXp +
                " | LastGrantedSoft=" + lastGrantedSoftCurrency +
                " | LastGrantedChestCount=" + lastGrantedChestCount +
                " | LastGrantedLp=" + lastGrantedRankedLp,
                this
            );
        }

        RefreshRewardTexts();

        if (presentationResultStore != null)
        {
            OnlineMatchPresentationResult result = BuildPresentationResult(
                category,
                startLevel,
                endLevel,
                startTotalXp,
                endTotalXp,
                startRankedLp,
                endRankedLp,
                levelUpCount,
                levelUpBonusSoft,
                levelUpBonusChestCount
            );

            if (logDebug)
            {
                Debug.Log(
                    "[OnlineMatchEndFlow] Saving presentation result -> " +
                    "HasData=" + result.hasData +
                    " | Title=" + result.titleText +
                    " | PlayerName=" + result.playerName +
                    " | StartLevel=" + result.startLevel +
                    " | EndLevel=" + result.endLevel +
                    " | RankedLpDelta=" + result.rankedLpDelta +
                    " | NewRankedLpTotal=" + result.newRankedLpTotal +
                    " | TotalSoft=" + result.totalSoftCurrencyGained +
                    " | TotalChestCount=" + result.totalChestCount +
                    " | LeveledUp=" + result.leveledUp,
                    this
                );
            }

            presentationResultStore.SetLatest(result);

            if (logDebug)
                Debug.Log("[OnlineMatchEndFlow] Presentation result saved.", this);
        }
        else
        {
            Debug.LogWarning("[OnlineMatchEndFlow] presentationResultStore is NULL, result not saved.", this);
        }

        rewardsApplied = true;

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchEnded();
    }

    private OnlineMatchPresentationResult BuildPresentationResult(
        OnlineRewardCategory category,
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
        OnlineMatchPresentationResult result = new OnlineMatchPresentationResult();
        result.hasData = true;
        result.isDraw = category == OnlineRewardCategory.Draw;
        result.isVictory =
            category == OnlineRewardCategory.NormalCompletionWin ||
            category == OnlineRewardCategory.DisconnectWin ||
            category == OnlineRewardCategory.SurrenderWin;

        result.isDefeat =
            category == OnlineRewardCategory.NormalCompletionLoss ||
            category == OnlineRewardCategory.DisconnectLoss ||
            category == OnlineRewardCategory.SurrenderLoss ||
            category == OnlineRewardCategory.ReconnectTimeoutLoss;

        result.titleText = result.isDraw ? "DRAW" : (result.isVictory ? "VICTORY" : "DEFEAT");
        result.playerName = profileManager != null ? profileManager.ActiveDisplayName : "PLAYER";
        result.startLevel = Mathf.Max(1, startLevel);
        result.endLevel = Mathf.Max(1, endLevel);
        result.levelText = "LV " + result.endLevel;
        result.startTotalXp = Mathf.Max(0, startTotalXp);
        result.endTotalXp = Mathf.Max(0, endTotalXp);
        result.grantedXp = Mathf.Max(0, lastGrantedXp);
        result.startLevelProgress01 = CalculateLevelProgress01(startTotalXp);
        result.endLevelProgress01 = CalculateLevelProgress01(endTotalXp);
        result.rankedLpDelta = lastGrantedRankedLp;
        result.newRankedLpTotal = Mathf.Max(0, endRankedLp);
        result.totalSoftCurrencyGained = Mathf.Max(0, lastGrantedSoftCurrency);
        result.totalChestCount = Mathf.Max(0, lastGrantedChestCount);
        result.leveledUp = levelUpCount > 0;
        result.levelUpCount = Mathf.Max(0, levelUpCount);
        result.levelUpBonusSoftCurrency = Mathf.Max(0, levelUpBonusSoft);
        result.levelUpBonusChestCount = Mathf.Max(0, levelUpBonusChestCount);
        result.overlayTitleText = "LEVEL UP!";
        result.rewardSummaryText = BuildSummaryText(result, startRankedLp);

        return result;
    }

    private string BuildSummaryText(OnlineMatchPresentationResult result, int startRankedLp)
    {
        string summary = string.Empty;

        if (result.rankedLpDelta != 0)
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
            summary = "TOTAL LP: " + startRankedLp + " -> " + result.newRankedLpTotal;

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
                return localWon
                    ? OnlineRewardCategory.NormalCompletionWin
                    : OnlineRewardCategory.NormalCompletionLoss;

            case FusionOnlineMatchController.MatchEndReason.OpponentDisconnected:
            case FusionOnlineMatchController.MatchEndReason.LocalDisconnected:
                return localWon
                    ? OnlineRewardCategory.DisconnectWin
                    : OnlineRewardCategory.DisconnectLoss;

            case FusionOnlineMatchController.MatchEndReason.OpponentSurrendered:
            case FusionOnlineMatchController.MatchEndReason.LocalSurrendered:
                return localWon
                    ? OnlineRewardCategory.SurrenderWin
                    : OnlineRewardCategory.SurrenderLoss;

            case FusionOnlineMatchController.MatchEndReason.ReconnectTimeout:
                return OnlineRewardCategory.ReconnectTimeoutLoss;

            case FusionOnlineMatchController.MatchEndReason.MatchCancelled:
                return OnlineRewardCategory.MatchCancelled;

            case FusionOnlineMatchController.MatchEndReason.None:
            default:
                return OnlineRewardCategory.None;
        }
    }

    private void RefreshRewardTexts()
    {
        if (rewardXpText != null)
            rewardXpText.text = "+" + lastGrantedXp + " XP";

        if (rewardSoftCurrencyText != null)
            rewardSoftCurrencyText.text = "+" + lastGrantedSoftCurrency;

        if (rewardChestText != null)
            rewardChestText.text = lastGrantedChestCount > 0 ? "X" + lastGrantedChestCount : string.Empty;

        if (rewardRankedLpText != null)
            rewardRankedLpText.text = (lastGrantedRankedLp >= 0 ? "+" : string.Empty) + lastGrantedRankedLp + " LP";
    }

    private void ClearRewardTexts()
    {
        if (rewardXpText != null)
            rewardXpText.text = string.Empty;

        if (rewardSoftCurrencyText != null)
            rewardSoftCurrencyText.text = string.Empty;

        if (rewardChestText != null)
            rewardChestText.text = string.Empty;

        if (rewardRankedLpText != null)
            rewardRankedLpText.text = string.Empty;
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

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Runtime reward flags -> " +
                "AllowChestRewards=" + allowChestRewards +
                " | AllowXpRewards=" + allowXpRewards +
                " | AllowStatsProgression=" + allowStatsProgression,
                this
            );
        }
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

    private string SafeName(Object obj)
    {
        return obj == null ? "<null>" : obj.name;
    }
}
