using System;
using UnityEngine;

namespace UncballArena.Core.Auth.Models
{
    [Serializable]
    public sealed class PlayerIdentity
    {
        [SerializeField] private string playerId;
        [SerializeField] private AuthProviderType providerType;
        [SerializeField] private string providerUserId;
        [SerializeField] private string displayName;
        [SerializeField] private bool isGuest;
        [SerializeField] private bool isLinked;
        [SerializeField] private long createdAtUnixSeconds;
        [SerializeField] private long lastLoginAtUnixSeconds;

        public string PlayerId => playerId;
        public AuthProviderType ProviderType => providerType;
        public string ProviderUserId => providerUserId;
        public string DisplayName => displayName;
        public bool IsGuest => isGuest;
        public bool IsLinked => isLinked;
        public long CreatedAtUnixSeconds => createdAtUnixSeconds;
        public long LastLoginAtUnixSeconds => lastLoginAtUnixSeconds;

        public PlayerIdentity(
            string playerId,
            AuthProviderType providerType,
            string providerUserId,
            string displayName,
            bool isGuest,
            bool isLinked,
            long createdAtUnixSeconds,
            long lastLoginAtUnixSeconds)
        {
            this.playerId = string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim();
            this.providerType = providerType;
            this.providerUserId = string.IsNullOrWhiteSpace(providerUserId) ? string.Empty : providerUserId.Trim();
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            this.isGuest = isGuest;
            this.isLinked = isLinked;
            this.createdAtUnixSeconds = Math.Max(0, createdAtUnixSeconds);
            this.lastLoginAtUnixSeconds = Math.Max(0, lastLoginAtUnixSeconds);
        }

        public static PlayerIdentity CreateGuest(string guestId, string displayName = "")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new PlayerIdentity(
                playerId: guestId,
                providerType: AuthProviderType.Guest,
                providerUserId: guestId,
                displayName: displayName,
                isGuest: true,
                isLinked: false,
                createdAtUnixSeconds: now,
                lastLoginAtUnixSeconds: now);
        }

        public static PlayerIdentity CreateLinked(
            AuthProviderType providerType,
            string providerUserId,
            string playerId,
            string displayName,
            bool isLinked = true)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new PlayerIdentity(
                playerId: playerId,
                providerType: providerType,
                providerUserId: providerUserId,
                displayName: displayName,
                isGuest: false,
                isLinked: isLinked,
                createdAtUnixSeconds: now,
                lastLoginAtUnixSeconds: now);
        }

        public PlayerIdentity WithDisplayName(string newDisplayName)
        {
            return new PlayerIdentity(
                playerId,
                providerType,
                providerUserId,
                newDisplayName,
                isGuest,
                isLinked,
                createdAtUnixSeconds,
                lastLoginAtUnixSeconds);
        }

        public PlayerIdentity WithLastLoginNow()
        {
            return new PlayerIdentity(
                playerId,
                providerType,
                providerUserId,
                displayName,
                isGuest,
                isLinked,
                createdAtUnixSeconds,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public PlayerIdentity AsLinked(AuthProviderType newProviderType, string newProviderUserId)
        {
            return new PlayerIdentity(
                playerId,
                newProviderType,
                newProviderUserId,
                displayName,
                isGuest: false,
                isLinked: true,
                createdAtUnixSeconds,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            if (providerType == AuthProviderType.None)
                return false;

            if (string.IsNullOrWhiteSpace(providerUserId))
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"PlayerIdentity(PlayerId={playerId}, ProviderType={providerType}, ProviderUserId={providerUserId}, DisplayName={displayName}, IsGuest={isGuest}, IsLinked={isLinked})";
        }
    }
}