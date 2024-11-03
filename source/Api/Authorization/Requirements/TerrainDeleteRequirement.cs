namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the terrain delete permission.
/// </summary>
public sealed class TerrainDeleteRequirement : TerrainIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="TerrainDeleteRequirement"/>.
    /// </summary>
    public TerrainDeleteRequirement() : base(Claims.ResourceBased.Terrain.Delete)
    {
    }
}