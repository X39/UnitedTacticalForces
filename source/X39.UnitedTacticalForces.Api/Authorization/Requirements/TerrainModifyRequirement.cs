namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the terrain modify permission.
/// </summary>
public sealed class TerrainModifyRequirement : TerrainIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="TerrainModifyRequirement"/>.
    /// </summary>
    public TerrainModifyRequirement() : base(Claims.ResourceBased.Terrain.Modify)
    {
    }
}