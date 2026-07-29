using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.StorageDemand;

public sealed record StorageDemandComponent(
    string Identifier,
    string DisplayName,
    string Kind,
    long RequiredBytes,
    EStorageEstimateStatus Status,
    string Source,
    Instant? ObservedAt = null,
    string? Detail = null);
