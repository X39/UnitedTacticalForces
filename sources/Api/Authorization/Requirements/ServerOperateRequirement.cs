namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for operating one server.
/// </summary>
public sealed class ServerOperateRequirement : ServerIdRequirement
{
    public ServerOperateRequirement() : base(Claims.ResourceBased.Server.Operate)
    {
    }
}
