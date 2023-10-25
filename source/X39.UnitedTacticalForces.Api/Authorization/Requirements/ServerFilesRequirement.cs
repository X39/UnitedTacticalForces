namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server files permission.
/// </summary>
public sealed class ServerFilesRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerFilesRequirement"/>.
    /// </summary>
    public ServerFilesRequirement() : base(Claims.ResourceBased.Server.Files)
    {
    }
}