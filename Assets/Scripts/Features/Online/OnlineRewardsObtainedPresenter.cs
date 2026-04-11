
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineRewardsObtainedPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineMatchPresentationResultStore resultStore;

    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject chestGainParent;
    [SerializeField] private GameObject levelUpOverlayPanel;

    [Header("Main Texts")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerLevelText;
    [SerializeField] private TMP_Text lpGainedText;
    [SerializeField] private TMP_Text animatedTotalLpText;
    [SerializeField] private TMP_Text normalCurrencyGainText;
    [SerializeField] private TMP_Text chestCountText;
    [SerializeField] private TMP_Text summaryText;

    [Header("XP Progress")]
    [SerializeField] private Image experienceFillImage;
    [SerializeField] private Slider experienceSlider;

    [Header("Level Up Overlay")]
    [SerializeField] private TMP_Text levelUpOverlayTitleText;
    [SerializeField] private TMP_Text levelUpOverlaySoftCurrencyText;
    [SerializeField] private GameObject levelUpOverlayChestParent;
    [SerializeField] private TMP_Text levelUpOverlayChestCountText;

    [Header("Animation")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelaySeconds = 0.05f;
    [SerializeField] private float xpAnimationDuration = 0.75f;
    [SerializeField] private float lpAnimationDuration = 0.90f;
    [SerializeField] private float levelUpOverlayVisibleSeconds = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private Coroutine playRoutine;

    private void Awake()
    {
        ResolveDependencies();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] Awake -> " +
                "StoreResolved=" + (resultStore != null) +
                " | PanelRoot=" + SafeName(panelRoot) +
                " | HeaderText=" + SafeName(headerText) +
                " | PlayerNameText=" + SafeName(playerNameText) +
                " | PlayerLevelText=" + SafeName(playerLevelText),
                this
            );
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] OnEnable -> " +
                "PlayOnEnable=" + playOnEnable +
                " | StoreResolved=" + (resultStore != null),
                this
            );
        }

        if (playOnEnable)
            ShowLatest();
    }

    public void ShowLatest()
    {
        ResolveDependencies();

        if (logDebug)
            Debug.Log("[OnlineRewardsObtainedPresenter] ShowLatest invoked.", this);

        if (resultStore == null)
        {
            Debug.LogWarning("[OnlineRewardsObtainedPresenter] resultStore is NULL.", this);
            return;
        }

        if (!resultStore.TryGetLatest(out OnlineMatchPresentationResult result))
        {
            Debug.LogWarning("[OnlineRewardsObtainedPresenter] No latest result found in store.", this);
            return;
        }

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] Result found -> " +
                "HasData=" + result.hasData +
                " | Title=" + result.titleText +
                " | PlayerName=" + result.playerName +
                " | StartLevel=" + result.startLevel +
                " | EndLevel=" + result.endLevel +
                " | LpDelta=" + result.rankedLpDelta +
                " | NewTotalLp=" + result.newRankedLpTotal +
                " | Soft=" + result.totalSoftCurrencyGained +
                " | ChestCount=" + result.totalChestCount +
                " | LeveledUp=" + result.leveledUp,
                this
            );
        }

        Show(result);
    }

    public void Show(OnlineMatchPresentationResult result)
    {
        if (result == null || !result.hasData)
        {
            Debug.LogWarning("[OnlineRewardsObtainedPresenter] Show aborted because result is null or hasData=false.", this);
            return;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        if (logDebug)
            Debug.Log("[OnlineRewardsObtainedPresenter] Starting PlaySequence.", this);

        playRoutine = StartCoroutine(PlaySequence(result));
    }

    public void Hide()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (levelUpOverlayPanel != null)
            levelUpOverlayPanel.SetActive(false);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (logDebug)
            Debug.Log("[OnlineRewardsObtainedPresenter] Hide called.", this);
    }

    private IEnumerator PlaySequence(OnlineMatchPresentationResult result)
    {
        ApplyStaticTexts(result);
        ApplyImmediateVisuals(result);

        if (startDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startDelaySeconds);

        yield return AnimateXp(result);
        yield return AnimateLp(result);

        if (result.leveledUp && levelUpOverlayPanel != null)
        {
            ShowLevelUpOverlay(result);

            if (levelUpOverlayVisibleSeconds > 0f)
                yield return new WaitForSecondsRealtime(levelUpOverlayVisibleSeconds);

            levelUpOverlayPanel.SetActive(false);

            if (logDebug)
                Debug.Log("[OnlineRewardsObtainedPresenter] Level up overlay hidden after timer.", this);
        }

        playRoutine = null;

        if (logDebug)
            Debug.Log("[OnlineRewardsObtainedPresenter] PlaySequence completed.", this);
    }

    private void ApplyStaticTexts(OnlineMatchPresentationResult result)
    {
        if (headerText != null)
            headerText.text = SafeUpper(result.titleText, "VICTORY");

        if (playerNameText != null)
            playerNameText.text = SafeUpper(result.playerName, "PLAYER");

        if (summaryText != null)
            summaryText.text = string.IsNullOrWhiteSpace(result.rewardSummaryText)
                ? string.Empty
                : result.rewardSummaryText.ToUpperInvariant();

        if (normalCurrencyGainText != null)
            normalCurrencyGainText.text = Mathf.Max(0, result.totalSoftCurrencyGained).ToString();

        bool hasChestGain = result.totalChestCount > 0;

        if (chestGainParent != null)
            chestGainParent.SetActive(hasChestGain);

        if (chestCountText != null)
            chestCountText.text = hasChestGain ? "X" + result.totalChestCount : string.Empty;

        if (levelUpOverlayPanel != null)
            levelUpOverlayPanel.SetActive(false);

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] ApplyStaticTexts -> " +
                "Header=" + (headerText != null ? headerText.text : "<null>") +
                " | PlayerName=" + (playerNameText != null ? playerNameText.text : "<null>") +
                " | Summary=" + (summaryText != null ? summaryText.text : "<null>") +
                " | Soft=" + (normalCurrencyGainText != null ? normalCurrencyGainText.text : "<null>") +
                " | ChestActive=" + hasChestGain +
                " | ChestText=" + (chestCountText != null ? chestCountText.text : "<null>"),
                this
            );
        }
    }

    private void ApplyImmediateVisuals(OnlineMatchPresentationResult result)
    {
        SetXpFill(result.startLevelProgress01);

        if (playerLevelText != null)
            playerLevelText.text = "LV " + Mathf.Max(1, result.startLevel);

        if (lpGainedText != null)
            lpGainedText.text = FormatLpDelta(result.rankedLpDelta);

        if (animatedTotalLpText != null)
        {
            int startLp = Mathf.Max(0, result.newRankedLpTotal - result.rankedLpDelta);
            animatedTotalLpText.text = "NEW TOTAL: " + startLp + " LP";
        }

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] ApplyImmediateVisuals -> " +
                "PlayerLevel=" + (playerLevelText != null ? playerLevelText.text : "<null>") +
                " | LpDelta=" + (lpGainedText != null ? lpGainedText.text : "<null>") +
                " | AnimatedTotal=" + (animatedTotalLpText != null ? animatedTotalLpText.text : "<null>") +
                " | StartFill=" + result.startLevelProgress01,
                this
            );
        }
    }

    private IEnumerator AnimateXp(OnlineMatchPresentationResult result)
    {
        float duration = Mathf.Max(0.01f, xpAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            SetXpFill(Mathf.Lerp(result.startLevelProgress01, result.endLevelProgress01, t));

            int displayedLevel = (!result.leveledUp || t < 0.85f)
                ? Mathf.Max(1, result.startLevel)
                : Mathf.Max(1, result.endLevel);

            if (playerLevelText != null)
                playerLevelText.text = "LV " + displayedLevel;

            yield return null;
        }

        SetXpFill(result.endLevelProgress01);

        if (playerLevelText != null)
            playerLevelText.text = "LV " + Mathf.Max(1, result.endLevel);

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] AnimateXp completed -> " +
                "EndFill=" + result.endLevelProgress01 +
                " | FinalLevel=" + (playerLevelText != null ? playerLevelText.text : "<null>"),
                this
            );
        }
    }

    private IEnumerator AnimateLp(OnlineMatchPresentationResult result)
    {
        float duration = Mathf.Max(0.01f, lpAnimationDuration);
        float elapsed = 0f;

        int endLp = Mathf.Max(0, result.newRankedLpTotal);
        int startLp = Mathf.Max(0, endLp - result.rankedLpDelta);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            int displayedLp = Mathf.RoundToInt(Mathf.Lerp(startLp, endLp, t));

            if (animatedTotalLpText != null)
                animatedTotalLpText.text = "NEW TOTAL: " + displayedLp + " LP";

            yield return null;
        }

        if (animatedTotalLpText != null)
            animatedTotalLpText.text = "NEW TOTAL: " + endLp + " LP";

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] AnimateLp completed -> " +
                "FinalLpText=" + (animatedTotalLpText != null ? animatedTotalLpText.text : "<null>"),
                this
            );
        }
    }

    private void ShowLevelUpOverlay(OnlineMatchPresentationResult result)
    {
        if (levelUpOverlayPanel == null)
            return;

        levelUpOverlayPanel.SetActive(true);

        if (levelUpOverlayTitleText != null)
            levelUpOverlayTitleText.text = SafeUpper(result.overlayTitleText, "LEVEL UP!");

        if (levelUpOverlaySoftCurrencyText != null)
            levelUpOverlaySoftCurrencyText.text = result.levelUpBonusSoftCurrency > 0
                ? "+" + result.levelUpBonusSoftCurrency
                : string.Empty;

        bool hasChest = result.levelUpBonusChestCount > 0;

        if (levelUpOverlayChestParent != null)
            levelUpOverlayChestParent.SetActive(hasChest);

        if (levelUpOverlayChestCountText != null)
            levelUpOverlayChestCountText.text = hasChest ? "X" + result.levelUpBonusChestCount : string.Empty;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineRewardsObtainedPresenter] ShowLevelUpOverlay -> " +
                "Title=" + (levelUpOverlayTitleText != null ? levelUpOverlayTitleText.text : "<null>") +
                " | Soft=" + (levelUpOverlaySoftCurrencyText != null ? levelUpOverlaySoftCurrencyText.text : "<null>") +
                " | ChestActive=" + hasChest +
                " | ChestText=" + (levelUpOverlayChestCountText != null ? levelUpOverlayChestCountText.text : "<null>"),
                this
            );
        }
    }

    private void SetXpFill(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (experienceFillImage != null)
            experienceFillImage.fillAmount = clamped;

        if (experienceSlider != null)
            experienceSlider.value = clamped;
    }

    private string FormatLpDelta(int value)
    {
        if (value > 0)
            return "+" + value + " LP";

        if (value < 0)
            return value + " LP";

        return "0 LP";
    }

    private string SafeUpper(string value, string fallback)
    {
        string resolved = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return resolved.ToUpperInvariant();
    }

    private string SafeName(Object obj)
    {
        return obj == null ? "<null>" : obj.name;
    }

    private void ResolveDependencies()
    {
        if (resultStore == null)
            resultStore = OnlineMatchPresentationResultStore.Instance;

#if UNITY_2023_1_OR_NEWER
        if (resultStore == null)
            resultStore = FindFirstObjectByType<OnlineMatchPresentationResultStore>();
#else
        if (resultStore == null)
            resultStore = FindObjectOfType<OnlineMatchPresentationResultStore>();
#endif
    }
}
