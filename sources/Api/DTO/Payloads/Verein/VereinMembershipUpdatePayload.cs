using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;

/// <summary>
/// Payload for creating or updating formal membership.
/// </summary>
public record VereinMembershipUpdatePayload
{
    /// <summary>
    /// Optional association member number.
    /// </summary>
    public string? MemberNumber { get; init; }

    /// <summary>
    /// New membership status.
    /// </summary>
    public EVereinMembershipStatus Status { get; init; } = EVereinMembershipStatus.Active;

    /// <summary>
    /// Membership start timestamp.
    /// </summary>
    public Instant? TimeStampStarted { get; init; }

    /// <summary>
    /// Membership end timestamp.
    /// </summary>
    public Instant? TimeStampEnded { get; init; }

    /// <summary>
    /// Administrative notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Audit reason.
    /// </summary>
    public string? Reason { get; init; }
}
