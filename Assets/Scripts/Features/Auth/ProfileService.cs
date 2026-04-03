using System;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Repositories;

namespace UncballArena.Core.Profile.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly IProfileRepository repository;

        public ProfileSnapshot CurrentProfile { get; private set; }
        public bool IsInitialized { get; private set; }

        public event Action<ProfileSnapshot> ProfileChanged;

        public ProfileService(IProfileRepository repository)
        {
            this.repository = repository;
        }

        public async Task InitializeAsync(string playerId, string displayName = "")
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                Debug.LogError("[ProfileService] InitializeAsync failed: playerId is null or empty.");
                return;
            }

            await LoadOrCreateProfileAsync(playerId, displayName);
            IsInitialized = true;

            Debug.Log($"[ProfileService] Initialized. PlayerId={playerId} | ProfileId={CurrentProfile?.ProfileId}");
        }

        public async Task<ProfileSnapshot> LoadOrCreateProfileAsync(string playerId, string displayName = "")
        {
            ProfileSnapshot loaded = await repository.LoadByPlayerIdAsync(playerId);

            if (loaded != null && loaded.IsValid())
            {
                CurrentProfile = loaded;
                RaiseProfileChanged();
                return CurrentProfile;
            }

            CurrentProfile = ProfileSnapshot.CreateNew(playerId, displayName);
            await repository.SaveAsync(CurrentProfile);

            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> ReloadAsync()
        {
            if (CurrentProfile == null || string.IsNullOrWhiteSpace(CurrentProfile.PlayerId))
                return null;

            ProfileSnapshot loaded = await repository.LoadByPlayerIdAsync(CurrentProfile.PlayerId);
            if (loaded != null && loaded.IsValid())
            {
                CurrentProfile = loaded;
                RaiseProfileChanged();
            }

            return CurrentProfile;
        }

        public Task SaveAsync()
        {
            if (CurrentProfile == null || !CurrentProfile.IsValid())
            {
                Debug.LogWarning("[ProfileService] SaveAsync skipped: CurrentProfile invalid or null.");
                return Task.CompletedTask;
            }

            return repository.SaveAsync(CurrentProfile);
        }

        public async Task<ProfileSnapshot> SetDisplayNameAsync(string displayName)
        {
            EnsureProfileExists();

            CurrentProfile = CurrentProfile.WithDisplayName(displayName);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetEquippedBallSkinAsync(string skinId)
        {
            EnsureProfileExists();

            CurrentProfile = CurrentProfile.WithEquippedBallSkin(skinId);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetEquippedTableSkinAsync(string skinId)
        {
            EnsureProfileExists();

            CurrentProfile = CurrentProfile.WithEquippedTableSkin(skinId);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetProgressionAsync(int xp, int level)
        {
            EnsureProfileExists();

            CurrentProfile = CurrentProfile.WithProgression(xp, level);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetStatsAsync(
            int totalMatches,
            int totalWins,
            int multiplayerMatches,
            int multiplayerWins,
            int rankedMatches,
            int rankedWins)
        {
            EnsureProfileExists();

            CurrentProfile = CurrentProfile.WithStats(
                totalMatches,
                totalWins,
                multiplayerMatches,
                multiplayerWins,
                rankedMatches,
                rankedWins
            );

            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> ApplyAuthoritativeSnapshotAsync(ProfileSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsValid())
            {
                Debug.LogError("[ProfileService] ApplyAuthoritativeSnapshotAsync failed: snapshot invalid or null.");
                return CurrentProfile;
            }

            CurrentProfile = snapshot;
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        private void EnsureProfileExists()
        {
            if (CurrentProfile == null)
                throw new InvalidOperationException("[ProfileService] CurrentProfile is null. InitializeAsync or LoadOrCreateProfileAsync must be called first.");
        }

        private void RaiseProfileChanged()
        {
            ProfileChanged?.Invoke(CurrentProfile);
        }
    }
}