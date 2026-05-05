using System.Collections;
using UnityEngine;

public sealed class LuckyShotAnimatedLight : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Light[] lightComponents;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private string emissionColorProperty = "_EmissionColor";

    [Header("Intensity Multipliers")]
    [Tooltip("Usato solo per il dim finale dopo un punto. L'intro reveal non modifica piu l'intensita delle luci.")]
    [SerializeField] private float dimMultiplier = 0.15f;

    [Header("Reveal Animation")]
    [SerializeField] private float revealFadeInSeconds = 0.25f;

    [Header("Winner Animation")]
    [SerializeField] private int winnerBlinkCount = 6;
    [SerializeField] private float winnerBlinkOnSeconds = 0.12f;
    [SerializeField] private float winnerBlinkOffSeconds = 0.08f;
    [SerializeField] private float winnerFadeOutSeconds = 0.45f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private AudioClip winnerBlinkClip;
    [SerializeField] private float revealVolume = 1f;
    [SerializeField] private float winnerBlinkVolume = 1f;

    [Header("Auto Cache")]
    [SerializeField] private bool autoFindLightsInChildren = true;
    [SerializeField] private bool autoFindRenderersInChildren = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs;

    private Coroutine activeRoutine;
    private float[] originalLightIntensities;
    private Color[] originalEmissionColors;
    private bool cachedOriginalValues;

    private void Awake()
    {
        CacheTargetsIfNeeded();
        CacheOriginalValues();
        SetOffImmediate();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    public void SetOffImmediate()
    {
        StopActiveRoutine();
        CacheTargetsIfNeeded();
        CacheOriginalValues();
        SetTargetsEnabled(false);
    }

    public void SetDimImmediate()
    {
        StopActiveRoutine();
        CacheTargetsIfNeeded();
        CacheOriginalValues();
        SetTargetsEnabled(true);
        ApplyDimMultiplier(dimMultiplier);
    }

    public void SetOnImmediate()
    {
        StopActiveRoutine();
        CacheTargetsIfNeeded();
        CacheOriginalValues();
        RestoreOriginalIntensityAndEmission();
        SetTargetsEnabled(true);
    }

    public void PlayReveal()
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(PlayRevealRoutine());
    }

    public Coroutine PlayRevealAsCoroutine()
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(PlayRevealRoutine());
        return activeRoutine;
    }

    public void PlayWinnerAndFadeOut()
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(PlayWinnerAndFadeOutRoutine());
    }

    public Coroutine PlayWinnerAndFadeOutAsCoroutine()
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(PlayWinnerAndFadeOutRoutine());
        return activeRoutine;
    }

    private IEnumerator PlayRevealRoutine()
    {
        CacheTargetsIfNeeded();
        CacheOriginalValues();

        PlayOneShot(revealClip, revealVolume);

        RestoreOriginalIntensityAndEmission();
        SetTargetsEnabled(true);

        if (revealFadeInSeconds > 0f)
            yield return new WaitForSeconds(revealFadeInSeconds);

        RestoreOriginalIntensityAndEmission();
        SetTargetsEnabled(true);
        activeRoutine = null;

        if (verboseLogs)
            Debug.Log("[LuckyShotAnimatedLight] Reveal completed -> " + name, this);
    }

    private IEnumerator PlayWinnerAndFadeOutRoutine()
    {
        CacheTargetsIfNeeded();
        CacheOriginalValues();

        RestoreOriginalIntensityAndEmission();

        int blinkCount = Mathf.Max(1, winnerBlinkCount);

        for (int i = 0; i < blinkCount; i++)
        {
            PlayOneShot(winnerBlinkClip, winnerBlinkVolume);
            RestoreOriginalIntensityAndEmission();
            SetTargetsEnabled(true);

            if (winnerBlinkOnSeconds > 0f)
                yield return new WaitForSeconds(winnerBlinkOnSeconds);

            SetTargetsEnabled(false);

            if (winnerBlinkOffSeconds > 0f)
                yield return new WaitForSeconds(winnerBlinkOffSeconds);
        }

        SetTargetsEnabled(true);

        if (winnerFadeOutSeconds <= 0f)
        {
            ApplyDimMultiplier(dimMultiplier);
            activeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        float startMultiplier = 1f;
        float targetMultiplier = Mathf.Max(0f, dimMultiplier);

        while (elapsed < winnerFadeOutSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / winnerFadeOutSeconds);
            float multiplier = Mathf.Lerp(startMultiplier, targetMultiplier, t);
            ApplyDimMultiplier(multiplier);
            yield return null;
        }

        ApplyDimMultiplier(targetMultiplier);
        activeRoutine = null;

        if (verboseLogs)
            Debug.Log("[LuckyShotAnimatedLight] Winner animation completed -> " + name, this);
    }

    private void SetTargetsEnabled(bool enabled)
    {
        SetLightsEnabled(enabled);
        SetEmissionEnabled(enabled);
    }

    private void SetLightsEnabled(bool enabled)
    {
        if (lightComponents == null)
            return;

        for (int i = 0; i < lightComponents.Length; i++)
        {
            Light targetLight = lightComponents[i];
            if (targetLight == null)
                continue;

            targetLight.enabled = enabled;
        }
    }

    private void SetEmissionEnabled(bool enabled)
    {
        if (emissiveRenderers == null)
            return;

        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            Renderer targetRenderer = emissiveRenderers[i];
            if (targetRenderer == null)
                continue;

            Material material = targetRenderer.material;
            if (material == null)
                continue;

            if (!material.HasProperty(emissionColorProperty))
                continue;

            if (enabled)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
        }
    }

    private void RestoreOriginalIntensityAndEmission()
    {
        RestoreOriginalLightIntensity();
        RestoreOriginalEmissionColor();
    }

    private void RestoreOriginalLightIntensity()
    {
        if (lightComponents == null)
            return;

        for (int i = 0; i < lightComponents.Length; i++)
        {
            Light targetLight = lightComponents[i];
            if (targetLight == null)
                continue;

            targetLight.intensity = GetOriginalLightIntensity(i);
        }
    }

    private void RestoreOriginalEmissionColor()
    {
        if (emissiveRenderers == null)
            return;

        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            Renderer targetRenderer = emissiveRenderers[i];
            if (targetRenderer == null)
                continue;

            Material material = targetRenderer.material;
            if (material == null)
                continue;

            if (!material.HasProperty(emissionColorProperty))
                continue;

            material.SetColor(emissionColorProperty, GetOriginalEmissionColor(i));
        }
    }

    private void ApplyDimMultiplier(float multiplier)
    {
        ApplyLightDimMultiplier(multiplier);
        ApplyEmissionDimMultiplier(multiplier);
    }

    private void ApplyLightDimMultiplier(float multiplier)
    {
        if (lightComponents == null)
            return;

        float safeMultiplier = Mathf.Max(0f, multiplier);

        for (int i = 0; i < lightComponents.Length; i++)
        {
            Light targetLight = lightComponents[i];
            if (targetLight == null)
                continue;

            float baseIntensity = GetOriginalLightIntensity(i);
            targetLight.enabled = safeMultiplier > 0.001f;
            targetLight.intensity = baseIntensity * safeMultiplier;
        }
    }

    private void ApplyEmissionDimMultiplier(float multiplier)
    {
        if (emissiveRenderers == null)
            return;

        float safeMultiplier = Mathf.Max(0f, multiplier);

        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            Renderer targetRenderer = emissiveRenderers[i];
            if (targetRenderer == null)
                continue;

            Material material = targetRenderer.material;
            if (material == null)
                continue;

            if (!material.HasProperty(emissionColorProperty))
                continue;

            Color baseColor = GetOriginalEmissionColor(i);
            Color finalColor = baseColor * safeMultiplier;

            material.SetColor(emissionColorProperty, finalColor);

            if (safeMultiplier > 0.001f)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
        }
    }

    private void CacheTargetsIfNeeded()
    {
        if (autoFindLightsInChildren && (lightComponents == null || lightComponents.Length == 0))
            lightComponents = GetComponentsInChildren<Light>(true);

        if (autoFindRenderersInChildren && (emissiveRenderers == null || emissiveRenderers.Length == 0))
            emissiveRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CacheOriginalValues()
    {
        if (cachedOriginalValues)
            return;

        if (lightComponents != null)
        {
            originalLightIntensities = new float[lightComponents.Length];

            for (int i = 0; i < lightComponents.Length; i++)
            {
                Light targetLight = lightComponents[i];
                originalLightIntensities[i] = targetLight != null
                    ? Mathf.Max(0.001f, targetLight.intensity)
                    : 1f;
            }
        }
        else
        {
            originalLightIntensities = new float[0];
        }

        if (emissiveRenderers != null)
        {
            originalEmissionColors = new Color[emissiveRenderers.Length];

            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                Renderer targetRenderer = emissiveRenderers[i];

                if (targetRenderer == null)
                {
                    originalEmissionColors[i] = Color.white;
                    continue;
                }

                Material material = targetRenderer.material;

                if (material != null && material.HasProperty(emissionColorProperty))
                    originalEmissionColors[i] = material.GetColor(emissionColorProperty);
                else
                    originalEmissionColors[i] = Color.white;
            }
        }
        else
        {
            originalEmissionColors = new Color[0];
        }

        cachedOriginalValues = true;
    }

    private float GetOriginalLightIntensity(int index)
    {
        if (originalLightIntensities == null)
            return 1f;

        if (index < 0 || index >= originalLightIntensities.Length)
            return 1f;

        return Mathf.Max(0.001f, originalLightIntensities[index]);
    }

    private Color GetOriginalEmissionColor(int index)
    {
        if (originalEmissionColors == null)
            return Color.white;

        if (index < 0 || index >= originalEmissionColors.Length)
            return Color.white;

        return originalEmissionColors[index];
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (audioSource == null)
            return;

        if (clip == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }
}
