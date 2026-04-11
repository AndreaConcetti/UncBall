
using UnityEngine;

public class OnlineMatchPresentationResultStore : MonoBehaviour
{
    public static OnlineMatchPresentationResultStore Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private OnlineMatchPresentationResult latestResult;

    public bool HasResult => latestResult != null && latestResult.hasData;

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
            Debug.LogWarning("[OnlineMatchPresentationResultStore] Duplicate instance detected, destroying duplicate.", this);

            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        Debug.Log(
            "[OnlineMatchPresentationResultStore] Awake -> " +
            "Instance registered on object: " + name +
            " | DontDestroyOnLoad=" + dontDestroyOnLoad,
            this
        );

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }
    }

    public void SetLatest(OnlineMatchPresentationResult result)
    {
        latestResult = Clone(result);

        Debug.Log(
            "[OnlineMatchPresentationResultStore] SetLatest -> " +
            "HasData=" + (latestResult != null && latestResult.hasData) +
            " | Title=" + (latestResult != null ? latestResult.titleText : "<null>") +
            " | Player=" + (latestResult != null ? latestResult.playerName : "<null>") +
            " | RankedLpDelta=" + (latestResult != null ? latestResult.rankedLpDelta : 0) +
            " | NewRankedLpTotal=" + (latestResult != null ? latestResult.newRankedLpTotal : 0),
            this
        );
    }

    public bool TryGetLatest(out OnlineMatchPresentationResult result)
    {
        result = LatestResult;

        bool success = result != null && result.hasData;

        Debug.Log(
            "[OnlineMatchPresentationResultStore] TryGetLatest -> " +
            "Success=" + success +
            " | HasLatest=" + (latestResult != null) +
            " | LatestHasData=" + (latestResult != null && latestResult.hasData),
            this
        );

        return success;
    }

    public void Clear()
    {
        latestResult = null;
        Debug.Log("[OnlineMatchPresentationResultStore] Clear called.", this);
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
            leveledUp = source.leveledUp,
            levelUpCount = source.levelUpCount,
            levelUpBonusSoftCurrency = source.levelUpBonusSoftCurrency,
            levelUpBonusChestCount = source.levelUpBonusChestCount,
            overlayTitleText = source.overlayTitleText
        };
    }
}
