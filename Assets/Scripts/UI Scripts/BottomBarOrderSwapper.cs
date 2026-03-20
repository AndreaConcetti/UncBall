using UnityEngine;

public class BottomBarOrderSwapper : MonoBehaviour
{
    [Header("References")]
    public RectTransform player1Container;
    public RectTransform player2Container;

    [Header("Default Order")]
    public bool applyDefaultOrderOnStart = true;
    public bool player1OnLeftAtStart = true;

    void Start()
    {
        if (applyDefaultOrderOnStart)
            SetOrder(player1OnLeftAtStart);
    }

    public void SetOrder(bool player1OnLeft)
    {
        if (player1Container == null || player2Container == null)
        {
            Debug.LogWarning("BottomBarOrderSwapper: riferimenti mancanti.");
            return;
        }

        if (player1OnLeft)
        {
            player1Container.SetSiblingIndex(0);
            player2Container.SetSiblingIndex(1);
        }
        else
        {
            player2Container.SetSiblingIndex(0);
            player1Container.SetSiblingIndex(1);
        }
    }

    public void SwapOrder()
    {
        if (player1Container == null || player2Container == null)
        {
            Debug.LogWarning("BottomBarOrderSwapper: riferimenti mancanti.");
            return;
        }

        int player1Index = player1Container.GetSiblingIndex();
        int player2Index = player2Container.GetSiblingIndex();

        player1Container.SetSiblingIndex(player2Index);
        player2Container.SetSiblingIndex(player1Index);
    }
}