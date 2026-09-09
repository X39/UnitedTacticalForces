using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Defines a reviewed provider source committed atomically with a revision.</summary>
public sealed record NormalizedModDraftPayload
{
    /// <summary>Gets the catalog title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the provider source input.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>Gets the provider version selector.</summary>
    public string VersionSelector { get; init; } = string.Empty;
    /// <summary>Gets where the mod is required.</summary>
    public EModUsageRole UsageRole { get; init; }
    /// <summary>Gets the game installer identifier.</summary>
    public string InstallerIdentifier { get; init; } = string.Empty;
    /// <summary>Gets installer configuration JSON.</summary>
    public string InstallerConfiguration { get; init; } = "{}";
}
