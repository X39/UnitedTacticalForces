namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the modPack modify permission.
/// </summary>
public sealed class ModPackModifyRequirement : ModPackIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ModPackModifyRequirement"/>.
    /// </summary>
    public ModPackModifyRequirement() : base(Claims.ResourceBased.ModPack.Modify)
    {
    }
}