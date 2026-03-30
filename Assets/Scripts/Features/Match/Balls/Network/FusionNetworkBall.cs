using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class FusionNetworkBall : NetworkBehaviour
{
    [Header("Cached References")]
    [SerializeField] private BallPhysics ballPhysics;
    [SerializeField] private BallOwnership ballOwnership;
    [SerializeField] private BallSkinApplier ballSkinApplier;

    [Header("Networked")]
    [Networked] private byte NetOwnerRaw { get; set; }
    [Networked, Capacity(64)] private NetworkString<_64> NetSkinUniqueId { get; set; }

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private string lastAppliedSkinUniqueId = string.Empty;

    public BallPhysics BallPhysics => ballPhysics;
    public PlayerID OwnerPlayerId => (PlayerID)NetOwnerRaw;
    public string SkinUniqueId => NetSkinUniqueId.ToString();

    public override void Spawned()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        ForcePlacementFrozenState();
        ApplySkinIfNeeded();
        RegisterIntoLocalGameplay();
    }

    public override void Render()
    {
        ResolveReferences();
        ApplyOwnerToLocalComponents();
        ApplySkinIfNeeded();
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
        ApplySkinIfNeeded();

        if (logDebug)
        {
            Debug.Log(
                "[FusionNetworkBall] SetOwnerAndSkin -> Owner=" + owner +
                " | SkinUniqueId=" + NetSkinUniqueId,
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

    private void ApplySkinIfNeeded()
    {
        string skinUniqueId = NetSkinUniqueId.ToString();

        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return;

        if (string.Equals(lastAppliedSkinUniqueId, skinUniqueId, StringComparison.Ordinal))
            return;

        ResolveReferences();

        if (ballSkinApplier == null)
            return;

        if (PlayerSkinLoadout.Instance == null || PlayerSkinLoadout.Instance.Database == null)
            return;

        BallSkinData resolvedSkin = ResolveSkinDataByUniqueId(PlayerSkinLoadout.Instance.Database, skinUniqueId);
        if (resolvedSkin == null)
            return;

        bool applied = ballSkinApplier.ApplySkinData(PlayerSkinLoadout.Instance.Database, resolvedSkin);
        if (!applied)
            return;

        lastAppliedSkinUniqueId = skinUniqueId;
    }

    private BallSkinData ResolveSkinDataByUniqueId(BallSkinDatabase database, string skinUniqueId)
    {
        if (database == null || string.IsNullOrWhiteSpace(skinUniqueId))
            return null;

        Type dbType = database.GetType();

        MethodInfo[] methods = dbType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1)
                continue;

            if (parameters[0].ParameterType != typeof(string))
                continue;

            if (!typeof(BallSkinData).IsAssignableFrom(method.ReturnType))
                continue;

            try
            {
                object result = method.Invoke(database, new object[] { skinUniqueId });
                if (result is BallSkinData directSkin && directSkin != null)
                {
                    if (string.Equals(directSkin.skinUniqueId, skinUniqueId, StringComparison.Ordinal))
                        return directSkin;
                }
            }
            catch
            {
            }
        }

        FieldInfo[] fields = dbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            object value = fields[i].GetValue(database);
            if (value == null)
                continue;

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is BallSkinData candidate &&
                        candidate != null &&
                        string.Equals(candidate.skinUniqueId, skinUniqueId, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private void RegisterIntoLocalGameplay()
    {
        BallTurnSpawner spawner = null;

#if UNITY_2023_1_OR_NEWER
        spawner = FindFirstObjectByType<BallTurnSpawner>();
#else
        spawner = FindObjectOfType<BallTurnSpawner>();
#endif

        if (spawner != null)
            spawner.RegisterNetworkSpawnedBall(this);
    }
}