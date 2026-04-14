using UnityEngine;

[System.Serializable]
public struct BotShotCandidate
{
    public int targetPlateIndex;
    public string targetPlateName;
    public int targetSlotIndex;
    public string targetSlotName;

    public Vector2 swipeDelta;
    public Vector3 launchDirection;
    public float launchForce;

    public Vector3 targetSlotCenter;
    public Vector3 bestSamplePosition;
    public float bestDistanceToTarget;

    public bool enteredTargetTrigger;
    public bool descendingAtEntry;
    public bool hitBlockingBoardBeforeEntry;

    public float score;

    public override string ToString()
    {
        return
            "BotShotCandidate | " +
            "PlateIndex=" + targetPlateIndex +
            " | PlateName=" + targetPlateName +
            " | SlotIndex=" + targetSlotIndex +
            " | SlotName=" + targetSlotName +
            " | Swipe=" + swipeDelta +
            " | Direction=" + launchDirection +
            " | Force=" + launchForce +
            " | TargetSlotCenter=" + targetSlotCenter +
            " | BestSample=" + bestSamplePosition +
            " | Distance=" + bestDistanceToTarget +
            " | EnteredTargetTrigger=" + enteredTargetTrigger +
            " | DescendingAtEntry=" + descendingAtEntry +
            " | HitBlockingBoardBeforeEntry=" + hitBlockingBoardBeforeEntry +
            " | Score=" + score;
    }
}
