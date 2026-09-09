namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Contains the direct or effective membership of a mod-pack revision.</summary>
public sealed record ModPackMembershipInfo
{
    /// <summary>Gets ordered mod members.</summary>
    public IReadOnlyList<ModPackModMemberInfo> Mods { get; init; } = [];
    /// <summary>Gets ordered direct nested mod packs.</summary>
    public IReadOnlyList<ModPackDependencyInfo> Dependencies { get; init; } = [];
}
