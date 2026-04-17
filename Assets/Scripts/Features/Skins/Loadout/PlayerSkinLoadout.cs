using System;
using UnityEngine;
using UncballArena.Core.Runtime;

[Serializable]
public class PlayerLoadoutSlotData
{
    public string profileId = "";
    public BallSkinData equippedSkin;
}

public class PlayerSkinLoadout : MonoBehaviour
{
    public static PlayerSkinLoadout Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private BallSkinDatabase database;
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Default Profiles")]
    [SerializeField] private string player1ProfileId = "local_player_1";
    [SerializeField] private string player2ProfileId = "local_player_2";

    [Header("Runtime Loadout")]
    [SerializeField] private PlayerLoadoutSlotData player1Slot = new PlayerLoadoutSlotData();
    [SerializeField] private PlayerLoadoutSlotData player2Slot = new PlayerLoadoutSlotData();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public event Action OnLoadoutChanged;

    public BallSkinDatabase Database => database;
    public string Player1ProfileId => player1Slot != null ? player1Slot.profileId : string.Empty;
    public string Player2ProfileId => player2Slot != null ? player2Slot.profileId : string.Empty;

    private PlayerProfileManager subscribedProfileManager;
    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        ResolveDependencies();
        EnsureRuntimeStructure();
        SanitizeRuntimeData();

        string resolvedLocalProfileId = ResolveLocalProfileId(false);
        if (!string.IsNullOrWhiteSpace(resolvedLocalProfileId))
            player1Slot.profileId = resolvedLocalProfileId;

        if (string.IsNullOrWhiteSpace(player2Slot.profileId))
            player2Slot.profileId = SanitizeProfileId(player2ProfileId, "remote_player");

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] Initialized. " +
                "Player1ProfileId=" + SafeProfileId(player1Slot.profileId) +
                " | Player2ProfileId=" + SafeProfileId(player2Slot.profileId) +
                " | P1Skin=" + GetSafeSkinId(player1Slot.equippedSkin) +
                " | P2Skin=" + GetSafeSkinId(player2Slot.equippedSkin) +
                " | Database=" + (database != null ? database.name : "null"),
                this
            );
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeProfileEvents();
    }

    private void OnDisable()
    {
        UnsubscribeProfileEvents();
    }

    private void Start()
    {
        ResolveDependencies();
        SubscribeProfileEvents();

        string resolvedLocalProfileId = ResolveLocalProfileId(false);
        if (!string.IsNullOrWhiteSpace(resolvedLocalProfileId))
            SetPlayer1ProfileId(resolvedLocalProfileId);
    }

    public void SetPlayer1ProfileId(string profileId)
    {
        EnsureRuntimeStructure();

        string fallback = ResolveLocalProfileId(true);
        string sanitized = SanitizeProfileId(profileId, fallback);

        if (string.IsNullOrWhiteSpace(sanitized))
            return;

        if (string.Equals(player1Slot.profileId, sanitized, StringComparison.Ordinal))
            return;

        player1Slot.profileId = sanitized;
        NotifyLoadoutChanged();

        if (logDebug)
            Debug.Log("[PlayerSkinLoadout] Player1 profile id set: " + player1Slot.profileId, this);
    }

    public void SetPlayer2ProfileId(string profileId)
    {
        EnsureRuntimeStructure();

        string sanitized = SanitizeProfileId(profileId, "remote_player");

        if (string.Equals(player2Slot.profileId, sanitized, StringComparison.Ordinal))
            return;

        player2Slot.profileId = sanitized;
        NotifyLoadoutChanged();

        if (logDebug)
            Debug.Log("[PlayerSkinLoadout] Player2 profile id set: " + player2Slot.profileId, this);
    }

    public void ConfigureMatchProfiles(string localProfileId, string remoteProfileId)
    {
        EnsureRuntimeStructure();

        player1Slot.profileId = SanitizeProfileId(localProfileId, ResolveLocalProfileId(true));
        player2Slot.profileId = SanitizeProfileId(remoteProfileId, "remote_player");

        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] ConfigureMatchProfiles -> " +
                "P1=" + SafeProfileId(player1Slot.profileId) +
                " | P2=" + SafeProfileId(player2Slot.profileId),
                this
            );
        }
    }

    public void EquipSkinForPlayer1(BallSkinData skin)
    {
        EnsureRuntimeStructure();

        if (skin == null)
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForPlayer1 called with null skin.", this);
            return;
        }

        if (!IsSkinDataUsable(skin))
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForPlayer1 called with invalid skin data.", this);
            return;
        }

        string resolvedLocalProfileId = ResolveLocalProfileId(true);
        if (!string.IsNullOrWhiteSpace(resolvedLocalProfileId))
            player1Slot.profileId = resolvedLocalProfileId;

        player1Slot.equippedSkin = CloneSkin(skin);
        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] Equipped skin for Player1/Profile=" + SafeProfileId(player1Slot.profileId) +
                " | SkinId=" + skin.skinUniqueId,
                this
            );
        }
    }

    public void EquipSkinForPlayer2(BallSkinData skin)
    {
        EnsureRuntimeStructure();

        if (skin == null)
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForPlayer2 called with null skin.", this);
            return;
        }

        if (!IsSkinDataUsable(skin))
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForPlayer2 called with invalid skin data.", this);
            return;
        }

        player2Slot.equippedSkin = CloneSkin(skin);
        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] Equipped skin for Player2/Profile=" + SafeProfileId(player2Slot.profileId) +
                " | SkinId=" + skin.skinUniqueId,
                this
            );
        }
    }

    public void EquipSkinForProfile(string profileId, BallSkinData skin)
    {
        EnsureRuntimeStructure();

        if (skin == null)
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForProfile called with null skin.", this);
            return;
        }

        if (!IsSkinDataUsable(skin))
        {
            Debug.LogWarning("[PlayerSkinLoadout] EquipSkinForProfile called with invalid skin data.", this);
            return;
        }

        string sanitized = SanitizeProfileId(profileId, ResolveLocalProfileId(true));

        if (string.Equals(player1Slot.profileId, sanitized, StringComparison.Ordinal))
        {
            EquipSkinForPlayer1(skin);
            return;
        }

        if (string.Equals(player2Slot.profileId, sanitized, StringComparison.Ordinal))
        {
            EquipSkinForPlayer2(skin);
            return;
        }

        player1Slot.profileId = sanitized;
        player1Slot.equippedSkin = CloneSkin(skin);
        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] EquipSkinForProfile mapped to Player1 slot. " +
                "Profile=" + SafeProfileId(sanitized) +
                " | SkinId=" + skin.skinUniqueId,
                this
            );
        }
    }

    public BallSkinData GetEquippedSkinForPlayer1()
    {
        EnsureRuntimeStructure();
        SanitizeRuntimeData();
        return player1Slot.equippedSkin;
    }

    public BallSkinData GetEquippedSkinForPlayer2()
    {
        EnsureRuntimeStructure();
        SanitizeRuntimeData();
        return player2Slot.equippedSkin;
    }

    public BallSkinData GetEquippedSkinForProfile(string profileId)
    {
        EnsureRuntimeStructure();
        SanitizeRuntimeData();

        string sanitized = SanitizeProfileId(profileId, string.Empty);

        if (string.Equals(player1Slot.profileId, sanitized, StringComparison.Ordinal))
            return player1Slot.equippedSkin;

        if (string.Equals(player2Slot.profileId, sanitized, StringComparison.Ordinal))
            return player2Slot.equippedSkin;

        return null;
    }

    public bool HasEquippedSkinForPlayer1()
    {
        return GetEquippedSkinForPlayer1() != null;
    }

    public bool HasEquippedSkinForPlayer2()
    {
        return GetEquippedSkinForPlayer2() != null;
    }

    public void ClearPlayer1Skin()
    {
        EnsureRuntimeStructure();

        player1Slot.equippedSkin = null;
        NotifyLoadoutChanged();

        if (logDebug)
            Debug.Log("[PlayerSkinLoadout] Cleared Player1 equipped skin.", this);
    }

    public void ClearPlayer2Skin()
    {
        EnsureRuntimeStructure();

        player2Slot.equippedSkin = null;
        NotifyLoadoutChanged();

        if (logDebug)
            Debug.Log("[PlayerSkinLoadout] Cleared Player2 equipped skin.", this);
    }

    public void ApplyMatchSnapshot(
        string newPlayer1ProfileId,
        BallSkinData newPlayer1Skin,
        string newPlayer2ProfileId,
        BallSkinData newPlayer2Skin)
    {
        EnsureRuntimeStructure();

        player1Slot.profileId = SanitizeProfileId(newPlayer1ProfileId, ResolveLocalProfileId(true));
        player2Slot.profileId = SanitizeProfileId(newPlayer2ProfileId, "remote_player");

        player1Slot.equippedSkin = IsSkinDataUsable(newPlayer1Skin) ? CloneSkin(newPlayer1Skin) : null;
        player2Slot.equippedSkin = IsSkinDataUsable(newPlayer2Skin) ? CloneSkin(newPlayer2Skin) : null;

        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] Applied match snapshot. " +
                "P1Profile=" + SafeProfileId(player1Slot.profileId) +
                " | P1Skin=" + GetSafeSkinId(player1Slot.equippedSkin) +
                " | P2Profile=" + SafeProfileId(player2Slot.profileId) +
                " | P2Skin=" + GetSafeSkinId(player2Slot.equippedSkin),
                this
            );
        }
    }

    public void ResetToDefaultProfiles()
    {
        EnsureRuntimeStructure();

        string resolvedLocalProfileId = ResolveLocalProfileId(true);
        if (!string.IsNullOrWhiteSpace(resolvedLocalProfileId))
            player1Slot.profileId = resolvedLocalProfileId;

        player2Slot.profileId = SanitizeProfileId(player2ProfileId, "remote_player");

        NotifyLoadoutChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinLoadout] ResetToDefaultProfiles -> " +
                "P1=" + SafeProfileId(player1Slot.profileId) +
                " | P2=" + SafeProfileId(player2Slot.profileId),
                this
            );
        }
    }

    private void HandleActiveProfileChanged(PlayerProfileRuntimeData profileData)
    {
        if (profileData == null)
            return;

        if (string.IsNullOrWhiteSpace(profileData.profileId))
            return;

        SetPlayer1ProfileId(profileData.profileId);
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif
    }

    private void SubscribeProfileEvents()
    {
        if (profileManager == null)
            return;

        if (subscribed && subscribedProfileManager == profileManager)
            return;

        UnsubscribeProfileEvents();

        profileManager.OnActiveProfileChanged -= HandleActiveProfileChanged;
        profileManager.OnActiveProfileChanged += HandleActiveProfileChanged;

        subscribedProfileManager = profileManager;
        subscribed = true;
    }

    private void UnsubscribeProfileEvents()
    {
        if (!subscribed || subscribedProfileManager == null)
        {
            subscribed = false;
            subscribedProfileManager = null;
            return;
        }

        subscribedProfileManager.OnActiveProfileChanged -= HandleActiveProfileChanged;
        subscribed = false;
        subscribedProfileManager = null;
    }

    private void EnsureRuntimeStructure()
    {
        if (player1Slot == null)
            player1Slot = new PlayerLoadoutSlotData();

        if (player2Slot == null)
            player2Slot = new PlayerLoadoutSlotData();

        if (string.IsNullOrWhiteSpace(player1Slot.profileId))
        {
            string resolvedLocalProfileId = ResolveLocalProfileId(false);
            if (!string.IsNullOrWhiteSpace(resolvedLocalProfileId))
                player1Slot.profileId = resolvedLocalProfileId;
        }

        if (string.IsNullOrWhiteSpace(player2Slot.profileId))
            player2Slot.profileId = SanitizeProfileId(player2ProfileId, "remote_player");
    }

    private void SanitizeRuntimeData()
    {
        if (player1Slot != null && !IsSkinDataUsable(player1Slot.equippedSkin))
            player1Slot.equippedSkin = null;

        if (player2Slot != null && !IsSkinDataUsable(player2Slot.equippedSkin))
            player2Slot.equippedSkin = null;
    }

    private bool IsSkinDataUsable(BallSkinData skin)
    {
        if (skin == null)
            return false;

        if (string.IsNullOrWhiteSpace(skin.baseColorId))
            return false;

        if (string.IsNullOrWhiteSpace(skin.patternId))
            return false;

        if (string.IsNullOrWhiteSpace(skin.patternColorId))
            return false;

        return true;
    }

    private void NotifyLoadoutChanged()
    {
        OnLoadoutChanged?.Invoke();
    }

    private string SanitizeProfileId(string profileId, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
            return profileId.Trim();

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback.Trim();

        return string.Empty;
    }

    private string ResolveLocalProfileId(bool allowConfiguredFallback)
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId.Trim();

        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId))
            return OnlineLocalPlayerContext.PlayerId.Trim();

        if (allowConfiguredFallback && !string.IsNullOrWhiteSpace(player1ProfileId))
            return player1ProfileId.Trim();

        return string.Empty;
    }

    private BallSkinData CloneSkin(BallSkinData source)
    {
        if (source == null)
            return null;

        return new BallSkinData
        {
            skinUniqueId = source.skinUniqueId,
            baseColorId = source.baseColorId,
            patternId = source.patternId,
            patternColorId = source.patternColorId,
            patternIntensity = source.patternIntensity,
            patternScale = source.patternScale,
            rarity = source.rarity
        };
    }

    private string GetSafeSkinId(BallSkinData skin)
    {
        return skin != null ? skin.skinUniqueId : "none";
    }

    private string SafeProfileId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<unresolved>" : value;
    }

    private void MarkRuntimeRootPersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
            return;

        GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
        if (runtimeRoot.transform.parent != null)
            runtimeRoot.transform.SetParent(null);

        DontDestroyOnLoad(runtimeRoot);
    }

    private void DestroyDuplicateRuntimeRoot()
    {
        GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
        Destroy(duplicateRoot);
    }
}