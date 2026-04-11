using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct ChestVisualMapping
{
    public ChestType chestType;
    public Sprite sprite;
    public string label;
}

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
    [SerializeField] private TMP_Text chestTypeText;
    [SerializeField] private TMP_Text summaryText;

    [Header("Main Chest Visual")]
    [SerializeField] private Image chestTypeImage;

    [Header("XP Progress")]
    [SerializeField] private Image experienceFillImage;
    [SerializeField] private Slider experienceSlider;

    [Header("Level Up Overlay")]
    [SerializeField] private TMP_Text levelUpOverlayTitleText;
    [SerializeField] private TMP_Text levelUpOverlaySoftCurrencyText;
    [SerializeField] private GameObject levelUpOverlayChestParent;
    [SerializeField] private TMP_Text levelUpOverlayChestCountText;
    [SerializeField] private TMP_Text levelUpOverlayChestTypeText;
    [SerializeField] private Image levelUpOverlayChestTypeImage;

    [Header("Chest Visual Mappings")]
    [SerializeField] private ChestVisualMapping[] chestVisualMappings;

    [Header("Animation")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private float startDelaySeconds = 0.05f;
    [SerializeField] private float xpAnimationDuration = 0.75f;
    [SerializeField] private float lpAnimationDuration = 0.90f;
    [SerializeField] private float levelUpOverlayVisibleSeconds = 2.5f;
    [SerializeField] private float levelUpXpResetPauseSeconds = 0.15f;

    private Coroutine playRoutine;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (playOnEnable)
            ShowLatest();
    }

    public void ShowLatest()
    {
        ResolveDependencies();

        if (resultStore == null)
            return;

        if (!resultStore.TryGetLatest(out OnlineMatchPresentationResult result))
            return;

        Show(result);
    }

    public void Show(OnlineMatchPresentationResult result)
    {
        if (result == null || !result.hasData)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (playRoutine != null)
            StopCoroutine(playRoutine);

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
    }

    private IEnumerator PlaySequence(OnlineMatchPresentationResult result)
    {
        ApplyStaticTexts(result);
        ApplyImmediateVisuals(result);

        if (startDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startDelaySeconds);

        yield return AnimateXp(result);
        yield return AnimateLp(result);

        bool shouldShowLevelUpOverlay =
            levelUpOverlayPanel != null &&
            (result.leveledUp || result.levelUpCount > 0 || result.endLevel > result.startLevel);

        if (shouldShowLevelUpOverlay)
        {
            ShowLevelUpOverlay(result);

            if (levelUpOverlayVisibleSeconds > 0f)
                yield return new WaitForSecondsRealtime(levelUpOverlayVisibleSeconds);

            levelUpOverlayPanel.SetActive(false);
        }

        playRoutine = null;
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

        ApplyChestVisual(result.totalChestType, chestTypeImage, chestTypeText, hasChestGain);

        if (levelUpOverlayPanel != null)
            levelUpOverlayPanel.SetActive(false);
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
    }

    private IEnumerator AnimateXp(OnlineMatchPresentationResult result)
    {
        int startLevel = Mathf.Max(1, result.startLevel);
        int endLevel = Mathf.Max(1, result.endLevel);

        if (endLevel <= startLevel)
        {
            yield return AnimateXpSegment(
                result.startLevelProgress01,
                result.endLevelProgress01,
                startLevel,
                xpAnimationDuration);

            yield break;
        }

        float firstSegmentDuration = Mathf.Max(0.05f, xpAnimationDuration * 0.55f);
        float resetPause = Mathf.Max(0f, levelUpXpResetPauseSeconds);
        float secondSegmentDuration = Mathf.Max(0.05f, xpAnimationDuration * 0.45f);

        yield return AnimateXpSegment(
            result.startLevelProgress01,
            1f,
            startLevel,
            firstSegmentDuration);

        if (playerLevelText != null)
            playerLevelText.text = "LV " + endLevel;

        SetXpFill(0f);

        if (resetPause > 0f)
            yield return new WaitForSecondsRealtime(resetPause);

        yield return AnimateXpSegment(
            0f,
            result.endLevelProgress01,
            endLevel,
            secondSegmentDuration);
    }

    private IEnumerator AnimateXpSegment(float fromFill, float toFill, int levelToDisplay, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            SetXpFill(Mathf.Lerp(fromFill, toFill, t));

            if (playerLevelText != null)
                playerLevelText.text = "LV " + Mathf.Max(1, levelToDisplay);

            yield return null;
        }

        SetXpFill(toFill);

        if (playerLevelText != null)
            playerLevelText.text = "LV " + Mathf.Max(1, levelToDisplay);
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

        ApplyChestVisual(result.levelUpBonusChestType, levelUpOverlayChestTypeImage, levelUpOverlayChestTypeText, hasChest);
    }

    private void ApplyChestVisual(ChestType chestType, Image imageTarget, TMP_Text labelTarget, bool visible)
    {
        if (imageTarget != null)
            imageTarget.gameObject.SetActive(visible);

        if (labelTarget != null)
            labelTarget.gameObject.SetActive(visible);

        if (!visible)
        {
            if (labelTarget != null)
                labelTarget.text = string.Empty;

            return;
        }

        ChestVisualMapping mapping = FindChestVisualMapping(chestType);

        if (imageTarget != null)
            imageTarget.sprite = mapping.sprite;

        if (labelTarget != null)
        {
            string resolvedLabel = string.IsNullOrWhiteSpace(mapping.label)
                ? BuildFallbackChestLabel(chestType)
                : mapping.label;

            labelTarget.text = resolvedLabel.ToUpperInvariant();
        }
    }

    private ChestVisualMapping FindChestVisualMapping(ChestType chestType)
    {
        if (chestVisualMappings != null)
        {
            for (int i = 0; i < chestVisualMappings.Length; i++)
            {
                if (chestVisualMappings[i].chestType.Equals(chestType))
                    return chestVisualMappings[i];
            }
        }

        return new ChestVisualMapping
        {
            chestType = chestType,
            sprite = null,
            label = BuildFallbackChestLabel(chestType)
        };
    }

    private string BuildFallbackChestLabel(ChestType chestType)
    {
        string raw = chestType.ToString().ToUpperInvariant();

        if (raw == "RANDOM")
            return "RANDOM";

        if (raw.Contains("LEGENDARY"))
            return "LEGENDARY";

        if (raw.Contains("EPIC"))
            return "EPIC";

        if (raw.Contains("RARE"))
            return "RARE";

        if (raw.Contains("COMMON"))
            return "COMMON";

        return raw.Replace("GUARANTEED_", string.Empty).Replace("_", " ");
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
