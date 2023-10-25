namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server start/stop permission.
/// </summary>
public sealed class ServerStartStopRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerStartStopRequirement"/>.
    /// </summary>
    public ServerStartStopRequirement() : base(Claims.ResourceBased.Server.StartStop)
    {
    }
}