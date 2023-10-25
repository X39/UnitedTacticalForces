namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the server mod pack permission.
/// </summary>
public sealed class ServerModPackRequirement : ServerIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ServerModPackRequirement"/>.
    /// </summary>
    public ServerModPackRequirement() : base(Claims.ResourceBased.Server.ModPack)
    {
    }
}