using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record StorageDemandComponentDto
{
    public string Identifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public long RequiredBytes { get; init; }
    public EStorageEstimateStatus Status { get; init; }
    public string Source { get; init; } = string.Empty;
    public Instant? ObservedAt { get; init; }
    public string? Detail { get; init; }
}
