using System;
using UnityEngine;

public class LocalGameSettings : MonoBehaviour
{
    public static LocalGameSettings Instance { get; private set; }

    private const string AudioEnabledPrefsKey = "LocalSettings.AudioEnabled";
    private const string TableDarkModePrefsKey = "LocalSettings.TableDarkModeEnabled";

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool savePreferences = true;

    [Header("Defaults")]
    [SerializeField] private bool defaultAudioEnabled = true;
    [SerializeField] private bool defaultTableDarkModeEnabled = false;

    [Header("Runtime State")]
    [SerializeField] private bool audioEnabled = true;
    [SerializeField] private bool tableDarkModeEnabled = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    public bool AudioEnabled => audioEnabled;
    public bool TableDarkModeEnabled => tableDarkModeEnabled;

    public event Action<bool> AudioEnabledChanged;
    public event Action<bool> TableDarkModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();
        LoadPreferences();

        if (logDebug)
        {
            Debug.Log(
                "[LocalGameSettings] Initialized -> " +
                "AudioEnabled=" + audioEnabled +
                " | TableDarkModeEnabled=" + tableDarkModeEnabled,
                this
            );
        }
    }

    public void SetAudioEnabled(bool enabled)
    {
        if (audioEnabled == enabled)
            return;

        audioEnabled = enabled;
        SavePreferences();

        if (logDebug)
            Debug.Log("[LocalGameSettings] SetAudioEnabled -> " + audioEnabled, this);

        AudioEnabledChanged?.Invoke(audioEnabled);
    }

    public void ToggleAudio()
    {
        SetAudioEnabled(!audioEnabled);
    }

    public void SetTableDarkModeEnabled(bool enabled)
    {
        if (tableDarkModeEnabled == enabled)
            return;

        tableDarkModeEnabled = enabled;
        SavePreferences();

        if (logDebug)
            Debug.Log("[LocalGameSettings] SetTableDarkModeEnabled -> " + tableDarkModeEnabled, this);

        TableDarkModeChanged?.Invoke(tableDarkModeEnabled);
    }

    public void ToggleTableDarkMode()
    {
        SetTableDarkModeEnabled(!tableDarkModeEnabled);
    }

    public void ForceRefreshEvents()
    {
        AudioEnabledChanged?.Invoke(audioEnabled);
        TableDarkModeChanged?.Invoke(tableDarkModeEnabled);
    }

    private void LoadPreferences()
    {
        if (!savePreferences)
        {
            audioEnabled = defaultAudioEnabled;
            tableDarkModeEnabled = defaultTableDarkModeEnabled;
            return;
        }

        audioEnabled = PlayerPrefs.GetInt(AudioEnabledPrefsKey, defaultAudioEnabled ? 1 : 0) == 1;
        tableDarkModeEnabled = PlayerPrefs.GetInt(TableDarkModePrefsKey, defaultTableDarkModeEnabled ? 1 : 0) == 1;
    }

    private void SavePreferences()
    {
        if (!savePreferences)
            return;

        PlayerPrefs.SetInt(AudioEnabledPrefsKey, audioEnabled ? 1 : 0);
        PlayerPrefs.SetInt(TableDarkModePrefsKey, tableDarkModeEnabled ? 1 : 0);
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