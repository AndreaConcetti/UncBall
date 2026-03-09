using UnityEngine;
using TMPro;
using System.Collections;

public class ScoreRollDisplay : MonoBehaviour
{
    [System.Serializable]
    public class RollingScoreUI
    {
        public TMP_Text currentText;
        public TMP_Text nextText;

        [HideInInspector] public int displayedScore;
        [HideInInspector] public bool isAnimating;
        [HideInInspector] public RectTransform currentRect;
        [HideInInspector] public RectTransform nextRect;
        [HideInInspector] public Vector2 currentStartPos;
        [HideInInspector] public Vector2 nextStartPos;
        [HideInInspector] public Coroutine colorReturnCoroutine;
    }

    [Header("Player UI")]
    public RollingScoreUI player1UI;
    public RollingScoreUI player2UI;

    [Header("Animation")]
    public float stepDuration = 0.12f;
    public float verticalOffset = 60f;

    [Header("Colors")]
    public Color normalColor = Color.black;
    public Color rollingColor = Color.yellow;

    [Header("Color Return")]
    public float colorReturnDuration = 2f;

    [Header("Fade")]
    [Range(0f, 1f)] public float currentTopFade = 0f;
    [Range(0f, 1f)] public float nextBottomFade = 0f;

    void Start()
    {
        Setup(player1UI, ScoreManager.Instance.player1Score);
        Setup(player2UI, ScoreManager.Instance.player2Score);
    }

    void Update()
    {
        int targetP1 = ScoreManager.Instance.player1Score;
        int targetP2 = ScoreManager.Instance.player2Score;

        if (!player1UI.isAnimating && targetP1 > player1UI.displayedScore)
            StartCoroutine(AnimateScoreIncrease(player1UI, targetP1));

        if (!player2UI.isAnimating && targetP2 > player2UI.displayedScore)
            StartCoroutine(AnimateScoreIncrease(player2UI, targetP2));
    }

    void Setup(RollingScoreUI ui, int initialScore)
    {
        ui.currentRect = ui.currentText.rectTransform;
        ui.nextRect = ui.nextText.rectTransform;

        ui.currentStartPos = ui.currentRect.anchoredPosition;
        ui.nextStartPos = ui.currentStartPos + new Vector2(0f, -verticalOffset);

        ui.currentRect.anchoredPosition = ui.currentStartPos;
        ui.nextRect.anchoredPosition = ui.nextStartPos;

        ui.displayedScore = initialScore;
        ui.currentText.text = initialScore.ToString();
        ui.nextText.text = "";
        ui.isAnimating = false;
        ui.colorReturnCoroutine = null;

        SetTextColor(ui.currentText, normalColor, 1f);
        SetTextColor(ui.nextText, normalColor, 0f);
    }

    IEnumerator AnimateScoreIncrease(RollingScoreUI ui, int targetScore)
    {
        ui.isAnimating = true;

        if (ui.colorReturnCoroutine != null)
        {
            StopCoroutine(ui.colorReturnCoroutine);
            ui.colorReturnCoroutine = null;
        }

        while (ui.displayedScore < targetScore)
        {
            int nextValue = ui.displayedScore + 1;
            ui.nextText.text = nextValue.ToString();

            ui.currentRect.anchoredPosition = ui.currentStartPos;
            ui.nextRect.anchoredPosition = ui.nextStartPos;

            SetTextColor(ui.currentText, rollingColor, 1f);
            SetTextColor(ui.nextText, rollingColor, nextBottomFade);

            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / stepDuration;
                float eased = EaseOutCubic(Mathf.Clamp01(t));

                ui.currentRect.anchoredPosition = Vector2.Lerp(
                    ui.currentStartPos,
                    ui.currentStartPos + new Vector2(0f, verticalOffset),
                    eased
                );

                ui.nextRect.anchoredPosition = Vector2.Lerp(
                    ui.nextStartPos,
                    ui.currentStartPos,
                    eased
                );

                float currentAlpha = Mathf.Lerp(1f, currentTopFade, eased);
                float nextAlpha = Mathf.Lerp(nextBottomFade, 1f, eased);

                SetTextColor(ui.currentText, rollingColor, currentAlpha);
                SetTextColor(ui.nextText, rollingColor, nextAlpha);

                yield return null;
            }

            ui.displayedScore = nextValue;
            ui.currentText.text = ui.displayedScore.ToString();

            ui.currentRect.anchoredPosition = ui.currentStartPos;
            ui.nextRect.anchoredPosition = ui.nextStartPos;
            ui.nextText.text = "";

            SetTextColor(ui.currentText, rollingColor, 1f);
            SetTextColor(ui.nextText, normalColor, 0f);
        }

        ui.isAnimating = false;
        ui.colorReturnCoroutine = StartCoroutine(ReturnToNormalColor(ui));
    }

    IEnumerator ReturnToNormalColor(RollingScoreUI ui)
    {
        float timer = 0f;

        Color startColor = ui.currentText.color;
        Color endColor = normalColor;
        endColor.a = 1f;

        while (timer < colorReturnDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / colorReturnDuration);

            ui.currentText.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        SetTextColor(ui.currentText, normalColor, 1f);
        ui.colorReturnCoroutine = null;
    }

    void SetTextColor(TMP_Text text, Color baseColor, float alpha)
    {
        Color c = baseColor;
        c.a = alpha;
        text.color = c;
    }

    float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}