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

    public bool HasAssignment => currentAssignment != null;
    public bool HasSession => currentSession != null;

    public bool IsOnlineHumanRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.OnlineHuman;
        }
    }

    public bool IsOfflineBotRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.OfflineBot;
        }
    }

    public bool IsRankedMaskedBotRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.RankedMaskedBot;
        }
    }

    public bool IsNormalMaskedBotRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.NormalMaskedBot;
        }
    }

    public bool IsAnyMaskedBotRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.RankedMaskedBot ||
                   runtimeType == MatchRuntimeType.NormalMaskedBot;
        }
    }

    public bool IsAnyBotRuntime
    {
        get
        {
            MatchRuntimeType runtimeType = ResolveRuntimeType();
            return runtimeType == MatchRuntimeType.OfflineBot ||
                   runtimeType == MatchRuntimeType.RankedMaskedBot ||
                   runtimeType == MatchRuntimeType.NormalMaskedBot;
        }
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
        if (IsAnyBotRuntime)
            return false;

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

    private MatchRuntimeType ResolveRuntimeType()
    {
        if (currentSession != null)
            return currentSession.runtimeType;

        if (currentAssignment != null)
            return currentAssignment.runtimeType;

        return MatchRuntimeType.OnlineHuman;
    }
}