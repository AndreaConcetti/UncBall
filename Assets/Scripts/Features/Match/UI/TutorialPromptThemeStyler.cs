using TMPro;
using UnityEngine;

public class TutorialPromptThemeStyler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;

    [Header("Tutorial Prompt Texts")]
    [SerializeField] private TextMeshProUGUI tipTextLock;
    [SerializeField] private TextMeshProUGUI tipTextShoot;

    [Header("Score Popup Texts")]
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI addPointsText;

    [Header("Light Theme - Dark Text Gradient")]
    [SerializeField] private Color lightTopLeft = new Color32(35, 35, 35, 255);
    [SerializeField] private Color lightTopRight = new Color32(35, 35, 35, 255);
    [SerializeField] private Color lightBottomLeft = new Color32(95, 95, 95, 255);
    [SerializeField] private Color lightBottomRight = new Color32(95, 95, 95, 255);

    [Header("Dark Theme - Bright Text Gradient")]
    [SerializeField] private Color darkTopLeft = new Color32(255, 245, 170, 255);
    [SerializeField] private Color darkTopRight = new Color32(255, 245, 170, 255);
    [SerializeField] private Color darkBottomLeft = new Color32(255, 255, 255, 255);
    [SerializeField] private Color darkBottomRight = new Color32(255, 255, 255, 255);

    [Header("Behavior")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();

        if (applyOnAwake)
            ApplyCurrentTheme();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (settingsPanelUI != null)
            settingsPanelUI.TableThemeDarkModeChanged += HandleThemeChanged;

        if (applyOnEnable)
            ApplyCurrentTheme();
    }

    private void OnDisable()
    {
        if (settingsPanelUI != null)
            settingsPanelUI.TableThemeDarkModeChanged -= HandleThemeChanged;
    }

    public void ApplyCurrentTheme()
    {
        ApplyTheme(LocalGameSettings.TableDarkModeEnabled);
    }

    public void ApplyTheme(bool darkMode)
    {
        VertexGradient gradient = darkMode
            ? new VertexGradient(darkTopLeft, darkTopRight, darkBottomLeft, darkBottomRight)
            : new VertexGradient(lightTopLeft, lightTopRight, lightBottomLeft, lightBottomRight);

        ApplyGradientToText(tipTextLock, gradient);
        ApplyGradientToText(tipTextShoot, gradient);
        ApplyGradientToText(comboText, gradient);
        ApplyGradientToText(addPointsText, gradient);

        if (logDebug)
            Debug.Log("[TutorialPromptThemeStyler] ApplyTheme -> DarkMode=" + darkMode, this);
    }

    private void HandleThemeChanged(bool darkMode)
    {
        ApplyTheme(darkMode);
    }

    private void ApplyGradientToText(TextMeshProUGUI textComponent, VertexGradient gradient)
    {
        if (textComponent == null)
            return;

        textComponent.enableVertexGradient = true;
        textComponent.colorGradient = gradient;
        textComponent.ForceMeshUpdate();
    }

    private void ResolveDependencies()
    {
#if UNITY_2023_1_OR_NEWER
        if (settingsPanelUI == null)
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>(FindObjectsInactive.Include);
#else
        if (settingsPanelUI == null)
            settingsPanelUI = FindObjectOfType<SettingsPanelUI>();
#endif
    }
}