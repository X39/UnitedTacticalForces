using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Executes heartbeat frames through <see cref="IWorkerProtocolService.HeartbeatAsync"/>.
/// </summary>
/// <remarks>
/// The route holds no liveness, evidence or credential logic. It validates the payload exactly like the HTTP
/// endpoint does and delegates; the protocol service re-verifies the held credential on every call.
/// After an accepted heartbeat it asks <see cref="IWorkerProtocolService.HasDeliverableCommandAsync"/> and requests a
/// Wake after the ack while a command is deliverable. That is the safety net for a missed push, a commit made by
/// another API instance or a save outside the change interceptor: it recovers within one heartbeat interval without
/// the worker ever claiming on a timer. A busy worker answers the Wake with a Running probe, which is how it learns
/// that a cost-stop superseded its command. The Wake is only a hint; the protocol service decides what happens next.
/// </remarks>
/// <param name="protocolService">The protocol service that owns every heartbeat decision.</param>
[Scoped<HeartbeatFrameRoute, IWorkerChannelFrameRoute>]
internal sealed class HeartbeatFrameRoute(IWorkerProtocolService protocolService) : IWorkerChannelFrameRoute
{
    /// <inheritdoc />
    public IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; } = [EWorkerChannelFrameType.Heartbeat];

    /// <inheritdoc />
    public async Task<WorkerChannelRouteResult> HandleAsync(
        WorkerChannelRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ReadPayload(request);
        await protocolService.HeartbeatAsync(request.Worker.WorkerId, payload, request.Credential, cancellationToken)
            .ConfigureAwait(false);
        var deliverable = await protocolService
            .HasDeliverableCommandAsync(request.Worker.WorkerId, request.Worker.DeploymentId, cancellationToken)
            .ConfigureAwait(false);
        return deliverable
            ? WorkerChannelRouteResult.Empty with { WakeAfterAck = true }
            : WorkerChannelRouteResult.Empty;
    }

    /// <summary>
    /// Reads, validates and deployment-checks the frame payload. Every channel route follows this shape: deserialize
    /// with the channel options, run data annotations and <see cref="IValidatableObject"/> rules as MVC would for the
    /// HTTP body, then require the payload deployment to equal the authenticated worker's deployment. The payload is
    /// never logged.
    /// </summary>
    /// <param name="request">The request whose frame payload is read.</param>
    /// <returns>The validated heartbeat payload.</returns>
    /// <exception cref="WorkerProtocolValidationException">
    /// The payload is missing, not a JSON object, unreadable, invalid or names another deployment.
    /// </exception>
    private static WorkerHeartbeatRequest ReadPayload(WorkerChannelRequest request)
    {
        if (request.Frame.Payload is not { ValueKind: JsonValueKind.Object } element)
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        WorkerHeartbeatRequest? payload;
        try
        {
            payload = element.Deserialize<WorkerHeartbeatRequest>(WorkerChannelJson.Options);
        }
        catch (JsonException)
        {
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        }
        if (payload is null)
            throw new WorkerProtocolValidationException("The frame payload is invalid.");

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(payload, new ValidationContext(payload), results, validateAllProperties: true))
        {
            throw new WorkerProtocolValidationException(
                results.FirstOrDefault()?.ErrorMessage ?? "The frame payload is invalid.");
        }
        if (payload.DeploymentId != request.Worker.DeploymentId)
            throw new WorkerProtocolValidationException("The frame payload names another deployment.");
        return payload;
    }
}
