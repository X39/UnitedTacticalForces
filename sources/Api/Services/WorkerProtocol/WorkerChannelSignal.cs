using System.Threading.Channels;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Queued implementation of <see cref="IWorkerChannelSignal"/> that sends Wake frames through the
/// <see cref="IWorkerChannelRegistry"/> from one background drainer.
/// </summary>
/// <remarks>
/// <para>
/// Callers only write into an unbounded single-reader channel, so a signal can never block or fail the save that
/// produced it. One drainer sends one request after another. A failed send is logged once per request with worker or
/// deployment ids only, and the drainer continues with later requests.
/// </para>
/// <para>
/// The signal is process-local: it reaches workers connected to this API instance only. The heartbeat Wake covers the
/// remaining cases.
/// </para>
/// </remarks>
/// <param name="registry">The registry that owns the live worker connections.</param>
/// <param name="logger">The logger receiving one warning per failed request.</param>
internal sealed class WorkerChannelSignal(IWorkerChannelRegistry registry, ILogger<WorkerChannelSignal> logger)
    : IWorkerChannelSignal, IHostedService
{
    /// <summary>
    /// The queue of wake requests; written by any thread, read only by the drainer.
    /// </summary>
    private readonly Channel<WakeRequest> _channel = Channel.CreateUnbounded<WakeRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>
    /// The running drainer, or <see langword="null"/> before <see cref="StartAsync"/> was called.
    /// </summary>
    private Task? _drainer;

    /// <inheritdoc />
    public void WakeWorkers(IReadOnlyCollection<Guid> workerIds)
    {
        if (workerIds.Count == 0)
            return;

        // TryWrite on an unbounded channel only fails after the writer was completed during shutdown.
        _channel.Writer.TryWrite(new WakeRequest(workerIds.ToArray(), IsDeployment: false));
    }

    /// <inheritdoc />
    public void WakeDeployments(IReadOnlyCollection<Guid> deploymentIds)
    {
        if (deploymentIds.Count == 0)
            return;

        _channel.Writer.TryWrite(new WakeRequest(deploymentIds.ToArray(), IsDeployment: true));
    }

    /// <summary>
    /// Starts the single drainer that sends queued wake requests.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process was aborted.</param>
    /// <returns>A completed task; the drainer keeps running in the background.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _drainer ??= Task.Run(DrainAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes the queue and waits for the drainer to send what was already queued.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown should no longer be graceful.</param>
    /// <returns>A task completing once the drainer ended or the shutdown token fired.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        if (_drainer is null)
            return;

        try
        {
            await _drainer.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A forced shutdown stops waiting; undelivered hints are recovered by the next heartbeat.
        }
    }

    /// <summary>
    /// Sends queued wake requests one after another until the queue is completed and empty.
    /// </summary>
    /// <returns>A task completing once the queue was completed and drained, or the drainer failed.</returns>
    private async Task DrainAsync()
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                await SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The worker channel signal drainer stopped unexpectedly");
        }
    }

    /// <summary>
    /// Sends the Wake frame of one request to each of its targets, logging instead of throwing.
    /// </summary>
    /// <param name="request">The request to deliver.</param>
    /// <returns>A task completing once every target was attempted or the failure was logged.</returns>
    private async Task SendAsync(WakeRequest request)
    {
        var frame = new WorkerChannelFrame { Type = EWorkerChannelFrameType.Wake };
        foreach (var id in request.Ids)
        {
            try
            {
                if (request.IsDeployment)
                    await registry.SendToDeploymentAsync(id, frame, CancellationToken.None).ConfigureAwait(false);
                else
                    await registry.TrySendAsync(id, frame, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to push a wake frame to {TargetKind} {TargetId} ({ExceptionType})",
                    request.IsDeployment ? "deployment" : "worker",
                    id,
                    ex.GetType().Name);
            }
        }
    }

    /// <summary>
    /// One queued request to wake a set of workers or deployments.
    /// </summary>
    /// <param name="Ids">The worker ids or deployment ids to wake.</param>
    /// <param name="IsDeployment">True when <paramref name="Ids"/> are deployment ids, false for worker ids.</param>
    private readonly record struct WakeRequest(Guid[] Ids, bool IsDeployment);
}
