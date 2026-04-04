using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Bootstrap;
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

            CurrentProfile = ProfileSnapshot.CreateNew(playerId, SanitizeDisplayName(displayName));
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

            string sanitized = SanitizeDisplayName(displayName);

            if (string.Equals(CurrentProfile.DisplayName, sanitized, StringComparison.Ordinal))
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithDisplayName(sanitized);
            await repository.SaveAsync(CurrentProfile);

            await SyncDisplayNameToAuthIfPossibleAsync(sanitized);

            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetEquippedBallSkinAsync(string skinId)
        {
            EnsureProfileExists();

            if (string.Equals(CurrentProfile.EquippedBallSkinId, skinId ?? string.Empty, StringComparison.Ordinal))
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithEquippedBallSkin(skinId);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetEquippedTableSkinAsync(string skinId)
        {
            EnsureProfileExists();

            if (string.Equals(CurrentProfile.EquippedTableSkinId, skinId ?? string.Empty, StringComparison.Ordinal))
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithEquippedTableSkin(skinId);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetProgressionAsync(int xp, int level)
        {
            EnsureProfileExists();

            int sanitizedXp = Mathf.Max(0, xp);
            int sanitizedLevel = Mathf.Max(1, level);

            if (CurrentProfile.Xp == sanitizedXp && CurrentProfile.Level == sanitizedLevel)
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithProgression(sanitizedXp, sanitizedLevel);
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

            int sanitizedTotalMatches = Mathf.Max(0, totalMatches);
            int sanitizedTotalWins = Mathf.Max(0, totalWins);
            int sanitizedMultiplayerMatches = Mathf.Max(0, multiplayerMatches);
            int sanitizedMultiplayerWins = Mathf.Max(0, multiplayerWins);
            int sanitizedRankedMatches = Mathf.Max(0, rankedMatches);
            int sanitizedRankedWins = Mathf.Max(0, rankedWins);

            bool unchanged =
                CurrentProfile.TotalMatches == sanitizedTotalMatches &&
                CurrentProfile.TotalWins == sanitizedTotalWins &&
                CurrentProfile.MultiplayerMatches == sanitizedMultiplayerMatches &&
                CurrentProfile.MultiplayerWins == sanitizedMultiplayerWins &&
                CurrentProfile.RankedMatches == sanitizedRankedMatches &&
                CurrentProfile.RankedWins == sanitizedRankedWins;

            if (unchanged)
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithStats(
                sanitizedTotalMatches,
                sanitizedTotalWins,
                sanitizedMultiplayerMatches,
                sanitizedMultiplayerWins,
                sanitizedRankedMatches,
                sanitizedRankedWins
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

            bool displayNameChanged =
                CurrentProfile == null ||
                !string.Equals(CurrentProfile.DisplayName, snapshot.DisplayName, StringComparison.Ordinal);

            CurrentProfile = snapshot;
            await repository.SaveAsync(CurrentProfile);

            if (displayNameChanged)
                await SyncDisplayNameToAuthIfPossibleAsync(CurrentProfile.DisplayName);

            RaiseProfileChanged();
            return CurrentProfile;
        }

        private async Task SyncDisplayNameToAuthIfPossibleAsync(string displayName)
        {
            GameCompositionRoot root = GameCompositionRoot.Instance;
            if (root == null || !root.IsReady || root.AuthService == null)
                return;

            if (root.AuthService.CurrentSession == null || !root.AuthService.CurrentSession.HasUsableIdentity())
                return;

            string currentAuthName = root.AuthService.CurrentSession.Identity.DisplayName ?? string.Empty;
            if (string.Equals(currentAuthName, displayName ?? string.Empty, StringComparison.Ordinal))
                return;

            await root.AuthService.UpdateDisplayNameAsync(displayName, CancellationToken.None);
        }

        private string SanitizeDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();
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