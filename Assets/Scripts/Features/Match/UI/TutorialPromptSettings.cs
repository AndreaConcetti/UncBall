using UnityEngine;

public class TutorialPromptSettings : MonoBehaviour
{
    public static TutorialPromptSettings Instance { get; private set; }

    private const string TutorialPromptsPrefsKey = "TutorialPromptsEnabled";

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("State")]
    [SerializeField] private bool tutorialPromptsEnabled = true;
    [SerializeField] private bool savePreference = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    public bool TutorialPromptsEnabled => tutorialPromptsEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        LoadPreference();

        if (logDebug)
            Debug.Log("[TutorialPromptSettings] Initialized. Enabled=" + tutorialPromptsEnabled, this);
    }

    public void SetEnabled(bool enabled)
    {
        tutorialPromptsEnabled = enabled;
        SavePreference();

        if (logDebug)
            Debug.Log("[TutorialPromptSettings] SetEnabled -> " + tutorialPromptsEnabled, this);
    }

    public void Toggle()
    {
        SetEnabled(!tutorialPromptsEnabled);
    }

    private void LoadPreference()
    {
        if (!savePreference)
            return;

        if (PlayerPrefs.HasKey(TutorialPromptsPrefsKey))
            tutorialPromptsEnabled = PlayerPrefs.GetInt(TutorialPromptsPrefsKey, 1) == 1;
    }

    private void SavePreference()
    {
        if (!savePreference)
            return;

        PlayerPrefs.SetInt(TutorialPromptsPrefsKey, tutorialPromptsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void MarkRuntimeRootPersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
            return;

        GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(runtimeRoot);
    }

    private void DestroyDuplicateRuntimeRoot()
    {
        GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
        Destroy(duplicateRoot);
    }
}