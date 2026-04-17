using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if PLAYFAB_ENABLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace UncballArena.Core.Auth
{
    public sealed class PlayFabBackendAuthService : IBackendAuthService
    {
        private readonly string titleId;
        private readonly bool createAccount;
        private readonly bool logDebug;

        public bool IsInitialized { get; private set; }

        public PlayFabBackendAuthService(string titleId, bool createAccount = true, bool logDebug = true)
        {
            this.titleId = titleId != null ? titleId.Trim() : string.Empty;
            this.createAccount = createAccount;
            this.logDebug = logDebug;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if PLAYFAB_ENABLED
            if (string.IsNullOrWhiteSpace(titleId))
                throw new InvalidOperationException("[PlayFabBackendAuthService] TitleId is null or empty.");

            PlayFabSettings.staticSettings.TitleId = titleId;
#endif

            IsInitialized = true;

            if (logDebug)
                Debug.Log($"[PlayFabBackendAuthService] Initialized. TitleId={titleId}");

            return Task.CompletedTask;
        }

        public Task<BackendAuthState> RestoreSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            return SignInAsync(localIdentity, cancellationToken);
        }

        public Task<BackendAuthState> RefreshSessionAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            return SignInAsync(localIdentity, cancellationToken);
        }

        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (logDebug)
            {
                Debug.Log(
                    "[PlayFabBackendAuthService] SignOutAsync completed. " +
                    "No explicit PlayFab client credential reset is required in this project flow.");
            }

            return Task.CompletedTask;
        }

        public Task<BackendAuthState> SignInAsync(PlayerIdentity localIdentity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (localIdentity == null || !localIdentity.IsValid)
                throw new InvalidOperationException("[PlayFabBackendAuthService] Local identity invalid.");

#if PLAYFAB_ENABLED
            if (string.IsNullOrWhiteSpace(titleId))
                throw new InvalidOperationException("[PlayFabBackendAuthService] TitleId is null or empty.");
#endif

            string customId = BuildStableCustomId(localIdentity);

#if PLAYFAB_ENABLED
            TaskCompletionSource<BackendAuthState> tcs = new TaskCompletionSource<BackendAuthState>();

            LoginWithCustomIDRequest request = new LoginWithCustomIDRequest
            {
                TitleId = titleId,
                CustomId = customId,
                CreateAccount = createAccount,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true
                }
            };

            if (logDebug)
            {
                Debug.Log(
                    $"[PlayFabBackendAuthService] Starting LoginWithCustomID. " +
                    $"LocalPlayerId={localIdentity.PlayerId} | CustomId={customId} | CreateAccount={createAccount}");
            }

            PlayFabClientAPI.LoginWithCustomID(
                request,
                result =>
                {
                    try
                    {
                        string backendPlayerId = result != null ? result.PlayFabId ?? string.Empty : string.Empty;
                        string sessionTicket = result != null ? result.SessionTicket ?? string.Empty : string.Empty;
                        string backendDisplayName = ExtractDisplayName(result, localIdentity.DisplayName);

                        BackendAuthState state = new BackendAuthState(
                            backendPlayerId: backendPlayerId,
                            sessionToken: sessionTicket,
                            isAuthenticated: !string.IsNullOrWhiteSpace(backendPlayerId),
                            backendDisplayName: backendDisplayName);

                        if (logDebug)
                        {
                            Debug.Log(
                                $"[PlayFabBackendAuthService] Login success. " +
                                $"LocalPlayerId={localIdentity.PlayerId} | " +
                                $"PlayFabId={backendPlayerId} | " +
                                $"DisplayName={backendDisplayName}");
                        }

                        tcs.TrySetResult(state);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                error =>
                {
                    string message = error != null
                        ? error.GenerateErrorReport()
                        : "Unknown PlayFab login error.";

                    if (logDebug)
                        Debug.LogError($"[PlayFabBackendAuthService] Login failed. {message}");

                    tcs.TrySetException(new InvalidOperationException(message));
                });

            cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled(cancellationToken);
            });

            return tcs.Task;
#else
            if (logDebug)
            {
                Debug.LogWarning(
                    "[PlayFabBackendAuthService] PLAYFAB_ENABLED not defined. " +
                    "Falling back to no backend session.");
            }

            return Task.FromResult(BackendAuthState.None);
#endif
        }

        private static string BuildStableCustomId(PlayerIdentity localIdentity)
        {
            string baseId = !string.IsNullOrWhiteSpace(localIdentity.PlayerId)
                ? localIdentity.PlayerId.Trim()
                : Guid.NewGuid().ToString("N");

            return $"uncball_{baseId}";
        }

#if PLAYFAB_ENABLED
        private static string ExtractDisplayName(LoginResult result, string fallback)
        {
            if (result != null &&
                result.InfoResultPayload != null &&
                result.InfoResultPayload.PlayerProfile != null &&
                !string.IsNullOrWhiteSpace(result.InfoResultPayload.PlayerProfile.DisplayName))
            {
                return result.InfoResultPayload.PlayerProfile.DisplayName.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Guest" : fallback.Trim();
        }
#endif
    }
}