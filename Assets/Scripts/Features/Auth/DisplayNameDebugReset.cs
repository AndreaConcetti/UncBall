using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Bootstrap;

namespace UncballArena.Core.Auth.DebugTools
{
    public sealed class DisplayNameDebugReset : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool resetToGuestOnStart = false;
        [SerializeField] private string forcedName = "ENTER NAME HERE";
        [SerializeField] private bool logDebug = true;

        private async void Start()
        {
            if (!resetToGuestOnStart)
                return;

            await ResetDisplayNameAsync();
        }

        [ContextMenu("Reset Display Name Now")]
        public async void ResetDisplayNameNow()
        {
            await ResetDisplayNameAsync();
        }

        private async Task ResetDisplayNameAsync()
        {
            GameCompositionRoot root = GameCompositionRoot.Instance;
            if (root == null)
            {
                if (logDebug)
                    Debug.LogWarning("[DisplayNameDebugReset] GameCompositionRoot not found.", this);
                return;
            }

            int guard = 0;
            while (!root.IsReady && guard < 300)
            {
                await Task.Delay(50);
                guard++;
            }

            if (!root.IsReady || root.AuthService == null)
            {
                if (logDebug)
                    Debug.LogWarning("[DisplayNameDebugReset] AuthService not ready.", this);
                return;
            }

            await root.AuthService.UpdateDisplayNameAsync(forcedName, CancellationToken.None);

            if (root.ProfileService != null && root.ProfileService.CurrentProfile != null)
                await root.ProfileService.SetDisplayNameAsync(forcedName);

            if (logDebug)
                Debug.Log($"[DisplayNameDebugReset] Display name reset to '{forcedName}'.", this);
        }
    }
}