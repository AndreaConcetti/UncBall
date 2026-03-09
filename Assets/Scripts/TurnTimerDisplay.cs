using UnityEngine;
using TMPro;

public class TurnTimerDisplay : MonoBehaviour
{
    [Header("References")]
    public TMP_Text timerText;

    [Header("Colors")]
    public Color startColor = Color.black;
    public Color endColor = Color.red;
    public float colorStartTime = 10f;

    [Header("Pulse Settings")]
    public float pulseStartTime = 5f;
    public float pulseScale = 1.75f;
    public float pulseSpeed = 8f;

    [Header("Shake Settings")]
    public float shakeStartTime = 3f;
    public float shakeAmount = 6f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private int lastDisplayedSecond = -1;
    private float pulseTimer = 0f;

    void Start()
    {
        originalScale = timerText.transform.localScale;
        originalPosition = timerText.transform.localPosition;
    }

    void Update()
    {
        float timer = TurnManager.Instance.CurrentTimer;
        int seconds = Mathf.CeilToInt(timer);

        timerText.text = seconds.ToString();

        UpdateColor(timer);
        UpdatePulse(timer, seconds);
        UpdateShake(timer);
    }

    void UpdateColor(float timer)
    {
        if (timer > colorStartTime)
        {
            timerText.color = startColor;
        }
        else
        {
            float t = 1f - Mathf.Clamp01(timer / colorStartTime);
            timerText.color = Color.Lerp(startColor, endColor, t);
        }
    }

    void UpdatePulse(float timer, int seconds)
    {
        if (timer <= pulseStartTime && seconds != lastDisplayedSecond)
        {
            pulseTimer = 1f;
            lastDisplayedSecond = seconds;
        }

        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.deltaTime * pulseSpeed;
            pulseTimer = Mathf.Max(pulseTimer, 0f);

            float scale = Mathf.Lerp(pulseScale, 1f, 1f - pulseTimer);
            timerText.transform.localScale = originalScale * scale;
        }
        else
        {
            timerText.transform.localScale = originalScale;
        }
    }

    void UpdateShake(float timer)
    {
        if (timer <= shakeStartTime)
        {
            Vector2 offset = Random.insideUnitCircle * shakeAmount;
            timerText.transform.localPosition = originalPosition + new Vector3(offset.x, offset.y, 0f);
        }
        else
        {
            timerText.transform.localPosition = originalPosition;
        }
    }
}