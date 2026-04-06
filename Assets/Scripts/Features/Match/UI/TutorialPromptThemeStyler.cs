using TMPro;
using UnityEngine;

public class TutorialPromptThemeStyler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;

    [Header("Tutorial Prompt Texts")]
    [SerializeField] private TMP_Text tipTextLock;
    [SerializeField] private TMP_Text tipTextShoot;

    [Header("Score Popup Texts")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text addPointsText;

    [Header("Light Theme - Dark Gradient")]
    [SerializeField] private Color lightTopLeft = Color.black;
    [SerializeField] private Color lightTopRight = Color.black;
    [SerializeField] private Color lightBottomLeft = Color.gray;
    [SerializeField] private Color lightBottomRight = Color.gray;

    [Header("Dark Theme - Bright Gradient")]
    [SerializeField] private Color darkTopLeft = Color.white;
    [SerializeField] private Color darkTopRight = Color.white;
    [SerializeField] private Color darkBottomLeft = Color.yellow;
    [SerializeField] private Color darkBottomRight = Color.red;

    [Header("Behavior")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        if (applyOnAwake)
            ApplyCurrentTheme();
    }

    private void OnEnable()
    {
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

    private void HandleThemeChanged(bool darkMode)
    {
        ApplyTheme(darkMode);
    }

    private void ApplyCurrentTheme()
    {
        if (settingsPanelUI == null)
            return;

        ApplyTheme(settingsPanelUI.DarkThemeEnabled);
    }

    private void ApplyTheme(bool darkMode)
    {
        Color tl = darkMode ? darkTopLeft : lightTopLeft;
        Color tr = darkMode ? darkTopRight : lightTopRight;
        Color bl = darkMode ? darkBottomLeft : lightBottomLeft;
        Color br = darkMode ? darkBottomRight : lightBottomRight;

        ApplyVertexGradient(tipTextLock, tl, tr, bl, br);
        ApplyVertexGradient(tipTextShoot, tl, tr, bl, br);
        ApplyVertexGradient(comboText, tl, tr, bl, br);
        ApplyVertexGradient(addPointsText, tl, tr, bl, br);

        if (logDebug)
            Debug.Log("[TutorialPromptThemeStyler] Applied theme -> darkMode=" + darkMode, this);
    }

    private static void ApplyVertexGradient(TMP_Text text, Color tl, Color tr, Color bl, Color br)
    {
        if (text == null)
            return;

        text.color = Color.white;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(tl, tr, bl, br);
        text.ForceMeshUpdate();
    }
}