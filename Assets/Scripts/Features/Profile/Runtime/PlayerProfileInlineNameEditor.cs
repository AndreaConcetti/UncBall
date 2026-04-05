using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileInlineNameEditor : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PlayerProfileAccountPanelUI accountPanelUi;

    [Header("Display Mode")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private Button changeNameButton;

    [Header("Edit Mode")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private GameObject inputRoot;
    [SerializeField] private TMP_Text errorText;

    [Header("Validation")]
    [SerializeField] private int minLength = 3;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private bool allowSpaces = true;
    [SerializeField] private bool allowUnderscore = true;
    [SerializeField] private bool blockGuestLikeNames = true;
    [SerializeField]
    private string[] blockedExactNames =
    {
        "guest",
        "default",
        "enter name here"
    };

    [Header("Messages")]
    [SerializeField] private string emptyNameError = "NAME REQUIRED";
    [SerializeField] private string tooShortError = "NAME TOO SHORT";
    [SerializeField] private string tooLongError = "NAME TOO LONG";
    [SerializeField] private string invalidCharsError = "INVALID CHARACTERS";
    [SerializeField] private string reservedNameError = "NAME NOT ALLOWED";

    [Header("Behavior")]
    [SerializeField] private bool hideDisplayWhileEditing = true;
    [SerializeField] private bool clearErrorOnBeginEdit = true;
    [SerializeField] private bool selectAllTextOnBeginEdit = true;
    [SerializeField] private bool saveOnEndEdit = true;
    [SerializeField] private bool saveOnSubmit = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool isEditing;
    private bool suppressNextEndEdit;

    private void Awake()
    {
        ResolveDependencies();
        BindUiEvents();

        isEditing = false;
        ExitEditModeVisuals(true);
        RefreshFromProfile();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        BindProfileEvents();

        isEditing = false;
        ExitEditModeVisuals(true);
        RefreshFromProfile();
        ClearError();

        if (accountPanelUi != null)
            accountPanelUi.RefreshUI();
    }

    private void OnDisable()
    {
        UnbindProfileEvents();
    }

    public void BeginEdit()
    {
        ResolveDependencies();

        string currentName = GetCurrentProfileDisplayName();
        if (string.IsNullOrWhiteSpace(currentName))
            currentName = "Guest";

        isEditing = true;

        if (clearErrorOnBeginEdit)
            ClearError();

        if (nameInputField != null)
        {
            nameInputField.text = currentName;
            nameInputField.caretPosition = nameInputField.text.Length;
            nameInputField.selectionStringAnchorPosition = 0;
            nameInputField.selectionStringFocusPosition = nameInputField.text.Length;
        }

        EnterEditModeVisuals();

        if (nameInputField != null)
        {
            nameInputField.Select();
            nameInputField.ActivateInputField();

            if (selectAllTextOnBeginEdit)
            {
                nameInputField.selectionStringAnchorPosition = 0;
                nameInputField.selectionStringFocusPosition = nameInputField.text.Length;
            }
        }

        if (logDebug)
            Debug.Log("[PlayerProfileInlineNameEditor] BeginEdit -> " + currentName, this);
    }

    public void CancelEdit()
    {
        isEditing = false;
        suppressNextEndEdit = true;

        ExitEditModeVisuals(true);
        RefreshFromProfile();
        ClearError();

        if (logDebug)
            Debug.Log("[PlayerProfileInlineNameEditor] CancelEdit", this);
    }

    public void SaveNow()
    {
        if (!isEditing)
            return;

        TrySaveName(nameInputField != null ? nameInputField.text : string.Empty);
    }

    private void HandleChangeNameButtonClicked()
    {
        BeginEdit();
    }

    private void HandleInputEndEdit(string value)
    {
        if (!saveOnEndEdit)
            return;

        if (suppressNextEndEdit)
        {
            suppressNextEndEdit = false;
            return;
        }

        if (!isEditing)
            return;

        TrySaveName(value);
    }

    private void HandleInputSubmit(string value)
    {
        if (!saveOnSubmit)
            return;

        if (!isEditing)
            return;

        TrySaveName(value);
    }

    private void TrySaveName(string rawValue)
    {
        string candidate = NormalizeName(rawValue);

        string error;
        if (!ValidateName(candidate, out error))
        {
            SetError(error);
            ReopenInputFieldNextFrame();

            if (logDebug)
                Debug.LogWarning("[PlayerProfileInlineNameEditor] Invalid name -> " + error, this);

            return;
        }

        ApplyName(candidate);
    }

    private void ApplyName(string validatedName)
    {
        ResolveDependencies();

        if (profileManager == null)
        {
            SetError("PROFILE MANAGER MISSING");
            return;
        }

        profileManager.UpdateDisplayName(validatedName);

        isEditing = false;
        ExitEditModeVisuals(true);
        ClearError();
        RefreshFromProfile();

        if (accountPanelUi != null)
            accountPanelUi.RefreshUI();

        if (logDebug)
            Debug.Log("[PlayerProfileInlineNameEditor] ApplyName -> " + validatedName, this);
    }

    private bool ValidateName(string value, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = emptyNameError;
            return false;
        }

        if (value.Length < minLength)
        {
            error = tooShortError;
            return false;
        }

        if (value.Length > maxLength)
        {
            error = tooLongError;
            return false;
        }

        if (blockGuestLikeNames)
        {
            string lowered = value.Trim().ToLowerInvariant();
            for (int i = 0; i < blockedExactNames.Length; i++)
            {
                if (lowered == blockedExactNames[i].Trim().ToLowerInvariant())
                {
                    error = reservedNameError;
                    return false;
                }
            }
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            bool valid =
                char.IsLetterOrDigit(c) ||
                (allowSpaces && c == ' ') ||
                (allowUnderscore && c == '_');

            if (!valid)
            {
                error = invalidCharsError;
                return false;
            }
        }

        return true;
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string trimmed = value.Trim();

        while (trimmed.Contains("  "))
            trimmed = trimmed.Replace("  ", " ");

        return trimmed;
    }

    private void RefreshFromProfile()
    {
        string currentName = GetCurrentProfileDisplayName();
        if (string.IsNullOrWhiteSpace(currentName))
            currentName = "Guest";

        if (displayNameText != null)
            displayNameText.text = currentName;

        if (!isEditing && nameInputField != null)
            nameInputField.text = currentName;

        if (logDebug)
            Debug.Log("[PlayerProfileInlineNameEditor] RefreshFromProfile -> " + currentName, this);
    }

    private string GetCurrentProfileDisplayName()
    {
        ResolveDependencies();

        if (profileManager != null && profileManager.ActiveProfile != null)
            return profileManager.ActiveProfile.displayName;

        return string.Empty;
    }

    private void EnterEditModeVisuals()
    {
        if (inputRoot != null)
            inputRoot.SetActive(true);
        else if (nameInputField != null)
            nameInputField.gameObject.SetActive(true);

        if (hideDisplayWhileEditing && displayNameText != null)
            displayNameText.gameObject.SetActive(false);
    }

    private void ExitEditModeVisuals(bool showDisplay)
    {
        if (inputRoot != null)
            inputRoot.SetActive(false);
        else if (nameInputField != null)
            nameInputField.gameObject.SetActive(false);

        if (displayNameText != null)
            displayNameText.gameObject.SetActive(showDisplay);
    }

    private void SetError(string message)
    {
        if (errorText == null)
            return;

        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void ClearError()
    {
        if (errorText == null)
            return;

        errorText.text = string.Empty;
        errorText.gameObject.SetActive(false);
    }

    private void ReopenInputFieldNextFrame()
    {
        if (nameInputField == null)
            return;

        suppressNextEndEdit = true;
        Invoke(nameof(ReactivateInputField), 0.02f);
    }

    private void ReactivateInputField()
    {
        suppressNextEndEdit = false;

        if (!isEditing || nameInputField == null)
            return;

        nameInputField.Select();
        nameInputField.ActivateInputField();
        nameInputField.caretPosition = nameInputField.text.Length;
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    private void BindUiEvents()
    {
        if (changeNameButton != null)
        {
            changeNameButton.onClick.RemoveListener(HandleChangeNameButtonClicked);
            changeNameButton.onClick.AddListener(HandleChangeNameButtonClicked);
        }

        if (nameInputField != null)
        {
            nameInputField.onEndEdit.RemoveListener(HandleInputEndEdit);
            nameInputField.onEndEdit.AddListener(HandleInputEndEdit);

            nameInputField.onSubmit.RemoveListener(HandleInputSubmit);
            nameInputField.onSubmit.AddListener(HandleInputSubmit);

            nameInputField.characterLimit = maxLength;
        }
    }

    private void BindProfileEvents()
    {
        if (profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
    }

    private void UnbindProfileEvents()
    {
        if (profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        if (!isEditing)
            RefreshFromProfile();
    }
}