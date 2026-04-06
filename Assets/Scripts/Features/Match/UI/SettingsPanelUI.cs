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
    [SerializeField] private TMP_Text tutorialPromptValueText;
    [SerializeField] private string tutorialPromptEnabledText = "ON";
    [SerializeField] private string tutorialPromptDisabledText = "OFF";

    [Header("Audio")]
    [SerializeField] private Button audioButton;
    [SerializeField] private Image audioStateImage;
    [SerializeField] private Sprite audioEnabledSprite;
    [SerializeField] private Sprite audioDisabledSprite;
    [SerializeField] private TMP_Text audioValueText;
    [SerializeField] private string audioEnabledText = "ON";
    [SerializeField] private string audioDisabledText = "OFF";

    [Header("Table Theme")]
    [SerializeField] private Button tableThemeButton;
    [SerializeField] private Image tableThemeStateImage;
    [SerializeField] private Sprite tableThemeLightSprite;
    [SerializeField] private Sprite tableThemeDarkSprite;
    [SerializeField] private TMP_Text tableThemeValueText;
    [SerializeField] private string tableThemeLightText = "LIGHT";
    [SerializeField] private string tableThemeDarkText = "DARK";

    [Header("Dependencies")]
    [SerializeField] private TableThemeController tableThemeController;

    [Header("Behavior")]
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool autoWireUi = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool tutorialPromptsEnabled = true;
    private bool audioEnabled = true;
    private bool darkThemeEnabled = false;

    public bool IsPanelOpen => panelRoot != null && panelRoot.activeSelf;
    public bool TutorialPromptsEnabled => tutorialPromptsEnabled;
    public bool AudioEnabled => audioEnabled;
    public bool DarkThemeEnabled => darkThemeEnabled;

    // Eventi legacy attesi dagli altri script del progetto
    public event Action<bool> TutorialPromptSettingChanged;
    public event Action<bool> AudioSettingChanged;
    public event Action<bool> TableThemeDarkModeChanged;
    public event Action<bool> PanelOpenStateChanged;

    // Eventi alias aggiuntivi, per compatibilità futura
    public event Action<bool> OnTutorialPromptsChanged;
    public event Action<bool> OnAudioChanged;
    public event Action<bool> OnTableThemeChanged;
    public event Action<bool> OnPanelOpenStateChanged;

    private void Awake()
    {
        LoadPreferences();
        ResolveDependencies();

        if (autoWireUi)
            WireButtons();

        SetPanelOpen(!startClosed, true);
        ApplyAllVisualState();
        ApplyAllRuntimeState(true);
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (autoWireUi)
            WireButtons();

        ApplyAllVisualState();
        ApplyAllRuntimeState(true);
    }

    private void OnDisable()
    {
        UnwireButtons();
    }

    private void ResolveDependencies()
    {
        if (tableThemeController == null)
        {
#if UNITY_2023_1_OR_NEWER
            tableThemeController = FindFirstObjectByType<TableThemeController>();
#else
            tableThemeController = FindObjectOfType<TableThemeController>();
#endif
        }
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

    public void OpenPanel()
    {
        SetPanelOpen(true, false);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false, false);
    }

    public void TogglePanel()
    {
        SetPanelOpen(!IsPanelOpen, false);
    }

    public void SetPanelOpen(bool open, bool silent)
    {
        if (panelRoot != null)
            panelRoot.SetActive(open);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] SetPanelOpen -> " + open, this);

        if (!silent)
            RaisePanelOpenStateChanged(open);
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
        RaiseTutorialPromptChanged(enabled);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] Tutorial prompts -> " + enabled, this);
    }

    public void SetAudioEnabled(bool enabled)
    {
        if (audioEnabled == enabled)
            return;

        audioEnabled = enabled;
        SavePreferences();

        ApplyAudioVisualState();
        RaiseAudioChanged(enabled);

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
        RaiseTableThemeChanged(enabled);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] Dark theme -> " + enabled, this);
    }

    private void ApplyAllVisualState()
    {
        ApplyTutorialPromptVisualState();
        ApplyAudioVisualState();
        ApplyTableThemeVisualState();
    }

    private void ApplyAllRuntimeState(bool notifyListeners)
    {
        if (!notifyListeners)
            return;

        RaiseTutorialPromptChanged(tutorialPromptsEnabled);
        RaiseAudioChanged(audioEnabled);
        RaiseTableThemeChanged(darkThemeEnabled);
    }

    private void ApplyTutorialPromptVisualState()
    {
        if (tutorialPromptStateImage != null)
            tutorialPromptStateImage.sprite = tutorialPromptsEnabled ? tutorialPromptEnabledSprite : tutorialPromptDisabledSprite;

        if (tutorialPromptValueText != null)
            tutorialPromptValueText.text = tutorialPromptsEnabled ? tutorialPromptEnabledText : tutorialPromptDisabledText;
    }

    private void ApplyAudioVisualState()
    {
        if (audioStateImage != null)
            audioStateImage.sprite = audioEnabled ? audioEnabledSprite : audioDisabledSprite;

        if (audioValueText != null)
            audioValueText.text = audioEnabled ? audioEnabledText : audioDisabledText;
    }

    private void ApplyTableThemeVisualState()
    {
        if (tableThemeStateImage != null)
            tableThemeStateImage.sprite = darkThemeEnabled ? tableThemeDarkSprite : tableThemeLightSprite;

        if (tableThemeValueText != null)
            tableThemeValueText.text = darkThemeEnabled ? tableThemeDarkText : tableThemeLightText;
    }

    private void RaiseTutorialPromptChanged(bool value)
    {
        TutorialPromptSettingChanged?.Invoke(value);
        OnTutorialPromptsChanged?.Invoke(value);
    }

    private void RaiseAudioChanged(bool value)
    {
        AudioSettingChanged?.Invoke(value);
        OnAudioChanged?.Invoke(value);
    }

    private void RaiseTableThemeChanged(bool value)
    {
        TableThemeDarkModeChanged?.Invoke(value);
        OnTableThemeChanged?.Invoke(value);
    }

    private void RaisePanelOpenStateChanged(bool value)
    {
        PanelOpenStateChanged?.Invoke(value);
        OnPanelOpenStateChanged?.Invoke(value);
    }
}