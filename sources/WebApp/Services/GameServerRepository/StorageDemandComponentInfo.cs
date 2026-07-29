namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record StorageDemandComponentInfo
{
    public string Identifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public long RequiredBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTimeOffset? ObservedAt { get; init; }
    public string? Detail { get; init; }
}
