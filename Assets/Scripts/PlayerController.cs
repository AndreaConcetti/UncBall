using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private bool isActive = false;

    public BallPhysics ball;

    public void SetActive(bool value)
    {
        isActive = value;
    }

    void Update()
    {
        if (!isActive)
            return;

        // Input di esempio
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log(name + " ha finito il turno");
            TurnManager.Instance.EndTurn();
        }
    }
}