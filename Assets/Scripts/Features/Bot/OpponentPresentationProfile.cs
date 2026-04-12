using System;
using UnityEngine;

[Serializable]
public sealed class OpponentPresentationProfile
{
    [Header("Common Identity")]
    [SerializeField] private string profileId;
    [SerializeField] private string displayName;

    [Header("Progression / MMR")]
    [SerializeField] private int level;
    [SerializeField] private int rankedLp;
    [SerializeField] private int winRatePercent;
    [SerializeField] private int totalMatches;
    [SerializeField] private int totalWins;

    [Header("Cosmetics")]
    [SerializeField] private string equippedSkinId;
    [SerializeField] private string avatarId;
    [SerializeField] private string frameId;

    [Header("Source")]
    [SerializeField] private bool isBot;
    [SerializeField] private bool isLocalBot;
    [SerializeField] private BotDifficulty difficulty;

    public string ProfileId => profileId;
    public string DisplayName => displayName;
    public int Level => level;
    public int RankedLp => rankedLp;
    public int WinRatePercent => winRatePercent;
    public int TotalMatches => totalMatches;
    public int TotalWins => totalWins;
    public string EquippedSkinId => equippedSkinId;
    public string AvatarId => avatarId;
    public string FrameId => frameId;
    public bool IsBot => isBot;
    public bool IsLocalBot => isLocalBot;
    public BotDifficulty Difficulty => difficulty;

    public OpponentPresentationProfile(
        string profileId,
        string displayName,
        int level,
        int rankedLp,
        int winRatePercent,
        int totalMatches,
        int totalWins,
        string equippedSkinId,
        string avatarId,
        string frameId,
        bool isBot,
        bool isLocalBot,
        BotDifficulty difficulty)
    {
        this.profileId = string.IsNullOrWhiteSpace(profileId) ? string.Empty : profileId.Trim();
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Opponent" : displayName.Trim();
        this.level = Mathf.Max(1, level);
        this.rankedLp = Mathf.Max(0, rankedLp);
        this.winRatePercent = Mathf.Clamp(winRatePercent, 0, 100);
        this.totalMatches = Mathf.Max(0, totalMatches);
        this.totalWins = Mathf.Clamp(totalWins, 0, this.totalMatches);
        this.equippedSkinId = string.IsNullOrWhiteSpace(equippedSkinId) ? string.Empty : equippedSkinId.Trim();
        this.avatarId = string.IsNullOrWhiteSpace(avatarId) ? string.Empty : avatarId.Trim();
        this.frameId = string.IsNullOrWhiteSpace(frameId) ? string.Empty : frameId.Trim();
        this.isBot = isBot;
        this.isLocalBot = isLocalBot;
        this.difficulty = difficulty;
    }

    public static OpponentPresentationProfile FromBot(BotProfileRuntimeData bot)
    {
        if (bot == null)
        {
            Debug.LogWarning("[OpponentPresentationProfile] FromBot called with null bot.");
            return null;
        }

        return new OpponentPresentationProfile(
            bot.BotId,
            bot.DisplayName,
            bot.FakeLevel,
            bot.FakeRankedLp,
            bot.FakeWinRate,
            bot.FakeMatchesPlayed,
            bot.FakeWins,
            bot.EquippedSkinId,
            bot.AvatarId,
            bot.FrameId,
            true,
            bot.IsLocalBot,
            bot.Difficulty);
    }

    public override string ToString()
    {
        return $"OpponentPresentationProfile | " +
               $"Id={profileId} | Name={displayName} | Level={level} | LP={rankedLp} | " +
               $"WinRate={winRatePercent}% | Matches={totalMatches} | Wins={totalWins} | " +
               $"Skin={equippedSkinId} | IsBot={isBot} | IsLocalBot={isLocalBot} | Difficulty={difficulty}";
    }
}