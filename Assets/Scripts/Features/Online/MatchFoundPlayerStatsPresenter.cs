using TMPro;
using UnityEngine;

public sealed class MatchFoundPlayerStatsPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlinePlayerPresentationResolver resolver;

    [Header("Left Slot Texts")]
    [SerializeField] private TMP_Text localNameText;
    [SerializeField] private TMP_Text localLevelText;
    [SerializeField] private TMP_Text localWinLoseText;
    [SerializeField] private TMP_Text localWinRateText;

    [Header("Right Slot Texts")]
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private TMP_Text opponentLevelText;
    [SerializeField] private TMP_Text opponentWinLoseText;
    [SerializeField] private TMP_Text opponentWinRateText;

    [Header("Options")]
    [SerializeField] private bool uppercaseNames = true;
    [SerializeField] private bool refreshEveryFrame = true;
    [SerializeField] private bool showLevelPrefix = true;
    [SerializeField] private string levelPrefix = "LV. ";

    [Header("Fallback Left")]
    [SerializeField] private string fallbackLocalName = "PLAYER";
    [SerializeField] private string fallbackLocalLevelText = "LV. 1";
    [SerializeField] private string fallbackLocalWinLoseText = "0W - 0L";
    [SerializeField] private string fallbackLocalWinRateText = "0%";

    [Header("Fallback Right")]
    [SerializeField] private string fallbackOpponentName = "OPPONENT";
    [SerializeField] private string fallbackOpponentLevelText = "LV. 1";
    [SerializeField] private string fallbackOpponentWinLoseText = "0W - 0L";
    [SerializeField] private string fallbackOpponentWinRateText = "0%";

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private string lastLeftName = string.Empty;
    private string lastRightName = string.Empty;
    private int lastLeftWins = -1;
    private int lastLeftLosses = -1;
    private int lastLeftWinRate = -1;
    private int lastLeftLevel = -1;
    private int lastRightWins = -1;
    private int lastRightLosses = -1;
    private int lastRightWinRate = -1;
    private int lastRightLevel = -1;

    private void Awake()
    {
        ResolveDependencies();
        RefreshNow(true);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        RefreshNow(true);
    }

    private void Start()
    {
        ResolveDependencies();
        RefreshNow(true);
    }

    private void Update()
    {
        if (!refreshEveryFrame)
            return;

        RefreshNow(false);
    }

    public void RefreshNow()
    {
        RefreshNow(true);
    }

    private void RefreshNow(bool force)
    {
        ResolveDependencies();

        OnlinePlayerMatchStatsSnapshot localSnapshot = null;
        OnlinePlayerMatchStatsSnapshot opponentSnapshot = null;

        bool hasLocal = resolver != null && resolver.TryGetLocalSnapshot(out localSnapshot);
        bool hasOpponent = resolver != null && resolver.TryGetOpponentSnapshot(out opponentSnapshot);

        bool localOnLeft = resolver == null || resolver.IsLocalOnLeft();

        OnlinePlayerMatchStatsSnapshot leftSnapshot = null;
        OnlinePlayerMatchStatsSnapshot rightSnapshot = null;

        if (localOnLeft)
        {
            leftSnapshot = hasLocal ? localSnapshot : null;
            rightSnapshot = hasOpponent ? opponentSnapshot : null;
        }
        else
        {
            leftSnapshot = hasOpponent ? opponentSnapshot : null;
            rightSnapshot = hasLocal ? localSnapshot : null;
        }

        if (leftSnapshot == null)
            ApplyLeftFallback(force);
        else
        {
            leftSnapshot.Normalize();
            ApplyLeftSnapshot(leftSnapshot, force);
        }

        if (rightSnapshot == null)
            ApplyRightFallback(force);
        else
        {
            rightSnapshot.Normalize();
            ApplyRightSnapshot(rightSnapshot, force);
        }

        if (logDebug)
        {
            Debug.Log(
                "[MatchFoundPlayerStatsPresenter] Layout -> " +
                "LocalOnLeft=" + localOnLeft +
                " | Left=" + (leftSnapshot != null ? leftSnapshot.displayName : "NULL") +
                " | Right=" + (rightSnapshot != null ? rightSnapshot.displayName : "NULL"),
                this
            );
        }
    }

    private void ResolveDependencies()
    {
#if UNITY_2023_1_OR_NEWER
        if (resolver == null)
            resolver = FindFirstObjectByType<OnlinePlayerPresentationResolver>(FindObjectsInactive.Include);
#else
        if (resolver == null)
            resolver = FindObjectOfType<OnlinePlayerPresentationResolver>(true);
#endif
    }

    private void ApplyLeftSnapshot(OnlinePlayerMatchStatsSnapshot snapshot, bool force)
    {
        string displayName = snapshot.GetDisplayNameOrFallback(fallbackLocalName);
        if (uppercaseNames)
            displayName = displayName.ToUpperInvariant();

        int wins = Mathf.Max(0, snapshot.totalWins);
        int losses = Mathf.Max(0, snapshot.totalLosses);
        int winRate = Mathf.Clamp(snapshot.winRatePercent, 0, 100);
        int level = Mathf.Max(1, snapshot.level);

        bool changed =
            force ||
            lastLeftName != displayName ||
            lastLeftWins != wins ||
            lastLeftLosses != losses ||
            lastLeftWinRate != winRate ||
            lastLeftLevel != level;

        if (!changed)
            return;

        lastLeftName = displayName;
        lastLeftWins = wins;
        lastLeftLosses = losses;
        lastLeftWinRate = winRate;
        lastLeftLevel = level;

        if (localNameText != null)
            localNameText.text = displayName;

        if (localLevelText != null)
            localLevelText.text = FormatLevel(level);

        if (localWinLoseText != null)
            localWinLoseText.text = wins + "W - " + losses + "L";

        if (localWinRateText != null)
            localWinRateText.text = winRate + "%";
    }

    private void ApplyRightSnapshot(OnlinePlayerMatchStatsSnapshot snapshot, bool force)
    {
        string displayName = snapshot.GetDisplayNameOrFallback(fallbackOpponentName);
        if (uppercaseNames)
            displayName = displayName.ToUpperInvariant();

        int wins = Mathf.Max(0, snapshot.totalWins);
        int losses = Mathf.Max(0, snapshot.totalLosses);
        int winRate = Mathf.Clamp(snapshot.winRatePercent, 0, 100);
        int level = Mathf.Max(1, snapshot.level);

        bool changed =
            force ||
            lastRightName != displayName ||
            lastRightWins != wins ||
            lastRightLosses != losses ||
            lastRightWinRate != winRate ||
            lastRightLevel != level;

        if (!changed)
            return;

        lastRightName = displayName;
        lastRightWins = wins;
        lastRightLosses = losses;
        lastRightWinRate = winRate;
        lastRightLevel = level;

        if (opponentNameText != null)
            opponentNameText.text = displayName;

        if (opponentLevelText != null)
            opponentLevelText.text = FormatLevel(level);

        if (opponentWinLoseText != null)
            opponentWinLoseText.text = wins + "W - " + losses + "L";

        if (opponentWinRateText != null)
            opponentWinRateText.text = winRate + "%";
    }

    private void ApplyLeftFallback(bool force)
    {
        bool changed =
            force ||
            lastLeftName != fallbackLocalName ||
            lastLeftLevel != -2 ||
            lastLeftWins != -2 ||
            lastLeftLosses != -2 ||
            lastLeftWinRate != -2;

        if (!changed)
            return;

        lastLeftName = fallbackLocalName;
        lastLeftLevel = -2;
        lastLeftWins = -2;
        lastLeftLosses = -2;
        lastLeftWinRate = -2;

        if (localNameText != null)
            localNameText.text = uppercaseNames ? fallbackLocalName.ToUpperInvariant() : fallbackLocalName;

        if (localLevelText != null)
            localLevelText.text = fallbackLocalLevelText;

        if (localWinLoseText != null)
            localWinLoseText.text = fallbackLocalWinLoseText;

        if (localWinRateText != null)
            localWinRateText.text = fallbackLocalWinRateText;
    }

    private void ApplyRightFallback(bool force)
    {
        bool changed =
            force ||
            lastRightName != fallbackOpponentName ||
            lastRightLevel != -2 ||
            lastRightWins != -2 ||
            lastRightLosses != -2 ||
            lastRightWinRate != -2;

        if (!changed)
            return;

        lastRightName = fallbackOpponentName;
        lastRightLevel = -2;
        lastRightWins = -2;
        lastRightLosses = -2;
        lastRightWinRate = -2;

        if (opponentNameText != null)
            opponentNameText.text = uppercaseNames ? fallbackOpponentName.ToUpperInvariant() : fallbackOpponentName;

        if (opponentLevelText != null)
            opponentLevelText.text = fallbackOpponentLevelText;

        if (opponentWinLoseText != null)
            opponentWinLoseText.text = fallbackOpponentWinLoseText;

        if (opponentWinRateText != null)
            opponentWinRateText.text = fallbackOpponentWinRateText;
    }

    private string FormatLevel(int level)
    {
        if (!showLevelPrefix)
            return level.ToString();

        string prefix = string.IsNullOrWhiteSpace(levelPrefix) ? "LV. " : levelPrefix;
        return prefix + level;
    }
}