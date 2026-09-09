namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Defines all members of a new immutable mod-pack revision.</summary>
public sealed record NormalizedModPackRevisionPayload
{
    /// <summary>Gets ordered direct mods.</summary>
    public IReadOnlyList<NormalizedModMembershipPayload> Mods { get; init; } = [];
    /// <summary>Gets reviewed new mods committed atomically with this revision.</summary>
    public IReadOnlyList<NormalizedModDraftPayload> NewMods { get; init; } = [];
    /// <summary>Gets ordered nested mod packs.</summary>
    public IReadOnlyList<NormalizedModPackDependencyPayload> Dependencies { get; init; } = [];
}
