using UnityEngine;

public class BottomBarOrderSwapper : MonoBehaviour
{
    [Header("References")]
    public RectTransform player1Container;
    public RectTransform player2Container;

    [Header("Default Order")]
    public bool applyDefaultOrderOnStart = true;
    public bool player1OnLeftAtStart = true;

    [Header("Debug")]
    public bool debugLogs = false;

    void Start()
    {
        if (applyDefaultOrderOnStart)
            SetOrder(player1OnLeftAtStart);
    }

    public void SetOrder(bool player1OnLeft)
    {
        if (!ValidateReferences())
            return;

        Transform parent = player1Container.parent;

        if (parent == null || player2Container.parent != parent)
        {
            Debug.LogWarning("BottomBarOrderSwapper: i due container devono avere lo stesso parent.");
            return;
        }

        int childCount = parent.childCount;

        if (childCount < 2)
        {
            Debug.LogWarning("BottomBarOrderSwapper: il parent deve contenere almeno 2 figli.");
            return;
        }

        int leftIndex = 0;
        int rightIndex = childCount - 1;

        if (player1OnLeft)
        {
            player1Container.SetSiblingIndex(leftIndex);
            player2Container.SetSiblingIndex(rightIndex);

            if (debugLogs)
                Debug.Log("[BottomBarOrderSwapper] Ordine applicato: Player1 a sinistra, Player2 a destra.");
        }
        else
        {
            player2Container.SetSiblingIndex(leftIndex);
            player1Container.SetSiblingIndex(rightIndex);

            if (debugLogs)
                Debug.Log("[BottomBarOrderSwapper] Ordine applicato: Player2 a sinistra, Player1 a destra.");
        }
    }

    public void SwapOrder()
    {
        if (!ValidateReferences())
            return;

        Transform parent = player1Container.parent;

        if (parent == null || player2Container.parent != parent)
        {
            Debug.LogWarning("BottomBarOrderSwapper: i due container devono avere lo stesso parent.");
            return;
        }

        int player1Index = player1Container.GetSiblingIndex();
        int player2Index = player2Container.GetSiblingIndex();

        bool player1CurrentlyOnLeft = player1Index < player2Index;
        SetOrder(!player1CurrentlyOnLeft);
    }

    public bool IsPlayer1OnLeft()
    {
        if (!ValidateReferences())
            return true;

        if (player1Container.parent == null || player2Container.parent == null)
            return true;

        if (player1Container.parent != player2Container.parent)
            return true;

        return player1Container.GetSiblingIndex() < player2Container.GetSiblingIndex();
    }

    bool ValidateReferences()
    {
        if (player1Container == null || player2Container == null)
        {
            Debug.LogWarning("BottomBarOrderSwapper: riferimenti mancanti.");
            return false;
        }

        return true;
    }
}