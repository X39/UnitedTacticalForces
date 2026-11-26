using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// The worker's single outbound channel to the control plane: heartbeats, claims, command reports and metrics
/// travel as correlated requests, and control-plane wake pushes are latched.
/// </summary>
public interface IWorkerControlChannel : IAsyncDisposable
{
    /// <summary>
    /// Gets a task that faults with <see cref="WorkerChannelAuthenticationException"/> when the control plane
    /// rejects the credential and the credential has not changed, and completes when the channel is disposed.
    /// </summary>
    /// <value>The channel lifetime task.</value>
    Task Completion { get; }

    /// <summary>
    /// Starts the self-healing connection loop.
    /// </summary>
    /// <param name="connection">The non-secret worker identity and control-plane location.</param>
    /// <param name="credentialProvider">
    /// Returns the current stored worker credential. It is called before every connect attempt so a rotated
    /// credential is picked up.
    /// </param>
    /// <param name="cancellationToken">A token that stops the channel when cancelled.</param>
    /// <returns>A task that completes once the loop has been started; it does not wait for a connection.</returns>
    Task StartAsync(
        CloudWorkerConnectionConfig connection,
        Func<CancellationToken, Task<string>> credentialProvider,
        CancellationToken cancellationToken);

    /// <summary>
    /// Waits until a connection is open.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the wait.</param>
    /// <returns>A task that completes once the channel is connected.</returns>
    Task WaitUntilConnectedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends a liveness heartbeat and waits for its ack.
    /// </summary>
    /// <param name="request">The heartbeat body.</param>
    /// <param name="cancellationToken">A token that aborts the request.</param>
    /// <returns>A task that completes when the control plane accepted the heartbeat.</returns>
    /// <exception cref="WorkerChannelUnavailableException">The channel was down, timed out or the ack refused it.</exception>
    Task HeartbeatAsync(WorkerHeartbeatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Asks the control plane for the next command. The channel never claims on its own.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the request.</param>
    /// <returns>The claimed command, or <see langword="null"/> when nothing is deliverable.</returns>
    /// <exception cref="WorkerChannelUnavailableException">The channel was down, timed out or the ack refused it.</exception>
    Task<WorkerCommandClaimResponse?> ClaimNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reports a command transition and waits for its ack.
    /// </summary>
    /// <param name="commandId">The command the report refers to.</param>
    /// <param name="request">The worker-internal report.</param>
    /// <param name="cancellationToken">A token that aborts the request.</param>
    /// <returns>
    /// <see langword="true"/> when the control plane accepted the transition; <see langword="false"/> when the
    /// command was superseded (the channel equivalent of an HTTP 409).
    /// </returns>
    /// <exception cref="WorkerChannelUnavailableException">The channel was down, timed out or the ack refused it.</exception>
    Task<bool> ReportCommandAsync(Guid commandId, WorkerCommandReportRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends one metrics sample exactly once. A sample is never retried or queued.
    /// </summary>
    /// <param name="sample">The sample to send.</param>
    /// <param name="cancellationToken">A token that aborts the request.</param>
    /// <returns>A task that completes when the control plane acknowledged the sample.</returns>
    /// <exception cref="WorkerChannelUnavailableException">
    /// The channel is not connected or dropped; the caller drops the sample.
    /// </exception>
    Task SendMetricsAsync(WorkerMetricsSample sample, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for a control-plane wake push. A push that arrived before the wait is remembered.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the wait.</param>
    /// <returns>A task that completes when a wake is pending; it consumes that wake.</returns>
    Task WaitForWakeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Closes the current socket normally and reconnects without backoff, so the next connect presents the
    /// credential the provider returns now. Used after a durable credential rotation.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the wait for the old socket to close.</param>
    /// <returns>A task that completes once the old socket is closed.</returns>
    Task ReconnectAsync(CancellationToken cancellationToken);
}
