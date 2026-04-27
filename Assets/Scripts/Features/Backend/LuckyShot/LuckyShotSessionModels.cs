using System;

public enum LuckyShotLaunchSide
{
    Left = 0,
    Right = 1
}

[Serializable]
public struct LuckyShotDailyLayout
{
    public string layoutDateUtc;
    public LuckyShotLaunchSide launchSide;
    public string board1WinningSlotId;
    public string board2WinningSlotId;
    public string board3WinningSlotId;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(layoutDateUtc) &&
               !string.IsNullOrWhiteSpace(board1WinningSlotId) &&
               !string.IsNullOrWhiteSpace(board2WinningSlotId) &&
               !string.IsNullOrWhiteSpace(board3WinningSlotId);
    }
}

[Serializable]
public struct LuckyShotActiveSession
{
    public bool hasActiveSession;
    public string sessionId;
    public string sessionDateUtc;
    public LuckyShotLaunchSide launchSide;

    public string board1WinningSlotId;
    public string board2WinningSlotId;
    public string board3WinningSlotId;

    public int remainingShots;
    public bool extraAdShotUsed;
    public bool shotAlreadyTaken;
    public bool rewardGranted;

    public int lastHitBoardNumber;
    public string lastHitSlotId;

    // Snapshot dei token disponibili subito dopo la creazione/consumo sessione.
    public int availableTokensSnapshotAfterConsume;

    // Versione del layout degli slot. Se non corrisponde alla versione corrente
    // definita in LuckyShotSessionRuntime.CurrentSlotLayoutVersion, la sessione
    // viene scartata e ricreata automaticamente.
    // Incrementa CurrentSlotLayoutVersion ogni volta che rinomini/riorganizzi gli slot.
    public int slotLayoutVersion;

    public bool HasAnyWinningSlot()
    {
        return !string.IsNullOrWhiteSpace(board1WinningSlotId) ||
               !string.IsNullOrWhiteSpace(board2WinningSlotId) ||
               !string.IsNullOrWhiteSpace(board3WinningSlotId);
    }

    public bool IsValid()
    {
        return hasActiveSession &&
               !string.IsNullOrWhiteSpace(sessionId) &&
               !string.IsNullOrWhiteSpace(sessionDateUtc) &&
               remainingShots >= 0 &&
               HasAnyWinningSlot();
    }
}

[Serializable]
public struct LuckyShotResolvedResult
{
    public bool success;
    public bool isWin;
    public bool rewardGranted;

    public int hitBoardNumber;
    public string hitSlotId;

    public int rewardWeight;
    public string rewardLabel;

    public int remainingShotsAfterResolve;
    public bool canRetry;
    public bool isFinalResolution;

    public LuckyShotActiveSession sessionAfterResolve;
}

[Serializable]
public struct LuckyShotSessionPreview
{
    public bool hasActiveSession;
    public bool canPlayNow;
    public bool canWatchAdForExtraShot;

    public int remainingShots;
    public LuckyShotLaunchSide launchSide;
    public string sessionDateUtc;

    public string board1WinningSlotId;
    public string board2WinningSlotId;
    public string board3WinningSlotId;

    public bool IsBoardWinningSlot(int boardNumber, string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        switch (boardNumber)
        {
            case 1: return string.Equals(board1WinningSlotId, slotId, StringComparison.Ordinal);
            case 2: return string.Equals(board2WinningSlotId, slotId, StringComparison.Ordinal);
            case 3: return string.Equals(board3WinningSlotId, slotId, StringComparison.Ordinal);
            default: return false;
        }
    }
}