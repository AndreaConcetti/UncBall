using UnityEngine;

/// <summary>
/// Tiny component that tags a ball as belonging to a specific player.
/// Add this to your ball prefab and set the owner when the ball is served.
/// </summary>
public class BallOwnership : MonoBehaviour
{
    public PlayerID Owner = PlayerID.Player1;
}