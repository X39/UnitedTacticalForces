namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Game-owned runtime installation and storage metadata consumed by generic hosting services.
/// </summary>
public sealed record GameRuntimeInstallationDefinition(
    string ApplicationIdentifier,
    long UnknownRuntimeBytes,
    string RuntimeDisplayName,
    string? WorkshopApplicationIdentifier = null,
    bool RequiresAuthenticatedSteamCmd = false);
