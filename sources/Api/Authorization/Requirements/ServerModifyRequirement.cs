namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for modifying one server.
/// </summary>
public sealed class ServerModifyRequirement : ServerIdRequirement
{
    public ServerModifyRequirement() : base(Claims.ResourceBased.Server.Modify)
    {
    }
}
