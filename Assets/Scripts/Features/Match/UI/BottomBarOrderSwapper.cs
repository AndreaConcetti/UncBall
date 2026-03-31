using UnityEngine;

public class BottomBarOrderSwapper : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private RectTransform player1Root;
    [SerializeField] private RectTransform player2Root;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    [SerializeField] private bool player1OnLeft = true;

    public bool IsPlayer1OnLeft()
    {
        return player1OnLeft;
    }

    public void SetOrder(bool shouldPlayer1BeOnLeft)
    {
        player1OnLeft = shouldPlayer1BeOnLeft;

        if (player1Root == null || player2Root == null)
            return;

        if (player1OnLeft)
        {
            player1Root.SetSiblingIndex(0);
            player2Root.SetSiblingIndex(1);
        }
        else
        {
            player1Root.SetSiblingIndex(1);
            player2Root.SetSiblingIndex(0);
        }

        if (logDebug)
        {
            Debug.Log(
                "[BottomBarOrderSwapper] SetOrder -> Player1OnLeft=" + player1OnLeft,
                this
            );
        }
    }
}