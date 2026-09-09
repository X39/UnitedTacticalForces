namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Declares how a game installs content obtained from a registered provider.</summary>
/// <param name="ProviderIdentifier">Registered source provider identifier.</param>
/// <param name="DisplayName">User-facing provider name.</param>
/// <param name="AcceptedInput">Description of accepted input.</param>
/// <param name="InstallerIdentifiers">Compatible game-specific installers.</param>
/// <param name="SupportsVersionSelector">Whether provider version selectors are accepted.</param>
public sealed record ModProviderBindingDefinition(
    string ProviderIdentifier,
    string DisplayName,
    string AcceptedInput,
    IReadOnlyList<string> InstallerIdentifiers,
    bool SupportsVersionSelector);
