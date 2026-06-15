namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for viewing one server.
/// </summary>
public sealed class ServerViewRequirement : ServerIdRequirement
{
    public ServerViewRequirement() : base(Claims.ResourceBased.Server.View)
    {
    }
}
