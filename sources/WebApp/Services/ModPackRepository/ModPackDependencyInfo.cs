namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents one ordered nested mod-pack membership.</summary>
public sealed record ModPackDependencyInfo
{
    /// <summary>Gets the child definition identifier.</summary>
    public long ChildDefinitionFk { get; init; }
    /// <summary>Gets the optional pinned child revision.</summary>
    public long? PinnedChildRevisionFk { get; init; }
    /// <summary>Gets the membership order.</summary>
    public int Order { get; init; }
}
