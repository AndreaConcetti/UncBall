using UnityEngine;
using TMPro;

public class FPSDebugDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Text fpsText;

    [Header("Update Settings")]
    public float updateInterval = 0.25f;

    [Header("Smoothing")]
    public bool useSmoothedDeltaTime = true;
    public float smoothingSpeed = 10f;

    [Header("Colors")]
    public bool useColorFeedback = true;
    public Color goodFpsColor = Color.green;
    public Color mediumFpsColor = Color.yellow;
    public Color lowFpsColor = Color.red;

    [Header("Thresholds")]
    public int goodFpsThreshold = 50;
    public int mediumFpsThreshold = 30;

    [Header("Display")]
    public bool showMilliseconds = true;
    public bool showAverageFps = true;
    public bool showTargetFps = true;

    private float timer;
    private float smoothedDeltaTime;
    private float accumulatedTime;
    private int frameCount;

    void Awake()
    {
        smoothedDeltaTime = Time.unscaledDeltaTime;
    }

    void Update()
    {
        float currentDelta = Time.unscaledDeltaTime;

        if (useSmoothedDeltaTime)
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, currentDelta, smoothingSpeed * Time.unscaledDeltaTime);
        else
            smoothedDeltaTime = currentDelta;

        timer += Time.unscaledDeltaTime;
        accumulatedTime += Time.unscaledDeltaTime;
        frameCount++;

        if (timer < updateInterval)
            return;

        float usedDelta = Mathf.Max(0.0001f, smoothedDeltaTime);
        int fps = Mathf.RoundToInt(1f / usedDelta);
        float ms = usedDelta * 1000f;
        int avgFps = Mathf.RoundToInt(frameCount / Mathf.Max(0.0001f, accumulatedTime));

        string text = "FPS: " + fps;

        if (showAverageFps)
            text += " | AVG: " + avgFps;

        if (showMilliseconds)
            text += " | " + ms.ToString("F1") + " ms";

        if (showTargetFps)
            text += " | Target: " + Application.targetFrameRate;

        if (fpsText != null)
        {
            fpsText.text = text;

            if (useColorFeedback)
            {
                if (fps >= goodFpsThreshold)
                    fpsText.color = goodFpsColor;
                else if (fps >= mediumFpsThreshold)
                    fpsText.color = mediumFpsColor;
                else
                    fpsText.color = lowFpsColor;
            }
        }

        timer = 0f;
    }
}