namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server access logs permission.
/// </summary>
public sealed class ServerAccessLogsRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerAccessLogsRequirement"/>.
    /// </summary>
    public ServerAccessLogsRequirement() : base(Claims.ResourceBased.Server.AccessLogs)
    {
    }
}