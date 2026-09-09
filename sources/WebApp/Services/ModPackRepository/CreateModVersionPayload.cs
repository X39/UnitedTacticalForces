namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Defines a provider selector and game-specific installer for a mod version.</summary>
public sealed record CreateModVersionPayload
{
    /// <summary>Gets the display version label.</summary>
    public string VersionLabel { get; init; } = string.Empty;
    /// <summary>Gets the provider-specific selector.</summary>
    public string VersionSelector { get; init; } = string.Empty;
    /// <summary>Gets the installer identifier.</summary>
    public string InstallerIdentifier { get; init; } = string.Empty;
    /// <summary>Gets installer configuration JSON.</summary>
    public string InstallerConfiguration { get; init; } = "{}";
}
