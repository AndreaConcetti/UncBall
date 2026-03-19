using UnityEngine;

/// <summary>
/// Da mettere su ogni slot trigger della board.
/// Quando una ball entra nello slot, inoltra tutto alla StarPlate padre.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlotScorer : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("StarPlate padre di questo slot")]
    public StarPlate parentPlate;

    [Tooltip("Indice univoco dello slot dentro la plate")]
    public int slotIndex;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (parentPlate == null)
            parentPlate = GetComponentInParent<StarPlate>();
    }

    void OnTriggerEnter(Collider other)
    {
        BallPhysics ball = other.GetComponent<BallPhysics>();
        if (ball == null)
            return;

        BallOwnership ownership = other.GetComponent<BallOwnership>();
        PlayerID owner = ownership != null ? ownership.Owner : PlayerID.None;

        if (owner == PlayerID.None)
            return;

        if (parentPlate != null)
            parentPlate.OnBallEntered(slotIndex, owner, ball, transform);
    }
}