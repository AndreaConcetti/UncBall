using System;

[Serializable]
public class OnlineMatchPresentationResult
{
    public bool hasData;

    public bool isVictory;
    public bool isDefeat;
    public bool isDraw;

    public string titleText = "VICTORY";
    public string playerName = "PLAYER";
    public string levelText = "LV 1";
    public string rewardSummaryText = string.Empty;

    public int startLevel = 1;
    public int endLevel = 1;

    public int startTotalXp = 0;
    public int endTotalXp = 0;
    public int grantedXp = 0;

    public float startLevelProgress01 = 0f;
    public float endLevelProgress01 = 0f;

    public int rankedLpDelta = 0;
    public int newRankedLpTotal = 1000;

    public int totalSoftCurrencyGained = 0;

    public int totalChestCount = 0;

    public bool leveledUp;
    public int levelUpCount = 0;
    public int levelUpBonusSoftCurrency = 0;
    public int levelUpBonusChestCount = 0;

    public string overlayTitleText = "LEVEL UP!";
}
