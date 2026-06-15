namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeProviderCapabilities(
    bool SupportsStableProvisioningIntent,
    bool SupportsProviderWideDiscovery,
    bool SupportsVerifiedRelease,
    bool SupportsPlatformManagedDanglingRelease = false,
    bool IsReady = true,
    string? ReadinessFailure = null,
    decimal? EstimatedHourlyCostEur = null,
    IReadOnlyList<string>? RequiredProvisionedResourceTypes = null);