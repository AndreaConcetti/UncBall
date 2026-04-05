using System.Collections;
using TMPro;
using UnityEngine;

namespace UncballArena.Core.Auth.UI
{
    public sealed class AccountOperationFeedbackPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;

        [Header("Behavior")]
        [SerializeField] private float visibleDuration = 1.5f;
        [SerializeField] private bool hideVisualRootOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private Coroutine activeRoutine;

        private void Awake()
        {
            if (root == null)
            {
                Debug.LogWarning("[AccountOperationFeedbackPanel] Root is not assigned.", this);
                return;
            }

            if (hideVisualRootOnAwake)
                root.SetActive(false);
        }

        public void Show(string message)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    "[AccountOperationFeedbackPanel] Cannot show feedback because the controller GameObject is inactive. " +
                    "Keep the controller active and only disable the visual root.",
                    this);
                return;
            }

            if (root == null || messageText == null)
            {
                Debug.LogWarning("[AccountOperationFeedbackPanel] Missing references.", this);
                return;
            }

            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeRoutine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            messageText.text = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Trim();

            root.SetActive(true);

            if (logDebug)
                Debug.Log("[AccountOperationFeedbackPanel] Show -> " + messageText.text, this);

            yield return new WaitForSecondsRealtime(visibleDuration);

            if (root != null)
                root.SetActive(false);

            activeRoutine = null;
        }

        public void HideImmediate()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (root != null)
                root.SetActive(false);
        }
    }
}