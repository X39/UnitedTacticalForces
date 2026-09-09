namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>Public game/provider installation capability.</summary>
public sealed record ModProviderBindingDto
{
    /// <summary>Gets the provider identifier.</summary>
    public string ProviderIdentifier { get; init; } = string.Empty;
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Gets the accepted input description.</summary>
    public string AcceptedInput { get; init; } = string.Empty;
    /// <summary>Gets compatible installer identifiers.</summary>
    public IReadOnlyCollection<string> InstallerIdentifiers { get; init; } = [];
    /// <summary>Gets whether a version selector may be supplied.</summary>
    public bool SupportsVersionSelector { get; init; }
}
