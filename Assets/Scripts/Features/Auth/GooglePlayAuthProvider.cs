using System;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

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
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return SignInAndroidAsync();
            }
            catch (Exception exception)
            {
                if (logDebug)
                    Debug.LogError($"[GooglePlayAuthProvider] SignInAsync exception: {exception}");

                return Task.FromResult(AuthProviderSignInResult.Failure(
                    $"Google Play Games exception: {exception.Message}"));
            }
#else
            if (logDebug)
            {
                Debug.Log("[GooglePlayAuthProvider] SignInAsync skipped. Google Play Games is not available on this platform/build.");
            }

            return Task.FromResult(AuthProviderSignInResult.Failure(
                "Google Play Games is not available on this platform/build."));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private Task<AuthProviderSignInResult> SignInAndroidAsync()
        {
            TaskCompletionSource<AuthProviderSignInResult> tcs = new TaskCompletionSource<AuthProviderSignInResult>();

            if (logDebug)
            {
                Debug.Log("[GooglePlayAuthProvider] Starting Google Play Games sign-in flow.");
                Debug.Log($"[GooglePlayAuthProvider] Unity platform={Application.platform} | isEditor={Application.isEditor}");
            }

            PlayGamesPlatform platform = PlayGamesPlatform.Instance;
            if (platform == null)
            {
                if (logDebug)
                    Debug.LogError("[GooglePlayAuthProvider] PlayGamesPlatform.Instance is null.");

                tcs.TrySetResult(AuthProviderSignInResult.Failure(
                    "Google Play Games platform instance is null."));
                return tcs.Task;
            }

            bool alreadyAuthenticated = false;

            try
            {
                alreadyAuthenticated = platform.localUser != null && platform.localUser.authenticated;
            }
            catch (Exception exception)
            {
                if (logDebug)
                    Debug.LogWarning($"[GooglePlayAuthProvider] Failed reading localUser.authenticated before sign-in: {exception.Message}");
            }

            if (logDebug)
                Debug.Log($"[GooglePlayAuthProvider] Pre-auth state -> alreadyAuthenticated={alreadyAuthenticated}");

            if (alreadyAuthenticated)
            {
                AuthProviderSignInResult immediateResult = BuildSuccessResult(platform, "AlreadyAuthenticated");
                tcs.TrySetResult(immediateResult);
                return tcs.Task;
            }

            platform.Authenticate(status =>
            {
                if (logDebug)
                {
                    Debug.Log($"[GooglePlayAuthProvider] Automatic authenticate result: {status}");
                    DebugLocalUserState(platform, "AfterAutomaticAuthenticate");
                }

                if (status == SignInStatus.Success)
                {
                    tcs.TrySetResult(BuildSuccessResult(platform, "AutomaticAuthenticate"));
                    return;
                }

                if (logDebug)
                    Debug.Log("[GooglePlayAuthProvider] Falling back to manual authenticate.");

                platform.ManuallyAuthenticate(manualStatus =>
                {
                    if (logDebug)
                    {
                        Debug.Log($"[GooglePlayAuthProvider] Manual authenticate result: {manualStatus}");
                        DebugLocalUserState(platform, "AfterManualAuthenticate");
                    }

                    if (manualStatus == SignInStatus.Success)
                    {
                        tcs.TrySetResult(BuildSuccessResult(platform, "ManualAuthenticate"));
                        return;
                    }

                    string failureMessage =
                        $"Google Play Games sign-in failed: {manualStatus}. " +
                        $"Possible causes: tester not enabled, SHA1 mismatch, wrong signing key, or user canceled the Play Games flow.";

                    if (logDebug)
                        Debug.LogError($"[GooglePlayAuthProvider] {failureMessage}");

                    tcs.TrySetResult(AuthProviderSignInResult.Failure(failureMessage));
                });
            });

            return tcs.Task;
        }

        private AuthProviderSignInResult BuildSuccessResult(PlayGamesPlatform platform, string source)
        {
            string providerUserId = string.Empty;
            string displayName = string.Empty;

            try
            {
                providerUserId = SafeTrim(platform.GetUserId());
            }
            catch (Exception exception)
            {
                if (logDebug)
                    Debug.LogWarning($"[GooglePlayAuthProvider] GetUserId exception: {exception.Message}");
            }

            try
            {
                displayName = SafeTrim(platform.GetUserDisplayName());
            }
            catch (Exception exception)
            {
                if (logDebug)
                    Debug.LogWarning($"[GooglePlayAuthProvider] GetUserDisplayName exception: {exception.Message}");
            }

            bool localAuthenticated = false;

            try
            {
                localAuthenticated = platform.localUser != null && platform.localUser.authenticated;
            }
            catch (Exception exception)
            {
                if (logDebug)
                    Debug.LogWarning($"[GooglePlayAuthProvider] localUser.authenticated read exception: {exception.Message}");
            }

            if (logDebug)
            {
                Debug.Log(
                    $"[GooglePlayAuthProvider] BuildSuccessResult source={source} | " +
                    $"localAuthenticated={localAuthenticated} | " +
                    $"providerUserId='{providerUserId}' | displayName='{displayName}'");
            }

            if (string.IsNullOrWhiteSpace(providerUserId))
            {
                if (logDebug)
                    Debug.LogError("[GooglePlayAuthProvider] Sign-in reported success but provider user id is empty.");

                return AuthProviderSignInResult.Failure(
                    "Google Play Games returned success but an empty user id.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "GooglePlayer";

            PlayerIdentity identity = new PlayerIdentity(
                playerId: string.Empty,
                providerType: AuthProviderType.GooglePlayGames,
                providerUserId: providerUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: true);

            if (logDebug)
            {
                Debug.Log(
                    $"[GooglePlayAuthProvider] Sign-in success. Source={source} | " +
                    $"ProviderUserId={providerUserId} | DisplayName={displayName}");
            }

            return AuthProviderSignInResult.Success(identity);
        }

        private void DebugLocalUserState(PlayGamesPlatform platform, string phase)
        {
            if (!logDebug)
                return;

            try
            {
                bool localAuthenticated = platform.localUser != null && platform.localUser.authenticated;
                string userName = platform.localUser != null ? platform.localUser.userName : "<null>";
                string id = string.Empty;
                string displayName = string.Empty;

                try
                {
                    if (localAuthenticated)
                    {
                        id = SafeTrim(platform.GetUserId());
                        displayName = SafeTrim(platform.GetUserDisplayName());
                    }
                }
                catch
                {
                    id = string.Empty;
                    displayName = string.Empty;
                }

                Debug.Log(
                    $"[GooglePlayAuthProvider] {phase} -> " +
                    $"localAuthenticated={localAuthenticated} | " +
                    $"localUserName='{userName}' | " +
                    $"providerUserId='{id}' | " +
                    $"displayName='{displayName}'");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GooglePlayAuthProvider] {phase} debug read exception: {exception.Message}");
            }
        }
#endif

        private static string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}