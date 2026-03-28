using UnityEngine;

public class PlayerProgressionRules : MonoBehaviour
{
    public static PlayerProgressionRules Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("XP Sources")]
    [SerializeField] private int dailyLoginBaseXp = 25;
    [SerializeField] private int dailyLoginStreakBonusPerDay = 5;
    [SerializeField] private int dailyLoginMaxStreakDays = 7;

    [SerializeField] private int matchPlayedXp = 15;
    [SerializeField] private int matchWinBonusXp = 10;

    [Header("Level Curve")]
    [SerializeField] private int baseXpPerLevel = 100;
    [SerializeField] private int extraXpPerLevelStep = 25;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    public int DailyLoginBaseXp => dailyLoginBaseXp;
    public int MatchPlayedXp => matchPlayedXp;
    public int MatchWinBonusXp => matchWinBonusXp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProgressionRules] Initialized. " +
                "DailyBase=" + dailyLoginBaseXp +
                " | MatchPlayed=" + matchPlayedXp +
                " | WinBonus=" + matchWinBonusXp +
                " | BaseXpPerLevel=" + baseXpPerLevel +
                " | ExtraPerStep=" + extraXpPerLevelStep,
                this
            );
        }
    }

    public int GetDailyLoginXp(int consecutiveLoginDays)
    {
        int clampedStreak = Mathf.Clamp(consecutiveLoginDays, 1, Mathf.Max(1, dailyLoginMaxStreakDays));
        int bonusDays = Mathf.Max(0, clampedStreak - 1);
        return Mathf.Max(0, dailyLoginBaseXp + bonusDays * dailyLoginStreakBonusPerDay);
    }

    public int GetMatchCompletionXp(bool wonMatch)
    {
        int xp = Mathf.Max(0, matchPlayedXp);

        if (wonMatch)
            xp += Mathf.Max(0, matchWinBonusXp);

        return xp;
    }

    public int GetXpRequiredToAdvanceFromLevel(int currentLevel)
    {
        int safeLevel = Mathf.Max(1, currentLevel);
        return Mathf.Max(1, baseXpPerLevel + (safeLevel - 1) * extraXpPerLevelStep);
    }

    public int GetTotalXpRequiredForLevel(int targetLevel)
    {
        int safeTargetLevel = Mathf.Max(1, targetLevel);

        if (safeTargetLevel <= 1)
            return 0;

        int total = 0;

        for (int level = 1; level < safeTargetLevel; level++)
            total += GetXpRequiredToAdvanceFromLevel(level);

        return total;
    }

    public int CalculateLevelFromTotalXp(int totalXp)
    {
        int safeXp = Mathf.Max(0, totalXp);
        int level = 1;
        int remainingXp = safeXp;

        while (true)
        {
            int xpForNextLevel = GetXpRequiredToAdvanceFromLevel(level);

            if (remainingXp < xpForNextLevel)
                break;

            remainingXp -= xpForNextLevel;
            level++;
        }

        return Mathf.Max(1, level);
    }

    public int GetXpIntoCurrentLevel(int totalXp)
    {
        int safeXp = Mathf.Max(0, totalXp);
        int level = CalculateLevelFromTotalXp(safeXp);
        int xpRequiredBeforeThisLevel = GetTotalXpRequiredForLevel(level);
        return Mathf.Max(0, safeXp - xpRequiredBeforeThisLevel);
    }

    public int GetXpNeededForNextLevel(int totalXp)
    {
        int safeXp = Mathf.Max(0, totalXp);
        int level = CalculateLevelFromTotalXp(safeXp);
        return GetXpRequiredToAdvanceFromLevel(level);
    }
}