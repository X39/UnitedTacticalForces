namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Grants or revokes one allowlisted server-scoped permission for a role.
/// </summary>
public sealed record GameServerRoleAccessPayload
{
    /// <summary>
    /// Role receiving or losing the grant.
    /// </summary>
    public long RoleId { get; init; }

    /// <summary>
    /// Exact server-scoped claim identifier.
    /// </summary>
    public string Permission { get; init; } = string.Empty;
}
