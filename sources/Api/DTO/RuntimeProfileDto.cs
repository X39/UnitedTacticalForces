namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// API-safe cloud compute profile and its current provider price estimate.
/// </summary>
public sealed record RuntimeProfileDto
{
    public string Identifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? VirtualCpuCount { get; init; }
    public decimal? MemoryGiB { get; init; }
    public int? DiskGiB { get; init; }
    public decimal? EstimatedHourlyCostEur { get; init; }
    public decimal? EstimatedMonthlyCostEur { get; init; }
}
