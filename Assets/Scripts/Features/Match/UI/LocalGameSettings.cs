using UnityEngine;

public static class LocalGameSettings
{
    private const string AudioEnabledPrefsKey = "LocalSettings.AudioEnabled";
    private const string TableDarkModePrefsKey = "LocalSettings.TableDarkModeEnabled";

    private const bool DefaultAudioEnabled = true;
    private const bool DefaultTableDarkModeEnabled = false;

    public static bool AudioEnabled
    {
        get => PlayerPrefs.GetInt(AudioEnabledPrefsKey, DefaultAudioEnabled ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(AudioEnabledPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool TableDarkModeEnabled
    {
        get => PlayerPrefs.GetInt(TableDarkModePrefsKey, DefaultTableDarkModeEnabled ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TableDarkModePrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void ToggleAudio()
    {
        AudioEnabled = !AudioEnabled;
    }

    public static void ToggleTableDarkMode()
    {
        TableDarkModeEnabled = !TableDarkModeEnabled;
    }
}