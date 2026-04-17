using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Profile.Models;

#if PLAYFAB_ENABLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace UncballArena.Core.Profile.Repositories
{
    public sealed class PlayFabProfileRepository : IProfileRepository
    {
        private const string ProfileDataKey = "profile_snapshot_v1";

        [Serializable]
        private sealed class ProfileWrapper
        {
            public ProfileSnapshot snapshot;
        }

        private readonly bool logDebug;

        public PlayFabProfileRepository(bool logDebug = true)
        {
            this.logDebug = logDebug;
        }

        public Task<ProfileSnapshot> LoadByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return Task.FromResult<ProfileSnapshot>(null);

#if PLAYFAB_ENABLED
            TaskCompletionSource<ProfileSnapshot> tcs = new TaskCompletionSource<ProfileSnapshot>();

            GetUserDataRequest request = new GetUserDataRequest();

            PlayFabClientAPI.GetUserData(
                request,
                result =>
                {
                    try
                    {
                        if (result == null || result.Data == null || !result.Data.TryGetValue(ProfileDataKey, out UserDataRecord record))
                        {
                            if (logDebug)
                            {
                                Debug.Log(
                                    $"[PlayFabProfileRepository] No remote profile found. PlayerId={playerId} | Key={ProfileDataKey}");
                            }

                            tcs.TrySetResult(null);
                            return;
                        }

                        string json = record != null ? record.Value ?? string.Empty : string.Empty;
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            tcs.TrySetResult(null);
                            return;
                        }

                        ProfileWrapper wrapper = JsonUtility.FromJson<ProfileWrapper>(json);
                        if (wrapper == null || wrapper.snapshot == null || !wrapper.snapshot.IsValid())
                        {
                            Debug.LogWarning(
                                $"[PlayFabProfileRepository] Invalid remote snapshot payload. PlayerId={playerId}");
                            tcs.TrySetResult(null);
                            return;
                        }

                        if (logDebug)
                        {
                            Debug.Log(
                                $"[PlayFabProfileRepository] Loaded remote profile. " +
                                $"PlayerId={playerId} | ProfileId={wrapper.snapshot.ProfileId}");
                        }

                        tcs.TrySetResult(wrapper.snapshot);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                error =>
                {
                    string message = error != null ? error.GenerateErrorReport() : "Unknown PlayFab GetUserData error.";
                    Debug.LogWarning($"[PlayFabProfileRepository] Load failed. PlayerId={playerId} | Error={message}");
                    tcs.TrySetResult(null);
                });

            return tcs.Task;
#else
            if (logDebug)
                Debug.LogWarning("[PlayFabProfileRepository] PLAYFAB_ENABLED not defined. Returning null.");

            return Task.FromResult<ProfileSnapshot>(null);
#endif
        }

        public Task SaveAsync(ProfileSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsValid())
            {
                Debug.LogError("[PlayFabProfileRepository] SaveAsync failed: snapshot invalid or null.");
                return Task.CompletedTask;
            }

#if PLAYFAB_ENABLED
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            ProfileWrapper wrapper = new ProfileWrapper
            {
                snapshot = snapshot
            };

            string json = JsonUtility.ToJson(wrapper);

            UpdateUserDataRequest request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>
                {
                    { ProfileDataKey, json }
                }
            };

            PlayFabClientAPI.UpdateUserData(
                request,
                result =>
                {
                    if (logDebug)
                    {
                        Debug.Log(
                            $"[PlayFabProfileRepository] Saved remote profile. " +
                            $"PlayerId={snapshot.PlayerId} | ProfileId={snapshot.ProfileId}");
                    }

                    TryUpdateTitleDisplayName(snapshot.DisplayName);
                    tcs.TrySetResult(true);
                },
                error =>
                {
                    string message = error != null ? error.GenerateErrorReport() : "Unknown PlayFab UpdateUserData error.";
                    Debug.LogError(
                        $"[PlayFabProfileRepository] Save failed. " +
                        $"PlayerId={snapshot.PlayerId} | ProfileId={snapshot.ProfileId} | Error={message}");
                    tcs.TrySetResult(false);
                });

            return tcs.Task;
#else
            if (logDebug)
                Debug.LogWarning("[PlayFabProfileRepository] PLAYFAB_ENABLED not defined. Save skipped.");

            return Task.CompletedTask;
#endif
        }

        public Task DeleteByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return Task.CompletedTask;

#if PLAYFAB_ENABLED
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            UpdateUserDataRequest request = new UpdateUserDataRequest
            {
                KeysToRemove = new List<string> { ProfileDataKey }
            };

            PlayFabClientAPI.UpdateUserData(
                request,
                result =>
                {
                    if (logDebug)
                        Debug.Log($"[PlayFabProfileRepository] Deleted remote profile data. PlayerId={playerId}");

                    tcs.TrySetResult(true);
                },
                error =>
                {
                    string message = error != null ? error.GenerateErrorReport() : "Unknown PlayFab delete profile error.";
                    Debug.LogWarning($"[PlayFabProfileRepository] Delete failed. PlayerId={playerId} | Error={message}");
                    tcs.TrySetResult(false);
                });

            return tcs.Task;
#else
            return Task.CompletedTask;
#endif
        }

        public bool Exists(string playerId)
        {
            return false;
        }

#if PLAYFAB_ENABLED
        private void TryUpdateTitleDisplayName(string displayName)
        {
            string sanitized = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
                return;

            UpdateUserTitleDisplayNameRequest request = new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = sanitized
            };

            PlayFabClientAPI.UpdateUserTitleDisplayName(
                request,
                result =>
                {
                    if (logDebug)
                        Debug.Log($"[PlayFabProfileRepository] Updated PlayFab title display name -> {sanitized}");
                },
                error =>
                {
                    string message = error != null ? error.GenerateErrorReport() : "Unknown PlayFab display name update error.";
                    Debug.LogWarning($"[PlayFabProfileRepository] Display name update failed. Error={message}");
                });
        }
#endif
    }
}