using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Builders for server metrics tests. The service is constructed only here, so later constructor changes touch one
/// place.
/// </summary>
internal static class ServerMetricsTestData
{
    /// <summary>
    /// Builds a worker metrics sample.
    /// </summary>
    /// <param name="deploymentId">The deployment the sample claims to belong to.</param>
    /// <param name="sampledAt">The worker-side sampling time.</param>
    /// <param name="cpu">The CPU percentage, or <see langword="null"/> when unavailable.</param>
    /// <param name="used">The used memory in bytes, or <see langword="null"/> when unavailable.</param>
    /// <param name="total">The total memory in bytes, or <see langword="null"/> when unavailable.</param>
    /// <param name="players">The player count, or <see langword="null"/> when unavailable.</param>
    /// <param name="capacity">The player capacity, or <see langword="null"/> when unknown.</param>
    /// <param name="cpuReason">The reason the CPU reading is unavailable, if any.</param>
    /// <param name="memoryReason">The reason the memory reading is unavailable, if any.</param>
    /// <param name="playersReason">The reason the player count is unavailable, if any.</param>
    /// <returns>The sample with exactly the given readings and reasons.</returns>
    public static WorkerMetricsSample Sample(
        Guid deploymentId,
        DateTimeOffset sampledAt,
        double? cpu,
        long? used,
        long? total,
        int? players,
        int? capacity = null,
        string? cpuReason = null,
        string? memoryReason = null,
        string? playersReason = null)
        => new()
        {
            DeploymentId             = deploymentId,
            SampledAt                = sampledAt,
            CpuPercent               = cpu,
            CpuUnavailableReason     = cpuReason,
            MemoryUsedBytes          = used,
            MemoryTotalBytes         = total,
            MemoryUnavailableReason  = memoryReason,
            PlayerCount              = players,
            PlayerCapacity           = capacity,
            PlayersUnavailableReason = playersReason,
        };

    /// <summary>
    /// Builds the trusted attribution of a deployment's primary-host worker.
    /// </summary>
    /// <param name="deployment">The deployment the worker belongs to.</param>
    /// <param name="workerId">The authenticated worker.</param>
    /// <returns>A source flagged as primary host.</returns>
    public static ServerMetricSampleSource PrimarySource(GameServerDeployment deployment, Guid workerId)
        => new(deployment.GameServerFk, deployment.PrimaryKey, workerId, true);

    /// <summary>
    /// Constructs the metrics service over a test database context.
    /// </summary>
    /// <param name="dbContext">The test database context.</param>
    /// <param name="options">The settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="workerOptions">The worker health settings, or <see langword="null"/> for the defaults.</param>
    /// <returns>The service under test.</returns>
    public static ServerMetricsService Create(
        DatabaseContext dbContext,
        ServerMetricsOptions? options = null,
        LifecycleWorkerOptions? workerOptions = null)
        => new(
            dbContext,
            new HostingAuthorizationService(dbContext),
            Options.Create(options ?? new ServerMetricsOptions()),
            Options.Create(workerOptions ?? new LifecycleWorkerOptions()));

    /// <summary>
    /// Adds the primary-host worker (game-server service, ordinal 0) of a deployment.
    /// </summary>
    /// <param name="dbContext">The test database context.</param>
    /// <param name="deployment">The deployment the worker belongs to.</param>
    /// <param name="lastHeartbeat">The time of the worker's latest heartbeat.</param>
    /// <param name="processStartedAt">The observed game process start, or <see langword="null"/> without a process.</param>
    /// <param name="status">The worker's reported status.</param>
    /// <param name="stoppedAt">The time the worker was stopped, or <see langword="null"/> while it is not stopped.</param>
    /// <returns>A task resolving to the saved worker.</returns>
    public static async Task<LifecycleWorker> AddPrimaryWorkerAsync(
        DatabaseContext dbContext,
        GameServerDeployment deployment,
        Instant lastHeartbeat,
        Instant? processStartedAt,
        EWorkerStatus status = EWorkerStatus.Online,
        Instant? stoppedAt = null)
    {
        var worker = new LifecycleWorker
        {
            PrimaryKey        = Guid.NewGuid(),
            DeploymentFk      = deployment.PrimaryKey,
            ServiceIdentifier = "game-server",
            ServiceOrdinal    = 0,
            Identity          = "metrics-primary-worker",
            Status            = status,
            LastHeartbeatAt   = lastHeartbeat,
            ProcessStartedAt  = processStartedAt,
            TimeStampCreated  = lastHeartbeat,
            TimeStampUpdated  = lastHeartbeat,
            TimeStampStopped  = stoppedAt,
        };
        dbContext.LifecycleWorkers.Add(worker);
        await dbContext.SaveChangesAsync();
        return worker;
    }

    /// <summary>
    /// Adds the latest metric snapshot of a server.
    /// </summary>
    /// <param name="dbContext">The test database context.</param>
    /// <param name="deployment">The deployment the snapshot claims to belong to.</param>
    /// <param name="workerId">The worker that reported the snapshot.</param>
    /// <param name="receivedAt">The receipt time of the snapshot.</param>
    /// <param name="sample">The readings and reasons stored in the snapshot.</param>
    /// <returns>A task resolving to the saved snapshot.</returns>
    public static async Task<ServerMetricSnapshot> AddSnapshotAsync(
        DatabaseContext dbContext,
        GameServerDeployment deployment,
        Guid workerId,
        Instant receivedAt,
        WorkerMetricsSample sample)
    {
        var snapshot = new ServerMetricSnapshot
        {
            PrimaryKey               = Guid.NewGuid(),
            GameServerFk             = deployment.GameServerFk,
            DeploymentFk             = deployment.PrimaryKey,
            WorkerFk                 = workerId,
            ReceivedAt               = receivedAt,
            WorkerSampledAt          = sample.SampledAt.ToUniversalTime(),
            CpuPercent               = sample.CpuPercent,
            CpuUnavailableReason     = sample.CpuUnavailableReason,
            MemoryUsedBytes          = sample.MemoryUsedBytes,
            MemoryTotalBytes         = sample.MemoryTotalBytes,
            MemoryUnavailableReason  = sample.MemoryUnavailableReason,
            PlayerCount              = sample.PlayerCount,
            PlayerCapacity           = sample.PlayerCapacity,
            PlayersUnavailableReason = sample.PlayersUnavailableReason,
        };
        dbContext.ServerMetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync();
        return snapshot;
    }
}
