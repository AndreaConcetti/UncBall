using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class FusionNetworkBall : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private BallPhysics ballPhysics;
    [SerializeField] private BallOwnership ballOwnership;
    [SerializeField] private BallSkinApplier ballSkinApplier;
    [SerializeField] private BallVisualRoll ballVisualRoll;

    [Header("Fallback")]
    [SerializeField] private BallSkinDatabase fallbackDatabase;

    [Header("Networked")]
    [Networked] private byte NetOwnerRaw { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetSkinUniqueId { get; set; }

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private string lastAppliedSkinUniqueId = string.Empty;
    private bool missingSkinLogged;
    private bool missingDatabaseLogged;
    private bool missingApplierLogged;
    private bool parseFailureLogged;

    public BallPhysics BallPhysics => ballPhysics;
    public PlayerID OwnerPlayerId => (PlayerID)NetOwnerRaw;
    public string SkinUniqueId => NetSkinUniqueId.ToString();

    public override void Spawned()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        ForcePlacementFrozenState();
        ResetVisualLayer();
        TryApplySkin("Spawned");
        RegisterIntoLocalGameplay();

        if (logDebug)
        {
            Debug.Log(
                "[FusionNetworkBall] Spawned -> " +
                "Owner=" + OwnerPlayerId +
                " | SkinId=" + SkinUniqueId +
                " | HasApplier=" + (ballSkinApplier != null),
                this
            );
        }
    }

    public override void Render()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        TryApplySkin("Render");
    }

    private void LateUpdate()
    {
        TryApplySkin("LateUpdate");
    }

    public void SetOwnerAndSkin(PlayerID owner, string skinUniqueId)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        NetOwnerRaw = (byte)owner;
        NetSkinUniqueId = string.IsNullOrWhiteSpace(skinUniqueId)
            ? default
            : skinUniqueId.Trim();

        ApplyOwnerToLocalComponents();

        if (logDebug)
        {
            Debug.Log(
                "[FusionNetworkBall] SetOwnerAndSkin -> " +
                "Owner=" + owner +
                " | SkinId=" + NetSkinUniqueId,
                this
            );
        }
    }

    private void ResolveReferences()
    {
        if (ballPhysics == null)
            ballPhysics = GetComponent<BallPhysics>();

        if (ballOwnership == null)
            ballOwnership = GetComponent<BallOwnership>();

        if (ballSkinApplier == null)
            ballSkinApplier = GetComponent<BallSkinApplier>();

        if (ballVisualRoll == null)
            ballVisualRoll = GetComponent<BallVisualRoll>();
    }

    private void ApplyOwnerToLocalComponents()
    {
        PlayerID owner = (PlayerID)NetOwnerRaw;

        if (ballOwnership != null)
            ballOwnership.Owner = owner;
    }

    private void ForcePlacementFrozenState()
    {
        if (ballPhysics != null)
            ballPhysics.Deactivate();
    }

    private void ResetVisualLayer()
    {
        if (ballVisualRoll != null)
            ballVisualRoll.ResetVisualRotation();
    }

    private void TryApplySkin(string caller)
    {
        ResolveReferences();

        string skinUniqueId = NetSkinUniqueId.ToString();

        if (string.IsNullOrWhiteSpace(skinUniqueId))
        {
            if (logDebug && !missingSkinLogged)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] TryApplySkin skipped because NetSkinUniqueId is empty. " +
                    "Caller=" + caller +
                    " | Owner=" + OwnerPlayerId,
                    this
                );
                missingSkinLogged = true;
            }

            return;
        }

        missingSkinLogged = false;

        if (string.Equals(lastAppliedSkinUniqueId, skinUniqueId, StringComparison.Ordinal))
            return;

        if (ballSkinApplier == null)
        {
            if (logDebug && !missingApplierLogged)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] TryApplySkin skipped because BallSkinApplier is missing. " +
                    "Caller=" + caller +
                    " | Owner=" + OwnerPlayerId +
                    " | SkinId=" + skinUniqueId,
                    this
                );
                missingApplierLogged = true;
            }

            return;
        }

        missingApplierLogged = false;

        BallSkinDatabase database = ResolveDatabase();
        if (database == null)
        {
            if (logDebug && !missingDatabaseLogged)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] TryApplySkin skipped because BallSkinDatabase is null. " +
                    "Caller=" + caller +
                    " | Owner=" + OwnerPlayerId +
                    " | SkinId=" + skinUniqueId,
                    this
                );
                missingDatabaseLogged = true;
            }

            return;
        }

        missingDatabaseLogged = false;

        BallSkinData parsedData = null;

        bool parsed =
            BallSkinIdParser.TryParse(skinUniqueId, database, out parsedData) ||
            BallSkinIdParser.TryParse(skinUniqueId, out parsedData);

        if (!parsed || parsedData == null)
        {
            if (logDebug && !parseFailureLogged)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] Cannot parse skinUniqueId. " +
                    "Caller=" + caller +
                    " | Owner=" + OwnerPlayerId +
                    " | SkinId=" + skinUniqueId,
                    this
                );
                parseFailureLogged = true;
            }

            return;
        }

        parseFailureLogged = false;

        bool applied = ballSkinApplier.ApplySkinData(database, parsedData);
        if (!applied)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] ApplySkinData returned false. " +
                    "Caller=" + caller +
                    " | Owner=" + OwnerPlayerId +
                    " | SkinId=" + skinUniqueId,
                    this
                );
            }

            return;
        }

        lastAppliedSkinUniqueId = skinUniqueId;

        if (logDebug)
        {
            Debug.Log(
                "[FusionNetworkBall] Skin applied -> " +
                "Caller=" + caller +
                " | Owner=" + OwnerPlayerId +
                " | SkinId=" + skinUniqueId,
                this
            );
        }
    }

    private BallSkinDatabase ResolveDatabase()
    {
        if (PlayerSkinLoadout.Instance != null && PlayerSkinLoadout.Instance.Database != null)
            return PlayerSkinLoadout.Instance.Database;

        if (fallbackDatabase != null)
            return fallbackDatabase;

        BallSkinDatabase[] loadedDatabases = Resources.FindObjectsOfTypeAll<BallSkinDatabase>();
        if (loadedDatabases != null && loadedDatabases.Length > 0)
        {
            fallbackDatabase = loadedDatabases[0];
            return fallbackDatabase;
        }

        return null;
    }

    private void RegisterIntoLocalGameplay()
    {
#if UNITY_2023_1_OR_NEWER
        BallTurnSpawner spawner = FindFirstObjectByType<BallTurnSpawner>();
#else
        BallTurnSpawner spawner = FindObjectOfType<BallTurnSpawner>();
#endif

        if (spawner != null)
            spawner.RegisterNetworkSpawnedBall(this);
    }
}