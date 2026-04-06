using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
    private const string PrefTutorialPromptEnabled = "SETTINGS_TUTORIAL_PROMPT_ENABLED";
    private const string PrefAudioEnabled = "SETTINGS_AUDIO_ENABLED";
    private const string PrefDarkThemeEnabled = "SETTINGS_DARK_THEME_ENABLED";

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Tutorial Prompt")]
    [SerializeField] private Button tutorialPromptButton;
    [SerializeField] private Image tutorialPromptStateImage;
    [SerializeField] private Sprite tutorialPromptEnabledSprite;
    [SerializeField] private Sprite tutorialPromptDisabledSprite;

    [Header("Audio")]
    [SerializeField] private Button audioButton;
    [SerializeField] private Image audioStateImage;
    [SerializeField] private Sprite audioEnabledSprite;
    [SerializeField] private Sprite audioDisabledSprite;

    [Header("Table Theme")]
    [SerializeField] private Button tableThemeButton;
    [SerializeField] private TMP_Text tableThemeValueText;
    [SerializeField] private string tableThemeLightText = "LIGHT";
    [SerializeField] private string tableThemeDarkText = "DARK";

    [Header("Direct Table Theme Apply")]
    [SerializeField] private Renderer[] tableThemeTargetRenderers;
    [SerializeField] private Material lightTableMaterial;
    [SerializeField] private Material darkTableMaterial;
    [SerializeField] private bool instantiateMaterialsAtRuntime = true;

    [Header("Behavior")]
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool autoWireUi = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool tutorialPromptsEnabled = true;
    private bool audioEnabled = true;
    private bool darkThemeEnabled = false;

    private Material runtimeLightMaterialInstance;
    private Material runtimeDarkMaterialInstance;

    public bool IsPanelOpen => panelRoot != null && panelRoot.activeSelf;
    public bool TutorialPromptsEnabled => tutorialPromptsEnabled;
    public bool AudioEnabled => audioEnabled;
    public bool DarkThemeEnabled => darkThemeEnabled;

    public event Action<bool> TutorialPromptSettingChanged;
    public event Action<bool> AudioSettingChanged;
    public event Action<bool> TableThemeDarkModeChanged;
    public event Action<bool> PanelOpenStateChanged;

    private void Awake()
    {
        LoadPreferences();
        PrepareRuntimeMaterials();

        if (autoWireUi)
            WireButtons();

        if (panelRoot != null)
            panelRoot.SetActive(!startClosed);

        ApplyAllVisualState();
        ApplyAllRuntimeState(notifyListeners: true);

        if (logDebug)
        {
            Debug.Log(
                $"[SettingsPanelUI] Awake -> Tutorial={tutorialPromptsEnabled} | Audio={audioEnabled} | DarkTheme={darkThemeEnabled}",
                this);
        }
    }

    private void OnEnable()
    {
        if (autoWireUi)
            WireButtons();

        ApplyAllVisualState();
        ApplyAllRuntimeState(notifyListeners: true);
    }

    private void OnDisable()
    {
        UnwireButtons();
    }

    private void OnDestroy()
    {
        if (runtimeLightMaterialInstance != null)
            Destroy(runtimeLightMaterialInstance);

        if (runtimeDarkMaterialInstance != null)
            Destroy(runtimeDarkMaterialInstance);
    }

    private void WireButtons()
    {
        UnwireButtons();

        if (openButton != null)
            openButton.onClick.AddListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (tutorialPromptButton != null)
            tutorialPromptButton.onClick.AddListener(ToggleTutorialPrompt);

        if (audioButton != null)
            audioButton.onClick.AddListener(ToggleAudio);

        if (tableThemeButton != null)
            tableThemeButton.onClick.AddListener(ToggleTableTheme);
    }

    private void UnwireButtons()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);

        if (tutorialPromptButton != null)
            tutorialPromptButton.onClick.RemoveListener(ToggleTutorialPrompt);

        if (audioButton != null)
            audioButton.onClick.RemoveListener(ToggleAudio);

        if (tableThemeButton != null)
            tableThemeButton.onClick.RemoveListener(ToggleTableTheme);
    }

    private void LoadPreferences()
    {
        tutorialPromptsEnabled = PlayerPrefs.GetInt(PrefTutorialPromptEnabled, 1) == 1;
        audioEnabled = PlayerPrefs.GetInt(PrefAudioEnabled, 1) == 1;
        darkThemeEnabled = PlayerPrefs.GetInt(PrefDarkThemeEnabled, 0) == 1;
    }

    private void SavePreferences()
    {
        PlayerPrefs.SetInt(PrefTutorialPromptEnabled, tutorialPromptsEnabled ? 1 : 0);
        PlayerPrefs.SetInt(PrefAudioEnabled, audioEnabled ? 1 : 0);
        PlayerPrefs.SetInt(PrefDarkThemeEnabled, darkThemeEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void PrepareRuntimeMaterials()
    {
        if (!instantiateMaterialsAtRuntime)
            return;

        if (lightTableMaterial != null && runtimeLightMaterialInstance == null)
            runtimeLightMaterialInstance = new Material(lightTableMaterial);

        if (darkTableMaterial != null && runtimeDarkMaterialInstance == null)
            runtimeDarkMaterialInstance = new Material(darkTableMaterial);
    }

    public void OpenPanel()
    {
        SetPanelOpen(true);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    public void TogglePanel()
    {
        SetPanelOpen(!IsPanelOpen);
    }

    public void SetPanelOpen(bool open)
    {
        if (panelRoot != null)
            panelRoot.SetActive(open);

        PanelOpenStateChanged?.Invoke(open);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] SetPanelOpen -> " + open, this);
    }

    public void ToggleTutorialPrompt()
    {
        SetTutorialPromptEnabled(!tutorialPromptsEnabled);
    }

    public void ToggleAudio()
    {
        SetAudioEnabled(!audioEnabled);
    }

    public void ToggleTableTheme()
    {
        SetDarkThemeEnabled(!darkThemeEnabled);
    }

    public void SetTutorialPromptEnabled(bool enabled)
    {
        if (tutorialPromptsEnabled == enabled)
            return;

        tutorialPromptsEnabled = enabled;
        SavePreferences();

        ApplyTutorialPromptVisualState();
        TutorialPromptSettingChanged?.Invoke(enabled);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] TutorialPrompts -> " + enabled, this);
    }

    public void SetAudioEnabled(bool enabled)
    {
        if (audioEnabled == enabled)
            return;

        audioEnabled = enabled;
        SavePreferences();

        ApplyAudioVisualState();
        AudioSettingChanged?.Invoke(enabled);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] Audio -> " + enabled, this);
    }

    public void SetDarkThemeEnabled(bool enabled)
    {
        if (darkThemeEnabled == enabled)
            return;

        darkThemeEnabled = enabled;
        SavePreferences();

        ApplyTableThemeVisualState();
        ApplyTableThemeToRenderers();
        TableThemeDarkModeChanged?.Invoke(enabled);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] DarkTheme -> " + enabled, this);
    }

    private void ApplyAllVisualState()
    {
        ApplyTutorialPromptVisualState();
        ApplyAudioVisualState();
        ApplyTableThemeVisualState();
    }

    private void ApplyAllRuntimeState(bool notifyListeners)
    {
        ApplyTableThemeToRenderers();

        if (notifyListeners)
        {
            TutorialPromptSettingChanged?.Invoke(tutorialPromptsEnabled);
            AudioSettingChanged?.Invoke(audioEnabled);
            TableThemeDarkModeChanged?.Invoke(darkThemeEnabled);
        }
    }

    private void ApplyTutorialPromptVisualState()
    {
        if (tutorialPromptStateImage != null)
        {
            tutorialPromptStateImage.sprite = tutorialPromptsEnabled
                ? tutorialPromptEnabledSprite
                : tutorialPromptDisabledSprite;
        }
    }

    private void ApplyAudioVisualState()
    {
        if (audioStateImage != null)
        {
            audioStateImage.sprite = audioEnabled
                ? audioEnabledSprite
                : audioDisabledSprite;
        }
    }

    private void ApplyTableThemeVisualState()
    {
        if (tableThemeValueText != null)
        {
            tableThemeValueText.text = darkThemeEnabled
                ? tableThemeDarkText
                : tableThemeLightText;
        }
    }

    private void ApplyTableThemeToRenderers()
    {
        if (tableThemeTargetRenderers == null || tableThemeTargetRenderers.Length == 0)
            return;

        Material targetMaterial = GetTargetThemeMaterial();
        if (targetMaterial == null)
            return;

        for (int i = 0; i < tableThemeTargetRenderers.Length; i++)
        {
            Renderer targetRenderer = tableThemeTargetRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.sharedMaterial = targetMaterial;
        }
    }

    private Material GetTargetThemeMaterial()
    {
        if (darkThemeEnabled)
        {
            if (instantiateMaterialsAtRuntime && runtimeDarkMaterialInstance != null)
                return runtimeDarkMaterialInstance;

            return darkTableMaterial;
        }

        if (instantiateMaterialsAtRuntime && runtimeLightMaterialInstance != null)
            return runtimeLightMaterialInstance;

        return lightTableMaterial;
    }
}