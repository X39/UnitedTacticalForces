using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Describes a finance-controlled cost-owner creation request.
/// </summary>
public sealed record CostOwnerCreationPayload
{
    /// <summary>
    /// Gets the kind of cost owner to create.
    /// </summary>
    public ECostOwnerKind Kind { get; init; }

    /// <summary>
    /// Gets the user represented by a user cost owner.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Gets the display name for an external organization.
    /// </summary>
    public string? DisplayName { get; init; }
}
