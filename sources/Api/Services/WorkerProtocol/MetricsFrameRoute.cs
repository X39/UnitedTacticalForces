using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Stores the metrics samples that a worker reports as <see cref="EWorkerChannelFrameType.Metrics"/> frames and
/// pushes the resulting readings to the viewers of the server.
/// </summary>
/// <remarks>
/// <para>
/// This route is the only metrics entry point of the control plane (D-03): no HTTP endpoint accepts a sample, and
/// provider provisioning, observation and teardown never read metrics. Every frame re-authenticates the held
/// credential through <see cref="IWorkerProtocolService.AuthenticateAsync"/>, so a revoked credential fails the frame
/// and the handler closes the channel. The sample is attributed only from the authenticated worker, never from the
/// payload; a payload that names another deployment is rejected by the metrics service.
/// </para>
/// <para>
/// A sample from a worker that is not the primary host is acknowledged and discarded so a multi-host deployment never
/// mixes machines into one reading (UI-SPEC M5). The worker never resends a sample, so a failed frame is simply
/// replaced by the next one about 15 seconds later.
/// </para>
/// </remarks>
/// <param name="protocolService">The protocol service that re-verifies the worker credential on every frame.</param>
/// <param name="metrics">The service that validates and stores the sample.</param>
/// <param name="publisher">The publisher that pushes the readings of a stored sample to the viewers.</param>
[Scoped<MetricsFrameRoute, IWorkerChannelFrameRoute>]
internal sealed class MetricsFrameRoute(
    IWorkerProtocolService protocolService,
    IServerMetricsService metrics,
    IServerMetricsPublisher publisher) : IWorkerChannelFrameRoute
{
    /// <inheritdoc />
    public IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; } = [EWorkerChannelFrameType.Metrics];

    /// <inheritdoc />
    public async Task<WorkerChannelRouteResult> HandleAsync(
        WorkerChannelRequest request,
        CancellationToken cancellationToken)
    {
        var sample = ReadPayload(request);
        var identity = await protocolService
            .AuthenticateAsync(request.Worker.WorkerId, request.Worker.DeploymentId, request.Credential, cancellationToken)
            .ConfigureAwait(false);
        var source = new ServerMetricSampleSource(
            identity.GameServerId,
            identity.DeploymentId,
            identity.WorkerId,
            identity.IsPrimaryHost);
        var result = await metrics
            .IngestAsync(source, sample, NodaSystemClock.Instance.GetCurrentInstant(), cancellationToken)
            .ConfigureAwait(false);
        if (result.Stored)
            await publisher.PublishAsync(result.GameServerId, cancellationToken).ConfigureAwait(false);
        return WorkerChannelRouteResult.Empty;
    }

    /// <summary>
    /// Reads the frame payload as a metrics sample. The sample itself is validated by the metrics service, which also
    /// checks its deployment against the authenticated worker.
    /// </summary>
    /// <param name="request">The request whose frame payload is read.</param>
    /// <returns>The deserialized sample.</returns>
    /// <exception cref="WorkerProtocolValidationException">
    /// The payload is missing, not a JSON object or unreadable. The payload is never logged.
    /// </exception>
    private static WorkerMetricsSample ReadPayload(WorkerChannelRequest request)
    {
        if (request.Frame.Payload is not { ValueKind: JsonValueKind.Object } element)
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        WorkerMetricsSample? sample;
        try
        {
            sample = element.Deserialize<WorkerMetricsSample>(WorkerChannelJson.Options);
        }
        catch (JsonException)
        {
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        }

        return sample ?? throw new WorkerProtocolValidationException("The frame payload is invalid.");
    }
}
