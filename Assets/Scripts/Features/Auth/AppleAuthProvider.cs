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
            if (!IsAvailable)
            {
                return Task.FromResult(AuthProviderSignInResult.Failure("Apple Sign In is not available on this platform/build."));
            }

            if (logDebug)
            {
                Debug.Log("[AppleAuthProvider] SignInAsync called. Real plugin flow not wired yet.");
            }

            return Task.FromResult(AuthProviderSignInResult.Failure("Apple Sign In plugin not wired yet."));
        }
    }
}