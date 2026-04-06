using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
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
    [SerializeField] private string tableThemeLightText = "DEFAULT";
    [SerializeField] private string tableThemeDarkText = "DARK";

    [Header("Dependencies")]
    [SerializeField] private TutorialPromptSettings tutorialPromptSettings;
    [SerializeField] private LocalGameSettings localGameSettings;

    [Header("Behavior")]
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool autoFindDependencies = true;
    [SerializeField] private bool autoWireUi = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();
        WireButtonsIfNeeded();

        if (panelRoot != null && startClosed)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeToSettings();
        RefreshUiFromSettings();
    }

    private void OnDisable()
    {
        UnsubscribeFromSettings();
        UnwireButtons();
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        RefreshUiFromSettings();

        if (logDebug)
            Debug.Log("[SettingsPanelUI] OpenPanel", this);
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] ClosePanel", this);
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
            return;

        bool next = !panelRoot.activeSelf;
        panelRoot.SetActive(next);

        if (next)
            RefreshUiFromSettings();

        if (logDebug)
            Debug.Log("[SettingsPanelUI] TogglePanel -> " + next, this);
    }

    public void RefreshUiFromSettings()
    {
        ResolveDependencies();

        bool tutorialEnabled = tutorialPromptSettings == null || tutorialPromptSettings.TutorialPromptsEnabled;
        bool audioEnabled = localGameSettings == null || localGameSettings.AudioEnabled;
        bool tableDarkEnabled = localGameSettings != null && localGameSettings.TableDarkModeEnabled;

        ApplyTutorialPromptVisual(tutorialEnabled);
        ApplyAudioVisual(audioEnabled);
        ApplyTableThemeVisual(tableDarkEnabled);

        if (logDebug)
        {
            Debug.Log(
                "[SettingsPanelUI] RefreshUiFromSettings -> " +
                "Tutorial=" + tutorialEnabled +
                " | Audio=" + audioEnabled +
                " | TableDark=" + tableDarkEnabled,
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (tutorialPromptSettings == null && autoFindDependencies)
            tutorialPromptSettings = TutorialPromptSettings.Instance;

        if (localGameSettings == null && autoFindDependencies)
            localGameSettings = LocalGameSettings.Instance;

#if UNITY_2023_1_OR_NEWER
        if (tutorialPromptSettings == null && autoFindDependencies)
            tutorialPromptSettings = FindFirstObjectByType<TutorialPromptSettings>(FindObjectsInactive.Include);

        if (localGameSettings == null && autoFindDependencies)
            localGameSettings = FindFirstObjectByType<LocalGameSettings>(FindObjectsInactive.Include);
#else
        if (tutorialPromptSettings == null && autoFindDependencies)
            tutorialPromptSettings = FindObjectOfType<TutorialPromptSettings>();

        if (localGameSettings == null && autoFindDependencies)
            localGameSettings = FindObjectOfType<LocalGameSettings>();
#endif
    }

    private void SubscribeToSettings()
    {
        if (localGameSettings != null)
        {
            localGameSettings.AudioEnabledChanged -= HandleAudioChanged;
            localGameSettings.AudioEnabledChanged += HandleAudioChanged;

            localGameSettings.TableDarkModeChanged -= HandleTableThemeChanged;
            localGameSettings.TableDarkModeChanged += HandleTableThemeChanged;
        }
    }

    private void UnsubscribeFromSettings()
    {
        if (localGameSettings != null)
        {
            localGameSettings.AudioEnabledChanged -= HandleAudioChanged;
            localGameSettings.TableDarkModeChanged -= HandleTableThemeChanged;
        }
    }

    private void WireButtonsIfNeeded()
    {
        if (!autoWireUi)
            return;

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenPanel);
            openButton.onClick.AddListener(OpenPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (tutorialPromptButton != null)
        {
            tutorialPromptButton.onClick.RemoveListener(OnTutorialPromptClicked);
            tutorialPromptButton.onClick.AddListener(OnTutorialPromptClicked);
        }

        if (audioButton != null)
        {
            audioButton.onClick.RemoveListener(OnAudioClicked);
            audioButton.onClick.AddListener(OnAudioClicked);
        }

        if (tableThemeButton != null)
        {
            tableThemeButton.onClick.RemoveListener(OnTableThemeClicked);
            tableThemeButton.onClick.AddListener(OnTableThemeClicked);
        }
    }

    private void UnwireButtons()
    {
        if (!autoWireUi)
            return;

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);

        if (tutorialPromptButton != null)
            tutorialPromptButton.onClick.RemoveListener(OnTutorialPromptClicked);

        if (audioButton != null)
            audioButton.onClick.RemoveListener(OnAudioClicked);

        if (tableThemeButton != null)
            tableThemeButton.onClick.RemoveListener(OnTableThemeClicked);
    }

    private void OnTutorialPromptClicked()
    {
        if (tutorialPromptSettings == null)
            return;

        bool next = !tutorialPromptSettings.TutorialPromptsEnabled;
        tutorialPromptSettings.SetEnabled(next);
        ApplyTutorialPromptVisual(next);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] OnTutorialPromptClicked -> " + next, this);
    }

    private void OnAudioClicked()
    {
        if (localGameSettings == null)
            return;

        bool next = !localGameSettings.AudioEnabled;
        localGameSettings.SetAudioEnabled(next);
        ApplyAudioVisual(next);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] OnAudioClicked -> " + next, this);
    }

    private void OnTableThemeClicked()
    {
        if (localGameSettings == null)
            return;

        bool next = !localGameSettings.TableDarkModeEnabled;
        localGameSettings.SetTableDarkModeEnabled(next);
        ApplyTableThemeVisual(next);

        if (logDebug)
            Debug.Log("[SettingsPanelUI] OnTableThemeClicked -> " + next, this);
    }

    private void HandleAudioChanged(bool enabled)
    {
        ApplyAudioVisual(enabled);
    }

    private void HandleTableThemeChanged(bool enabled)
    {
        ApplyTableThemeVisual(enabled);
    }

    private void ApplyTutorialPromptVisual(bool enabled)
    {
        if (tutorialPromptStateImage != null)
        {
            tutorialPromptStateImage.sprite = enabled
                ? tutorialPromptEnabledSprite
                : tutorialPromptDisabledSprite;
        }

        if (tutorialPromptValueText != null)
        {
            tutorialPromptValueText.text = enabled
                ? tutorialPromptEnabledText
                : tutorialPromptDisabledText;
        }
    }

    private void ApplyAudioVisual(bool enabled)
    {
        if (audioStateImage != null)
        {
            audioStateImage.sprite = enabled
                ? audioEnabledSprite
                : audioDisabledSprite;
        }

        if (audioValueText != null)
        {
            audioValueText.text = enabled
                ? audioEnabledText
                : audioDisabledText;
        }
    }

    private void ApplyTableThemeVisual(bool darkEnabled)
    {
        if (tableThemeStateImage != null)
        {
            tableThemeStateImage.sprite = darkEnabled
                ? tableThemeDarkSprite
                : tableThemeLightSprite;
        }

        if (tableThemeValueText != null)
        {
            tableThemeValueText.text = darkEnabled
                ? tableThemeDarkText
                : tableThemeLightText;
        }
    }
}