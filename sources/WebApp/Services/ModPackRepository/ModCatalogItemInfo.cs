using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents a reusable game-scoped mod in the normalized catalog.</summary>
public sealed record ModCatalogItemInfo
{
    /// <summary>Gets the mod identifier.</summary>
    public Guid PrimaryKey { get; init; }
    /// <summary>Gets the owning game identifier.</summary>
    public string GameIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the source provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the canonical source identifier.</summary>
    public string CanonicalSourceIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the display title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets where the mod is required.</summary>
    public EModUsageRole UsageRole { get; init; }
    /// <summary>Gets immutable versions, newest first.</summary>
    public IReadOnlyList<ModVersionInfo> Versions { get; init; } = [];
}
