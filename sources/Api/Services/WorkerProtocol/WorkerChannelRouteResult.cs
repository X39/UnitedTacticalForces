namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// The result of executing one frame through its route.
/// </summary>
/// <param name="Result">
/// The object serialized as the ack payload, or null when the frame has no result. A claim without a deliverable
/// command legitimately yields a null result.
/// </param>
/// <param name="WakeAfterAck">
/// True when the handler should send a Wake frame after the ack. A later route uses this hook to nudge the worker
/// without making the push the source of truth.
/// </param>
internal sealed record WorkerChannelRouteResult(object? Result, bool WakeAfterAck = false)
{
    /// <summary>Gets the result of a frame that completed without producing an ack payload.</summary>
    /// <value>A result with no payload and no wake request.</value>
    public static WorkerChannelRouteResult Empty { get; } = new(Result: null);
}
