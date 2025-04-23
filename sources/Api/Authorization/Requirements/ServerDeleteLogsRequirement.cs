namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server delete logs permission.
/// </summary>
public sealed class ServerDeleteLogsRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerDeleteLogsRequirement"/>.
    /// </summary>
    public ServerDeleteLogsRequirement() : base(Claims.ResourceBased.Server.DeleteLogs)
    {
    }
}