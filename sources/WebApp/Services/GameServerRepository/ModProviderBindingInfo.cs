namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>Describes a mod provider that a game can install.</summary>
public sealed record ModProviderBindingInfo
{
    /// <summary>Gets the stable provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the provider display name.</summary>
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Gets a description of accepted source input.</summary>
    public string AcceptedInput { get; init; } = string.Empty;
    /// <summary>Gets compatible installer identifiers.</summary>
    public IReadOnlyCollection<string> InstallerIdentifiers { get; init; } = [];
    /// <summary>Gets whether the provider accepts a version selector.</summary>
    public bool SupportsVersionSelector { get; init; }
}
