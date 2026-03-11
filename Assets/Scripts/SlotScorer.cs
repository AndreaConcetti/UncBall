using UnityEngine;

/// <summary>
/// Attached to each individual slot collider inside a Star Plate prefab.
/// Detects when a ball enters and reports it up to the parent StarPlate.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlotScorer : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("The Star Plate this slot belongs to — assign the parent StarPlate component")]
    public StarPlate parentPlate;

    [Tooltip("Unique index of this slot within the plate (0-based). Used to track which slots are filled.")]
    public int slotIndex;

    private void Awake()
    {
        // Ensure the collider is a trigger
        GetComponent<Collider>().isTrigger = true;

        // Auto-find parent StarPlate if not assigned
        if (parentPlate == null)
            parentPlate = GetComponentInParent<StarPlate>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if what entered is a ball and belongs to a player
        BallPhysics ball = other.GetComponent<BallPhysics>();
        if (ball == null) return;

        PlayerID owner = other.GetComponent<BallOwnership>()?.Owner ?? PlayerID.None;
        if (owner == PlayerID.None) return;

        parentPlate?.OnBallEntered(slotIndex, owner, ball);
    }
}