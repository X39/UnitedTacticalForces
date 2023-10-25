namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server upgrade permission.
/// </summary>
public sealed class ServerUpgradeRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerUpgradeRequirement"/>.
    /// </summary>
    public ServerUpgradeRequirement() : base(Claims.ResourceBased.Server.Upgrade)
    {
    }
}