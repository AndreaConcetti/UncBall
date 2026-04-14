using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BotHumanShotSeedLibrary", menuName = "Uncball/Bot/Human Shot Seed Library")]
public sealed class BotHumanShotSeedLibrary : ScriptableObject
{
    [SerializeField]
    private List<BotHumanShotSeed> seeds = new List<BotHumanShotSeed>()
    {
        // BOARD 1
        new BotHumanShotSeed()
        {
            seedId = "board1_slot0_standard",
            targetPlateIndex = 0,
            targetSlotIndex = 0,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(90.59f, 403.29f),
            launchDirection = new Vector3(0.22f, 0f, 0.98f),
            launchForce = 23.51901f,
            swipeXVariation = 26f,
            swipeYVariation = 40f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot1_standard",
            targetPlateIndex = 0,
            targetSlotIndex = 1,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(105.21f, 394.52f),
            launchDirection = new Vector3(0.26f, 0f, 0.97f),
            launchForce = 23.25179f,
            swipeXVariation = 24f,
            swipeYVariation = 38f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot2_offset_left",
            targetPlateIndex = 0,
            targetSlotIndex = 2,
            requiresBallOffset = true,
            referenceStartPosition = new Vector3(-2.13f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.40f,
            swipe = new Vector2(137.35f, 385.75f),
            launchDirection = new Vector3(0.34f, 0f, 0.94f),
            launchForce = 22.98723f,
            swipeXVariation = 24f,
            swipeYVariation = 36f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot3_standard",
            targetPlateIndex = 0,
            targetSlotIndex = 3,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(131.51f, 391.60f),
            launchDirection = new Vector3(0.32f, 0f, 0.95f),
            launchForce = 23.16331f,
            swipeXVariation = 24f,
            swipeYVariation = 38f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot4_standard",
            targetPlateIndex = 0,
            targetSlotIndex = 4,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(146.12f, 400.37f),
            launchDirection = new Vector3(0.34f, 0f, 0.94f),
            launchForce = 23.42964f,
            swipeXVariation = 24f,
            swipeYVariation = 38f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot5_offset_left",
            targetPlateIndex = 0,
            targetSlotIndex = 5,
            requiresBallOffset = true,
            referenceStartPosition = new Vector3(-2.16f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.42f,
            swipe = new Vector2(166.58f, 409.13f),
            launchDirection = new Vector3(0.38f, 0f, 0.93f),
            launchForce = 23.69861f,
            swipeXVariation = 26f,
            swipeYVariation = 40f,
            lateralSamples = 9,
            verticalSamples = 9
        },
        new BotHumanShotSeed()
        {
            seedId = "board1_slot6_standard",
            targetPlateIndex = 0,
            targetSlotIndex = 6,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(160.73f, 426.67f),
            launchDirection = new Vector3(0.35f, 0f, 0.94f),
            launchForce = 24.24434f,
            swipeXVariation = 26f,
            swipeYVariation = 42f,
            lateralSamples = 9,
            verticalSamples = 9
        },

        // BOARD 2
        new BotHumanShotSeed()
        {
            seedId = "board2_slot2_standard",
            targetPlateIndex = 1,
            targetSlotIndex = 2,
            requiresBallOffset = false,
            referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f),
            allowedStartPositionTolerance = 0.35f,
            swipe = new Vector2(128.58f, 602.01f),
            launchDirection = new Vector3(0.21f, 0f, 0.98f),
            launchForce = 30.22902f,
            swipeXVariation = 28f,
            swipeYVariation = 45f,
            lateralSamples = 9,
            verticalSamples = 9
        }
    };

    public IReadOnlyList<BotHumanShotSeed> Seeds => seeds;

    public bool TryGetBestSeed(int targetPlateIndex, int targetSlotIndex, Vector3 currentStartPosition, out BotHumanShotSeed bestSeed)
    {
        bestSeed = null;
        float bestDistance = float.MaxValue;

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

    public bool TryGetSeedById(string seedId, out BotHumanShotSeed seed)
    {
        seed = null;
        if (string.IsNullOrWhiteSpace(seedId))
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
}
