using TMPro;
using UnityEngine;

public class PlayerDisplayNamePanel : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("UI")]
    [SerializeField] private TMP_InputField displayNameInputField;
    [SerializeField] private TMP_Text currentNameText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Config")]
    [SerializeField] private string emptyNameFallback = "Player";
    [SerializeField] private int maxLength = 16;
    [SerializeField] private bool logDebug = true;

    private void Start()
    {
        ResolveDependencies();
        RefreshUi();
    }

    public void ApplyDisplayName()
    {
        ResolveDependencies();

        if (profileManager == null)
            return;

        string raw = displayNameInputField != null ? displayNameInputField.text : string.Empty;
        string sanitized = SanitizeName(raw);

        profileManager.UpdateDisplayName(sanitized);
        RefreshUi();

        if (feedbackText != null)
            feedbackText.text = "NAME SAVED";

        if (logDebug)
            Debug.Log("[PlayerDisplayNamePanel] ApplyDisplayName -> " + sanitized, this);
    }

    public void RefreshUi()
    {
        ResolveDependencies();

        string currentName =
            profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName)
            ? profileManager.ActiveDisplayName.Trim()
            : emptyNameFallback;

        if (displayNameInputField != null)
        {
            displayNameInputField.characterLimit = Mathf.Max(1, maxLength);
            displayNameInputField.text = currentName;
        }

        if (currentNameText != null)
            currentNameText.text = currentName.ToUpperInvariant();

        if (feedbackText != null)
            feedbackText.text = string.Empty;
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    private string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return emptyNameFallback;

        string trimmed = value.Trim();

        if (trimmed.Length > maxLength)
            trimmed = trimmed.Substring(0, maxLength);

        return trimmed;
    }
}