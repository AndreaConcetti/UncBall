using UnityEngine;

public class TutorialPromptVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;
    [SerializeField] private GameObject tutorialPromptsRoot;

    [Header("Behavior")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();

        if (applyOnAwake)
            ApplyVisibility();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (settingsPanelUI != null)
            settingsPanelUI.TutorialPromptSettingChanged += HandleTutorialPromptSettingChanged;

        if (applyOnEnable)
            ApplyVisibility();
    }

    private void OnDisable()
    {
        if (settingsPanelUI != null)
            settingsPanelUI.TutorialPromptSettingChanged -= HandleTutorialPromptSettingChanged;
    }

    public void ApplyVisibility()
    {
        if (tutorialPromptsRoot == null)
            return;

        bool visible = TutorialPromptSettings.TutorialPromptsEnabled;
        tutorialPromptsRoot.SetActive(visible);

        if (logDebug)
            Debug.Log("[TutorialPromptVisibilityController] ApplyVisibility -> " + visible, this);
    }

    private void HandleTutorialPromptSettingChanged(bool enabled)
    {
        if (tutorialPromptsRoot == null)
            return;

        tutorialPromptsRoot.SetActive(enabled);

        if (logDebug)
            Debug.Log("[TutorialPromptVisibilityController] HandleTutorialPromptSettingChanged -> " + enabled, this);
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