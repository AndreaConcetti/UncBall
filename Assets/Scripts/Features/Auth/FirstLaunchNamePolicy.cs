using UnityEngine;
using UncballArena.Core.Auth.Models;

namespace UncballArena.Core.Auth.DisplayName
{
    public sealed class FirstLaunchNamePolicy : MonoBehaviour
    {
        [Header("Policy")]
        [SerializeField] private bool requireNameIfGuest = true;
        [SerializeField] private string requiredPlaceholderName = "ENTER NAME HERE";
        [SerializeField] private bool ignoreCase = true;

        public bool ShouldRequireName(AuthSession session, string profileDisplayName)
        {
            if (session == null || !session.HasUsableIdentity())
                return true;

            if (requireNameIfGuest && !session.Identity.IsGuest)
                return false;

            string resolved = !string.IsNullOrWhiteSpace(profileDisplayName)
                ? profileDisplayName
                : session.Identity.DisplayName;

            return IsRequiredPlaceholderName(resolved);
        }

        public bool IsRequiredPlaceholderName(string value)
        {
            if (string.IsNullOrWhiteSpace(requiredPlaceholderName))
                return false;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            string left = value.Trim();
            string right = requiredPlaceholderName.Trim();

            if (ignoreCase)
                return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);

            return string.Equals(left, right, System.StringComparison.Ordinal);
        }

        public string GetRequiredPlaceholderName()
        {
            return requiredPlaceholderName ?? string.Empty;
        }
    }
}