namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Describes the desired direct profile permissions for one user.
/// </summary>
public sealed record ConfigurationProfileUserAccessRowPayload
{
    /// <summary>Gets the user identity.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the desired exact permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
