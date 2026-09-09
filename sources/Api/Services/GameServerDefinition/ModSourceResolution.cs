namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Canonical metadata resolved from a mod source input.</summary>
/// <param name="ProviderIdentifier">Provider identifier.</param>
/// <param name="CanonicalSourceIdentifier">Stable canonical source identity.</param>
/// <param name="Title">Resolved title.</param>
/// <param name="VersionSelector">Normalized optional version selector.</param>
public sealed record ModSourceResolution(
    string ProviderIdentifier,
    string CanonicalSourceIdentifier,
    string Title,
    string VersionSelector);
