using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for transitioning a formal Verein membership request.
/// </summary>
public record VereinMembershipRequestTransitionPayload
{
    /// <summary>
    /// Target request status.
    /// </summary>
    public EVereinMembershipRequestStatus Status { get; init; }

    /// <summary>
    /// Administrative decision reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Optional member number to apply when approving.
    /// </summary>
    public string? MemberNumber { get; init; }

    /// <summary>
    /// Optional membership notes to apply when approving.
    /// </summary>
    public string? Notes { get; init; }
}
