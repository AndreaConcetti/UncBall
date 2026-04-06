using UnityEngine;

public static class TutorialPromptSettings
{
    private const string TutorialPromptsPrefsKey = "TutorialPromptsEnabled";
    private const bool DefaultEnabled = true;

    public static bool TutorialPromptsEnabled
    {
        get => PlayerPrefs.GetInt(TutorialPromptsPrefsKey, DefaultEnabled ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TutorialPromptsPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void SetEnabled(bool enabled)
    {
        TutorialPromptsEnabled = enabled;
    }

    public static void Toggle()
    {
        TutorialPromptsEnabled = !TutorialPromptsEnabled;
    }
}