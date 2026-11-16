namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Identifies a workload or host instance within a deployment.</summary>
/// <param name="ServiceIdentifier">The stable service identifier used for runtime attribution.</param>
/// <param name="ServiceOrdinal">The zero-based instance number within the service.</param>
internal sealed record WorkloadPlacementKey(string ServiceIdentifier, int ServiceOrdinal);
