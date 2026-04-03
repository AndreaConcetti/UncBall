using System;
using System.Text;
using UnityEngine;

public static class OnlinePlayerTokenCodec
{
    private const char Separator = '|';
    private const int CurrentVersion = 1;

    public static byte[] Encode(OnlinePlayerMatchStatsSnapshot snapshot)
    {
        if (snapshot == null)
            return Array.Empty<byte>();

        snapshot.Normalize();

        string onlinePlayerId = Sanitize(snapshot.onlinePlayerId, 24, "p");
        string profileId = Sanitize(snapshot.profileId, 24, "profile");
        string displayName = Sanitize(snapshot.displayName, 20, "Player");
        int level = Mathf.Clamp(snapshot.level, 1, 999);
        int totalMatches = Mathf.Clamp(snapshot.totalMatches, 0, 999999);
        int totalWins = Mathf.Clamp(snapshot.totalWins, 0, 999999);
        int totalLosses = Mathf.Clamp(snapshot.totalLosses, 0, 999999);
        int winRatePercent = Mathf.Clamp(snapshot.winRatePercent, 0, 100);

        // Formato compatto e stabile:
        // version|onlineId|profileId|displayName|level|matches|wins|losses|winRate
        string payload =
            CurrentVersion.ToString() + Separator +
            Escape(onlinePlayerId) + Separator +
            Escape(profileId) + Separator +
            Escape(displayName) + Separator +
            level.ToString() + Separator +
            totalMatches.ToString() + Separator +
            totalWins.ToString() + Separator +
            totalLosses.ToString() + Separator +
            winRatePercent.ToString();

        byte[] bytes = Encoding.UTF8.GetBytes(payload);

        if (bytes.Length > 220)
        {
            Debug.LogWarning(
                "[OnlinePlayerTokenCodec] Token too large after encode (" + bytes.Length +
                " bytes). Falling back to minimal token.");

            payload =
                CurrentVersion.ToString() + Separator +
                Escape(onlinePlayerId) + Separator +
                Escape(profileId) + Separator +
                Escape(Truncate(displayName, 12)) + Separator +
                level.ToString() + Separator +
                "0" + Separator +
                "0" + Separator +
                "0" + Separator +
                "0";

            bytes = Encoding.UTF8.GetBytes(payload);
        }

        return bytes;
    }

    public static bool TryDecode(byte[] token, out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = null;

        if (token == null || token.Length == 0)
            return false;

        try
        {
            string payload = Encoding.UTF8.GetString(token);
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            string[] parts = payload.Split(Separator);
            if (parts.Length != 9)
            {
                Debug.LogWarning(
                    "[OnlinePlayerTokenCodec] Decode failed: expected 9 parts, got " +
                    parts.Length + ". Payload=" + payload);
                return false;
            }

            if (!int.TryParse(parts[0], out int version))
            {
                Debug.LogWarning("[OnlinePlayerTokenCodec] Decode failed: invalid version.");
                return false;
            }

            if (version != CurrentVersion)
            {
                Debug.LogWarning(
                    "[OnlinePlayerTokenCodec] Decode failed: unsupported version " + version);
                return false;
            }

            string onlinePlayerId = Unescape(parts[1]);
            string profileId = Unescape(parts[2]);
            string displayName = Unescape(parts[3]);

            if (!int.TryParse(parts[4], out int level))
                level = 1;

            if (!int.TryParse(parts[5], out int totalMatches))
                totalMatches = 0;

            if (!int.TryParse(parts[6], out int totalWins))
                totalWins = 0;

            if (!int.TryParse(parts[7], out int totalLosses))
                totalLosses = 0;

            if (!int.TryParse(parts[8], out int winRatePercent))
                winRatePercent = 0;

            snapshot = new OnlinePlayerMatchStatsSnapshot
            {
                onlinePlayerId = onlinePlayerId,
                profileId = profileId,
                displayName = displayName,
                level = Mathf.Max(1, level),
                totalMatches = Mathf.Max(0, totalMatches),
                totalWins = Mathf.Max(0, totalWins),
                totalLosses = Mathf.Max(0, totalLosses),
                winRatePercent = Mathf.Clamp(winRatePercent, 0, 100)
            };

            snapshot.Normalize();
            return snapshot.IsValid;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[OnlinePlayerTokenCodec] Decode failed: " + ex.Message);
            snapshot = null;
            return false;
        }
    }

    private static string Sanitize(string value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = fallback;

        value = value.Trim();
        value = value.Replace("\n", " ").Replace("\r", " ");
        value = value.Replace("|", "/");

        return Truncate(value, maxLength);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength);
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value ?? string.Empty);
    }

    private static string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return Uri.UnescapeDataString(value);
    }
}