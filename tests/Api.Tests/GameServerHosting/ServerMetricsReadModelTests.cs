using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// MET-01 and MET-02 proof that the control plane derives running since from the primary worker's observed process
/// start and the current readings from its fresh sample, and that only viewers may read them. The tests run against
/// in-memory SQLite.
/// </summary>
public sealed class ServerMetricsReadModelTests
{
    private const long Gib = 1024L * 1024L * 1024L;

    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_100);

    private static readonly Instant ProcessStart = Instant.FromUnixTimeSeconds(1_700_000_030);

    /// <summary>
    /// Running since is the observed process start, never the deployment's start timestamp.
    /// </summary>
    /// <returns>A task completing after the reading was verified.</returns>
    [Fact]
    public async Task RunningSince_IsTheObservedProcessStart()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), ProcessStart);

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Equal(ProcessStart, dto.RunningSince);
        Assert.NotEqual(deployment.TimeStampStarted, dto.RunningSince);
        Assert.Null(dto.RunningSinceUnavailableReason);
    }

    /// <summary>
    /// A live worker without an observed game process gives server-not-running instead of a running since.
    /// </summary>
    /// <returns>A task completing after the reading was verified.</returns>
    [Fact]
    public async Task WorkerWithoutProcess_GivesServerNotRunning()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), null);

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Null(dto.RunningSince);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.RunningSinceUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.PlayersUnavailableReason);
    }

    /// <summary>
    /// A fresh sample of the active session returns its CPU, memory and players, the receipt time and the stale
    /// threshold.
    /// </summary>
    /// <returns>A task completing after the readings were verified.</returns>
    [Fact]
    public async Task FreshSample_ReturnsTheSampleValues()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), ProcessStart);
        var receivedAt = Now - Duration.FromSeconds(10);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey, receivedAt.ToDateTimeOffset(), 42.5d, 3 * Gib, 8 * Gib, 7, 32));

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Equal(deployment.GameServerFk, dto.GameServerId);
        Assert.Equal(receivedAt, dto.AsOf);
        Assert.Equal(45, dto.StaleAfterSeconds);
        Assert.Equal(42.5d, dto.CpuPercent);
        Assert.Null(dto.CpuUnavailableReason);
        Assert.Equal(3 * Gib, dto.MemoryUsedBytes);
        Assert.Equal(8 * Gib, dto.MemoryTotalBytes);
        Assert.Null(dto.MemoryUnavailableReason);
        Assert.Equal(7, dto.PlayerCount);
        Assert.Equal(32, dto.PlayerCapacity);
        Assert.Null(dto.PlayersUnavailableReason);
    }

    /// <summary>
    /// A measured player count of zero stays zero and is not reported as unavailable.
    /// </summary>
    /// <returns>A task completing after the reading was verified.</returns>
    [Fact]
    public async Task FreshZeroPlayers_StaysZero()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), ProcessStart);
        var receivedAt = Now - Duration.FromSeconds(10);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey, receivedAt.ToDateTimeOffset(), 0d, Gib, 8 * Gib, 0, 32));

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Equal(0, dto.PlayerCount);
        Assert.Null(dto.PlayersUnavailableReason);
        Assert.Equal(0d, dto.CpuPercent);
        Assert.Null(dto.CpuUnavailableReason);
    }

    /// <summary>
    /// A worker-reported reason is returned as the reason without a value.
    /// </summary>
    /// <returns>A task completing after the readings were verified.</returns>
    [Fact]
    public async Task WorkerReason_IsReturnedWithoutValue()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(5), ProcessStart);
        var receivedAt = Now - Duration.FromSeconds(10);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey,
                receivedAt.ToDateTimeOffset(),
                null,
                null,
                null,
                null,
                cpuReason: WorkerMetricUnavailableReasons.Measuring,
                memoryReason: WorkerMetricUnavailableReasons.QueryFailed,
                playersReason: WorkerMetricUnavailableReasons.GameDoesNotReport));

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Null(dto.CpuPercent);
        Assert.Equal(WorkerMetricUnavailableReasons.Measuring, dto.CpuUnavailableReason);
        Assert.Null(dto.MemoryUsedBytes);
        Assert.Null(dto.MemoryTotalBytes);
        Assert.Equal(WorkerMetricUnavailableReasons.QueryFailed, dto.MemoryUnavailableReason);
        Assert.Null(dto.PlayerCount);
        Assert.Equal(WorkerMetricUnavailableReasons.GameDoesNotReport, dto.PlayersUnavailableReason);
    }

    /// <summary>
    /// Without an active deployment every reading is server-not-running.
    /// </summary>
    /// <returns>A task completing after the readings were verified.</returns>
    [Fact]
    public async Task NoActiveDeployment_GivesServerNotRunningForEveryReading()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(seed.Server.PrimaryKey, Now, CancellationToken.None);

        Assert.Null(dto.AsOf);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.RunningSinceUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.CpuUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.MemoryUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.ServerNotRunning, dto.PlayersUnavailableReason);
        Assert.Null(dto.RunningSince);
        Assert.Null(dto.CpuPercent);
        Assert.Null(dto.MemoryUsedBytes);
        Assert.Null(dto.PlayerCount);
    }

    /// <summary>
    /// A heartbeat older than the stale threshold gives worker-offline even with a fresh sample.
    /// </summary>
    /// <returns>A task completing after the readings were verified.</returns>
    [Fact]
    public async Task StaleHeartbeat_GivesWorkerOffline()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var deployment = (await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true))
            .Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, deployment, Now - Duration.FromSeconds(61), ProcessStart);
        var receivedAt = Now - Duration.FromSeconds(10);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(
                deployment.PrimaryKey, receivedAt.ToDateTimeOffset(), 10d, Gib, 8 * Gib, 3, 32));

        var dto = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(deployment.GameServerFk, Now, CancellationToken.None);

        Assert.Equal(WorkerMetricUnavailableReasons.WorkerOffline, dto.RunningSinceUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.WorkerOffline, dto.CpuUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.WorkerOffline, dto.MemoryUnavailableReason);
        Assert.Equal(WorkerMetricUnavailableReasons.WorkerOffline, dto.PlayersUnavailableReason);
        Assert.Null(dto.CpuPercent);
        Assert.Null(dto.PlayerCount);
    }

    /// <summary>
    /// A viewer receives the readings, while a member without the view claim and an unknown server give not found.
    /// </summary>
    /// <returns>A task completing after the three calls were verified.</returns>
    [Fact]
    public async Task GetCurrent_RequiresTheServerViewClaim()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext, seed.Deployment!, Now - Duration.FromSeconds(5), ProcessStart);
        var service = ServerMetricsTestData.Create(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
        var outsider = HostingTestData.Principal(seed.User);

        var dto = await service.GetCurrentAsync(seed.Server.PrimaryKey, viewer, Now, CancellationToken.None);
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.GetCurrentAsync(seed.Server.PrimaryKey, outsider, Now, CancellationToken.None));
        await Assert.ThrowsAsync<HostingNotFoundException>(
            () => service.GetCurrentAsync(Guid.NewGuid(), viewer, Now, CancellationToken.None));

        Assert.Equal(ProcessStart, dto.RunningSince);
    }
}
