using UnityEngine;

[System.Serializable]
public class BotHumanShotSeed
{
    public string seedId = "board1_left_standard";
    public bool enabled = true;

    [Header("Classification")]
    public int targetPlateIndex = 0;
    public int targetSlotIndex = 0;
    public bool requiresBallOffset = false;

    [Header("Reference Start Position")]
    public Vector3 referenceStartPosition = new Vector3(-1.95f, 1.44f, -2.96f);
    public float allowedStartPositionTolerance = 0.35f;

    [Header("Recorded Human Shot")]
    public Vector2 swipe = new Vector2(90.59f, 403.29f);
    public Vector3 launchDirection = new Vector3(0.22f, 0f, 0.98f);
    public float launchForce = 23.51901f;

    [Header("Search Window Around Seed")]
    public float swipeXVariation = 28f;
    public float swipeYVariation = 42f;
    public int lateralSamples = 9;
    public int verticalSamples = 9;

    public bool MatchesTarget(int plateIndex, int slotIndex)
    {
        return enabled && targetPlateIndex == plateIndex && targetSlotIndex == slotIndex;
    }

    public bool IsStartPositionCompatible(Vector3 startPos)
    {
        return Vector3.Distance(referenceStartPosition, startPos) <= allowedStartPositionTolerance;
    }

    public override string ToString()
    {
        return "BotHumanShotSeed | " +
               "Id=" + seedId +
               " | Plate=" + targetPlateIndex +
               " | Slot=" + targetSlotIndex +
               " | StartPos=" + referenceStartPosition +
               " | Swipe=" + swipe +
               " | Direction=" + launchDirection +
               " | Force=" + launchForce;
    }
}
