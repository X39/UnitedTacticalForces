using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>Normalized effective mod supplied to a game-owned exporter.</summary>
/// <param name="Title">Mod title.</param>
/// <param name="ProviderIdentifier">Source provider identifier.</param>
/// <param name="CanonicalSourceIdentifier">Canonical provider identity.</param>
/// <param name="UsageRole">Mod usage role.</param>
public sealed record ModPackFormatExportEntry(
    string Title,
    string ProviderIdentifier,
    string CanonicalSourceIdentifier,
    EModUsageRole UsageRole);
