using System;
using UnityEngine;

[Serializable]
public sealed class OnlineRuntimeContext
{
    public OnlineFlowState state;
    public QueueType queueType;
    public MatchAssignment currentAssignment;
    public MatchSessionContext currentSession;
    public string statusMessage;
    public string lastError;

    [Header("Prematch Integrity")]
    public bool hasGameplayValidation;
    public bool prematchHostForfeitWinResolved;
    public bool prematchHostForfeitRewardsApplied;
    public string prematchResolutionMessage;

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
        hasGameplayValidation = false;
        prematchHostForfeitWinResolved = false;
        prematchHostForfeitRewardsApplied = false;
        prematchResolutionMessage = string.Empty;
    }

    public void SetError(string message)
    {
        state = OnlineFlowState.Error;
        lastError = string.IsNullOrWhiteSpace(message) ? "Unknown online error." : message.Trim();
        statusMessage = lastError;
    }

    public void ResetMatchLifecycleFlags()
    {
        hasGameplayValidation = false;
        prematchHostForfeitWinResolved = false;
        prematchHostForfeitRewardsApplied = false;
        prematchResolutionMessage = string.Empty;
    }

    public void MarkGameplayValidated()
    {
        hasGameplayValidation = true;
    }

    public bool CanResolvePrematchHostForfeitWin()
    {
        return !hasGameplayValidation && !prematchHostForfeitWinResolved;
    }

    public bool TryResolvePrematchHostForfeitWin(string message)
    {
        if (!CanResolvePrematchHostForfeitWin())
            return false;

        prematchHostForfeitWinResolved = true;
        prematchResolutionMessage = string.IsNullOrWhiteSpace(message)
            ? "Host disconnected before gameplay start."
            : message.Trim();

        return true;
    }

    public void MarkPrematchHostForfeitRewardsApplied()
    {
        prematchHostForfeitRewardsApplied = true;
    }
}
