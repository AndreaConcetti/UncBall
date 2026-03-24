using UnityEngine;

[CreateAssetMenu(fileName = "BallSkinDatabase", menuName = "Uncball/Skins/Ball Skin Database")]
public class BallSkinDatabase : ScriptableObject
{
    public BallColorLibrary baseColorLibrary;
    public BallPatternLibrary patternLibrary;
    public BallColorLibrary patternColorLibrary;
}