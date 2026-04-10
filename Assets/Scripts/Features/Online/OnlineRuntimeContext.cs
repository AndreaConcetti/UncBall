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

    [Header("Pre-Gameplay Integrity")]
    public bool hasGameplayValidation;
    public bool hasPrematchHostForfeitWin;
    public bool hasAppliedPrematchHostForfeitWin;
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
        ResetPrematchIntegrity();
    }

    public void SetError(string message)
    {
        state = OnlineFlowState.Error;
        lastError = string.IsNullOrWhiteSpace(message) ? "Unknown online error." : message.Trim();
        statusMessage = lastError;
    }

    public void ResetPrematchIntegrity()
    {
        hasGameplayValidation = false;
        hasPrematchHostForfeitWin = false;
        hasAppliedPrematchHostForfeitWin = false;
        prematchResolutionMessage = string.Empty;
    }

    public bool IsWaitingForGameplayValidation()
    {
        return !hasGameplayValidation &&
               (state == OnlineFlowState.MatchAssigned ||
                state == OnlineFlowState.JoiningSession ||
                state == OnlineFlowState.LoadingGameplay ||
                state == OnlineFlowState.InMatch);
    }

    public void MarkGameplayValidated()
    {
        hasGameplayValidation = true;
    }

    public bool TryMarkPrematchHostForfeitWin(string message)
    {
        if (hasGameplayValidation)
            return false;

        if (hasPrematchHostForfeitWin)
            return false;

        hasPrematchHostForfeitWin = true;
        prematchResolutionMessage = string.IsNullOrWhiteSpace(message)
            ? "Host left before gameplay start."
            : message.Trim();

        return true;
    }
}
