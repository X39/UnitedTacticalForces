using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Executes command claim frames through <see cref="IWorkerProtocolService.ClaimCommandAsync"/>.
/// </summary>
/// <remarks>
/// The route holds no claim ordering, redelivery, lease, supersession or credential logic. The protocol service
/// decides which command is delivered, redelivers Received and Running commands first and refuses claims that
/// cost-stop state forbids; its conflict propagates to the handler unchanged.
/// </remarks>
/// <param name="protocolService">The protocol service that owns every claim decision.</param>
[Scoped<ClaimFrameRoute, IWorkerChannelFrameRoute>]
internal sealed class ClaimFrameRoute(IWorkerProtocolService protocolService) : IWorkerChannelFrameRoute
{
    /// <inheritdoc />
    public IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; } = [EWorkerChannelFrameType.Claim];

    /// <inheritdoc />
    public async Task<WorkerChannelRouteResult> HandleAsync(
        WorkerChannelRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ReadPayload(request);
        var response = await protocolService
            .ClaimCommandAsync(request.Worker.WorkerId, payload, request.Credential, cancellationToken)
            .ConfigureAwait(false);
        return new WorkerChannelRouteResult(response);
    }

    /// <summary>
    /// Reads, validates and deployment-checks the claim payload with the same sequence as every channel route. The
    /// payload is never logged.
    /// </summary>
    /// <param name="request">The request whose frame payload is read.</param>
    /// <returns>The validated claim payload.</returns>
    /// <exception cref="WorkerProtocolValidationException">
    /// The payload is missing, not a JSON object, unreadable, invalid or names another deployment.
    /// </exception>
    private static WorkerCommandClaimRequest ReadPayload(WorkerChannelRequest request)
    {
        if (request.Frame.Payload is not { ValueKind: JsonValueKind.Object } element)
            throw new WorkerProtocolValidationException("The frame payload is invalid.");
        WorkerCommandClaimRequest? payload;
        try
        {
            payload = element.Deserialize<WorkerCommandClaimRequest>(WorkerChannelJson.Options);
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
