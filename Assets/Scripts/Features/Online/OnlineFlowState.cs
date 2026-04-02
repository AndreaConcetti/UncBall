using System;

public enum OnlineFlowState
{
    Offline = 0,
    Idle = 1,
    Queueing = 2,
    MatchAssigned = 3,
    JoiningSession = 4,
    LoadingGameplay = 5,
    InMatch = 6,
    EndingMatch = 7,
    ReturningToMenu = 8,
    Error = 9
}