namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Contains identifiers created by a normalized mod-pack operation.</summary>
public sealed record NormalizedModPackResult
{
    /// <summary>Gets the created definition identifier.</summary>
    public long DefinitionId { get; init; }
    /// <summary>Gets the created revision identifier.</summary>
    public long RevisionId { get; init; }
}
