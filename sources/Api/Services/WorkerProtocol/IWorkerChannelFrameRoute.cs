using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Executes frames of one or more types received on a worker channel.
/// </summary>
/// <remarks>
/// A route is a thin adapter in front of <see cref="IWorkerProtocolService"/>: it validates the payload, checks the
/// deployment binding and delegates. It must not duplicate claim ordering, lease, supersession or credential rules.
/// </remarks>
internal interface IWorkerChannelFrameRoute
{
    /// <summary>Gets the frame types this route executes.</summary>
    /// <value>The non-empty set of frame types this route handles; no two routes handle the same type.</value>
    IReadOnlyCollection<EWorkerChannelFrameType> FrameTypes { get; }

    /// <summary>Executes one frame for its authenticated worker.</summary>
    /// <param name="request">The frame with the worker it is attributed to and the held credential.</param>
    /// <param name="cancellationToken">A token cancelling the execution.</param>
    /// <returns>The route result carrying the optional ack payload.</returns>
    /// <exception cref="WorkerProtocolValidationException">
    /// The payload is missing, unreadable, invalid or names another deployment.
    /// </exception>
    /// <exception cref="WorkerProtocolAuthenticationException">The protocol service rejected the credential.</exception>
    /// <exception cref="WorkerProtocolConflictException">The protocol service refused the request as a conflict.</exception>
    Task<WorkerChannelRouteResult> HandleAsync(WorkerChannelRequest request, CancellationToken cancellationToken);
}
