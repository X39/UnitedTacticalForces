using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.ServerMetrics;

/// <summary>
/// Database-backed storage of server metrics reported by dedicated workers.
/// </summary>
/// <remarks>
/// Only the primary host's samples (the game-server service with ordinal 0) are stored, so a multi-host deployment
/// never mixes machines into one reading (UI-SPEC M5). Attribution comes only from the
/// <see cref="ServerMetricSampleSource"/> that the caller builds from the authenticated worker; the payload's
/// deployment identifier is only checked against it. Samples are not deduplicated because the worker never resends
/// a sample.
/// <para>
/// History is bucketed by the API receipt time, not by the worker's clock, so a worker with a wrong clock cannot
/// write into the past or the future. Each accepted sample is merged into the 15, 60 and 300 second rollup rows
/// with count, sum, minimum and maximum. The minimum and maximum are kept so the peak of a spike survives
/// downsampling (D-05).
/// </para>
/// <para>
/// The current readings are decided here, not in the browser (D-09). The active deployment is the newest deployment
/// of the server that is not resolved; its primary worker is the newest not stopped worker of the game-server service
/// with ordinal 0. The worker is live when it is online and its heartbeat is younger than
/// <see cref="LifecycleWorkerOptions.StaleThreshold"/>. A snapshot only counts when it belongs to the active
/// deployment, and it is stale when it is older than <see cref="ServerMetricsOptions.StaleAfter"/> at read time.
/// </para>
/// <para>
/// Reason precedence for CPU and memory: server-not-running, worker-offline, measuring (no counted snapshot), stale,
/// then the snapshot's own value or reason. Players and running since insert server-not-running for a worker that
/// observes no game process after worker-offline. A reading therefore carries a value only when every condition
/// holds, and never a value together with a reason.
/// </para>
/// </remarks>
/// <param name="dbContext">The scoped database context the snapshots and buckets are stored in.</param>
/// <param name="authorizationService">The rules that decide who may view a server.</param>
/// <param name="options">The metrics settings.</param>
/// <param name="workerOptions">The worker health settings that define when a heartbeat is stale.</param>
[Scoped<ServerMetricsService, IServerMetricsService>]
internal sealed class ServerMetricsService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    IOptions<ServerMetricsOptions> options,
    IOptions<LifecycleWorkerOptions> workerOptions) : IServerMetricsService
{
    private const string PrimaryServiceIdentifier = "game-server";

    private readonly ServerMetricsOptions _options = options.Value;

    private readonly LifecycleWorkerOptions _workerOptions = workerOptions.Value;

    /// <inheritdoc />
    public async Task<GameServerMetricsDto> GetCurrentAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.View)
            || !await dbContext.GameServers
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
            throw new HostingNotFoundException("The game server was not found.");

        return await EvaluateCurrentAsync(gameServerId, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GameServerMetricsDto> EvaluateCurrentAsync(
        Guid gameServerId,
        Instant now,
        CancellationToken cancellationToken)
    {
        var deploymentId = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null)
            .OrderByDescending(q => q.TimeStampCreated)
            .Select(q => (Guid?) q.PrimaryKey)
            .FirstOrDefaultAsync(cancellationToken);
        LifecycleWorker? worker = null;
        ServerMetricSnapshot? snapshot = null;
        if (deploymentId is { } activeDeploymentId)
        {
            worker = await dbContext.LifecycleWorkers
                .AsNoTracking()
                .Where(q => q.DeploymentFk == activeDeploymentId
                            && q.ServiceIdentifier == PrimaryServiceIdentifier
                            && q.ServiceOrdinal == 0
                            && q.TimeStampStopped == null)
                .OrderByDescending(q => q.TimeStampCreated)
                .FirstOrDefaultAsync(cancellationToken);
            snapshot = await dbContext.ServerMetricSnapshots
                .AsNoTracking()
                .Where(q => q.GameServerFk == gameServerId && q.DeploymentFk == activeDeploymentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var hasDeployment = deploymentId is not null;
        var workerLive = worker is { Status: EWorkerStatus.Online, LastHeartbeatAt: { } heartbeat }
                         && now - heartbeat < Duration.FromTimeSpan(_workerOptions.StaleThreshold);
        var processStartedAt = workerLive ? worker!.ProcessStartedAt : null;
        var snapshotStale = snapshot is not null
                            && now - snapshot.ReceivedAt > Duration.FromTimeSpan(_options.StaleAfter);

        // The precedence lives only in this function, so CPU, memory, players and running since cannot disagree.
        string? Gate(bool needsProcess)
        {
            if (!hasDeployment) return WorkerMetricUnavailableReasons.ServerNotRunning;
            if (!workerLive) return WorkerMetricUnavailableReasons.WorkerOffline;
            if (needsProcess && processStartedAt is null) return WorkerMetricUnavailableReasons.ServerNotRunning;
            if (snapshot is null) return WorkerMetricUnavailableReasons.Measuring;
            return snapshotStale ? WorkerMetricUnavailableReasons.Stale : null;
        }

        var machineGate = Gate(false);
        var playersGate = Gate(true);
        var runningSinceReason = !hasDeployment
            ? WorkerMetricUnavailableReasons.ServerNotRunning
            : !workerLive
                ? WorkerMetricUnavailableReasons.WorkerOffline
                : processStartedAt is null
                    ? WorkerMetricUnavailableReasons.ServerNotRunning
                    : null;

        double? cpu = null;
        var cpuReason = machineGate;
        long? used = null;
        long? total = null;
        var memoryReason = machineGate;
        int? players = null;
        int? capacity = null;
        var playersReason = playersGate;
        if (snapshot is not null)
        {
            if (machineGate is null)
            {
                (cpu, cpuReason) = Reading(snapshot.CpuPercent, snapshot.CpuUnavailableReason);
                if (snapshot.MemoryUsedBytes is { } u && snapshot.MemoryTotalBytes is { } t)
                {
                    used         = u;
                    total        = t;
                    memoryReason = null;
                }
                else
                {
                    memoryReason = snapshot.MemoryUnavailableReason ?? WorkerMetricUnavailableReasons.Measuring;
                }
            }

            if (playersGate is null)
            {
                (players, playersReason) = Reading(snapshot.PlayerCount, snapshot.PlayersUnavailableReason);
                capacity                 = players is null ? null : snapshot.PlayerCapacity;
            }
        }

        return new GameServerMetricsDto
        {
            GameServerId                  = gameServerId,
            AsOf                          = snapshot?.ReceivedAt,
            StaleAfterSeconds             = (int) _options.StaleAfter.TotalSeconds,
            RunningSince                  = runningSinceReason is null ? processStartedAt : null,
            RunningSinceUnavailableReason = runningSinceReason,
            CpuPercent                    = cpu,
            CpuUnavailableReason          = cpuReason,
            MemoryUsedBytes               = used,
            MemoryTotalBytes              = total,
            MemoryUnavailableReason       = memoryReason,
            PlayerCount                   = players,
            PlayerCapacity                = capacity,
            PlayersUnavailableReason      = playersReason,
        };
    }

    /// <inheritdoc />
    public async Task<GameServerMetricsHistoryDto> GetHistoryAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        EServerMetricsRange range,
        Instant now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.View)
            || !await dbContext.GameServers
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == gameServerId, cancellationToken))
            throw new HostingNotFoundException("The game server was not found.");

        var resolution = ServerMetricResolutions.For(range);
        var from = now - ServerMetricResolutions.Span(range);

        // Only rows that exist become points: a window the worker never reported stays absent so the consumer breaks
        // its line (D-09). The window filter and ordering run in memory because SQLite cannot compare or order
        // instant columns; the tier of one server is bounded by its retention.
        var buckets = (await dbContext.ServerMetricBuckets
                .AsNoTracking()
                .Where(q => q.GameServerFk == gameServerId && q.ResolutionSeconds == resolution)
                .ToListAsync(cancellationToken))
            .Where(q => q.BucketStart >= from && q.BucketStart <= now)
            .OrderBy(q => q.BucketStart)
            .ToList();
        return new GameServerMetricsHistoryDto
        {
            GameServerId     = gameServerId,
            Range            = ServerMetricResolutions.Format(range),
            From             = from,
            To               = now,
            BucketSeconds    = resolution,
            MemoryTotalBytes = buckets.LastOrDefault(q => q.MemoryTotalBytes is not null)?.MemoryTotalBytes,
            PlayerCapacity   = buckets.LastOrDefault(q => q.PlayerCapacity is not null)?.PlayerCapacity,
            Points = buckets
                .Select(q => new ServerMetricPointDto
                {
                    BucketStart = q.BucketStart,
                    Cpu         = Aggregate(q.CpuCount, q.CpuSum, q.CpuMin, q.CpuMax),
                    MemoryUsed  = Aggregate(q.MemoryCount, q.MemoryUsedSum, q.MemoryUsedMin, q.MemoryUsedMax),
                    Players     = Aggregate(q.PlayersCount, q.PlayersSum, q.PlayersMin, q.PlayersMax),
                })
                .ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<int> PruneAsync(Instant now, CancellationToken cancellationToken)
    {
        var removed = 0;
        foreach (var resolution in ServerMetricResolutions.All)
        {
            var threshold = now - Duration.FromTimeSpan(RetentionOf(resolution));
            removed += await dbContext.ServerMetricBuckets
                .Where(q => q.ResolutionSeconds == resolution && q.BucketStart < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return removed;
    }

    /// <summary>
    /// Gets how long the rollup rows of one tier are kept.
    /// </summary>
    /// <param name="resolutionSeconds">The tier resolution in seconds.</param>
    /// <returns>The configured retention of the tier.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resolutionSeconds"/> is not a stored tier.</exception>
    private TimeSpan RetentionOf(int resolutionSeconds)
        => resolutionSeconds switch
        {
            ServerMetricResolutions.Raw         => _options.RawRetention,
            ServerMetricResolutions.Minute      => _options.MinuteRetention,
            ServerMetricResolutions.FiveMinutes => _options.FiveMinuteRetention,
            _                                   => throw new ArgumentOutOfRangeException(nameof(resolutionSeconds)),
        };

    /// <summary>
    /// Builds the aggregate of one metric, or nothing when no sample of the bucket carried the metric.
    /// </summary>
    /// <param name="count">The number of samples that carried the metric.</param>
    /// <param name="sum">The sum of those samples.</param>
    /// <param name="min">The lowest sample, or <see langword="null"/> without samples.</param>
    /// <param name="max">The highest sample, or <see langword="null"/> without samples.</param>
    /// <returns>The aggregate with a two-decimal average, or <see langword="null"/> for a zero count.</returns>
    private static ServerMetricAggregateDto? Aggregate(int count, double sum, double? min, double? max)
        => count > 0 && min is { } minimum && max is { } maximum
            ? new ServerMetricAggregateDto
            {
                Average = Math.Round(sum / count, 2),
                Minimum = minimum,
                Maximum = maximum,
            }
            : null;

    /// <summary>
    /// Turns one stored value and reason pair into exactly one of them, treating a pair with neither as measuring.
    /// </summary>
    /// <typeparam name="T">The value type of the reading.</typeparam>
    /// <param name="value">The stored value, or <see langword="null"/>.</param>
    /// <param name="reason">The stored reason, or <see langword="null"/>.</param>
    /// <returns>The value with no reason, or no value with a reason.</returns>
    private static (T? Value, string? Reason) Reading<T>(T? value, string? reason)
        where T : struct
        => value is not null
            ? (value, null)
            : (null, reason ?? WorkerMetricUnavailableReasons.Measuring);

    /// <inheritdoc />
    /// <exception cref="WorkerProtocolValidationException">
    /// The sample violates the contract, belongs to another deployment, lies further in the future than the
    /// allowed skew, or is older than the stale limit.
    /// </exception>
    public async Task<ServerMetricsIngestResult> IngestAsync(
        ServerMetricSampleSource source,
        WorkerMetricsSample sample,
        Instant receivedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sample);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(sample, new ValidationContext(sample), results, validateAllProperties: true))
        {
            throw new WorkerProtocolValidationException(
                $"Metrics sample is invalid: {results.FirstOrDefault()?.ErrorMessage}");
        }

        if (sample.DeploymentId != source.DeploymentId)
            throw new WorkerProtocolValidationException("Metrics sample belongs to another deployment.");
        var sampledAt = Instant.FromDateTimeOffset(sample.SampledAt);
        if (sampledAt > receivedAt + Duration.FromTimeSpan(_options.MaxSampleSkew))
            throw new WorkerProtocolValidationException("Metrics sample lies too far in the future.");
        if (sampledAt < receivedAt - Duration.FromTimeSpan(_options.StaleAfter))
            throw new WorkerProtocolValidationException("Metrics sample is too old.");
        if (!source.IsPrimaryHost)
            return new ServerMetricsIngestResult(false, source.GameServerId);

        try
        {
            await StoreAsync(source, sample, receivedAt, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent first insert of the same snapshot or bucket key won the race; the whole ingest is
            // repeated once on a clean change tracker so it merges into the row the other writer created.
            dbContext.ChangeTracker.Clear();
            await StoreAsync(source, sample, receivedAt, cancellationToken);
        }

        return new ServerMetricsIngestResult(true, source.GameServerId);
    }

    private async Task StoreAsync(
        ServerMetricSampleSource source,
        WorkerMetricsSample sample,
        Instant receivedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await UpsertSnapshotAsync(source, sample, receivedAt, cancellationToken);
        foreach (var resolution in ServerMetricResolutions.All)
        {
            var bucketStart = ServerMetricResolutions.Floor(receivedAt, resolution);
            var bucket = await dbContext.ServerMetricBuckets.SingleOrDefaultAsync(
                q => q.GameServerFk == source.GameServerId
                     && q.ResolutionSeconds == resolution
                     && q.BucketStart == bucketStart,
                cancellationToken);
            if (bucket is null)
            {
                bucket = new ServerMetricBucket
                {
                    PrimaryKey        = Guid.NewGuid(),
                    GameServerFk      = source.GameServerId,
                    ResolutionSeconds = resolution,
                    BucketStart       = bucketStart,
                };
                dbContext.ServerMetricBuckets.Add(bucket);
            }

            Merge(bucket, sample);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpsertSnapshotAsync(
        ServerMetricSampleSource source,
        WorkerMetricsSample sample,
        Instant receivedAt,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.ServerMetricSnapshots
            .SingleOrDefaultAsync(q => q.GameServerFk == source.GameServerId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new ServerMetricSnapshot
            {
                PrimaryKey   = Guid.NewGuid(),
                GameServerFk = source.GameServerId,
            };
            dbContext.ServerMetricSnapshots.Add(snapshot);
        }

        snapshot.DeploymentFk             = source.DeploymentId;
        snapshot.WorkerFk                 = source.WorkerId;
        snapshot.ReceivedAt               = receivedAt;
        snapshot.WorkerSampledAt          = sample.SampledAt.ToUniversalTime();
        snapshot.CpuPercent               = sample.CpuPercent;
        snapshot.CpuUnavailableReason     = sample.CpuUnavailableReason;
        snapshot.MemoryUsedBytes          = sample.MemoryUsedBytes;
        snapshot.MemoryTotalBytes         = sample.MemoryTotalBytes;
        snapshot.MemoryUnavailableReason  = sample.MemoryUnavailableReason;
        snapshot.PlayerCount              = sample.PlayerCount;
        snapshot.PlayerCapacity           = sample.PlayerCapacity;
        snapshot.PlayersUnavailableReason = sample.PlayersUnavailableReason;
    }

    private static void Merge(ServerMetricBucket bucket, WorkerMetricsSample sample)
    {
        if (sample.CpuPercent is { } cpu)
        {
            bucket.CpuCount++;
            bucket.CpuSum += cpu;
            bucket.CpuMin =  bucket.CpuMin is { } cpuMin ? Math.Min(cpuMin, cpu) : cpu;
            bucket.CpuMax =  bucket.CpuMax is { } cpuMax ? Math.Max(cpuMax, cpu) : cpu;
        }

        if (sample.MemoryUsedBytes is { } used && sample.MemoryTotalBytes is { } total)
        {
            bucket.MemoryCount++;
            bucket.MemoryUsedSum += used;
            bucket.MemoryUsedMin =  bucket.MemoryUsedMin is { } usedMin ? Math.Min(usedMin, used) : used;
            bucket.MemoryUsedMax =  bucket.MemoryUsedMax is { } usedMax ? Math.Max(usedMax, used) : used;
            bucket.MemoryTotalBytes = total;
        }

        if (sample.PlayerCount is { } players)
        {
            bucket.PlayersCount++;
            bucket.PlayersSum += players;
            bucket.PlayersMin =  bucket.PlayersMin is { } playersMin ? Math.Min(playersMin, players) : players;
            bucket.PlayersMax =  bucket.PlayersMax is { } playersMax ? Math.Max(playersMax, players) : players;
        }

        if (sample.PlayerCapacity is { } capacity)
            bucket.PlayerCapacity = capacity;
    }
}
