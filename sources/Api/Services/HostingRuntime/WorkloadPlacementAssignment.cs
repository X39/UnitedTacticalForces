namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Assigns one workload instance to a host in the deployment.</summary>
/// <param name="WorkloadKey">The identity of the workload.</param>
/// <param name="HostKey">The identity of the host that executes the workload.</param>
internal sealed record WorkloadPlacementAssignment(WorkloadPlacementKey WorkloadKey, WorkloadPlacementKey HostKey);
