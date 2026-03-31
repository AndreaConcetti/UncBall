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

    [Header("Networked")]
    [Networked] private byte NetOwnerRaw { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetSkinUniqueId { get; set; }

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private string lastAppliedSkinUniqueId = string.Empty;
    private bool initialApplyAttempted;

    public BallPhysics BallPhysics => ballPhysics;
    public PlayerID OwnerPlayerId => (PlayerID)NetOwnerRaw;
    public string SkinUniqueId => NetSkinUniqueId.ToString();

    public override void Spawned()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        ForcePlacementFrozenState();
        TryApplySkin();
        RegisterIntoLocalGameplay();

        if (logDebug)
        {
            Debug.Log(
                "[FusionNetworkBall] Spawned -> " +
                "Owner=" + OwnerPlayerId +
                " | SkinId=" + SkinUniqueId,
                this
            );
        }
    }

    public override void Render()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        TryApplySkin();
    }

    private void LateUpdate()
    {
        TryApplySkin();
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

    private void TryApplySkin()
    {
        ResolveReferences();

        string skinUniqueId = NetSkinUniqueId.ToString();

        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return;

        if (string.Equals(lastAppliedSkinUniqueId, skinUniqueId, StringComparison.Ordinal))
            return;

        if (ballSkinApplier == null)
            return;

        if (PlayerSkinLoadout.Instance == null)
            return;

        BallSkinDatabase database = PlayerSkinLoadout.Instance.Database;
        if (database == null)
            return;

        BallSkinData parsedData = null;

        bool parsed =
            BallSkinIdParser.TryParse(skinUniqueId, database, out parsedData) ||
            BallSkinIdParser.TryParse(skinUniqueId, out parsedData);

        if (!parsed || parsedData == null)
        {
            if (logDebug && !initialApplyAttempted)
            {
                Debug.LogWarning(
                    "[FusionNetworkBall] Cannot parse skinUniqueId: " + skinUniqueId,
                    this
                );
            }

            initialApplyAttempted = true;
            return;
        }

        bool applied = ballSkinApplier.ApplySkinData(database, parsedData);
        if (!applied)
        {
            initialApplyAttempted = true;
            return;
        }

        lastAppliedSkinUniqueId = skinUniqueId;
        initialApplyAttempted = true;

        if (logDebug)
            Debug.Log("[FusionNetworkBall] Skin applied -> " + skinUniqueId, this);
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