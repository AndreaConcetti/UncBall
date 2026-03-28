using System;
using UnityEngine;

public abstract class OnlineLobbyServiceBase : MonoBehaviour
{
    public event Action<OnlineLobbyState> OnLobbyStateChanged;
    public event Action<string> OnLobbyError;
    public event Action<string> OnLobbyInfo;

    public abstract bool HasActiveLobby { get; }
    public abstract OnlineLobbyState CurrentLobbyState { get; }

    public abstract void CreateLobby(
        string localPlayerId,
        string localDisplayName,
        StartEndController.MatchMode matchMode,
        int pointsToWin,
        float matchDuration,
        bool isRanked);

    public abstract void JoinLobby(
        string roomCode,
        string localPlayerId,
        string localDisplayName);

    public abstract void LeaveLobby(string localPlayerId);

    public abstract void SetLocalReady(string localPlayerId, bool isReady);

    public abstract void RequestStartMatch(string localPlayerId);

    protected void RaiseLobbyStateChanged(OnlineLobbyState state)
    {
        OnLobbyStateChanged?.Invoke(state);
    }

    protected void RaiseLobbyError(string message)
    {
        OnLobbyError?.Invoke(message);
    }

    protected void RaiseLobbyInfo(string message)
    {
        OnLobbyInfo?.Invoke(message);
    }
}