using System.Threading.Tasks;
using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class AppleAuthProvider : IAuthProvider
    {
        private readonly bool logDebug;

        public AuthProviderType ProviderType => AuthProviderType.Apple;

#if UNITY_IOS && !UNITY_EDITOR
        public bool IsAvailable => true;
#else
        public bool IsAvailable => false;
#endif

        public AppleAuthProvider(bool logDebug = true)
        {
            this.logDebug = logDebug;
        }

        public Task<AuthProviderSignInResult> SignInAsync()
        {
            if (logDebug)
            {
                Debug.Log(
                    "[AppleAuthProvider] SignInAsync not implemented yet or unavailable on this platform.");
            }

            return Task.FromResult(AuthProviderSignInResult.Failure(
                "Apple Sign In is not implemented yet or unavailable on this platform."));
        }

        public Task SignOutAsync()
        {
            if (logDebug)
            {
                Debug.Log("[AppleAuthProvider] SignOutAsync skipped.");
            }

            return Task.CompletedTask;
        }
    }
}