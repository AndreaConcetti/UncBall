using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileStatsPanelUI : MonoBehaviour
{
    private enum StatsCategory
    {
        All = 0,
        Normal = 1,
        Ranked = 2
    }

    [System.Serializable]
    public class IntTextFormat
    {
        public string prefix = "";
        public string suffix = "";
    }

    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PlayerProgressionRules progressionRules;

    [Header("Level / XP UI")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private string playerNamePrefix = "";
    [SerializeField] private string playerNameSuffix = "";

    [SerializeField] private TMP_Text levelText;
    [SerializeField] private IntTextFormat levelFormat = new IntTextFormat { prefix = "LV ", suffix = "" };

    [SerializeField] private TMP_Text xpProgressText;
    [SerializeField] private string xpProgressPrefix = "";
    [SerializeField] private string xpProgressSuffix = " XP";
    [SerializeField] private string xpProgressSeparator = "/";

    [SerializeField] private Image xpFillImage;

    [Header("Category Tabs")]
    [SerializeField] private Button allButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button rankedButton;

    [SerializeField] private TMP_Text allButtonText;
    [SerializeField] private TMP_Text normalButtonText;
    [SerializeField] private TMP_Text rankedButtonText;

    [SerializeField] private Image allButtonBackground;
    [SerializeField] private Image normalButtonBackground;
    [SerializeField] private Image rankedButtonBackground;

    [Header("Selected Tab State")]
    [SerializeField] private bool disableSelectedTabInteractable = true;
    [SerializeField] private Color selectedTabTextColor = Color.white;
    [SerializeField] private Color unselectedTabTextColor = Color.gray;
    [SerializeField] private Color selectedTabBackgroundColor = Color.white;
    [SerializeField] private Color unselectedTabBackgroundColor = Color.gray;

    [Header("Current Category Label")]
    [SerializeField] private TMP_Text currentCategoryText;
    [SerializeField] private string allCategoryLabel = "ALL";
    [SerializeField] private string normalCategoryLabel = "NORMAL";
    [SerializeField] private string rankedCategoryLabel = "RANKED";

    [Header("Main Stats UI")]
    [SerializeField] private TMP_Text matchesValueText;
    [SerializeField] private TMP_Text winsValueText;
    [SerializeField] private TMP_Text lossesValueText;
    [SerializeField] private TMP_Text winRateValueText;

    [Header("Stat Labels Optional")]
    [SerializeField] private TMP_Text matchesLabelText;
    [SerializeField] private TMP_Text winsLabelText;
    [SerializeField] private TMP_Text lossesLabelText;
    [SerializeField] private TMP_Text winRateLabelText;

    [SerializeField] private string matchesLabel = "MATCHES";
    [SerializeField] private string winsLabel = "WINS";
    [SerializeField] private string lossesLabel = "LOSSES";
    [SerializeField] private string winRateLabel = "WIN RATE";

    [Header("Ranked LP UI")]
    [SerializeField] private GameObject rankedLpRoot;
    [SerializeField] private TMP_Text rankedLpValueText;
    [SerializeField] private TMP_Text rankedLpLabelText;
    [SerializeField] private string rankedLpLabel = "LP";
    [SerializeField] private string rankedLpPrefix = "";
    [SerializeField] private string rankedLpSuffix = "";

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool autoWireTabButtons = true;
    [SerializeField] private StatsCategory defaultCategory = StatsCategory.All;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private StatsCategory currentCategory;
    private bool subscribed;

    private struct StatsViewData
    {
        public int matches;
        public int wins;
        public int losses;
        public float winRate01;
        public int rankedLp;
        public bool showRankedLp;
    }

    private void Awake()
    {
        ResolveDependencies();
        WireButtonsIfNeeded();
        currentCategory = defaultCategory;
        ApplyStaticLabels();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        WireButtonsIfNeeded();
        Subscribe();
        ApplyStaticLabels();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnwireButtonsIfNeeded();
    }

    public void ShowAllStats()
    {
        SetCategory(StatsCategory.All);
    }

    public void ShowNormalStats()
    {
        SetCategory(StatsCategory.Normal);
    }

    public void ShowRankedStats()
    {
        SetCategory(StatsCategory.Ranked);
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        if (profileManager == null || progressionRules == null || profileManager.ActiveProfile == null)
        {
            if (logDebug)
                Debug.LogWarning("[PlayerProfileStatsPanelUI] Missing dependencies, cannot refresh UI.", this);

            ClearUI();
            RefreshTabVisuals();
            return;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        RefreshIdentityAndProgression(profile);
        RefreshCategoryStats(profile);
        RefreshTabVisuals();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileStatsPanelUI] RefreshUI -> " +
                "Profile=" + profile.profileId +
                " | Category=" + currentCategory +
                " | Level=" + profile.level +
                " | XP=" + profile.xp +
                " | RankedLp=" + profile.rankedLp,
                this
            );
        }
    }

    private void SetCategory(StatsCategory category)
    {
        if (currentCategory == category)
        {
            RefreshTabVisuals();
            return;
        }

        currentCategory = category;
        RefreshUI();

        if (logDebug)
            Debug.Log("[PlayerProfileStatsPanelUI] SetCategory -> " + currentCategory, this);
    }

    private void RefreshIdentityAndProgression(PlayerProfileRuntimeData profile)
    {
        string safeName = string.IsNullOrWhiteSpace(profile.displayName) ? "PLAYER" : profile.displayName.Trim();

        if (playerNameText != null)
            playerNameText.text = playerNamePrefix + safeName.ToUpperInvariant() + playerNameSuffix;

        int totalXp = Mathf.Max(0, profile.xp);
        int currentLevelValue = Mathf.Max(1, profile.level);
        int xpIntoCurrentLevel = progressionRules.GetXpIntoCurrentLevel(totalXp);
        int xpNeededForNextLevel = progressionRules.GetXpNeededForNextLevel(totalXp);

        if (levelText != null)
            levelText.text = ApplyIntFormat(currentLevelValue, levelFormat);

        if (xpProgressText != null)
            xpProgressText.text = xpProgressPrefix + xpIntoCurrentLevel + xpProgressSeparator + xpNeededForNextLevel + xpProgressSuffix;

        if (xpFillImage != null)
        {
            float fill = xpNeededForNextLevel > 0
                ? Mathf.Clamp01(xpIntoCurrentLevel / (float)xpNeededForNextLevel)
                : 0f;

            xpFillImage.fillAmount = fill;
        }
    }

    private void RefreshCategoryStats(PlayerProfileRuntimeData profile)
    {
        StatsViewData data = BuildStatsForCurrentCategory(profile);

        if (currentCategoryText != null)
            currentCategoryText.text = GetCurrentCategoryLabel();

        if (matchesValueText != null)
            matchesValueText.text = data.matches.ToString();

        if (winsValueText != null)
            winsValueText.text = data.wins.ToString();

        if (lossesValueText != null)
            lossesValueText.text = data.losses.ToString();

        if (winRateValueText != null)
            winRateValueText.text = Mathf.RoundToInt(data.winRate01 * 100f) + "%";

        if (rankedLpRoot != null)
            rankedLpRoot.SetActive(data.showRankedLp);

        if (rankedLpValueText != null)
            rankedLpValueText.text = data.showRankedLp
                ? rankedLpPrefix + data.rankedLp + rankedLpSuffix
                : string.Empty;
    }

    private StatsViewData BuildStatsForCurrentCategory(PlayerProfileRuntimeData profile)
    {
        switch (currentCategory)
        {
            case StatsCategory.Normal:
                return BuildNormalStats(profile);

            case StatsCategory.Ranked:
                return BuildRankedStats(profile);

            default:
                return BuildAllStats(profile);
        }
    }

    private StatsViewData BuildAllStats(PlayerProfileRuntimeData profile)
    {
        int matches = Mathf.Max(0, profile.totalMatchesPlayed);
        int wins = Mathf.Clamp(profile.totalWins, 0, matches);
        int losses = Mathf.Max(0, matches - wins);

        return new StatsViewData
        {
            matches = matches,
            wins = wins,
            losses = losses,
            winRate01 = matches > 0 ? wins / (float)matches : 0f,
            rankedLp = Mathf.Max(0, profile.rankedLp),
            showRankedLp = false
        };
    }

    private StatsViewData BuildNormalStats(PlayerProfileRuntimeData profile)
    {
        int normalMatches = Mathf.Max(0, profile.multiplayerMatchesPlayed - profile.rankedMatchesPlayed);
        int normalWins = Mathf.Max(0, profile.multiplayerWins - profile.rankedWins);

        normalWins = Mathf.Clamp(normalWins, 0, normalMatches);
        int normalLosses = Mathf.Max(0, normalMatches - normalWins);

        return new StatsViewData
        {
            matches = normalMatches,
            wins = normalWins,
            losses = normalLosses,
            winRate01 = normalMatches > 0 ? normalWins / (float)normalMatches : 0f,
            rankedLp = Mathf.Max(0, profile.rankedLp),
            showRankedLp = false
        };
    }

    private StatsViewData BuildRankedStats(PlayerProfileRuntimeData profile)
    {
        int rankedMatches = Mathf.Max(0, profile.rankedMatchesPlayed);
        int rankedWins = Mathf.Clamp(profile.rankedWins, 0, rankedMatches);
        int rankedLosses = Mathf.Max(0, rankedMatches - rankedWins);

        return new StatsViewData
        {
            matches = rankedMatches,
            wins = rankedWins,
            losses = rankedLosses,
            winRate01 = rankedMatches > 0 ? rankedWins / (float)rankedMatches : 0f,
            rankedLp = Mathf.Max(0, profile.rankedLp),
            showRankedLp = true
        };
    }

    private string GetCurrentCategoryLabel()
    {
        switch (currentCategory)
        {
            case StatsCategory.Normal:
                return normalCategoryLabel;

            case StatsCategory.Ranked:
                return rankedCategoryLabel;

            default:
                return allCategoryLabel;
        }
    }

    private void RefreshTabVisuals()
    {
        ApplyTabVisual(allButton, allButtonText, allButtonBackground, currentCategory == StatsCategory.All);
        ApplyTabVisual(normalButton, normalButtonText, normalButtonBackground, currentCategory == StatsCategory.Normal);
        ApplyTabVisual(rankedButton, rankedButtonText, rankedButtonBackground, currentCategory == StatsCategory.Ranked);
    }

    private void ApplyTabVisual(Button button, TMP_Text label, Image background, bool selected)
    {
        if (button != null && disableSelectedTabInteractable)
            button.interactable = !selected;

        if (label != null)
            label.color = selected ? selectedTabTextColor : unselectedTabTextColor;

        if (background != null)
            background.color = selected ? selectedTabBackgroundColor : unselectedTabBackgroundColor;
    }

    private void ApplyStaticLabels()
    {
        if (matchesLabelText != null)
            matchesLabelText.text = matchesLabel;

        if (winsLabelText != null)
            winsLabelText.text = winsLabel;

        if (lossesLabelText != null)
            lossesLabelText.text = lossesLabel;

        if (winRateLabelText != null)
            winRateLabelText.text = winRateLabel;

        if (rankedLpLabelText != null)
            rankedLpLabelText.text = rankedLpLabel;
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUI();
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (progressionRules == null)
            progressionRules = PlayerProgressionRules.Instance;
    }

    private void Subscribe()
    {
        if (profileManager == null || subscribed)
            return;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (profileManager == null || !subscribed)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
        subscribed = false;
    }

    private void WireButtonsIfNeeded()
    {
        if (!autoWireTabButtons)
            return;

        if (allButton != null)
        {
            allButton.onClick.RemoveListener(ShowAllStats);
            allButton.onClick.AddListener(ShowAllStats);
        }

        if (normalButton != null)
        {
            normalButton.onClick.RemoveListener(ShowNormalStats);
            normalButton.onClick.AddListener(ShowNormalStats);
        }

        if (rankedButton != null)
        {
            rankedButton.onClick.RemoveListener(ShowRankedStats);
            rankedButton.onClick.AddListener(ShowRankedStats);
        }
    }

    private void UnwireButtonsIfNeeded()
    {
        if (!autoWireTabButtons)
            return;

        if (allButton != null)
            allButton.onClick.RemoveListener(ShowAllStats);

        if (normalButton != null)
            normalButton.onClick.RemoveListener(ShowNormalStats);

        if (rankedButton != null)
            rankedButton.onClick.RemoveListener(ShowRankedStats);
    }

    private void ClearUI()
    {
        if (playerNameText != null)
            playerNameText.text = playerNamePrefix + "PLAYER" + playerNameSuffix;

        if (levelText != null)
            levelText.text = ApplyIntFormat(1, levelFormat);

        if (xpProgressText != null)
            xpProgressText.text = xpProgressPrefix + "0" + xpProgressSeparator + "0" + xpProgressSuffix;

        if (xpFillImage != null)
            xpFillImage.fillAmount = 0f;

        if (currentCategoryText != null)
            currentCategoryText.text = GetCurrentCategoryLabel();

        if (matchesValueText != null)
            matchesValueText.text = "0";

        if (winsValueText != null)
            winsValueText.text = "0";

        if (lossesValueText != null)
            lossesValueText.text = "0";

        if (winRateValueText != null)
            winRateValueText.text = "0%";

        if (rankedLpRoot != null)
            rankedLpRoot.SetActive(currentCategory == StatsCategory.Ranked);

        if (rankedLpValueText != null)
            rankedLpValueText.text = rankedLpPrefix + "0" + rankedLpSuffix;
    }

    private string ApplyIntFormat(int value, IntTextFormat format)
    {
        string prefix = format != null ? format.prefix : string.Empty;
        string suffix = format != null ? format.suffix : string.Empty;
        return prefix + value + suffix;
    }
}
