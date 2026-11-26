namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// The facts a runtime provider declares about what it can do and what it costs.
/// </summary>
/// <param name="SupportsStableProvisioningIntent">
/// Whether provisioning intent can be persisted before the provider is mutated and replayed idempotently.
/// </param>
/// <param name="SupportsProviderWideDiscovery">
/// Whether every platform-managed resource in the provider scope can be discovered independently of a deployment.
/// </param>
/// <param name="SupportsVerifiedRelease">
/// Whether a release can be verified as non-billable before cost is considered stopped.
/// </param>
/// <param name="SupportsPlatformManagedDanglingRelease">
/// Whether discovered platform-managed resources without a valid deployment can be released.
/// </param>
/// <param name="IsReady">Whether the provider is configured well enough to provision.</param>
/// <param name="ReadinessFailure">The reason the provider is not ready, or <see langword="null"/> when it is.</param>
/// <param name="EstimatedHourlyCostEur">
/// The conservative estimated hourly compute cost in EUR, or <see langword="null"/> when unknown.
/// </param>
/// <param name="RequiredProvisionedResourceTypes">
/// The resource types a successful provision must report, or <see langword="null"/> when none are required.
/// </param>
/// <param name="Storage">The external storage capabilities, or <see langword="null"/> when none are declared.</param>
/// <param name="BillingUnit">
/// The granularity the provider bills at, or <see langword="null"/> when it declares none; an undeclared unit makes
/// the accrued session cost unavailable instead of guessed.
/// </param>
public sealed record RuntimeProviderCapabilities(
    bool SupportsStableProvisioningIntent,
    bool SupportsProviderWideDiscovery,
    bool SupportsVerifiedRelease,
    bool SupportsPlatformManagedDanglingRelease = false,
    bool IsReady = true,
    string? ReadinessFailure = null,
    decimal? EstimatedHourlyCostEur = null,
    IReadOnlyList<string>? RequiredProvisionedResourceTypes = null,
    RuntimeStorageCapabilities? Storage = null,
    RuntimeBillingUnit? BillingUnit = null);
