using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace Api.Tests.GameServerHosting;

/// <summary>Supplies a two-workload host for protocol and health regression tests.</summary>
internal static class WorkloadProtocolTestData
{
    /// <summary>Creates a valid placement with two workloads sharing one worker.</summary>
    /// <param name="includeCompanion">Whether the host also runs a companion workload.</param>
    /// <returns>A serialized immutable placement snapshot.</returns>
    internal static string Placement(bool includeCompanion = true)
    {
        var host = new WorkloadPlacementKey("game-server", 0);
        return new WorkloadPlacementPlan(1, host, null,
            [new WorkloadPlacementHost(host, [], "test",
                new RuntimeStoragePlan(ERuntimeStorageStrategy.IncludedDisk, 0, 0, "/runtime", 0),
                0.1m, null, 0)],
            includeCompanion
                ? [new WorkloadPlacementAssignment(host, host),
                    new WorkloadPlacementAssignment(new WorkloadPlacementKey("headless-client", 0), host)]
                : [new WorkloadPlacementAssignment(host, host)])
            .Serialize();
    }

    /// <summary>Creates command assignments for the two colocated workloads.</summary>
    /// <returns>The full deployment assignment list.</returns>
    internal static WorkerWorkloadAssignment[] Assignments()
        => [new() { ServiceIdentifier = "game-server", HostServiceIdentifier = "game-server", IsPrimary = true },
            new() { ServiceIdentifier = "headless-client", HostServiceIdentifier = "game-server" }];

    /// <summary>Creates current attributed process observations for both workloads.</summary>
    /// <returns>Independent running-process observations, without asserting game readiness.</returns>
    internal static WorkerWorkloadObservation[] Running()
        => [Observation("game-server", 123), Observation("headless-client", 456)];

    /// <summary>Creates one running-process observation.</summary>
    /// <param name="service">The assigned service identifier.</param>
    /// <param name="pid">The attributed process identifier.</param>
    /// <returns>A recent running-process observation.</returns>
    private static WorkerWorkloadObservation Observation(string service, int pid)
        => new()
        {
            ServiceIdentifier = service,
            ProcessId = pid,
            ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            ObservedAt = DateTimeOffset.UtcNow,
            State = EWorkerWorkloadState.Running,
        };
}
