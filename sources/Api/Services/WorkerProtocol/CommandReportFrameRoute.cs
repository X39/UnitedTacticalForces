using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Executes the received, running, succeeded and failed command report frames through the matching
/// <see cref="IWorkerProtocolService"/> report methods.
/// </summary>
/// <remarks>
/// The route holds no transition, idempotency, lease or supersession logic. The protocol service applies the
/// command state machine and throws a conflict when the command was superseded; that exception propagates to the
/// handler unchanged.
/// </remarks>
/// <param name="protocolService">The protocol service that owns every report decision.</param>
[Scoped<CommandReportFrameRoute, IWorkerChannelFrameRoute>]
internal sealed class CommandReportFrameRoute(IWorkerProtocolService protocolService) : IWorkerChannelFrameRoute
{
    /// <inheritdoc />
    public IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; } =
    [
        EWorkerChannelFrameType.ReportReceived,
        EWorkerChannelFrameType.ReportRunning,
        EWorkerChannelFrameType.ReportSucceeded,
        EWorkerChannelFrameType.ReportFailed,
    ];

    /// <inheritdoc />
    public async Task<WorkerChannelRouteResult> HandleAsync(
        WorkerChannelRequest request,
        CancellationToken cancellationToken)
    {
        var commandId = request.Frame.CommandId
                        ?? throw new WorkerProtocolValidationException("The frame command id is missing.");
        var workerId = request.Worker.WorkerId;
        var credential = request.Credential;
        switch (request.Frame.Type)
        {
            case EWorkerChannelFrameType.ReportReceived:
                await protocolService.ReportReceivedAsync(
                        workerId,
                        commandId,
                        ReadPayload<WorkerCommandStageRequest>(request, q => q.DeploymentId),
                        credential,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EWorkerChannelFrameType.ReportRunning:
                await protocolService.ReportRunningAsync(
                        workerId,
                        commandId,
                        ReadPayload<WorkerCommandProcessRequest>(request, q => q.DeploymentId),
                        credential,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EWorkerChannelFrameType.ReportSucceeded:
                await protocolService.ReportSucceededAsync(
                        workerId,
                        commandId,
                        ReadPayload<WorkerCommandProcessRequest>(request, q => q.DeploymentId),
                        credential,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case EWorkerChannelFrameType.ReportFailed:
                await protocolService.ReportFailedAsync(
                        workerId,
                        commandId,
                        ReadPayload<WorkerCommandFailureRequest>(request, q => q.DeploymentId),
                        credential,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new WorkerProtocolValidationException("The frame type is not a command report.");
        }
        return WorkerChannelRouteResult.Empty;
    }

    /// <summary>
    /// Reads, validates and deployment-checks a report payload with the same sequence as every channel route. The
    /// payload is never logged.
    /// </summary>
    /// <typeparam name="TPayload">The report payload type selected by the frame type.</typeparam>
    /// <param name="request">The request whose frame payload is read.</param>
    /// <param name="deploymentIdOf">Selects the deployment id carried by the payload.</param>
    /// <returns>The validated report payload.</returns>
    /// <exception cref="WorkerProtocolValidationException">
    /// The payload is missing, not a JSON object, unreadable, invalid or names another deployment.
    /// </exception>
    private static TPayload ReadPayload<TPayload>(WorkerChannelRequest request, Func<TPayload, Guid> deploymentIdOf)
        where TPayload : class
    {
        if (request.Frame.Payload is not { ValueKind: JsonValueKind.Object } element)
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        TPayload? payload;
        try
        {
            payload = element.Deserialize<TPayload>(WorkerChannelJson.Options);
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
        if (deploymentIdOf(payload) != request.Worker.DeploymentId)
            throw new WorkerProtocolValidationException("The frame payload names another deployment.");
        return payload;
    }
}
