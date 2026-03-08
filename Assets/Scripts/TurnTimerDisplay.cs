using UnityEngine;
using TMPro;

public class TurnTimerDisplay : MonoBehaviour
{
    public TMP_Text timerText;

    void Update()
    {
        float timer = TurnManager.Instance.CurrentTimer;
        timerText.text = Mathf.CeilToInt(timer).ToString();
    }
}