namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Grants or revokes one allowlisted server-scoped permission for a user.
/// </summary>
public sealed record GameServerAccessPayload
{
    /// <summary>
    /// User receiving or losing the grant.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Exact server-scoped claim identifier.
    /// </summary>
    public string Permission { get; init; } = string.Empty;
}
