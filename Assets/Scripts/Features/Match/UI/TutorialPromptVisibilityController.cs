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
        if (applyOnAwake)
            ApplyCurrentState();
    }

    private void OnEnable()
    {
        if (settingsPanelUI != null)
            settingsPanelUI.TutorialPromptSettingChanged += HandleTutorialPromptChanged;

        if (applyOnEnable)
            ApplyCurrentState();
    }

    private void OnDisable()
    {
        if (settingsPanelUI != null)
            settingsPanelUI.TutorialPromptSettingChanged -= HandleTutorialPromptChanged;
    }

    private void HandleTutorialPromptChanged(bool enabled)
    {
        ApplyVisibility(enabled);
    }

    private void ApplyCurrentState()
    {
        if (settingsPanelUI == null)
            return;

        ApplyVisibility(settingsPanelUI.TutorialPromptsEnabled);
    }

    private void ApplyVisibility(bool enabled)
    {
        if (tutorialPromptsRoot != null)
            tutorialPromptsRoot.SetActive(enabled);

        if (logDebug)
            Debug.Log("[TutorialPromptVisibilityController] TutorialPromptsRoot -> " + enabled, this);
    }
}