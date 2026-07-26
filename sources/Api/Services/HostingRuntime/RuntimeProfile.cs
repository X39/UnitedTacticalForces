namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// A provider-owned compute profile that can be selected before a deployment starts.
/// </summary>
public sealed record RuntimeProfile(
    string Identifier,
    string DisplayName,
    string Description,
    int? VirtualCpuCount = null,
    decimal? MemoryGiB = null,
    int? DiskGiB = null,
    decimal? EstimatedHourlyCostEur = null,
    decimal? EstimatedMonthlyCostEur = null);
