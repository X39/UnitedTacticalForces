using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Materializes the host placement supported by a workload definition and its effective configuration.</summary>
internal static class WorkloadPlacementResolver
{
    /// <summary>Freezes the host identities, ingress, allocation, and prices for an accepted deployment.</summary>
    /// <param name="definition">The definition supplying the workload instances and placement policy.</param>
    /// <param name="configuration">The effective configuration, or null to retain historical separate placement.</param>
    /// <param name="profile">The compute profile selected for each host.</param>
    /// <param name="storagePlan">The storage allocation selected for each host.</param>
    /// <param name="fallbackHourlyCostEur">The configured per-host rate if the profile has no hourly price.</param>
    /// <param name="profileSystemReserveBytes">Root disk capacity reserved for the operating system and bootstrap.</param>
    /// <returns>The validated placement for quoting and allocation.</returns>
    /// <exception cref="InvalidOperationException">The definition, placement, or pricing cannot be used.</exception>
    public static WorkloadPlacementPlan Resolve(
        GameServerDefinition.GameServerDefinition definition,
        ResolvedGameConfiguration? configuration,
        RuntimeProfile profile,
        RuntimeStoragePlan storagePlan,
        decimal? fallbackHourlyCostEur = null,
        long profileSystemReserveBytes = 0)
    {
        var workloadDefinitions = definition.Nodes
            .SelectMany(node => Enumerable.Range(0, node.Count).Select(ordinal => new
            {
                Key = new WorkloadPlacementKey(node.ServiceIdentifier, ordinal),
                node.IsPrimary,
                node.InboundNetworkRequirements,
            }))
            .ToDictionary(workload => workload.Key);
        var primary = workloadDefinitions.Values.Single(workload => workload.IsPrimary);
        var workloads = workloadDefinitions.Keys
            .Select(key => new WorkloadPlacementAssignment(
                key,
                configuration is not null && definition.ResolveWorkloadHost is { } resolveHost
                    ? resolveHost(configuration, key)
                    : key))
            .ToArray();
        if (workloads.Any(workload => !workloadDefinitions.ContainsKey(workload.HostKey))
            || workloads.Single(workload => workload.WorkloadKey == primary.Key).HostKey != primary.Key)
        {
            throw new InvalidOperationException("The workload placement references an unavailable host.");
        }

        var computeHourlyCostEur = profile.EstimatedHourlyCostEur is > 0m and { } rate
            ? rate
            : fallbackHourlyCostEur is > 0m and { } fallback
                ? fallback
                : throw new InvalidOperationException("No usable hourly compute price is available for this profile.");
        var hosts = workloads
            .GroupBy(workload => workload.HostKey)
            .Select(group => new WorkloadPlacementHost(
                group.Key,
                group.SelectMany(workload => workload.WorkloadKey == primary.Key
                        ? definition.InboundNetworkRequirements
                        : workloadDefinitions[workload.WorkloadKey].InboundNetworkRequirements)
                    .Distinct()
                    .ToArray(),
                profile.Identifier,
                storagePlan,
                computeHourlyCostEur,
                profile.EstimatedMonthlyCostEur,
                profileSystemReserveBytes))
            .ToArray();
        var plan = new WorkloadPlacementPlan(
            WorkloadPlacementPlan.CurrentVersion,
            primary.Key,
            definition.PrimaryConnection is { } connection
                ? new WorkloadPlacementConnection(connection.Protocol, connection.Port, connection.Description)
                : null,
            hosts,
            workloads);
        plan.Validate();
        return plan;
    }
}
