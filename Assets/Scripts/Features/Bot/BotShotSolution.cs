using UnityEngine;

[System.Serializable]
public struct BotShotSolution
{
    public bool hasSolution;
    public int evaluatedCandidates;
    public BotShotCandidate bestCandidate;

    public override string ToString()
    {
        return
            "BotShotSolution | " +
            "HasSolution=" + hasSolution +
            " | Evaluated=" + evaluatedCandidates +
            " | Candidate={" + bestCandidate + "}";
    }
}
