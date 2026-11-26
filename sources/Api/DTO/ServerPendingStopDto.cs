using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// An automatic stop that was announced to the owner and has not happened yet.
/// </summary>
/// <remarks>
/// Every viewer of the server receives it, so the in-app notice does not depend on the owner being online.
/// </remarks>
public sealed record ServerPendingStopDto
{
    /// <summary>
    /// Gets why the stop was announced.
    /// </summary>
    /// <value>Idle, maximum runtime or schedule.</value>
    public required EAutoStopReason Reason { get; init; }

    /// <summary>
    /// Gets the instant the stop happens unless it is cancelled first.
    /// </summary>
    /// <value>The stop instant recorded when the warning opened.</value>
    public required Instant StopsAt { get; init; }

    /// <summary>
    /// Gets the limit that applied when the warning opened.
    /// </summary>
    /// <value>Minutes for an idle stop, hours for a maximum runtime stop, or <see langword="null"/> for a schedule.</value>
    public int? Limit { get; init; }
}
