namespace X39.UnitedTacticalForces.Api.Authorization.Requirements;

/// <summary>
/// Requirement for the modPack delete permission.
/// </summary>
public sealed class ModPackDeleteRequirement : ModPackIdRequirement
{
    /// <summary>
    /// Creates a new instance of <see cref="ModPackDeleteRequirement"/>.
    /// </summary>
    public ModPackDeleteRequirement() : base(Claims.ResourceBased.ModPack.Delete)
    {
    }
}