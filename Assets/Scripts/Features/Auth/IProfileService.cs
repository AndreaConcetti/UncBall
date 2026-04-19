
using System;
using System.Threading;
using System.Threading.Tasks;
using UncballArena.Core.Profile.Models;

namespace UncballArena.Core.Profile.Services
{
    public interface IProfileService
    {
        ProfileSnapshot CurrentProfile { get; }
        bool IsInitialized { get; }

        event Action<ProfileSnapshot> ProfileChanged;

        Task InitializeAsync(string playerId, string displayName = "");
        Task<ProfileSnapshot> LoadOrCreateProfileAsync(string playerId, string displayName = "");
        Task<ProfileSnapshot> ReloadAsync();
        Task SaveAsync();

        Task<ProfileSnapshot> SetDisplayNameAsync(string displayName);
        Task<ProfileSnapshot> SetEquippedBallSkinAsync(string skinId);
        Task<ProfileSnapshot> SetEquippedTableSkinAsync(string skinId);
        Task<ProfileSnapshot> SetProgressionAsync(int xp, int level);
        Task<ProfileSnapshot> SetStatsAsync(
            int totalMatches,
            int totalWins,
            int multiplayerMatches,
            int multiplayerWins,
            int rankedMatches,
            int rankedWins);

        Task<ProfileSnapshot> SetCurrenciesAsync(int softCurrency, int hardCurrency);
        Task<ProfileSnapshot> SetRankedLpAsync(int rankedLp);
        Task<ProfileSnapshot> AddRankedLpAsync(int delta);

        Task<ProfileSnapshot> SetDailyLoginStateAsync(
            string lastDailyLoginClaimDateUtc,
            int consecutiveLoginDays);

        Task<ProfileSnapshot> SetProgressionAndDailyLoginAsync(
            int xp,
            int level,
            string lastDailyLoginClaimDateUtc,
            int consecutiveLoginDays);

        Task<ProfileSnapshot> SetLuckyShotStateAsync(
            int luckyShotTokens,
            int luckyShotTotalPlays,
            int luckyShotBestScore);

        Task<ProfileSnapshot> AddLuckyShotTokensAsync(int amount);
        Task<bool> TryConsumeLuckyShotTokenAsync();
        Task<ProfileSnapshot> RegisterLuckyShotPlayAsync(int score);

        Task<ProfileSnapshot> ApplyAuthoritativeSnapshotAsync(ProfileSnapshot snapshot, CancellationToken cancellationToken = default);
    }
}
