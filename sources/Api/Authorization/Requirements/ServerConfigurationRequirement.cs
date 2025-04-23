namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server configuration permission.
/// </summary>
public sealed class ServerConfigurationRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerConfigurationRequirement"/>.
    /// </summary>
    public ServerConfigurationRequirement() : base(Claims.ResourceBased.Server.Configuration)
    {
    }
}