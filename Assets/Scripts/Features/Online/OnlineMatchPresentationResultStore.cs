using UnityEngine;

public class OnlineMatchPresentationResultStore : MonoBehaviour
{
    public static OnlineMatchPresentationResultStore Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private OnlineMatchPresentationResult latestResult;
    [SerializeField] private string lastProcessedMatchId = string.Empty;

    public bool HasResult => latestResult != null && latestResult.hasData;
    public string LastProcessedMatchId => lastProcessedMatchId;

    public OnlineMatchPresentationResult LatestResult
    {
        get
        {
            if (latestResult == null)
                return null;

            return Clone(latestResult);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }
    }

    public void SetLatest(OnlineMatchPresentationResult result)
    {
        latestResult = Clone(result);
    }

    public bool TryGetLatest(out OnlineMatchPresentationResult result)
    {
        result = LatestResult;
        return result != null && result.hasData;
    }

    public void MarkMatchProcessed(string matchId)
    {
        lastProcessedMatchId = string.IsNullOrWhiteSpace(matchId) ? string.Empty : matchId.Trim();
    }

    public bool HasProcessedMatch(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            return false;

        return string.Equals(lastProcessedMatchId, matchId.Trim(), System.StringComparison.Ordinal);
    }

    public void ClearPresentationOnly()
    {
        latestResult = null;
    }

    public void ClearAll()
    {
        latestResult = null;
        lastProcessedMatchId = string.Empty;
    }

    private OnlineMatchPresentationResult Clone(OnlineMatchPresentationResult source)
    {
        if (source == null)
            return null;

        return new OnlineMatchPresentationResult
        {
            hasData = source.hasData,
            isVictory = source.isVictory,
            isDefeat = source.isDefeat,
            isDraw = source.isDraw,
            titleText = source.titleText,
            playerName = source.playerName,
            levelText = source.levelText,
            rewardSummaryText = source.rewardSummaryText,
            startLevel = source.startLevel,
            endLevel = source.endLevel,
            startTotalXp = source.startTotalXp,
            endTotalXp = source.endTotalXp,
            grantedXp = source.grantedXp,
            startLevelProgress01 = source.startLevelProgress01,
            endLevelProgress01 = source.endLevelProgress01,
            rankedLpDelta = source.rankedLpDelta,
            newRankedLpTotal = source.newRankedLpTotal,
            totalSoftCurrencyGained = source.totalSoftCurrencyGained,
            totalChestCount = source.totalChestCount,
            totalChestType = source.totalChestType,
            leveledUp = source.leveledUp,
            levelUpCount = source.levelUpCount,
            levelUpBonusSoftCurrency = source.levelUpBonusSoftCurrency,
            levelUpBonusChestCount = source.levelUpBonusChestCount,
            levelUpBonusChestType = source.levelUpBonusChestType,
            overlayTitleText = source.overlayTitleText,
            sourceMatchId = source.sourceMatchId
        };
    }
}
