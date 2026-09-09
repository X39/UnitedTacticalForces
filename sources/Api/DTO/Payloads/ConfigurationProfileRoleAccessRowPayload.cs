namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Describes the desired direct profile permissions for one role.
/// </summary>
public sealed record ConfigurationProfileRoleAccessRowPayload
{
    /// <summary>Gets the role identity.</summary>
    public long RoleId { get; init; }

    /// <summary>Gets the desired exact permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
