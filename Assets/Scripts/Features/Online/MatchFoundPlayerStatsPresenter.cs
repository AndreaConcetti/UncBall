using TMPro;
using UnityEngine;

public sealed class MatchFoundPlayerStatsPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlinePlayerPresentationResolver resolver;

    [Header("Local Player Texts")]
    [SerializeField] private TMP_Text localNameText;
    [SerializeField] private TMP_Text localLevelText;
    [SerializeField] private TMP_Text localWinLoseText;
    [SerializeField] private TMP_Text localWinRateText;

    [Header("Opponent Texts")]
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private TMP_Text opponentLevelText;
    [SerializeField] private TMP_Text opponentWinLoseText;
    [SerializeField] private TMP_Text opponentWinRateText;

    [Header("Options")]
    [SerializeField] private bool uppercaseNames = true;
    [SerializeField] private bool refreshEveryFrame = true;
    [SerializeField] private bool showLevelPrefix = true;
    [SerializeField] private string levelPrefix = "LV. ";

    [Header("Fallback Local")]
    [SerializeField] private string fallbackLocalName = "PLAYER";
    [SerializeField] private string fallbackLocalLevelText = "LV. 1";
    [SerializeField] private string fallbackLocalWinLoseText = "0W - 0L";
    [SerializeField] private string fallbackLocalWinRateText = "0%";

    [Header("Fallback Opponent")]
    [SerializeField] private string fallbackOpponentName = "OPPONENT";
    [SerializeField] private string fallbackOpponentLevelText = "LV. 1";
    [SerializeField] private string fallbackOpponentWinLoseText = "0W - 0L";
    [SerializeField] private string fallbackOpponentWinRateText = "0%";

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private string lastLocalName = string.Empty;
    private string lastOpponentName = string.Empty;
    private int lastLocalWins = -1;
    private int lastLocalLosses = -1;
    private int lastLocalWinRate = -1;
    private int lastLocalLevel = -1;
    private int lastOpponentWins = -1;
    private int lastOpponentLosses = -1;
    private int lastOpponentWinRate = -1;
    private int lastOpponentLevel = -1;

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

        if (!hasLocal || localSnapshot == null)
        {
            ApplyLocalFallback(force);
        }
        else
        {
            localSnapshot.Normalize();
            ApplyLocalSnapshot(localSnapshot, force);
        }

        if (!hasOpponent || opponentSnapshot == null)
        {
            ApplyOpponentFallback(force);
        }
        else
        {
            opponentSnapshot.Normalize();
            ApplyOpponentSnapshot(opponentSnapshot, force);
        }
    }

    private void ResolveDependencies()
    {
        if (resolver == null)
            resolver = FindAnyObjectByType<OnlinePlayerPresentationResolver>();
    }

    private void ApplyLocalSnapshot(OnlinePlayerMatchStatsSnapshot snapshot, bool force)
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
            lastLocalName != displayName ||
            lastLocalWins != wins ||
            lastLocalLosses != losses ||
            lastLocalWinRate != winRate ||
            lastLocalLevel != level;

        if (!changed)
            return;

        lastLocalName = displayName;
        lastLocalWins = wins;
        lastLocalLosses = losses;
        lastLocalWinRate = winRate;
        lastLocalLevel = level;

        if (localNameText != null)
            localNameText.text = displayName;

        if (localLevelText != null)
            localLevelText.text = FormatLevel(level);

        if (localWinLoseText != null)
            localWinLoseText.text = wins + "W - " + losses + "L";

        if (localWinRateText != null)
            localWinRateText.text = winRate + "%";

        if (logDebug)
        {
            Debug.Log(
                "[MatchFoundPlayerStatsPresenter] Local -> " +
                displayName + " | Level=" + level +
                " | " + wins + "W-" + losses + "L" +
                " | WR=" + winRate + "%",
                this
            );
        }
    }

    private void ApplyOpponentSnapshot(OnlinePlayerMatchStatsSnapshot snapshot, bool force)
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
            lastOpponentName != displayName ||
            lastOpponentWins != wins ||
            lastOpponentLosses != losses ||
            lastOpponentWinRate != winRate ||
            lastOpponentLevel != level;

        if (!changed)
            return;

        lastOpponentName = displayName;
        lastOpponentWins = wins;
        lastOpponentLosses = losses;
        lastOpponentWinRate = winRate;
        lastOpponentLevel = level;

        if (opponentNameText != null)
            opponentNameText.text = displayName;

        if (opponentLevelText != null)
            opponentLevelText.text = FormatLevel(level);

        if (opponentWinLoseText != null)
            opponentWinLoseText.text = wins + "W - " + losses + "L";

        if (opponentWinRateText != null)
            opponentWinRateText.text = winRate + "%";

        if (logDebug)
        {
            Debug.Log(
                "[MatchFoundPlayerStatsPresenter] Opponent -> " +
                displayName + " | Level=" + level +
                " | " + wins + "W-" + losses + "L" +
                " | WR=" + winRate + "%",
                this
            );
        }
    }

    private void ApplyLocalFallback(bool force)
    {
        bool changed =
            force ||
            lastLocalName != fallbackLocalName ||
            lastLocalLevel != -2 ||
            lastLocalWins != -2 ||
            lastLocalLosses != -2 ||
            lastLocalWinRate != -2;

        if (!changed)
            return;

        lastLocalName = fallbackLocalName;
        lastLocalLevel = -2;
        lastLocalWins = -2;
        lastLocalLosses = -2;
        lastLocalWinRate = -2;

        if (localNameText != null)
            localNameText.text = uppercaseNames ? fallbackLocalName.ToUpperInvariant() : fallbackLocalName;

        if (localLevelText != null)
            localLevelText.text = fallbackLocalLevelText;

        if (localWinLoseText != null)
            localWinLoseText.text = fallbackLocalWinLoseText;

        if (localWinRateText != null)
            localWinRateText.text = fallbackLocalWinRateText;
    }

    private void ApplyOpponentFallback(bool force)
    {
        bool changed =
            force ||
            lastOpponentName != fallbackOpponentName ||
            lastOpponentLevel != -2 ||
            lastOpponentWins != -2 ||
            lastOpponentLosses != -2 ||
            lastOpponentWinRate != -2;

        if (!changed)
            return;

        lastOpponentName = fallbackOpponentName;
        lastOpponentLevel = -2;
        lastOpponentWins = -2;
        lastOpponentLosses = -2;
        lastOpponentWinRate = -2;

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