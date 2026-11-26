namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Hints connected workers, without blocking the caller, that control-plane state they act on has changed.
/// </summary>
/// <remarks>
/// A signal is a hint and never a command push: it sends an empty Wake frame, and the worker answers by asking the
/// unchanged protocol service (claim or running probe). Delivery is best effort and process-local. A missed signal is
/// recovered by the Wake that follows each heartbeat acknowledgement while a command is deliverable, so nothing in the
/// system depends on a signal arriving.
/// </remarks>
internal interface IWorkerChannelSignal
{
    /// <summary>
    /// Queues a Wake frame for each of the specified workers.
    /// </summary>
    /// <remarks>Never blocks and never throws; a worker without a live connection is skipped.</remarks>
    /// <param name="workerIds">The workers to wake; an empty collection queues nothing.</param>
    void WakeWorkers(IReadOnlyCollection<Guid> workerIds);

    /// <summary>
    /// Queues a Wake frame for every connected worker of each of the specified deployments.
    /// </summary>
    /// <remarks>Never blocks and never throws; a deployment without a live connection is skipped.</remarks>
    /// <param name="deploymentIds">The deployments whose workers to wake; an empty collection queues nothing.</param>
    void WakeDeployments(IReadOnlyCollection<Guid> deploymentIds);
}
