using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Dismisses, for the calling viewer, the listed cost alerts of one game server.
/// </summary>
public sealed record CostAlertDismissAllPayload
{
    /// <summary>
    /// Gets the game server whose alerts are dismissed.
    /// </summary>
    /// <value>The primary key of the game server.</value>
    public Guid GameServerId { get; init; }

    /// <summary>
    /// Gets the alerts the client shows; alerts raised later are never dismissed by this request.
    /// </summary>
    /// <value>One to fifty alert ids; ids of other servers or unviewable cost owners are ignored.</value>
    [MinLength(1)]
    [MaxLength(50)]
    public required IReadOnlyList<Guid> CostAlertIds { get; init; }
}
