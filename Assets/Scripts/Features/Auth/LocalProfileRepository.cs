using System;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Profile.Models;

namespace UncballArena.Core.Profile.Repositories
{
    public sealed class LocalProfileRepository : IProfileRepository
    {
        private const string KeyPrefix = "uncball.profile.";

        [Serializable]
        private sealed class ProfileWrapper
        {
            public ProfileSnapshot snapshot;
        }

        public Task<ProfileSnapshot> LoadByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return Task.FromResult<ProfileSnapshot>(null);

            string key = BuildKey(playerId);

            if (!PlayerPrefs.HasKey(key))
                return Task.FromResult<ProfileSnapshot>(null);

            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return Task.FromResult<ProfileSnapshot>(null);

            try
            {
                ProfileWrapper wrapper = JsonUtility.FromJson<ProfileWrapper>(json);
                if (wrapper == null || wrapper.snapshot == null || !wrapper.snapshot.IsValid())
                    return Task.FromResult<ProfileSnapshot>(null);

                return Task.FromResult(wrapper.snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalProfileRepository] Load failed for PlayerId={playerId}. Exception: {ex}");
                return Task.FromResult<ProfileSnapshot>(null);
            }
        }

        public Task SaveAsync(ProfileSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsValid())
            {
                Debug.LogError("[LocalProfileRepository] SaveAsync failed: snapshot invalid or null.");
                return Task.CompletedTask;
            }

            string key = BuildKey(snapshot.PlayerId);

            try
            {
                ProfileWrapper wrapper = new ProfileWrapper
                {
                    snapshot = snapshot
                };

                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();

                Debug.Log($"[LocalProfileRepository] Saved profile. PlayerId={snapshot.PlayerId} | ProfileId={snapshot.ProfileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalProfileRepository] Save failed for PlayerId={snapshot.PlayerId}. Exception: {ex}");
            }

            return Task.CompletedTask;
        }

        public Task DeleteByPlayerIdAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return Task.CompletedTask;

            string key = BuildKey(playerId);

            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"[LocalProfileRepository] Deleted profile for PlayerId={playerId}");
            }

            return Task.CompletedTask;
        }

        public bool Exists(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            return PlayerPrefs.HasKey(BuildKey(playerId));
        }

        private static string BuildKey(string playerId)
        {
            return $"{KeyPrefix}{playerId.Trim()}";
        }
    }
}