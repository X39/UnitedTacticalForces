namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

internal sealed record SteamWorkshopMetadata(
    string ItemIdentifier,
    long? ReportedBytes,
    string Status,
    string? Version,
    Instant? UpdatedAt);
