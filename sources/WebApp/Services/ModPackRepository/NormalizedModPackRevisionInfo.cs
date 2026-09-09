namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Describes one immutable normalized mod-pack revision.</summary>
public sealed record NormalizedModPackRevisionInfo
{
    /// <summary>Gets the revision identifier.</summary>
    public long RevisionId { get; init; }
    /// <summary>Gets whether this revision is active.</summary>
    public bool IsActive { get; init; }
    /// <summary>Gets the creation time.</summary>
    public DateTimeOffset TimeStampCreated { get; init; }
    /// <summary>Gets the user that created the revision.</summary>
    public Guid UpdatedByFk { get; init; }
    /// <summary>Gets the number of direct mods.</summary>
    public int DirectModCount { get; init; }
    /// <summary>Gets the number of nested packs.</summary>
    public int NestedModPackCount { get; init; }
}
