using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Auth;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Repositories;

namespace UncballArena.Core.Profile.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly IProfileRepository repository;
        private readonly IAuthService authService;

        public ProfileSnapshot CurrentProfile { get; private set; }
        public bool IsInitialized { get; private set; }

        public event Action<ProfileSnapshot> ProfileChanged;

        public ProfileService(IProfileRepository repository, IAuthService authService = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.authService = authService;
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
            string sanitizedPlayerId = SanitizePlayerId(playerId);
            string sanitizedDisplayName = SanitizeDisplayName(displayName);

            ProfileSnapshot loaded = await repository.LoadByPlayerIdAsync(sanitizedPlayerId);

            if (loaded != null && loaded.IsValid())
            {
                bool needsDisplayNameBackfill =
                    string.IsNullOrWhiteSpace(loaded.DisplayName) &&
                    !string.IsNullOrWhiteSpace(sanitizedDisplayName);

                CurrentProfile = needsDisplayNameBackfill
                    ? loaded.WithDisplayName(sanitizedDisplayName)
                    : loaded;

                if (needsDisplayNameBackfill)
                    await repository.SaveAsync(CurrentProfile);

                RaiseProfileChanged();
                return CurrentProfile;
            }

            CurrentProfile = ProfileSnapshot.CreateNew(sanitizedPlayerId, sanitizedDisplayName);
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

            string sanitized = string.IsNullOrWhiteSpace(skinId) ? string.Empty : skinId.Trim();

            if (string.Equals(CurrentProfile.EquippedBallSkinId, sanitized, StringComparison.Ordinal))
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithEquippedBallSkin(sanitized);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetEquippedTableSkinAsync(string skinId)
        {
            EnsureProfileExists();

            string sanitized = string.IsNullOrWhiteSpace(skinId) ? string.Empty : skinId.Trim();

            if (string.Equals(CurrentProfile.EquippedTableSkinId, sanitized, StringComparison.Ordinal))
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithEquippedTableSkin(sanitized);
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

        public async Task<ProfileSnapshot> SetCurrenciesAsync(int softCurrency, int hardCurrency)
        {
            EnsureProfileExists();

            int sanitizedSoft = Mathf.Max(0, softCurrency);
            int sanitizedHard = Mathf.Max(0, hardCurrency);

            if (CurrentProfile.SoftCurrency == sanitizedSoft &&
                CurrentProfile.HardCurrency == sanitizedHard)
            {
                return CurrentProfile;
            }

            CurrentProfile = CurrentProfile.WithCurrencies(sanitizedSoft, sanitizedHard);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetRankedLpAsync(int rankedLp)
        {
            EnsureProfileExists();

            int sanitizedLp = Mathf.Max(0, rankedLp);

            if (CurrentProfile.RankedLp == sanitizedLp)
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithRankedLp(sanitizedLp);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> AddRankedLpAsync(int delta)
        {
            EnsureProfileExists();

            int target = Mathf.Max(0, CurrentProfile.RankedLp + delta);
            return await SetRankedLpAsync(target);
        }

        public async Task<ProfileSnapshot> SetDailyLoginStateAsync(
            string lastDailyLoginClaimDateUtc,
            int consecutiveLoginDays)
        {
            EnsureProfileExists();

            string sanitizedDate = string.IsNullOrWhiteSpace(lastDailyLoginClaimDateUtc)
                ? string.Empty
                : lastDailyLoginClaimDateUtc.Trim();

            int sanitizedStreak = Mathf.Max(0, consecutiveLoginDays);

            bool unchanged =
                string.Equals(CurrentProfile.LastDailyLoginClaimDateUtc, sanitizedDate, StringComparison.Ordinal) &&
                CurrentProfile.ConsecutiveLoginDays == sanitizedStreak;

            if (unchanged)
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithDailyLoginState(sanitizedDate, sanitizedStreak);
            await repository.SaveAsync(CurrentProfile);
            RaiseProfileChanged();
            return CurrentProfile;
        }

        public async Task<ProfileSnapshot> SetProgressionAndDailyLoginAsync(
            int xp,
            int level,
            string lastDailyLoginClaimDateUtc,
            int consecutiveLoginDays)
        {
            EnsureProfileExists();

            int sanitizedXp = Mathf.Max(0, xp);
            int sanitizedLevel = Mathf.Max(1, level);
            string sanitizedDate = string.IsNullOrWhiteSpace(lastDailyLoginClaimDateUtc)
                ? string.Empty
                : lastDailyLoginClaimDateUtc.Trim();
            int sanitizedStreak = Mathf.Max(0, consecutiveLoginDays);

            bool unchanged =
                CurrentProfile.Xp == sanitizedXp &&
                CurrentProfile.Level == sanitizedLevel &&
                string.Equals(CurrentProfile.LastDailyLoginClaimDateUtc, sanitizedDate, StringComparison.Ordinal) &&
                CurrentProfile.ConsecutiveLoginDays == sanitizedStreak;

            if (unchanged)
                return CurrentProfile;

            CurrentProfile = CurrentProfile.WithProgressionAndDailyLogin(
                sanitizedXp,
                sanitizedLevel,
                sanitizedDate,
                sanitizedStreak);

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
            if (authService == null)
                return;

            if (authService.CurrentSession == null || !authService.CurrentSession.HasUsableIdentity())
                return;

            string currentAuthName = authService.CurrentSession.Identity.DisplayName ?? string.Empty;
            if (string.Equals(currentAuthName, displayName ?? string.Empty, StringComparison.Ordinal))
                return;

            await authService.UpdateDisplayNameAsync(displayName, CancellationToken.None);
        }

        private string SanitizeDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();
        }

        private string SanitizePlayerId(string playerId)
        {
            return string.IsNullOrWhiteSpace(playerId)
                ? string.Empty
                : playerId.Trim();
        }

        private void EnsureProfileExists()
        {
            if (CurrentProfile != null && CurrentProfile.IsValid())
                return;

            throw new InvalidOperationException("[ProfileService] CurrentProfile is null or invalid.");
        }

        private void RaiseProfileChanged()
        {
            ProfileChanged?.Invoke(CurrentProfile);
        }
    }
}