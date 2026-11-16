namespace X39.UnitedTacticalForces.CloudWorker;

public sealed partial class Arma3WorkerCommandExecutor
{
    /// <summary>Applies current admission after all overlays and records the topology before process creation.</summary>
    /// <param name="payload">The immutable command whose endpoints the process will use.</param>
    /// <param name="cancellationToken">Cancels configuration and state publication before launch.</param>
    /// <returns>The asynchronous configuration operation.</returns>
    private async Task PrepareAdmissionAsync(Arma3CommandPayload payload, CancellationToken cancellationToken)
    {
        await _renderer.ReconcileAdmissionAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
        await Arma3LaunchTopologyContext.SaveAsync(payload, _paths, cancellationToken).ConfigureAwait(false);
        if (RequiresJoinEvidence(payload))
            await _joinMonitor.PrepareAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refuses to adopt a live process whose loaded endpoint configuration cannot be verified.</summary>
    /// <returns>A failure requiring a new authorized restart, without restarting or changing a live process.</returns>
    private static WorkerCommandExecutionResult TopologyRestartRequired()
        => WorkerCommandExecutionResult.Failure(
            "topology-restart-required",
            "topology-restart-required",
            "The running Arma process has different or unknown endpoint configuration; an explicit restart is required.");
}
