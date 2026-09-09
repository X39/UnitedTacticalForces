namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public sealed record SteamWorkshopMetadata(
    string ItemIdentifier,
    long? ReportedBytes,
    string Status,
    string? Version,
    Instant? UpdatedAt,
    string? Title = null);
