using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BotHumanShotSeedLibrary", menuName = "Uncball/Bot/Human Shot Seed Library")]
public sealed class BotHumanShotSeedLibrary : ScriptableObject
{
    [SerializeField]
    private List<BotHumanShotSeed> seeds = new List<BotHumanShotSeed>();

    public IReadOnlyList<BotHumanShotSeed> Seeds => seeds;

    private void OnEnable()
    {
        if (seeds == null)
            seeds = new List<BotHumanShotSeed>();
    }

    [ContextMenu("Overwrite Seeds With Default Test Set")]
    public void OverwriteSeedsWithDefaultTestSet()
    {
        seeds = CreateDefaultTestSeedList();
    }

    public void EditorOverwriteSeedsWithDefaultTestSet()
    {
        seeds = CreateDefaultTestSeedList();
    }

    public bool TryGetBestSeed(int targetPlateIndex, int targetSlotIndex, Vector3 currentStartPosition, out BotHumanShotSeed bestSeed)
    {
        bestSeed = null;
        float bestDistance = float.MaxValue;

        if (seeds == null || seeds.Count == 0)
            return false;

        for (int i = 0; i < seeds.Count; i++)
        {
            BotHumanShotSeed seed = seeds[i];
            if (seed == null || !seed.enabled)
                continue;

            if (!seed.MatchesTarget(targetPlateIndex, targetSlotIndex))
                continue;

            float distance = Vector3.Distance(seed.referenceStartPosition, currentStartPosition);
            if (distance > seed.allowedStartPositionTolerance)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSeed = seed;
            }
        }

        return bestSeed != null;
    }

    // Nuovo: ignora completamente la posizione corrente.
    // In test mode serve per prendere comunque il seed del target,
    // anche se richiede uno start offset che verrà applicato DOPO.
    public bool TryGetAnySeedForTarget(int targetPlateIndex, int targetSlotIndex, out BotHumanShotSeed seed)
    {
        seed = null;

        if (seeds == null || seeds.Count == 0)
            return false;

        for (int i = 0; i < seeds.Count; i++)
        {
            BotHumanShotSeed current = seeds[i];
            if (current == null || !current.enabled)
                continue;

            if (!current.MatchesTarget(targetPlateIndex, targetSlotIndex))
                continue;

            seed = current;
            return true;
        }

        return false;
    }

    public bool TryGetSeedById(string seedId, out BotHumanShotSeed seed)
    {
        seed = null;

        if (string.IsNullOrWhiteSpace(seedId))
            return false;

        if (seeds == null || seeds.Count == 0)
            return false;

        for (int i = 0; i < seeds.Count; i++)
        {
            BotHumanShotSeed current = seeds[i];
            if (current == null || !current.enabled)
                continue;

            if (current.seedId == seedId)
            {
                seed = current;
                return true;
            }
        }

        return false;
    }

    public List<BotHumanShotSeed> GetSeedsForPlate(int targetPlateIndex)
    {
        List<BotHumanShotSeed> result = new List<BotHumanShotSeed>();

        if (seeds == null || seeds.Count == 0)
            return result;

        for (int i = 0; i < seeds.Count; i++)
        {
            BotHumanShotSeed seed = seeds[i];
            if (seed == null || !seed.enabled)
                continue;

            if (seed.targetPlateIndex == targetPlateIndex)
                result.Add(seed);
        }

        return result;
    }

    private static List<BotHumanShotSeed> CreateDefaultTestSeedList()
    {
        return new List<BotHumanShotSeed>()
        {
            // BOARD 1
            new BotHumanShotSeed()
            {
                seedId = "board1_slot0_standard",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 0,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(90.59f, 403.29f),
                launchDirection = new Vector3(0.22f, 0f, 0.98f),
                launchForce = 23.51901f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot1_standard",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 1,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(105.21f, 394.52f),
                launchDirection = new Vector3(0.26f, 0f, 0.97f),
                launchForce = 23.25179f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot2_offset_left",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 2,
                requiresBallOffset = true,
                referenceStartPosition = new Vector3(-2.13f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(137.35f, 385.75f),
                launchDirection = new Vector3(0.34f, 0f, 0.94f),
                launchForce = 22.98723f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot3_standard",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 3,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(131.51f, 391.60f),
                launchDirection = new Vector3(0.32f, 0f, 0.95f),
                launchForce = 23.16331f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot4_standard",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 4,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(146.12f, 400.37f),
                launchDirection = new Vector3(0.34f, 0f, 0.94f),
                launchForce = 23.42964f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot5_offset_left",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 5,
                requiresBallOffset = true,
                referenceStartPosition = new Vector3(-2.16f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(166.58f, 409.13f),
                launchDirection = new Vector3(0.38f, 0f, 0.93f),
                launchForce = 23.69861f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board1_slot6_standard",
                enabled = true,
                targetPlateIndex = 0,
                targetSlotIndex = 6,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(160.73f, 426.67f),
                launchDirection = new Vector3(0.35f, 0f, 0.94f),
                launchForce = 24.24434f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },

            // BOARD 2
            new BotHumanShotSeed()
            {
                seedId = "board2_slot0_standard",
                enabled = true,
                targetPlateIndex = 1,
                targetSlotIndex = 0,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(105.21f, 619.54f),
                launchDirection = new Vector3(0.17f, 0f, 0.99f),
                launchForce = 30.87637f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board2_slot1_standard",
                enabled = true,
                targetPlateIndex = 1,
                targetSlotIndex = 1,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(116.90f, 590.32f),
                launchDirection = new Vector3(0.19f, 0f, 0.98f),
                launchForce = 29.80212f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board2_slot2_standard",
                enabled = true,
                targetPlateIndex = 1,
                targetSlotIndex = 2,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(128.58f, 602.01f),
                launchDirection = new Vector3(0.21f, 0f, 0.98f),
                launchForce = 30.22902f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board2_slot3_standard",
                enabled = true,
                targetPlateIndex = 1,
                targetSlotIndex = 3,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(140.27f, 619.54f),
                launchDirection = new Vector3(0.22f, 0f, 0.98f),
                launchForce = 30.87637f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board2_slot4_offset_left",
                enabled = true,
                targetPlateIndex = 1,
                targetSlotIndex = 4,
                requiresBallOffset = true,
                referenceStartPosition = new Vector3(-2.30f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(175.34f, 628.31f),
                launchDirection = new Vector3(0.27f, 0f, 0.96f),
                launchForce = 31.20317f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },

            // BOARD 3
            new BotHumanShotSeed()
            {
                seedId = "board3_slot0_standard",
                enabled = true,
                targetPlateIndex = 2,
                targetSlotIndex = 0,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(108.13f, 794.89f),
                launchDirection = new Vector3(0.13f, 0f, 0.99f),
                launchForce = 37.78711f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board3_slot1_standard",
                enabled = true,
                targetPlateIndex = 2,
                targetSlotIndex = 1,
                requiresBallOffset = false,
                referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(119.82f, 780.27f),
                launchDirection = new Vector3(0.15f, 0f, 0.99f),
                launchForce = 37.18225f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            },
            new BotHumanShotSeed()
            {
                seedId = "board3_slot2_offset_left",
                enabled = true,
                targetPlateIndex = 2,
                targetSlotIndex = 2,
                requiresBallOffset = true,
                referenceStartPosition = new Vector3(-2.09f, 1.44f, -2.96f),
                allowedStartPositionTolerance = 0.10f,
                swipe = new Vector2(146.12f, 780.27f),
                launchDirection = new Vector3(0.18f, 0f, 0.98f),
                launchForce = 37.18225f,
                swipeXVariation = 0f,
                swipeYVariation = 0f,
                lateralSamples = 1,
                verticalSamples = 1
            }
        };
    }
}