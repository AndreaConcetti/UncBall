using System;
using UncballArena.Core.Runtime;

[Serializable]
public sealed class OnlinePlayerIdentity
{
    public string profileId;
    public string onlinePlayerId;
    public string displayName;

    public OnlinePlayerIdentity()
    {
        profileId = string.Empty;
        onlinePlayerId = string.Empty;
        displayName = string.Empty;
    }

    public OnlinePlayerIdentity(string profileId, string onlinePlayerId, string displayName)
    {
        this.profileId = Sanitize(profileId, ResolveLocalProfileFallback());
        this.onlinePlayerId = Sanitize(onlinePlayerId, "online_player");
        this.displayName = Sanitize(displayName, ResolveLocalDisplayNameFallback());
    }

    private static string ResolveLocalProfileFallback()
    {
        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId))
            return OnlineLocalPlayerContext.PlayerId.Trim();

        return "guest_fallback";
    }

    private static string ResolveLocalDisplayNameFallback()
    {
        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.DisplayName))
            return OnlineLocalPlayerContext.DisplayName.Trim();

        return "Player";
    }

    public static string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}