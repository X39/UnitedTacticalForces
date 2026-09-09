namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

/// <summary>Represents an immutable version of a catalog mod.</summary>
public sealed record ModVersionInfo
{
    /// <summary>Gets the version identifier.</summary>
    public Guid PrimaryKey { get; init; }
    /// <summary>Gets the ordinal version number.</summary>
    public int VersionNumber { get; init; }
    /// <summary>Gets the user-facing version label.</summary>
    public string VersionLabel { get; init; } = string.Empty;
    /// <summary>Gets the provider-specific version selector.</summary>
    public string VersionSelector { get; init; } = string.Empty;
    /// <summary>Gets the game installer identifier.</summary>
    public string InstallerIdentifier { get; init; } = string.Empty;
    /// <summary>Gets installer configuration JSON.</summary>
    public string InstallerConfiguration { get; init; } = "{}";
}
