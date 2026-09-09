namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Contains identifiers created for a new catalog mod.</summary>
public sealed record CreatedModResult
{
    /// <summary>Gets the mod identifier.</summary>
    public Guid PrimaryKey { get; init; }
    /// <summary>Gets the first version identifier.</summary>
    public Guid VersionId { get; init; }
}
