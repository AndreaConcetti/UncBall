using System.Threading.Tasks;
using UnityEngine;

namespace UncballArena.Core.Auth
{
    public sealed class GooglePlayAuthProvider : IAuthProvider
    {
        private readonly bool logDebug;

        public AuthProviderType ProviderType => AuthProviderType.GooglePlayGames;

#if UNITY_ANDROID && !UNITY_EDITOR
        public bool IsAvailable => true;
#else
        public bool IsAvailable => false;
#endif

        public GooglePlayAuthProvider(bool logDebug = true)
        {
            this.logDebug = logDebug;
        }

        public Task<AuthProviderSignInResult> SignInAsync()
        {
            if (!IsAvailable)
            {
                return Task.FromResult(AuthProviderSignInResult.Failure("Google Play Games is not available on this platform/build."));
            }

            if (logDebug)
            {
                Debug.Log("[GooglePlayAuthProvider] SignInAsync called. Real plugin flow not wired yet.");
            }

            return Task.FromResult(AuthProviderSignInResult.Failure("Google Play Games plugin not wired yet."));
        }
    }
}