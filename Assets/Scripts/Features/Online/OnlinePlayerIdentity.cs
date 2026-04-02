using System;

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
        this.profileId = Sanitize(profileId, "local_player_1");
        this.onlinePlayerId = Sanitize(onlinePlayerId, "online_player");
        this.displayName = Sanitize(displayName, "Player");
    }

    public static string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }
}