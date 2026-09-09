namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Replaces the complete direct-access matrix of one configuration profile.
/// </summary>
public sealed record ConfigurationProfileAccessMatrixPayload
{
    /// <summary>Gets the desired user rows.</summary>
    public IReadOnlyList<ConfigurationProfileUserAccessRowPayload> Users { get; init; } = [];

    /// <summary>Gets the desired role rows.</summary>
    public IReadOnlyList<ConfigurationProfileRoleAccessRowPayload> Roles { get; init; } = [];
}
