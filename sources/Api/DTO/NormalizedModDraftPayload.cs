using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Defines a new provider-backed catalog mod committed atomically with a revision.</summary>
public sealed record NormalizedModDraftPayload
{
    /// <summary>Gets the reviewed catalog title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the registered provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the provider input URL or identifier.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>Gets the optional provider version selector.</summary>
    public string VersionSelector { get; init; } = string.Empty;
    /// <summary>Gets the client/server usage role.</summary>
    public EModUsageRole UsageRole { get; init; }
    /// <summary>Gets the compatible game installer identifier.</summary>
    public string InstallerIdentifier { get; init; } = string.Empty;
    /// <summary>Gets provider-specific installer configuration JSON.</summary>
    public string InstallerConfiguration { get; init; } = "{}";
}
