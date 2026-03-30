using System.Collections;
using TMPro;
using UnityEngine;

public class ShotScorePopupUI : MonoBehaviour
{
    [Header("References")]
    public ScoreManager scoreManager;
    public Camera worldCamera;
    public Canvas parentCanvas;
    public RectTransform canvasRect;

    [Header("Shared Popup")]
    public RectTransform popupRoot;
    public CanvasGroup comboCanvasGroup;
    public TMP_Text comboText;
    public CanvasGroup pointsCanvasGroup;
    public TMP_Text pointsText;

    [Header("World To UI Offset")]
    public Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);
    public Vector2 screenOffset = new Vector2(0f, 30f);

    [Header("Text Formatting")]
    public string comboPrefix = "COMBO X";
    public string comboSuffix = "!";
    public string pointsPrefix = "+";

    [Header("Timings")]
    public float comboFadeInDuration = 0.15f;
    public float comboVisibleDuration = 0.45f;
    public float comboFadeOutDuration = 0.15f;
    public float delayBetweenComboAndPoints = 0.08f;
    public float pointsFadeInDuration = 0.15f;
    public float pointsVisibleDuration = 0.55f;
    public float pointsFadeOutDuration = 0.2f;

    [Header("Movement")]
    public bool animateVerticalFloat = true;
    public float comboFloatY = 20f;
    public float pointsFloatY = 20f;

    private Coroutine popupRoutine;
    private Vector2 comboLocalStartPos;
    private Vector2 pointsLocalStartPos;

    private bool isFollowingWorldTarget;
    private Vector3 currentTrackedWorldPosition;

    void Awake()
    {
        AutoAssignReferences();
        CacheLocalStartPositions();
        ForceHide();
    }

    void LateUpdate()
    {
        if (isFollowingWorldTarget)
            UpdatePopupAnchorFromTrackedWorldPosition();
    }

    void OnEnable()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (scoreManager != null)
            scoreManager.onShotScoreDetailed.AddListener(HandleShotScoreDetailed);
    }

    void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.onShotScoreDetailed.RemoveListener(HandleShotScoreDetailed);
    }

    void AutoAssignReferences()
    {
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        if (canvasRect == null && parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (worldCamera == null)
        {
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                worldCamera = parentCanvas.worldCamera;

            if (worldCamera == null)
                worldCamera = Camera.main;
        }
    }

    void CacheLocalStartPositions()
    {
        comboLocalStartPos = GetLocalAnchoredPosition(comboText);
        pointsLocalStartPos = GetLocalAnchoredPosition(pointsText);
    }

    Vector2 GetLocalAnchoredPosition(TMP_Text text)
    {
        if (text == null)
            return Vector2.zero;

        return text.rectTransform.anchoredPosition;
    }

    void HandleShotScoreDetailed(ShotScoreData data)
    {
        if (data == null)
            return;

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        popupRoutine = StartCoroutine(PlayPopupSequence(data));
    }

    IEnumerator PlayPopupSequence(ShotScoreData data)
    {
        currentTrackedWorldPosition = data.slotWorldPosition;
        isFollowingWorldTarget = true;

        UpdatePopupAnchorFromTrackedWorldPosition();
        ResetPopupVisuals();

        if (popupRoot != null)
            popupRoot.gameObject.SetActive(true);

        if (comboText != null)
            comboText.text = comboPrefix + data.comboStreak + comboSuffix;

        if (pointsText != null)
            pointsText.text = pointsPrefix + data.shotPoints;

        yield return FadeAndFloat(
            comboCanvasGroup,
            comboText,
            comboLocalStartPos,
            comboFloatY,
            comboFadeInDuration,
            comboVisibleDuration,
            comboFadeOutDuration
        );

        yield return Wait(delayBetweenComboAndPoints);

        yield return FadeAndFloat(
            pointsCanvasGroup,
            pointsText,
            pointsLocalStartPos,
            pointsFloatY,
            pointsFadeInDuration,
            pointsVisibleDuration,
            pointsFadeOutDuration
        );

        isFollowingWorldTarget = false;
        HidePopup();
        popupRoutine = null;
    }

    void UpdatePopupAnchorFromTrackedWorldPosition()
    {
        if (popupRoot == null || canvasRect == null)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 worldPoint = currentTrackedWorldPosition + worldOffset;
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);

        if (screenPoint.z < 0f)
        {
            popupRoot.gameObject.SetActive(false);
            return;
        }

        Vector2 localPoint;
        Camera eventCamera = null;

        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            eventCamera = parentCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            eventCamera,
            out localPoint
        );

        popupRoot.anchoredPosition = localPoint + screenOffset;
    }

    IEnumerator FadeAndFloat(
        CanvasGroup canvasGroup,
        TMP_Text text,
        Vector2 startPos,
        float floatY,
        float fadeInDuration,
        float visibleDuration,
        float fadeOutDuration)
    {
        if (canvasGroup == null || text == null)
            yield break;

        RectTransform rect = text.rectTransform;

        canvasGroup.alpha = 0f;
        rect.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);

            canvasGroup.alpha = t;

            if (animateVerticalFloat)
                rect.anchoredPosition = Vector2.Lerp(startPos, startPos + Vector2.up * floatY, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;

        elapsed = 0f;
        while (elapsed < visibleDuration)
        {
            elapsed += Time.deltaTime;

            if (animateVerticalFloat)
            {
                float t = visibleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / visibleDuration);
                rect.anchoredPosition = Vector2.Lerp(startPos, startPos + Vector2.up * floatY, t);
            }

            yield return null;
        }

        Vector2 fadeOutStartPos = rect.anchoredPosition;
        Vector2 fadeOutEndPos = startPos + Vector2.up * floatY;

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeOutDuration);

            canvasGroup.alpha = 1f - t;

            if (animateVerticalFloat)
                rect.anchoredPosition = Vector2.Lerp(fadeOutStartPos, fadeOutEndPos, t);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        rect.anchoredPosition = startPos;
    }

    IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void ResetPopupVisuals()
    {
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(true);

        if (comboCanvasGroup != null)
            comboCanvasGroup.alpha = 0f;

        if (pointsCanvasGroup != null)
            pointsCanvasGroup.alpha = 0f;

        if (comboText != null)
            comboText.rectTransform.anchoredPosition = comboLocalStartPos;

        if (pointsText != null)
            pointsText.rectTransform.anchoredPosition = pointsLocalStartPos;
    }

    void HidePopup()
    {
        if (comboCanvasGroup != null)
            comboCanvasGroup.alpha = 0f;

        if (pointsCanvasGroup != null)
            pointsCanvasGroup.alpha = 0f;

        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    void ForceHide()
    {
        isFollowingWorldTarget = false;
        HidePopup();
    }
}