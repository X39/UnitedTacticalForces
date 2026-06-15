namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for irreversibly deleting one server's persistent data.
/// </summary>
public sealed class ServerDeleteDataRequirement : ServerIdRequirement
{
    public ServerDeleteDataRequirement() : base(Claims.ResourceBased.Server.DeleteData)
    {
    }
}
