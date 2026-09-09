namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Defines the complete membership of a new immutable revision.</summary>
public sealed record NormalizedModPackRevisionPayload
{
    /// <summary>Gets ordered direct mod memberships.</summary>
    public IReadOnlyList<NormalizedModMembershipPayload> Mods { get; init; } = [];
    /// <summary>Gets reviewed new catalog mods committed atomically with this revision.</summary>
    public IReadOnlyList<NormalizedModDraftPayload> NewMods { get; init; } = [];
    /// <summary>Gets ordered nested mod-pack memberships.</summary>
    public IReadOnlyList<NormalizedModPackDependencyPayload> Dependencies { get; init; } = [];
}
