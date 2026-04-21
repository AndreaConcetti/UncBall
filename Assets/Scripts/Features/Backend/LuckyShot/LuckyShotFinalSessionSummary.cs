using System;

[Serializable]
public struct LuckyShotBoardRewardResult
{
    public int boardNumber;
    public string targetSlotId;
    public bool wasHit;
    public bool rewardGranted;
    public int rewardWeight;
    public string rewardLabel;
}

[Serializable]
public struct LuckyShotFinalSessionSummary
{
    public string sessionId;
    public string sessionDateUtc;
    public LuckyShotLaunchSide launchSide;

    public int shotsTaken;
    public int baseShotsConsumed;
    public bool extraShotOffered;
    public bool extraShotUsed;

    public int hitBoardsCount;
    public int totalRewardWeight;
    public bool anyRewardGranted;

    public LuckyShotBoardRewardResult board1;
    public LuckyShotBoardRewardResult board2;
    public LuckyShotBoardRewardResult board3;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(sessionId) &&
               !string.IsNullOrWhiteSpace(sessionDateUtc);
    }
}
