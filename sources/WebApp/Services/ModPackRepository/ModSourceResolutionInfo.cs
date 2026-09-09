namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Contains canonical metadata returned by a registered source provider.</summary>
public sealed record ModSourceResolutionInfo
{
    /// <summary>Gets the provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the canonical source identifier.</summary>
    public string CanonicalSourceIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the canonical source URL or locator.</summary>
    public string CanonicalSource { get; init; } = string.Empty;
    /// <summary>Gets the resolved title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets the resolved version label.</summary>
    public string VersionLabel { get; init; } = string.Empty;
    /// <summary>Gets the resolved version selector.</summary>
    public string VersionSelector { get; init; } = string.Empty;
}
