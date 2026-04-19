using System;
using UnityEngine;

namespace UncballArena.Core.Profile.Models
{
    [Serializable]
    public sealed class ProfileSnapshot
    {
        [Header("Identity")]
        [SerializeField] private string profileId;
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;

        [Header("Progression")]
        [SerializeField] private int xp;
        [SerializeField] private int level;

        [Header("Stats")]
        [SerializeField] private int totalMatches;
        [SerializeField] private int totalWins;
        [SerializeField] private int multiplayerMatches;
        [SerializeField] private int multiplayerWins;
        [SerializeField] private int rankedMatches;
        [SerializeField] private int rankedWins;

        [Header("Cosmetics")]
        [SerializeField] private string equippedBallSkinId;
        [SerializeField] private string equippedTableSkinId;

        [Header("Economy")]
        [SerializeField] private int softCurrency;
        [SerializeField] private int hardCurrency;
        [SerializeField] private int rankedLp;

        [Header("Daily Login")]
        [SerializeField] private string lastDailyLoginClaimDateUtc;
        [SerializeField] private int consecutiveLoginDays;

        [Header("Metadata")]
        [SerializeField] private long createdAtUnixSeconds;
        [SerializeField] private long updatedAtUnixSeconds;

        public string ProfileId => profileId;
        public string PlayerId => playerId;
        public string DisplayName => displayName;

        public int Xp => xp;
        public int Level => level;

        public int TotalMatches => totalMatches;
        public int TotalWins => totalWins;
        public int MultiplayerMatches => multiplayerMatches;
        public int MultiplayerWins => multiplayerWins;
        public int RankedMatches => rankedMatches;
        public int RankedWins => rankedWins;

        public string EquippedBallSkinId => equippedBallSkinId;
        public string EquippedTableSkinId => equippedTableSkinId;

        public int SoftCurrency => softCurrency;
        public int HardCurrency => hardCurrency;
        public int RankedLp => rankedLp;

        public string LastDailyLoginClaimDateUtc => lastDailyLoginClaimDateUtc;
        public int ConsecutiveLoginDays => consecutiveLoginDays;

        public long CreatedAtUnixSeconds => createdAtUnixSeconds;
        public long UpdatedAtUnixSeconds => updatedAtUnixSeconds;

        public float TotalWinRate => totalMatches > 0 ? (float)totalWins / totalMatches : 0f;
        public float MultiplayerWinRate => multiplayerMatches > 0 ? (float)multiplayerWins / multiplayerMatches : 0f;
        public float RankedWinRate => rankedMatches > 0 ? (float)rankedWins / rankedMatches : 0f;

        public bool hasData => IsValid();
        public int premiumCurrency => HardCurrency;
        public int dailyLoginStreakDay => ConsecutiveLoginDays;
        public int lastDailyRewardDayIndex => string.IsNullOrWhiteSpace(lastDailyLoginClaimDateUtc) ? 0 : 1;
        public long lastDailyRewardClaimUnixSeconds => TryParseClaimDateUtcToUnix(lastDailyLoginClaimDateUtc);

        public ProfileSnapshot(
            string profileId,
            string playerId,
            string displayName,
            int xp,
            int level,
            int totalMatches,
            int totalWins,
            int multiplayerMatches,
            int multiplayerWins,
            int rankedMatches,
            int rankedWins,
            string equippedBallSkinId,
            string equippedTableSkinId,
            int softCurrency,
            int hardCurrency,
            int rankedLp,
            string lastDailyLoginClaimDateUtc,
            int consecutiveLoginDays,
            long createdAtUnixSeconds,
            long updatedAtUnixSeconds)
        {
            this.profileId = Sanitize(profileId);
            this.playerId = Sanitize(playerId);
            this.displayName = Sanitize(displayName);

            this.xp = Mathf.Max(0, xp);
            this.level = Mathf.Max(1, level);

            this.totalMatches = Mathf.Max(0, totalMatches);
            this.totalWins = Mathf.Clamp(totalWins, 0, this.totalMatches);

            this.multiplayerMatches = Mathf.Max(0, multiplayerMatches);
            this.multiplayerWins = Mathf.Clamp(multiplayerWins, 0, this.multiplayerMatches);

            this.rankedMatches = Mathf.Max(0, rankedMatches);
            this.rankedWins = Mathf.Clamp(rankedWins, 0, this.rankedMatches);

            this.equippedBallSkinId = Sanitize(equippedBallSkinId);
            this.equippedTableSkinId = Sanitize(equippedTableSkinId);

            this.softCurrency = Mathf.Max(0, softCurrency);
            this.hardCurrency = Mathf.Max(0, hardCurrency);
            this.rankedLp = Mathf.Max(0, rankedLp);

            this.lastDailyLoginClaimDateUtc = Sanitize(lastDailyLoginClaimDateUtc);
            this.consecutiveLoginDays = Mathf.Clamp(consecutiveLoginDays, 0, 7);

            this.createdAtUnixSeconds = Math.Max(0, createdAtUnixSeconds);
            this.updatedAtUnixSeconds = Math.Max(0, updatedAtUnixSeconds);
        }

        public ProfileSnapshot(
            string profileId,
            string playerId,
            string displayName,
            int xp,
            int level,
            int totalMatches,
            int totalWins,
            int multiplayerMatches,
            int multiplayerWins,
            int rankedMatches,
            int rankedWins,
            string equippedBallSkinId,
            string equippedTableSkinId,
            int softCurrency,
            int hardCurrency,
            long createdAtUnixSeconds,
            long updatedAtUnixSeconds)
            : this(
                profileId,
                playerId,
                displayName,
                xp,
                level,
                totalMatches,
                totalWins,
                multiplayerMatches,
                multiplayerWins,
                rankedMatches,
                rankedWins,
                equippedBallSkinId,
                equippedTableSkinId,
                softCurrency,
                hardCurrency,
                1000,
                string.Empty,
                0,
                createdAtUnixSeconds,
                updatedAtUnixSeconds)
        {
        }

        public static ProfileSnapshot CreateNew(string playerId, string displayName, string equippedBallSkinId = "")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new ProfileSnapshot(
                profileId: $"profile_{Guid.NewGuid():N}",
                playerId: playerId,
                displayName: displayName,
                xp: 0,
                level: 1,
                totalMatches: 0,
                totalWins: 0,
                multiplayerMatches: 0,
                multiplayerWins: 0,
                rankedMatches: 0,
                rankedWins: 0,
                equippedBallSkinId: equippedBallSkinId,
                equippedTableSkinId: string.Empty,
                softCurrency: 0,
                hardCurrency: 0,
                rankedLp: 1000,
                lastDailyLoginClaimDateUtc: string.Empty,
                consecutiveLoginDays: 0,
                createdAtUnixSeconds: now,
                updatedAtUnixSeconds: now);
        }

        public ProfileSnapshot WithDisplayName(string newDisplayName)
        {
            return CreateCopy(displayName: newDisplayName);
        }

        public ProfileSnapshot WithProgression(int newXp, int newLevel)
        {
            return CreateCopy(xp: newXp, level: newLevel);
        }

        public ProfileSnapshot WithStats(
            int newTotalMatches,
            int newTotalWins,
            int newMultiplayerMatches,
            int newMultiplayerWins,
            int newRankedMatches,
            int newRankedWins)
        {
            return CreateCopy(
                totalMatches: newTotalMatches,
                totalWins: newTotalWins,
                multiplayerMatches: newMultiplayerMatches,
                multiplayerWins: newMultiplayerWins,
                rankedMatches: newRankedMatches,
                rankedWins: newRankedWins);
        }

        public ProfileSnapshot WithEquippedBallSkin(string newSkinId)
        {
            return CreateCopy(equippedBallSkinId: newSkinId);
        }

        public ProfileSnapshot WithEquippedTableSkin(string newSkinId)
        {
            return CreateCopy(equippedTableSkinId: newSkinId);
        }

        public ProfileSnapshot WithCurrencies(int newSoftCurrency, int newHardCurrency)
        {
            return CreateCopy(softCurrency: newSoftCurrency, hardCurrency: newHardCurrency);
        }

        public ProfileSnapshot WithRankedLp(int newRankedLp)
        {
            return CreateCopy(rankedLp: newRankedLp);
        }

        public ProfileSnapshot WithDailyLoginState(string newLastDailyLoginClaimDateUtc, int newConsecutiveLoginDays)
        {
            return CreateCopy(
                lastDailyLoginClaimDateUtc: newLastDailyLoginClaimDateUtc,
                consecutiveLoginDays: newConsecutiveLoginDays);
        }

        public ProfileSnapshot WithProgressionAndDailyLogin(
            int newXp,
            int newLevel,
            string newLastDailyLoginClaimDateUtc,
            int newConsecutiveLoginDays)
        {
            return CreateCopy(
                xp: newXp,
                level: newLevel,
                lastDailyLoginClaimDateUtc: newLastDailyLoginClaimDateUtc,
                consecutiveLoginDays: newConsecutiveLoginDays);
        }

        public ProfileSnapshot WithCurrenciesAndDailyLogin(
            int newSoftCurrency,
            int newPremiumCurrency,
            int newDailyLoginStreakDay,
            long newLastDailyRewardClaimUnixSeconds,
            int newLastDailyRewardDayIndex)
        {
            string claimDate = UnixToRewardDateString(newLastDailyRewardClaimUnixSeconds);
            return CreateCopy(
                softCurrency: newSoftCurrency,
                hardCurrency: newPremiumCurrency,
                lastDailyLoginClaimDateUtc: claimDate,
                consecutiveLoginDays: newDailyLoginStreakDay);
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            if (level <= 0)
                return false;

            if (xp < 0)
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"ProfileSnapshot(ProfileId={profileId}, PlayerId={playerId}, DisplayName={displayName}, XP={xp}, Level={level}, TotalMatches={totalMatches}, TotalWins={totalWins})";
        }

        private ProfileSnapshot CreateCopy(
            string profileId = null,
            string playerId = null,
            string displayName = null,
            int? xp = null,
            int? level = null,
            int? totalMatches = null,
            int? totalWins = null,
            int? multiplayerMatches = null,
            int? multiplayerWins = null,
            int? rankedMatches = null,
            int? rankedWins = null,
            string equippedBallSkinId = null,
            string equippedTableSkinId = null,
            int? softCurrency = null,
            int? hardCurrency = null,
            int? rankedLp = null,
            string lastDailyLoginClaimDateUtc = null,
            int? consecutiveLoginDays = null,
            long? createdAtUnixSeconds = null,
            long? updatedAtUnixSeconds = null)
        {
            return new ProfileSnapshot(
                profileId ?? this.profileId,
                playerId ?? this.playerId,
                displayName ?? this.displayName,
                xp ?? this.xp,
                level ?? this.level,
                totalMatches ?? this.totalMatches,
                totalWins ?? this.totalWins,
                multiplayerMatches ?? this.multiplayerMatches,
                multiplayerWins ?? this.multiplayerWins,
                rankedMatches ?? this.rankedMatches,
                rankedWins ?? this.rankedWins,
                equippedBallSkinId ?? this.equippedBallSkinId,
                equippedTableSkinId ?? this.equippedTableSkinId,
                softCurrency ?? this.softCurrency,
                hardCurrency ?? this.hardCurrency,
                rankedLp ?? this.rankedLp,
                lastDailyLoginClaimDateUtc ?? this.lastDailyLoginClaimDateUtc,
                consecutiveLoginDays ?? this.consecutiveLoginDays,
                createdAtUnixSeconds ?? this.createdAtUnixSeconds,
                updatedAtUnixSeconds ?? Now());
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string UnixToRewardDateString(long unixSeconds)
        {
            if (unixSeconds <= 0)
                return string.Empty;

            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd");
        }

        private static long TryParseClaimDateUtcToUnix(string claimDate)
        {
            if (string.IsNullOrWhiteSpace(claimDate))
                return 0;

            if (!DateTime.TryParse(claimDate, out DateTime parsed))
                return 0;

            DateTime utc = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }
    }
}
