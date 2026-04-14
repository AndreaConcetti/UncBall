using System;
using UnityEngine;

[Serializable]
public enum BotDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2,
    Unbeatable = 3
}

[Serializable]
public enum BotArchetype
{
    Balanced = 0,
    Aggressive = 1,
    Defensive = 2,
    Trickster = 3,
    Precision = 4
}

[Serializable]
public sealed class BotProfileRuntimeData
{
    [Header("Identity")]
    [SerializeField] private string botId;
    [SerializeField] private string displayName;

    [Header("Gameplay")]
    [SerializeField] private BotDifficulty difficulty;
    [SerializeField] private BotArchetype botArchetype;

    [Header("Cosmetics")]
    [SerializeField] private string equippedSkinId;
    [SerializeField] private string avatarId;
    [SerializeField] private string frameId;

    [Header("Fake Profile Stats")]
    [SerializeField] private int fakeRankedLp;
    [SerializeField] private int fakeWinRate;
    [SerializeField] private int fakeMatchesPlayed;
    [SerializeField] private int fakeWins;
    [SerializeField] private int fakeLevel;

    [Header("Flags")]
    [SerializeField] private bool isEligibleForOnlineDisguise;
    [SerializeField] private bool isLocalBot;

    public string BotId => botId;
    public string DisplayName => displayName;
    public BotDifficulty Difficulty => difficulty;
    public BotArchetype Archetype => botArchetype;
    public string EquippedSkinId => equippedSkinId;
    public string AvatarId => avatarId;
    public string FrameId => frameId;
    public int FakeRankedLp => fakeRankedLp;
    public int FakeWinRate => fakeWinRate;
    public int FakeMatchesPlayed => fakeMatchesPlayed;
    public int FakeWins => fakeWins;
    public int FakeLevel => fakeLevel;
    public bool IsEligibleForOnlineDisguise => isEligibleForOnlineDisguise;
    public bool IsLocalBot => isLocalBot;

    public BotProfileRuntimeData(
        string botId,
        string displayName,
        BotDifficulty difficulty,
        BotArchetype botArchetype,
        string equippedSkinId,
        int fakeRankedLp,
        int fakeWinRate,
        int fakeMatchesPlayed,
        int fakeWins,
        int fakeLevel,
        string avatarId,
        string frameId,
        bool isEligibleForOnlineDisguise,
        bool isLocalBot)
    {
        this.botId = string.IsNullOrWhiteSpace(botId) ? Guid.NewGuid().ToString("N") : botId;
        this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Bot" : displayName.Trim();
        this.difficulty = difficulty;
        this.botArchetype = botArchetype;
        this.equippedSkinId = string.IsNullOrWhiteSpace(equippedSkinId) ? string.Empty : equippedSkinId.Trim();
        this.fakeRankedLp = Mathf.Max(0, fakeRankedLp);
        this.fakeWinRate = Mathf.Clamp(fakeWinRate, 0, 100);
        this.fakeMatchesPlayed = Mathf.Max(0, fakeMatchesPlayed);
        this.fakeWins = Mathf.Clamp(fakeWins, 0, this.fakeMatchesPlayed);
        this.fakeLevel = Mathf.Max(1, fakeLevel);
        this.avatarId = string.IsNullOrWhiteSpace(avatarId) ? string.Empty : avatarId.Trim();
        this.frameId = string.IsNullOrWhiteSpace(frameId) ? string.Empty : frameId.Trim();
        this.isEligibleForOnlineDisguise = isEligibleForOnlineDisguise;
        this.isLocalBot = isLocalBot;
    }

    public override string ToString()
    {
        return $"BotProfileRuntimeData | " +
               $"BotId={botId} | Name={displayName} | Difficulty={difficulty} | Archetype={botArchetype} | " +
               $"Skin={equippedSkinId} | LP={fakeRankedLp} | WinRate={fakeWinRate}% | " +
               $"Matches={fakeMatchesPlayed} | Wins={fakeWins} | Level={fakeLevel} | " +
               $"Avatar={avatarId} | Frame={frameId} | LocalBot={isLocalBot} | OnlineDisguise={isEligibleForOnlineDisguise}";
    }
}