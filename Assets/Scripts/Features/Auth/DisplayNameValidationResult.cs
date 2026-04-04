using System;

namespace UncballArena.Core.Auth.DisplayName
{
    [Serializable]
    public readonly struct DisplayNameValidationResult
    {
        public bool IsValid { get; }
        public string SanitizedValue { get; }
        public string ErrorMessage { get; }

        public DisplayNameValidationResult(bool isValid, string sanitizedValue, string errorMessage)
        {
            IsValid = isValid;
            SanitizedValue = sanitizedValue ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static DisplayNameValidationResult Valid(string sanitizedValue)
        {
            return new DisplayNameValidationResult(true, sanitizedValue, string.Empty);
        }

        public static DisplayNameValidationResult Invalid(string errorMessage, string sanitizedValue = "")
        {
            return new DisplayNameValidationResult(false, sanitizedValue, errorMessage);
        }

        public override string ToString()
        {
            return $"DisplayNameValidationResult(IsValid={IsValid}, SanitizedValue={SanitizedValue}, Error={ErrorMessage})";
        }
    }
}