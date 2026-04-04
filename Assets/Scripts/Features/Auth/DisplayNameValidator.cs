using System.Text;
using UnityEngine;

namespace UncballArena.Core.Auth.DisplayName
{
    public sealed class DisplayNameValidator : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private int minLength = 3;
        [SerializeField] private int maxLength = 16;
        [SerializeField] private bool allowSpaces = true;
        [SerializeField] private bool allowUnderscore = true;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        public DisplayNameValidationResult Validate(string rawValue)
        {
            string sanitized = Sanitize(rawValue);

            if (string.IsNullOrWhiteSpace(sanitized))
                return Invalid("Name cannot be empty.", sanitized);

            if (sanitized.Length < minLength)
                return Invalid($"Name must be at least {minLength} characters.", sanitized);

            if (sanitized.Length > maxLength)
                return Invalid($"Name must be at most {maxLength} characters.", sanitized);

            for (int i = 0; i < sanitized.Length; i++)
            {
                char c = sanitized[i];
                if (char.IsLetterOrDigit(c))
                    continue;

                if (allowSpaces && c == ' ')
                    continue;

                if (allowUnderscore && c == '_')
                    continue;

                return Invalid("Only letters, numbers, spaces and underscore are allowed.", sanitized);
            }

            if (HasRepeatedSpaces(sanitized))
                return Invalid("Use single spaces only.", sanitized);

            if (logDebug)
                Debug.Log($"[DisplayNameValidator] Valid -> {sanitized}", this);

            return DisplayNameValidationResult.Valid(sanitized);
        }

        public string Sanitize(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return string.Empty;

            string trimmed = rawValue.Trim();

            StringBuilder sb = new StringBuilder(trimmed.Length);
            bool lastWasSpace = false;

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];

                if (char.IsWhiteSpace(c))
                {
                    if (!allowSpaces)
                        continue;

                    if (lastWasSpace)
                        continue;

                    sb.Append(' ');
                    lastWasSpace = true;
                    continue;
                }

                sb.Append(c);
                lastWasSpace = false;
            }

            return sb.ToString().Trim();
        }

        private bool HasRepeatedSpaces(string value)
        {
            for (int i = 1; i < value.Length; i++)
            {
                if (value[i] == ' ' && value[i - 1] == ' ')
                    return true;
            }

            return false;
        }

        private DisplayNameValidationResult Invalid(string message, string sanitized)
        {
            if (logDebug)
                Debug.Log($"[DisplayNameValidator] Invalid -> {message} | Sanitized={sanitized}", this);

            return DisplayNameValidationResult.Invalid(message, sanitized);
        }
    }
}