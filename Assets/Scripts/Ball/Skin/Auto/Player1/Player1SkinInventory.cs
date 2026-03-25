using System.Collections.Generic;
using UnityEngine;

public class Player1SkinInventory : MonoBehaviour
{
    [System.Serializable]
    public class StartupUnlockedSkinDefinition
    {
        [Header("IDs")]
        public string baseColorId;
        public string patternId;
        public string patternColorId;

        [Header("Material Params")]
        public float patternIntensity = 1f;
        public float patternScale = 1f;

        [Header("Meta")]
        public SkinRarity rarity = SkinRarity.Common;
        public bool equipOnInitialization = false;
    }

    public static Player1SkinInventory Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private BallSkinDatabase database;
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;

    [Header("Startup Unlocked Skins")]
    [SerializeField] private bool createStartupUnlockedSkinsIfSaveEmpty = true;
    [SerializeField] private List<StartupUnlockedSkinDefinition> startupUnlockedSkins = new List<StartupUnlockedSkinDefinition>();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private const string SaveKey = "PLAYER1_SKIN_INVENTORY";

    [SerializeField] private Player1SkinInventorySaveData runtimeData = new Player1SkinInventorySaveData();

    public BallSkinDatabase Database => database;
    public IReadOnlyList<BallSkinData> UnlockedSkins => runtimeData.unlockedSkins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        LoadInventory();

        if (createStartupUnlockedSkinsIfSaveEmpty && IsInventoryEmpty())
            CreateStartupUnlockedSkins();

        EnsureEquippedSkinAppliedToLoadout();

        if (logDebug)
        {
            Debug.Log("[Player1SkinInventory] Unlocked skins count: " + runtimeData.unlockedSkins.Count, this);
            Debug.Log("[Player1SkinInventory] Equipped skin id: " + runtimeData.equippedSkinId, this);
        }
    }

    public bool AddUnlockedSkin(BallSkinData skin)
    {
        if (skin == null)
        {
            Debug.LogWarning("[Player1SkinInventory] Tried to add null skin.", this);
            return false;
        }

        if (ContainsSkin(skin.skinUniqueId))
        {
            if (logDebug)
                Debug.Log("[Player1SkinInventory] Skin already unlocked: " + skin.skinUniqueId, this);

            return false;
        }

        runtimeData.unlockedSkins.Add(CloneSkin(skin));
        SaveInventory();

        if (logDebug)
            Debug.Log("[Player1SkinInventory] Added unlocked skin: " + skin.skinUniqueId, this);

        return true;
    }

    public bool ContainsSkin(string skinUniqueId)
    {
        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return false;

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin != null && skin.skinUniqueId == skinUniqueId)
                return true;
        }

        return false;
    }

    public BallSkinData GetUnlockedSkinById(string skinUniqueId)
    {
        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return null;

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin != null && skin.skinUniqueId == skinUniqueId)
                return skin;
        }

        return null;
    }

    public bool EquipSkin(string skinUniqueId)
    {
        BallSkinData skin = GetUnlockedSkinById(skinUniqueId);
        if (skin == null)
        {
            Debug.LogWarning("[Player1SkinInventory] Cannot equip missing skin: " + skinUniqueId, this);
            return false;
        }

        runtimeData.equippedSkinId = skinUniqueId;

        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        if (playerSkinLoadout != null)
            playerSkinLoadout.EquipSkinForPlayer1(skin);

        SaveInventory();

        if (logDebug)
            Debug.Log("[Player1SkinInventory] Equipped skin: " + skinUniqueId, this);

        return true;
    }

    public BallSkinData GetEquippedSkin()
    {
        if (string.IsNullOrWhiteSpace(runtimeData.equippedSkinId))
            return null;

        return GetUnlockedSkinById(runtimeData.equippedSkinId);
    }

    public void SaveInventory()
    {
        string json = JsonUtility.ToJson(runtimeData);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void LoadInventory()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            runtimeData = new Player1SkinInventorySaveData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            runtimeData = new Player1SkinInventorySaveData();
            return;
        }

        runtimeData = JsonUtility.FromJson<Player1SkinInventorySaveData>(json);

        if (runtimeData == null)
            runtimeData = new Player1SkinInventorySaveData();

        if (runtimeData.unlockedSkins == null)
            runtimeData.unlockedSkins = new List<BallSkinData>();
    }

    public void ClearInventoryForDebug()
    {
        runtimeData = new Player1SkinInventorySaveData();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        if (logDebug)
            Debug.Log("[Player1SkinInventory] Inventory cleared for debug.", this);
    }

    public void RebuildStartupInventoryForDebug()
    {
        ClearInventoryForDebug();
        LoadInventory();

        if (createStartupUnlockedSkinsIfSaveEmpty && IsInventoryEmpty())
            CreateStartupUnlockedSkins();

        EnsureEquippedSkinAppliedToLoadout();

        if (logDebug)
        {
            Debug.Log("[Player1SkinInventory] Rebuilt startup inventory for debug.", this);
            Debug.Log("[Player1SkinInventory] Unlocked skins count after rebuild: " + runtimeData.unlockedSkins.Count, this);
        }
    }

    private bool IsInventoryEmpty()
    {
        return runtimeData == null || runtimeData.unlockedSkins == null || runtimeData.unlockedSkins.Count == 0;
    }

    private void CreateStartupUnlockedSkins()
    {
        if (database == null)
        {
            Debug.LogError("[Player1SkinInventory] Database missing, cannot create startup unlocked skins.", this);
            return;
        }

        if (startupUnlockedSkins == null || startupUnlockedSkins.Count == 0)
        {
            Debug.LogWarning("[Player1SkinInventory] No startup unlocked skins configured.", this);
            return;
        }

        string skinToEquip = null;

        for (int i = 0; i < startupUnlockedSkins.Count; i++)
        {
            StartupUnlockedSkinDefinition definition = startupUnlockedSkins[i];
            if (definition == null)
                continue;

            if (!TryBuildSkinFromDefinition(definition, out BallSkinData builtSkin))
                continue;

            bool added = AddUnlockedSkin(builtSkin);

            if (logDebug)
            {
                Debug.Log(
                    "[Player1SkinInventory] Startup skin built -> " +
                    builtSkin.skinUniqueId +
                    " | Base: " + builtSkin.baseColorId +
                    " | Pattern: " + builtSkin.patternId +
                    " | PatternColor: " + builtSkin.patternColorId +
                    " | Rarity: " + builtSkin.rarity +
                    " | Added: " + added,
                    this
                );
            }

            if ((definition.equipOnInitialization && added) || (definition.equipOnInitialization && ContainsSkin(builtSkin.skinUniqueId)))
                skinToEquip = builtSkin.skinUniqueId;
        }

        if (!string.IsNullOrWhiteSpace(skinToEquip))
            EquipSkin(skinToEquip);
        else if (runtimeData.unlockedSkins.Count > 0 && string.IsNullOrWhiteSpace(runtimeData.equippedSkinId))
            EquipSkin(runtimeData.unlockedSkins[0].skinUniqueId);

        if (logDebug)
            Debug.Log("[Player1SkinInventory] Final startup unlocked skins count: " + runtimeData.unlockedSkins.Count, this);
    }

    private bool TryBuildSkinFromDefinition(StartupUnlockedSkinDefinition definition, out BallSkinData builtSkin)
    {
        builtSkin = null;

        if (definition == null)
            return false;

        if (string.IsNullOrWhiteSpace(definition.baseColorId))
        {
            Debug.LogError("[Player1SkinInventory] Startup skin missing baseColorId.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.patternId))
        {
            Debug.LogError("[Player1SkinInventory] Startup skin missing patternId.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.patternColorId))
        {
            Debug.LogError("[Player1SkinInventory] Startup skin missing patternColorId.", this);
            return false;
        }

        if (database == null ||
            database.baseColorLibrary == null ||
            database.patternLibrary == null ||
            database.patternColorLibrary == null)
        {
            Debug.LogError("[Player1SkinInventory] Database or libraries missing.", this);
            return false;
        }

        if (database.baseColorLibrary.GetById(definition.baseColorId) == null)
        {
            Debug.LogError("[Player1SkinInventory] BaseColor ID not found in library: " + definition.baseColorId, this);
            return false;
        }

        if (database.patternLibrary.GetById(definition.patternId) == null)
        {
            Debug.LogError("[Player1SkinInventory] Pattern ID not found in library: " + definition.patternId, this);
            return false;
        }

        if (database.patternColorLibrary.GetById(definition.patternColorId) == null)
        {
            Debug.LogError("[Player1SkinInventory] PatternColor ID not found in library: " + definition.patternColorId, this);
            return false;
        }

        builtSkin = BallSkinFactory.CreateSkin(
            definition.baseColorId,
            definition.patternId,
            definition.patternColorId,
            definition.patternIntensity,
            definition.patternScale,
            definition.rarity
        );

        return builtSkin != null;
    }

    private void EnsureEquippedSkinAppliedToLoadout()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        BallSkinData equipped = GetEquippedSkin();

        if (equipped != null && playerSkinLoadout != null)
            playerSkinLoadout.EquipSkinForPlayer1(equipped);
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
}