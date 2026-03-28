using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileStatsPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class IntTextFormat
    {
        public string prefix = "";
        public string suffix = "";
    }

    [System.Serializable]
    public class StringTextFormat
    {
        public string prefix = "";
        public string suffix = "";
    }

    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PlayerProgressionRules progressionRules;

    [Header("Identity UI")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private StringTextFormat displayNameFormat = new StringTextFormat();

    [Header("Level / XP UI")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private IntTextFormat levelFormat = new IntTextFormat { prefix = "LV ", suffix = "" };

    [SerializeField] private TMP_Text xpProgressText;
    [SerializeField] private string xpProgressPrefix = "";
    [SerializeField] private string xpProgressSuffix = " XP";
    [SerializeField] private string xpProgressSeparator = "/";

    [SerializeField] private Image xpFillImage;

    [Header("General Match Stats UI")]
    [SerializeField] private TMP_Text totalMatchesPlayedText;
    [SerializeField] private IntTextFormat totalMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " PLAYED" };

    [SerializeField] private TMP_Text totalWinsText;
    [SerializeField] private IntTextFormat totalWinsFormat = new IntTextFormat { prefix = "", suffix = " TOTAL WIN" };

    [Header("Category Match Stats UI")]
    [SerializeField] private TMP_Text versusMatchesPlayedText;
    [SerializeField] private IntTextFormat versusMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " VERSUS PLAYED" };

    [SerializeField] private TMP_Text versusWinsText;
    [SerializeField] private IntTextFormat versusWinsFormat = new IntTextFormat { prefix = "", suffix = " VERSUS WIN" };

    [SerializeField] private TMP_Text versusTimeMatchesPlayedText;
    [SerializeField] private IntTextFormat versusTimeMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " TIME MATCHES" };

    [SerializeField] private TMP_Text versusScoreMatchesPlayedText;
    [SerializeField] private IntTextFormat versusScoreMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " SCORE MATCHES" };

    [SerializeField] private TMP_Text botMatchesPlayedText;
    [SerializeField] private IntTextFormat botMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " BOT MATCHES" };

    [SerializeField] private TMP_Text botWinsText;
    [SerializeField] private IntTextFormat botWinsFormat = new IntTextFormat { prefix = "", suffix = " BOT WIN" };

    [SerializeField] private TMP_Text multiplayerMatchesPlayedText;
    [SerializeField] private IntTextFormat multiplayerMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " MP MATCHES" };

    [SerializeField] private TMP_Text multiplayerWinsText;
    [SerializeField] private IntTextFormat multiplayerWinsFormat = new IntTextFormat { prefix = "", suffix = " MP WIN" };

    [SerializeField] private TMP_Text rankedMatchesPlayedText;
    [SerializeField] private IntTextFormat rankedMatchesPlayedFormat = new IntTextFormat { prefix = "", suffix = " RANKED MATCHES" };

    [SerializeField] private TMP_Text rankedWinsText;
    [SerializeField] private IntTextFormat rankedWinsFormat = new IntTextFormat { prefix = "", suffix = " RANKED WIN" };

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        if (profileManager == null || progressionRules == null || profileManager.ActiveProfile == null)
        {
            if (logDebug)
                Debug.LogWarning("[PlayerProfileStatsPanelUI] Missing dependencies, cannot refresh UI.", this);

            ClearUI();
            return;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        int totalXp = Mathf.Max(0, profile.xp);
        int currentLevel = Mathf.Max(1, profile.level);
        int xpIntoCurrentLevel = progressionRules.GetXpIntoCurrentLevel(totalXp);
        int xpNeededForNextLevel = progressionRules.GetXpNeededForNextLevel(totalXp);

        if (displayNameText != null)
            displayNameText.text = ApplyStringFormat(profile.displayName, displayNameFormat);

        if (levelText != null)
            levelText.text = ApplyIntFormat(currentLevel, levelFormat);

        if (xpProgressText != null)
            xpProgressText.text = xpProgressPrefix + xpIntoCurrentLevel + xpProgressSeparator + xpNeededForNextLevel + xpProgressSuffix;

        if (xpFillImage != null)
        {
            float fill = xpNeededForNextLevel > 0
                ? Mathf.Clamp01(xpIntoCurrentLevel / (float)xpNeededForNextLevel)
                : 0f;

            xpFillImage.fillAmount = fill;
        }

        SetFormattedInt(totalMatchesPlayedText, profile.totalMatchesPlayed, totalMatchesPlayedFormat);
        SetFormattedInt(totalWinsText, profile.totalWins, totalWinsFormat);

        SetFormattedInt(versusMatchesPlayedText, profile.versusMatchesPlayed, versusMatchesPlayedFormat);
        SetFormattedInt(versusWinsText, profile.versusWins, versusWinsFormat);
        SetFormattedInt(versusTimeMatchesPlayedText, profile.versusTimeMatchesPlayed, versusTimeMatchesPlayedFormat);
        SetFormattedInt(versusScoreMatchesPlayedText, profile.versusScoreMatchesPlayed, versusScoreMatchesPlayedFormat);

        SetFormattedInt(botMatchesPlayedText, profile.botMatchesPlayed, botMatchesPlayedFormat);
        SetFormattedInt(botWinsText, profile.botWins, botWinsFormat);

        SetFormattedInt(multiplayerMatchesPlayedText, profile.multiplayerMatchesPlayed, multiplayerMatchesPlayedFormat);
        SetFormattedInt(multiplayerWinsText, profile.multiplayerWins, multiplayerWinsFormat);

        SetFormattedInt(rankedMatchesPlayedText, profile.rankedMatchesPlayed, rankedMatchesPlayedFormat);
        SetFormattedInt(rankedWinsText, profile.rankedWins, rankedWinsFormat);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileStatsPanelUI] RefreshUI -> " +
                "Profile=" + profile.profileId +
                " | Level=" + currentLevel +
                " | TotalXP=" + totalXp +
                " | XPInLevel=" + xpIntoCurrentLevel +
                "/" + xpNeededForNextLevel,
                this
            );
        }
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
        if (profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
    }

    private void Unsubscribe()
    {
        if (profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
    }

    private void ClearUI()
    {
        if (displayNameText != null)
            displayNameText.text = ApplyStringFormat(string.Empty, displayNameFormat);

        if (levelText != null)
            levelText.text = ApplyIntFormat(1, levelFormat);

        if (xpProgressText != null)
            xpProgressText.text = xpProgressPrefix + "0" + xpProgressSeparator + "0" + xpProgressSuffix;

        if (xpFillImage != null)
            xpFillImage.fillAmount = 0f;

        SetFormattedInt(totalMatchesPlayedText, 0, totalMatchesPlayedFormat);
        SetFormattedInt(totalWinsText, 0, totalWinsFormat);

        SetFormattedInt(versusMatchesPlayedText, 0, versusMatchesPlayedFormat);
        SetFormattedInt(versusWinsText, 0, versusWinsFormat);
        SetFormattedInt(versusTimeMatchesPlayedText, 0, versusTimeMatchesPlayedFormat);
        SetFormattedInt(versusScoreMatchesPlayedText, 0, versusScoreMatchesPlayedFormat);

        SetFormattedInt(botMatchesPlayedText, 0, botMatchesPlayedFormat);
        SetFormattedInt(botWinsText, 0, botWinsFormat);

        SetFormattedInt(multiplayerMatchesPlayedText, 0, multiplayerMatchesPlayedFormat);
        SetFormattedInt(multiplayerWinsText, 0, multiplayerWinsFormat);

        SetFormattedInt(rankedMatchesPlayedText, 0, rankedMatchesPlayedFormat);
        SetFormattedInt(rankedWinsText, 0, rankedWinsFormat);
    }

    private void SetFormattedInt(TMP_Text target, int value, IntTextFormat format)
    {
        if (target != null)
            target.text = ApplyIntFormat(value, format);
    }

    private string ApplyIntFormat(int value, IntTextFormat format)
    {
        string prefix = format != null ? format.prefix : string.Empty;
        string suffix = format != null ? format.suffix : string.Empty;
        return prefix + value + suffix;
    }

    private string ApplyStringFormat(string value, StringTextFormat format)
    {
        string prefix = format != null ? format.prefix : string.Empty;
        string suffix = format != null ? format.suffix : string.Empty;
        return prefix + value + suffix;
    }
}