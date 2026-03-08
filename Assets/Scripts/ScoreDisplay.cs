using UnityEngine;
using TMPro;

public class ScoreDisplay : MonoBehaviour
{
    public TMP_Text player1Text;
    public TMP_Text player2Text;

    void Update()
    {
        player1Text.text = ScoreManager.Instance.player1Score.ToString();
        player2Text.text = ScoreManager.Instance.player2Score.ToString();
    }
}