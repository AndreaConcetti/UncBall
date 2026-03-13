using UnityEngine;

/// <summary>
/// Rappresenta un player logico della partita.
/// Non gestisce input diretto.
/// Serve solo a mantenere:
/// - lo stato attivo del player
/// - il riferimento alla ball corrente.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public bool isActive = false;

    [HideInInspector]
    public BallPhysics ball;

    /// <summary>
    /// Attiva o disattiva il player nel turno corrente.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
    }
}