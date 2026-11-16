namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Freezes the allocation and estimated compute cost of one host.</summary>
/// <param name="Key">The worker and provider attribution key for the host.</param>
/// <param name="InboundNetworkRequirements">The ingress policy required by workloads assigned to the host.</param>
/// <param name="RuntimeProfileIdentifier">The selected compute profile identifier.</param>
/// <param name="StoragePlan">The allocation and estimated storage cost for this host.</param>
/// <param name="ComputeHourlyCostEur">The estimated hourly compute rate for this host.</param>
/// <param name="ComputeMonthlyCostEur">The estimated monthly compute rate, if reported by the provider.</param>
/// <param name="ProfileSystemReserveBytes">Root disk capacity reserved for the operating system and bootstrap.</param>
internal sealed record WorkloadPlacementHost(
    WorkloadPlacementKey Key,
    IReadOnlyList<RuntimeInboundNetworkRequirement> InboundNetworkRequirements,
    string RuntimeProfileIdentifier,
    RuntimeStoragePlan StoragePlan,
    decimal ComputeHourlyCostEur,
    decimal? ComputeMonthlyCostEur,
    long ProfileSystemReserveBytes);
