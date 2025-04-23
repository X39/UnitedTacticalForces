namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server rename permission.
/// </summary>
public sealed class ServerRenameRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerRenameRequirement"/>.
    /// </summary>
    public ServerRenameRequirement() : base(Claims.ResourceBased.Server.Rename)
    {
    }
}