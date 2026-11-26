using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// D-09 and MET-05 proof, over the whole product of deployment, primary worker and sample states, that a reading is
/// a value only when the server runs, the worker is live and the sample belongs to the active session and is fresh,
/// and that otherwise it carries exactly one reason in the documented precedence. The tests run against in-memory
/// SQLite.
/// </summary>
public sealed class ServerMetricsAvailabilityTests
{
    private const long Gib = 1024L * 1024L * 1024L;

    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_100);

    private static readonly Instant ProcessStart = Instant.FromUnixTimeSeconds(1_700_000_030);

    /// <summary>
    /// The state of the server's deployments.
    /// </summary>
    public enum EDeploymentCase
    {
        /// <summary>Every deployment of the server is resolved.</summary>
        None,

        /// <summary>The newest deployment is not resolved.</summary>
        Active,
    }

    /// <summary>
    /// The state of the active deployment's primary worker.
    /// </summary>
    public enum EWorkerCase
    {
        /// <summary>The only game-server worker with ordinal 0 is stopped.</summary>
        Missing,

        /// <summary>The worker is online but its heartbeat is older than the stale threshold.</summary>
        StaleHeartbeat,

        /// <summary>The worker heartbeats freshly but reports a status other than online.</summary>
        NotOnline,

        /// <summary>The worker is online and fresh but observes no game process.</summary>
        OnlineWithoutProcess,

        /// <summary>The worker is online and fresh and observes a running game process.</summary>
        OnlineWithProcess,
    }

    /// <summary>
    /// The state of the stored metric snapshot.
    /// </summary>
    public enum ESampleCase
    {
        /// <summary>No snapshot exists.</summary>
        None,

        /// <summary>The snapshot belongs to a previous deployment.</summary>
        PreviousDeployment,

        /// <summary>The snapshot belongs to the active deployment but is older than the stale limit.</summary>
        OlderThanStaleLimit,

        /// <summary>A fresh snapshot of the active deployment holding values for every reading.</summary>
        FreshValues,

        /// <summary>A fresh snapshot of the active deployment holding a worker reason for every reading.</summary>
        FreshReasons,
    }

    /// <summary>
    /// Enumerates the product of deployment, worker and sample states.
    /// </summary>
    /// <returns>One argument row per combination.</returns>
    public static IEnumerable<object[]> Cases()
    {
        foreach (var deployment in Enum.GetValues<EDeploymentCase>())
        foreach (var worker in Enum.GetValues<EWorkerCase>())
        foreach (var sample in Enum.GetValues<ESampleCase>())
            yield return [deployment, worker, sample];
    }

    /// <summary>
    /// Every combination returns a numeric value only when all availability conditions hold, and otherwise exactly
    /// one reason of the documented precedence.
    /// </summary>
    /// <param name="deploymentCase">The deployment state to seed.</param>
    /// <param name="workerCase">The worker state to seed.</param>
    /// <param name="sampleCase">The snapshot state to seed.</param>
    /// <returns>A task completing after the readings were verified.</returns>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Reading_IsAValueOnlyWhenEveryConditionHolds(
        EDeploymentCase deploymentCase,
        EWorkerCase workerCase,
        ESampleCase sampleCase)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        if (deploymentCase == EDeploymentCase.None)
        {
            deployment.TimeStampResolved = Now - Duration.FromSeconds(50);
            await store.DbContext.SaveChangesAsync();
        }

        var worker = await SeedWorkerAsync(store, deployment, workerCase);
        await SeedSampleAsync(store, deployment, worker, sampleCase);

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        var active = deploymentCase == EDeploymentCase.Active;
        var live = workerCase is EWorkerCase.OnlineWithoutProcess or EWorkerCase.OnlineWithProcess;
        var process = workerCase == EWorkerCase.OnlineWithProcess;
        var counted = sampleCase is ESampleCase.OlderThanStaleLimit
            or ESampleCase.FreshValues
            or ESampleCase.FreshReasons;
        var fresh = sampleCase is ESampleCase.FreshValues or ESampleCase.FreshReasons;

        string? Gate(bool needsProcess)
            => !active ? WorkerMetricUnavailableReasons.ServerNotRunning
                : !live ? WorkerMetricUnavailableReasons.WorkerOffline
                : needsProcess && !process ? WorkerMetricUnavailableReasons.ServerNotRunning
                : !counted ? WorkerMetricUnavailableReasons.Measuring
                : !fresh ? WorkerMetricUnavailableReasons.Stale
                : null;

        var machineValue = Gate(false) is null && sampleCase == ESampleCase.FreshValues;
        var machineReason = Gate(false) ?? (sampleCase == ESampleCase.FreshReasons
            ? WorkerMetricUnavailableReasons.QueryFailed
            : null);
        var playersValue = Gate(true) is null && sampleCase == ESampleCase.FreshValues;
        var playersReason = Gate(true) ?? (sampleCase == ESampleCase.FreshReasons
            ? WorkerMetricUnavailableReasons.GameDoesNotReport
            : null);
        var runningSinceValue = active && live && process;
        var runningSinceReason = !active ? WorkerMetricUnavailableReasons.ServerNotRunning
            : !live ? WorkerMetricUnavailableReasons.WorkerOffline
            : !process ? WorkerMetricUnavailableReasons.ServerNotRunning
            : null;

        AssertExactlyOne(dto.RunningSince is not null, dto.RunningSinceUnavailableReason);
        AssertExactlyOne(dto.CpuPercent is not null, dto.CpuUnavailableReason);
        AssertExactlyOne(dto.MemoryUsedBytes is not null && dto.MemoryTotalBytes is not null,
            dto.MemoryUnavailableReason);
        AssertExactlyOne(dto.PlayerCount is not null, dto.PlayersUnavailableReason);
        Assert.Equal(runningSinceValue, dto.RunningSince is not null);
        Assert.Equal(runningSinceReason, dto.RunningSinceUnavailableReason);
        Assert.Equal(machineValue, dto.CpuPercent is not null);
        Assert.Equal(machineReason, dto.CpuUnavailableReason);
        Assert.Equal(machineValue, dto.MemoryUsedBytes is not null);
        Assert.Equal(machineValue, dto.MemoryTotalBytes is not null);
        Assert.Equal(machineReason, dto.MemoryUnavailableReason);
        Assert.Equal(playersValue, dto.PlayerCount is not null);
        Assert.Equal(playersReason, dto.PlayersUnavailableReason);
        Assert.Equal(playersValue, dto.PlayerCapacity is not null);
        if (!machineValue) Assert.Null(dto.MemoryTotalBytes);
        if (!playersValue) Assert.Null(dto.PlayerCapacity);
        Assert.Equal(45, dto.StaleAfterSeconds);
        Assert.Equal(counted && active ? (Instant?) SnapshotReceivedAt(sampleCase) : null, dto.AsOf);
    }

    /// <summary>
    /// A sample received exactly at the stale limit still counts as fresh.
    /// </summary>
    /// <returns>A task completing after the readings were verified.</returns>
    [Fact]
    public async Task SampleAtTheStaleLimit_IsStillFresh()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), ProcessStart);
        var receivedAt = Now - Duration.FromSeconds(45);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey, receivedAt.ToDateTimeOffset(), 1d, Gib, 2 * Gib, 0, 8));

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Equal(1d, dto.CpuPercent);
        Assert.Equal(0, dto.PlayerCount);
    }

    private static void AssertExactlyOne(bool hasValue, string? reason)
        => Assert.NotEqual(hasValue, reason is not null);

    private static Instant SnapshotReceivedAt(ESampleCase sampleCase)
        => sampleCase == ESampleCase.OlderThanStaleLimit
            ? Now - Duration.FromSeconds(46)
            : Now - Duration.FromSeconds(10);

    private static async Task<Guid> SeedWorkerAsync(
        HostingTestStore store,
        GameServerDeployment deployment,
        EWorkerCase workerCase)
    {
        var fresh = Now - Duration.FromSeconds(5);
        var worker = workerCase switch
        {
            EWorkerCase.Missing => await ServerMetricsTestData.AddPrimaryWorkerAsync(
                store.DbContext, deployment, fresh, ProcessStart, stoppedAt: Now - Duration.FromSeconds(30)),
            EWorkerCase.StaleHeartbeat => await ServerMetricsTestData.AddPrimaryWorkerAsync(
                store.DbContext, deployment, Now - Duration.FromSeconds(61), ProcessStart),
            EWorkerCase.NotOnline => await ServerMetricsTestData.AddPrimaryWorkerAsync(
                store.DbContext, deployment, fresh, ProcessStart, EWorkerStatus.Offline),
            EWorkerCase.OnlineWithoutProcess => await ServerMetricsTestData.AddPrimaryWorkerAsync(
                store.DbContext, deployment, fresh, null),
            EWorkerCase.OnlineWithProcess => await ServerMetricsTestData.AddPrimaryWorkerAsync(
                store.DbContext, deployment, fresh, ProcessStart),
            _ => throw new ArgumentOutOfRangeException(nameof(workerCase), workerCase, null),
        };
        return worker.PrimaryKey;
    }

    private static async Task SeedSampleAsync(
        HostingTestStore store,
        GameServerDeployment deployment,
        Guid workerId,
        ESampleCase sampleCase)
    {
        if (sampleCase == ESampleCase.None)
            return;
        var snapshotDeployment = deployment;
        if (sampleCase == ESampleCase.PreviousDeployment)
        {
            snapshotDeployment = new GameServerDeployment
            {
                PrimaryKey        = Guid.NewGuid(),
                GameServerFk      = deployment.GameServerFk,
                CostOwnerFk       = deployment.CostOwnerFk,
                Status            = EDeploymentStatus.Stopped,
                Stage             = "stopped",
                TimeStampCreated  = deployment.TimeStampCreated - Duration.FromHours(1),
                TimeStampUpdated  = deployment.TimeStampCreated - Duration.FromMinutes(30),
                TimeStampResolved = deployment.TimeStampCreated - Duration.FromMinutes(30),
            };
            store.DbContext.GameServerDeployments.Add(snapshotDeployment);
            await store.DbContext.SaveChangesAsync();
        }

        var receivedAt = SnapshotReceivedAt(sampleCase);
        var sampledAt = receivedAt.ToDateTimeOffset();
        var sample = sampleCase == ESampleCase.FreshReasons
            ? ServerMetricsTestData.Sample(
                snapshotDeployment.PrimaryKey,
                sampledAt,
                null,
                null,
                null,
                null,
                cpuReason: WorkerMetricUnavailableReasons.QueryFailed,
                memoryReason: WorkerMetricUnavailableReasons.QueryFailed,
                playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport)
            : ServerMetricsTestData.Sample(snapshotDeployment.PrimaryKey, sampledAt, 55d, 2 * Gib, 8 * Gib, 4, 16);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext, snapshotDeployment, workerId, receivedAt, sample);
    }
}
