using System.Threading.Channels;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Serialized, coalescing publisher of <see cref="GameServerFleetChangedMessage"/> frames, addressed one recipient at
/// a time to the users who may view the changed game server.
/// </summary>
/// <remarks>
/// <para>
/// Callers only write server identifiers into an unbounded single-reader channel. One reader loop waits for the
/// first identifier, waits one <see cref="CoalesceWindow"/>, drains everything available and removes duplicates.
/// Serialization and coalescing are unchanged from the original broadcast design: every send still happens from
/// this one loop, one after another, so two lifecycle writers can never issue concurrent sends to the same socket,
/// which the update stream service would otherwise answer by silently dropping the subscriber.
/// </para>
/// <para>
/// Delivery is per recipient from a publish-time audience (D-17). For every drained batch the loop reads the
/// audience exactly once through <see cref="IFleetAudienceSource"/>, which applies the same per-server view rule as
/// the REST fleet endpoints to the claims stored at that moment. For each server of the batch and each user listed
/// for it, the loop sends one message carrying only the server id to the path
/// <see cref="FleetChangeStreamPaths.ForUser"/> returns for that user. A server without an audience entry reaches
/// nobody. A revoked claim therefore stops delivery with the next batch, without any cache to invalidate.
/// </para>
/// <para>
/// The notifier fails closed (D-15). When the audience cannot be resolved, the batch is dropped with one warning
/// and the loop continues with the next batch; there is no broadcast fallback and no shared publish target. The
/// recovery path for a dropped batch is the client's reconnect refetch and its Refresh action.
/// </para>
/// <para>
/// The signal is process-local (D-14). The audience and the sockets belong to one API instance, so a multi-instance
/// API needs a shared backplane before subscribers connected to one instance can observe lifecycle changes
/// committed by another instance.
/// </para>
/// </remarks>
internal sealed class FleetChangeNotifier : IFleetChangeNotifier, IHostedService
{
    /// <summary>
    /// The update stream service that delivers each frame to the sockets registered on one recipient's path.
    /// </summary>
    private readonly IUpdateStreamService _updateStreamService;

    /// <summary>
    /// The source of the users who may view each changed game server, read once per drained batch.
    /// </summary>
    private readonly IFleetAudienceSource _audienceSource;

    /// <summary>
    /// The logger receiving the structured warnings for dropped batches and failed recipient sends.
    /// </summary>
    private readonly ILogger<FleetChangeNotifier> _logger;

    /// <summary>
    /// Cancelled when a shutdown stops being graceful; skips the coalesce window and aborts a running audience read.
    /// </summary>
    /// <remarks>
    /// Never disposed on purpose: the loop may still observe the token while the host disposes singletons, and a
    /// source without a timer or wait handle holds no unmanaged resources.
    /// </remarks>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// The queue of changed server identifiers; written by any thread, read only by the reader loop.
    /// </summary>
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>
    /// The running reader loop, or <see langword="null"/> before <see cref="StartAsync"/> was called.
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// Creates a notifier that publishes through the existing update stream service to a publish-time audience.
    /// </summary>
    /// <param name="updateStreamService">The update stream service that delivers frames to registered sockets.</param>
    /// <param name="audienceSource">The source of the users who may view each changed game server.</param>
    /// <param name="logger">The logger receiving structured delivery warnings.</param>
    public FleetChangeNotifier(
        IUpdateStreamService updateStreamService,
        IFleetAudienceSource audienceSource,
        ILogger<FleetChangeNotifier> logger)
    {
        _updateStreamService = updateStreamService;
        _audienceSource      = audienceSource;
        _logger              = logger;
    }

    /// <summary>
    /// Gets the time the reader loop waits after the first queued identifier before draining and sending.
    /// </summary>
    /// <value>The coalesce window; defaults to 250 milliseconds. Tests may shorten it.</value>
    internal TimeSpan CoalesceWindow { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public void Notify(Guid gameServerId)
    {
        // TryWrite on an unbounded channel only fails after the writer was completed during shutdown.
        _channel.Writer.TryWrite(gameServerId);
    }

    /// <summary>
    /// Starts the single reader loop that serializes fleet-changed sends.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process was aborted.</param>
    /// <returns>A completed task; the reader loop keeps running in the background.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop ??= Task.Run(RunAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes the channel and waits for the reader loop to send what was already queued.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown should no longer be graceful.</param>
    /// <returns>A task completing once the reader loop ended or the shutdown token fired.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        if (_loop is null)
            return;

        try
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the single reader loop: waits for a first identifier, coalesces, drains one batch of distinct server
    /// identifiers and delivers it, until the channel is completed.
    /// </summary>
    /// <returns>A task completing once the channel was completed and drained, or the loop failed.</returns>
    private async Task RunAsync()
    {
        var reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
            {
                await WaitCoalesceWindowAsync().ConfigureAwait(false);
                var serverIds = new HashSet<Guid>();
                while (reader.TryRead(out var serverId))
                    serverIds.Add(serverId);

                await DeliverBatchAsync(serverIds).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The fleet change notifier loop stopped unexpectedly");
        }
    }

    /// <summary>
    /// Waits one <see cref="CoalesceWindow"/> so that further notifications can join the current batch.
    /// </summary>
    /// <returns>A task completing after the window elapsed or a forced shutdown skipped it.</returns>
    private async Task WaitCoalesceWindowAsync()
    {
        if (CoalesceWindow <= TimeSpan.Zero)
            return;

        try
        {
            await Task.Delay(CoalesceWindow, _stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A forced shutdown skips the window; whatever is queued is still drained once.
        }
    }

    /// <summary>
    /// Resolves the audience of one batch once and sends one frame per server and recipient, one after another.
    /// </summary>
    /// <remarks>
    /// A failed audience read drops the whole batch with one warning (fail closed); a forced shutdown that cancels
    /// the read drops the batch silently. Neither case sends anything.
    /// </remarks>
    /// <param name="serverIds">The distinct identifiers of the game servers changed in this batch.</param>
    /// <returns>A task completing once every recipient of the batch was attempted or the batch was dropped.</returns>
    private async Task DeliverBatchAsync(HashSet<Guid> serverIds)
    {
        if (serverIds.Count == 0)
            return;

        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> audience;
        try
        {
            audience = await _audienceSource
                .GetViewersAsync(serverIds, _stopping.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve the viewers of {GameServerCount} changed game servers; the fleet change signals "
                + "of this batch were not sent",
                serverIds.Count);
            return;
        }

        foreach (var serverId in serverIds)
        {
            if (!audience.TryGetValue(serverId, out var viewerIds))
                continue;

            foreach (var userId in viewerIds.Distinct())
                await SendAsync(serverId, userId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends one fleet-changed frame for one server to the path of one recipient, logging instead of throwing.
    /// </summary>
    /// <param name="serverId">The identifier of the changed game server, the only content of the frame.</param>
    /// <param name="userId">The identifier of the user whose fleet path receives the frame.</param>
    /// <returns>A task completing once the send finished or its failure was logged.</returns>
    private async Task SendAsync(Guid serverId, Guid userId)
    {
        try
        {
            await _updateStreamService.SendUpdateAsync(
                    FleetChangeStreamPaths.ForUser(userId),
                    new GameServerFleetChangedMessage { ServerId = serverId })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish the fleet change signal for game server {GameServerId} to user {UserId}",
                serverId,
                userId);
        }
    }
}
