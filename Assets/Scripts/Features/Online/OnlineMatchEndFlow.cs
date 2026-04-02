using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OnlineMatchEndFlow : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private FusionOnlineRematchController rematchController;
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private RewardManager rewardManager;

    [Header("Progression Policy")]
    [SerializeField] private bool allowChestRewards = false;
    [SerializeField] private bool allowXpRewards = false;
    [SerializeField] private bool allowStatsProgression = false;

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

    [Header("Optional Reward UI")]
    [SerializeField] private TMP_Text rewardXpText;
    [SerializeField] private TMP_Text rewardSoftCurrencyText;
    [SerializeField] private TMP_Text rewardPremiumCurrencyText;
    [SerializeField] private TMP_Text rewardChestText;

    [Header("Rewards")]
    [SerializeField] private int normalWinXp = 25;
    [SerializeField] private int normalLoseXp = 10;
    [SerializeField] private int normalDrawXp = 15;
    [SerializeField] private int rankedWinXp = 60;
    [SerializeField] private int rankedLoseXp = 20;
    [SerializeField] private int rankedDrawXp = 30;

    [SerializeField] private int normalWinSoftCurrency = 20;
    [SerializeField] private int normalLoseSoftCurrency = 8;
    [SerializeField] private int normalDrawSoftCurrency = 12;

    [SerializeField] private int rankedWinSoftCurrency = 35;
    [SerializeField] private int rankedLoseSoftCurrency = 12;
    [SerializeField] private int rankedDrawSoftCurrency = 18;

    [SerializeField] private int rankedWinPremiumCurrency = 1;
    [SerializeField] private int rankedLosePremiumCurrency = 0;
    [SerializeField] private int rankedDrawPremiumCurrency = 0;

    [Header("Chest Rules")]
    [SerializeField] private ChestType normalWinChestType = ChestType.Random;
    [SerializeField] private ChestType rankedWinChestType = ChestType.GuaranteedRare;
    [SerializeField] private bool grantChestOnlyIfLocalPlayerWins = true;

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
    private int lastGrantedPremiumCurrency;
    private bool lastGrantedChest;
    private ChestType lastGrantedChestType = ChestType.Random;

    private void Awake()
    {
        ResolveDependencies();
        ApplyRuntimeRewardFlagsIfAvailable();
        HideAllRematchPanels();
        ClearRewardTexts();
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

        bool canReadMatch = matchController != null && matchController.IsNetworkStateReadable;
        bool matchEnded = canReadMatch && matchController.MatchEnded;

        if (matchEnded)
            ApplyEndMatchRewardsIfNeeded();

        if (matchEnded)
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

        if (exitTriggered || rematchController == null)
            return;

        rematchController.RequestLocalRematch();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressCancelRequest()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null)
            return;

        rematchController.CancelLocalRematchRequest();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressAcceptRematch()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null)
            return;

        rematchController.AcceptLocalRematch();
        RefreshAllVisualState();
        RefreshButtonInteractableState();
    }

    public void OnPressDeclineRematch()
    {
        ResolveDependencies();

        if (exitTriggered || rematchController == null)
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

    private void HandleRematchVisualRefresh()
    {
        if (rematchController == null || matchController == null || !matchController.IsNetworkStateReadable || !matchController.MatchEnded)
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
                {
                    ShowRequestSentPanel(
                        rematchController.OpponentDisplayName,
                        "WAITING FOR OPPONENT..."
                    );
                }
                else if (rematchController.HasIncomingRequestForLocalPlayer())
                {
                    ShowIncomingRequestPanel(
                        rematchController.RequesterDisplayName,
                        "REQUESTED TO PLAY AGAIN!"
                    );
                }
                break;

            case FusionOnlineRematchController.RematchState.Accepted:
                ShowRequestSentPanel(
                    rematchController.OpponentDisplayName,
                    "REMATCH ACCEPTED!"
                );
                break;

            case FusionOnlineRematchController.RematchState.Declined:
                if (!exitTriggered && onlineFlowController != null)
                {
                    exitTriggered = true;
                    onlineFlowController.ReturnToMenuFromMatch(true);
                }
                break;

            case FusionOnlineRematchController.RematchState.Cancelled:
            case FusionOnlineRematchController.RematchState.None:
            default:
                HideAllRematchPanels();
                break;
        }
    }

    private void RefreshAllVisualState()
    {
        bool matchEnded =
            matchController != null &&
            matchController.IsNetworkStateReadable &&
            matchController.MatchEnded;

        if (!matchEnded || rematchController == null)
        {
            HideAllRematchPanels();
            return;
        }

        switch (rematchController.State)
        {
            case FusionOnlineRematchController.RematchState.Pending:
                if (rematchController.HasOutgoingRequestFromLocalPlayer())
                {
                    ShowRequestSentPanel(
                        rematchController.OpponentDisplayName,
                        "WAITING FOR OPPONENT..."
                    );
                }
                else if (rematchController.HasIncomingRequestForLocalPlayer())
                {
                    ShowIncomingRequestPanel(
                        rematchController.RequesterDisplayName,
                        "REQUESTED TO PLAY AGAIN!"
                    );
                }
                else
                {
                    HideAllRematchPanels();
                }
                break;

            case FusionOnlineRematchController.RematchState.Accepted:
                ShowRequestSentPanel(
                    rematchController.OpponentDisplayName,
                    "REMATCH ACCEPTED!"
                );
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

        bool canReadMatch = matchController != null && matchController.IsNetworkStateReadable;
        bool matchEnded = canReadMatch && matchController.MatchEnded;
        bool canRequest = rematchController != null && matchEnded && rematchController.CanLocalRequestRematch();

        if (requestRematchButton != null)
            requestRematchButton.interactable = !exitTriggered && !returningToMenu && canRequest;

        if (cancelRequestButton != null)
            cancelRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                rematchController != null &&
                rematchController.HasOutgoingRequestFromLocalPlayer();

        if (acceptRequestButton != null)
            acceptRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
                rematchController != null &&
                rematchController.HasIncomingRequestForLocalPlayer();

        if (declineRequestButton != null)
            declineRequestButton.interactable =
                !exitTriggered &&
                !returningToMenu &&
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

        if (profileManager == null || matchController == null || !matchController.IsNetworkStateReadable)
            return;

        PlayerID localPlayerId = matchController.EffectiveLocalPlayerId;
        PlayerID winner = matchController.Winner;

        bool localWon = winner == localPlayerId;
        bool draw = winner == PlayerID.None;
        bool ranked = IsRankedMatch();
        MatchMode matchMode = ResolveMatchMode();

        lastGrantedXp = 0;
        lastGrantedSoftCurrency = 0;
        lastGrantedPremiumCurrency = 0;
        lastGrantedChest = false;
        lastGrantedChestType = ChestType.Random;

        if (allowStatsProgression)
        {
            profileManager.RegisterMatchResult(
                PlayerMatchCategory.OnlineMultiplayer,
                matchMode,
                localWon,
                ranked
            );
        }

        if (allowXpRewards)
        {
            lastGrantedXp = ranked
                ? (draw ? rankedDrawXp : (localWon ? rankedWinXp : rankedLoseXp))
                : (draw ? normalDrawXp : (localWon ? normalWinXp : normalLoseXp));

            if (lastGrantedXp > 0)
                profileManager.AddXp(lastGrantedXp);
        }

        lastGrantedSoftCurrency = ranked
            ? (draw ? rankedDrawSoftCurrency : (localWon ? rankedWinSoftCurrency : rankedLoseSoftCurrency))
            : (draw ? normalDrawSoftCurrency : (localWon ? normalWinSoftCurrency : normalLoseSoftCurrency));

        if (lastGrantedSoftCurrency > 0)
            profileManager.AddSoftCurrency(lastGrantedSoftCurrency);

        if (ranked)
        {
            lastGrantedPremiumCurrency = draw
                ? rankedDrawPremiumCurrency
                : (localWon ? rankedWinPremiumCurrency : rankedLosePremiumCurrency);

            if (lastGrantedPremiumCurrency > 0)
                profileManager.AddPremiumCurrency(lastGrantedPremiumCurrency);
        }

        if (allowChestRewards)
        {
            bool canGrantChest = !grantChestOnlyIfLocalPlayerWins || localWon;

            if (canGrantChest)
            {
                ChestType chestTypeToGrant = ranked ? rankedWinChestType : normalWinChestType;

                if (rewardManager != null)
                {
                    lastGrantedChest = rewardManager.GrantChestReward(
                        chestTypeToGrant,
                        ranked ? "online_ranked_match_win" : "online_normal_match_win",
                        localWon ? "local_player_win" : "configured_non_winner_reward",
                        winner
                    );
                }

                if (lastGrantedChest)
                {
                    lastGrantedChestType = chestTypeToGrant;
                }
                else
                {
                    if (ranked)
                        onRankedChestRewardRequested?.Invoke();
                    else
                        onNormalChestRewardRequested?.Invoke();
                }
            }
        }

        RefreshRewardTexts();

        rewardsApplied = true;

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchEnded();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchEndFlow] Rewards applied -> " +
                "LocalWon=" + localWon +
                " | Draw=" + draw +
                " | Ranked=" + ranked +
                " | Mode=" + matchMode +
                " | Xp=" + lastGrantedXp +
                " | Soft=" + lastGrantedSoftCurrency +
                " | Premium=" + lastGrantedPremiumCurrency +
                " | ChestGranted=" + lastGrantedChest +
                " | ChestType=" + lastGrantedChestType,
                this
            );
        }
    }

    private void RefreshRewardTexts()
    {
        if (rewardXpText != null)
            rewardXpText.text = "+" + lastGrantedXp + " XP";

        if (rewardSoftCurrencyText != null)
            rewardSoftCurrencyText.text = "+" + lastGrantedSoftCurrency;

        if (rewardPremiumCurrencyText != null)
            rewardPremiumCurrencyText.text = "+" + lastGrantedPremiumCurrency;

        if (rewardChestText != null)
            rewardChestText.text = lastGrantedChest
                ? lastGrantedChestType.ToString().ToUpperInvariant()
                : "NO CHEST";
    }

    private void ClearRewardTexts()
    {
        if (rewardXpText != null)
            rewardXpText.text = string.Empty;

        if (rewardSoftCurrencyText != null)
            rewardSoftCurrencyText.text = string.Empty;

        if (rewardPremiumCurrencyText != null)
            rewardPremiumCurrencyText.text = string.Empty;

        if (rewardChestText != null)
            rewardChestText.text = string.Empty;
    }

    private void ResolveDependencies()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchController == null)
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();

        if (rematchController == null)
            rematchController = FindFirstObjectByType<FusionOnlineRematchController>();
#else
        if (matchController == null)
            matchController = FindObjectOfType<FusionOnlineMatchController>();

        if (rematchController == null)
            rematchController = FindObjectOfType<FusionOnlineRematchController>();
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

    private bool IsRankedMatch()
    {
        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return onlineFlowController.RuntimeContext.currentSession.isRanked;
        }

        return false;
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