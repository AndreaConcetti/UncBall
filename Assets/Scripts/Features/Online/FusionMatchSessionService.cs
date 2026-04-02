using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class FusionMatchSessionService : IMatchSessionService
{
    private readonly PhotonFusionRunnerManager runnerManager;

    public bool HasActiveSession =>
        runnerManager != null &&
        runnerManager.HasActiveRunner &&
        runnerManager.IsRunning;

    public FusionMatchSessionService(PhotonFusionRunnerManager runnerManager)
    {
        this.runnerManager = runnerManager;
    }

    public async Task<bool> JoinAssignedMatchAsync(
        MatchSessionContext context,
        CancellationToken cancellationToken)
    {
        if (runnerManager == null)
        {
            Debug.LogError("[FusionMatchSessionService] Runner manager is null.");
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (runnerManager.HasActiveRunner)
            await runnerManager.ShutdownRunnerAsync();

        cancellationToken.ThrowIfCancellationRequested();

        bool ok;

        if (context.localIsHost)
        {
            ok = await runnerManager.StartHostLobbyAsync(
                context.sessionName,
                2,
                null,
                runnerManager.PrivateLobbyName
            );
        }
        else
        {
            ok = await runnerManager.StartClientLobbyAsync(
                context.sessionName,
                2,
                runnerManager.PrivateLobbyName,
                null
            );
        }

        return ok;
    }

    public Task<bool> LoadGameplaySceneAsync(
        MatchSessionContext context,
        CancellationToken cancellationToken)
    {
        if (runnerManager == null || !runnerManager.IsRunning)
            return Task.FromResult(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!runnerManager.IsCurrentRunnerServer())
            return Task.FromResult(true);

        bool loaded = runnerManager.LoadNetworkScene(context.gameplaySceneName);
        return Task.FromResult(loaded);
    }

    public async Task ShutdownSessionAsync()
    {
        if (runnerManager == null)
            return;

        if (!runnerManager.HasActiveRunner)
            return;

        await runnerManager.ShutdownRunnerAsync();
    }
}