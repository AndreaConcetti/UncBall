using System;

[Serializable]
public sealed class OnlineRuntimeContext
{
    public OnlineFlowState state;
    public QueueType queueType;
    public MatchAssignment currentAssignment;
    public MatchSessionContext currentSession;
    public string statusMessage;
    public string lastError;

    public OnlineRuntimeContext()
    {
        ResetToIdle();
    }

    public void ResetToIdle()
    {
        state = OnlineFlowState.Idle;
        queueType = QueueType.Normal;
        currentAssignment = null;
        currentSession = null;
        statusMessage = "Idle";
        lastError = string.Empty;
    }

    public void SetError(string message)
    {
        state = OnlineFlowState.Error;
        lastError = string.IsNullOrWhiteSpace(message) ? "Unknown online error." : message.Trim();
        statusMessage = lastError;
    }
}