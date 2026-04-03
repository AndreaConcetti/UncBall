using System;
using UnityEngine;

namespace UncballArena.Core.Auth.Models
{
    [Serializable]
    public sealed class AuthSession
    {
        [SerializeField] private bool isAuthenticated;
        [SerializeField] private bool isExpired;
        [SerializeField] private string accessToken;
        [SerializeField] private string refreshToken;
        [SerializeField] private long issuedAtUnixSeconds;
        [SerializeField] private long expiresAtUnixSeconds;
        [SerializeField] private PlayerIdentity identity;

        public bool IsAuthenticated => isAuthenticated;
        public bool IsExpired => isExpired || HasReachedExpiration();
        public string AccessToken => accessToken;
        public string RefreshToken => refreshToken;
        public long IssuedAtUnixSeconds => issuedAtUnixSeconds;
        public long ExpiresAtUnixSeconds => expiresAtUnixSeconds;
        public PlayerIdentity Identity => identity;

        public AuthSession(
            bool isAuthenticated,
            bool isExpired,
            string accessToken,
            string refreshToken,
            long issuedAtUnixSeconds,
            long expiresAtUnixSeconds,
            PlayerIdentity identity)
        {
            this.isAuthenticated = isAuthenticated;
            this.isExpired = isExpired;
            this.accessToken = string.IsNullOrWhiteSpace(accessToken) ? string.Empty : accessToken.Trim();
            this.refreshToken = string.IsNullOrWhiteSpace(refreshToken) ? string.Empty : refreshToken.Trim();
            this.issuedAtUnixSeconds = Math.Max(0, issuedAtUnixSeconds);
            this.expiresAtUnixSeconds = Math.Max(0, expiresAtUnixSeconds);
            this.identity = identity;
        }

        public static AuthSession CreateGuestSession(PlayerIdentity identity)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new AuthSession(
                isAuthenticated: identity != null && identity.IsValid(),
                isExpired: false,
                accessToken: string.Empty,
                refreshToken: string.Empty,
                issuedAtUnixSeconds: now,
                expiresAtUnixSeconds: 0,
                identity: identity);
        }

        public static AuthSession CreateAuthenticatedSession(
            PlayerIdentity identity,
            string accessToken,
            string refreshToken,
            long expiresAtUnixSeconds)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return new AuthSession(
                isAuthenticated: identity != null && identity.IsValid(),
                isExpired: false,
                accessToken: accessToken,
                refreshToken: refreshToken,
                issuedAtUnixSeconds: now,
                expiresAtUnixSeconds: expiresAtUnixSeconds,
                identity: identity);
        }

        public static AuthSession CreateLoggedOut()
        {
            return new AuthSession(
                isAuthenticated: false,
                isExpired: true,
                accessToken: string.Empty,
                refreshToken: string.Empty,
                issuedAtUnixSeconds: 0,
                expiresAtUnixSeconds: 0,
                identity: null);
        }

        public AuthSession WithIdentity(PlayerIdentity newIdentity)
        {
            return new AuthSession(
                isAuthenticated,
                isExpired,
                accessToken,
                refreshToken,
                issuedAtUnixSeconds,
                expiresAtUnixSeconds,
                newIdentity);
        }

        public AuthSession WithRefreshedTokens(string newAccessToken, string newRefreshToken, long newExpiresAtUnixSeconds)
        {
            return new AuthSession(
                isAuthenticated: true,
                isExpired: false,
                accessToken: newAccessToken,
                refreshToken: newRefreshToken,
                issuedAtUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                expiresAtUnixSeconds: newExpiresAtUnixSeconds,
                identity: identity);
        }

        public AuthSession MarkExpired()
        {
            return new AuthSession(
                isAuthenticated,
                isExpired: true,
                accessToken,
                refreshToken,
                issuedAtUnixSeconds,
                expiresAtUnixSeconds,
                identity);
        }

        public bool HasUsableIdentity()
        {
            return identity != null && identity.IsValid();
        }

        private bool HasReachedExpiration()
        {
            if (expiresAtUnixSeconds <= 0)
                return false;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now >= expiresAtUnixSeconds;
        }

        public override string ToString()
        {
            string identityInfo = identity == null ? "null" : identity.ToString();
            return $"AuthSession(IsAuthenticated={IsAuthenticated}, IsExpired={IsExpired}, ExpiresAt={expiresAtUnixSeconds}, Identity={identityInfo})";
        }
    }
}