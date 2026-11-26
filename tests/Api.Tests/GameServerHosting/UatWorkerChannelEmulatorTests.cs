using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Api.Services.UpdateStreamService;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the UAT-only worker emulation feeds the seeded metrics server through the real worker channel handler,
/// ends by itself when the channel refuses it, stays idle without the scenario or while the control plane is paused, and
/// is registered only in Development with the UAT scenarios flag. The tests run the real handler, metrics route, metrics
/// service and publisher over a loopback socket.
/// </summary>
public sealed class UatWorkerChannelEmulatorTests
{
    /// <summary>
    /// The interval of both frame kinds; slow enough to stay below the channel frame rate limit.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How long a test waits for the emulation before it fails.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The default emulated server of these tests: the metrics scenario with the synthetic feed.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, EUatPlayerProfile> MetricsOnly =
        new Dictionary<string, EUatPlayerProfile>
        {
            [UatFleetScenarioSeeder.MetricsScenarioTitle] = EUatPlayerProfile.Synthetic,
        };

    /// <summary>
    /// The title of a seeded running server that the several-sessions tests give the no-players feed.
    /// </summary>
    private const string EmptyServerTitle = "UAT 2 Running";

    /// <summary>
    /// The title of a seeded running server that the several-sessions tests give the few-players feed.
    /// </summary>
    private const string FewPlayersServerTitle = UatFleetScenarioSeeder.CostingScenarioTitle;

    /// <summary>
    /// The emulator's heartbeat and metrics frames create the snapshot and the 15 second bucket through the handler,
    /// advance the worker heartbeat and make the current readings available.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Emulation_FeedsTheServerThroughTheChannelHandler()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var stream = new GatedUpdateStream();
        await using var host = await CreateHostAsync(store, stream);
        var (serverId, seededHeartbeat) = await SeedAsync(store);
        var emulator = CreateEmulator(host, paused: false, new RecordingLogger());

        await emulator.StartAsync(CancellationToken.None);
        await stream.FirstPublished.WaitAsync(Timeout);

        // The handler is parked inside the publisher, so the database is idle while it is inspected.
        Assert.Equal(1, await store.DbContext.ServerMetricSnapshots.CountAsync());
        Assert.True(await store.DbContext.ServerMetricBuckets.CountAsync(q => q.ResolutionSeconds == 15) >= 1);
        var worker = await store.DbContext.LifecycleWorkers.AsNoTracking()
            .SingleAsync(q => q.Deployment!.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        Assert.True(worker.LastHeartbeatAt > seededHeartbeat);
        var readings = await ServerMetricsTestData.Create(store.DbContext)
            .EvaluateCurrentAsync(serverId, NodaSystemClock.Instance.GetCurrentInstant(), CancellationToken.None);
        Assert.NotNull(readings.RunningSince);
        Assert.NotNull(readings.CpuPercent);
        Assert.NotNull(readings.MemoryUsedBytes);
        Assert.NotNull(readings.PlayerCount);
        Assert.Null(readings.CpuUnavailableReason);
        stream.Release();
        await emulator.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// After the deployment is resolved, the next frame is refused by the handler, the channel closes and the emulation
    /// completes without throwing.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Emulation_EndsByItselfWhenTheDeploymentIsResolved()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var stream = new GatedUpdateStream();
        await using var host = await CreateHostAsync(store, stream);
        await SeedAsync(store);
        var emulator = CreateEmulator(host, paused: false, new RecordingLogger());
        await emulator.StartAsync(CancellationToken.None);
        await stream.FirstPublished.WaitAsync(Timeout);

        var deployment = await store.DbContext.GameServerDeployments.SingleAsync(q => q.TimeStampResolved == null
            && q.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        deployment.Status = EDeploymentStatus.Terminated;
        deployment.PossibleCost = false;
        deployment.TimeStampResolved = NodaSystemClock.Instance.GetCurrentInstant();
        await store.DbContext.SaveChangesAsync();
        stream.Release();
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        Assert.Equal(TaskStatus.RanToCompletion, emulator.ExecuteTask.Status);
        await emulator.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Without the metrics scenario in the database the emulator logs exactly one information line and completes.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NoMetricsScenario_LogsOneInformationLineAndCompletes()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await using var host = await CreateHostAsync(store, new GatedUpdateStream());
        var logger = new RecordingLogger();
        var emulator = CreateEmulator(host, paused: false, logger);

        await emulator.StartAsync(CancellationToken.None);
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        Assert.Equal(TaskStatus.RanToCompletion, emulator.ExecuteTask.Status);
        Assert.Equal(1, logger.InformationCount);
        Assert.Equal(0, logger.WarningCount);
        Assert.Equal(0, await store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// While the control plane is paused the emulator completes without issuing a credential, connecting or storing
    /// anything.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PausedControlPlane_CompletesWithoutConnecting()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await using var host = await CreateHostAsync(store, new GatedUpdateStream());
        var (_, seededHeartbeat) = await SeedAsync(store);
        var emulator = CreateEmulator(host, paused: true, new RecordingLogger());

        await emulator.StartAsync(CancellationToken.None);
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        var worker = await store.DbContext.LifecycleWorkers.AsNoTracking()
            .SingleAsync(q => q.Deployment!.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        Assert.Null(worker.PendingWorkerCredentialHash);
        Assert.Null(worker.WorkerCredentialHash);
        Assert.Equal(seededHeartbeat, worker.LastHeartbeatAt);
        Assert.Equal(0, await store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// A persisted stop command is claimed and reported received and succeeded through the channel, the protocol service
    /// (not the emulator) records the outcome, and the session ends by itself afterwards.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StopCommand_IsClaimedAndSucceededThroughTheChannelAndTheSessionEnds()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(store, stream);
        await SeedAsync(store);
        var commandId = await QueueCommandAsync(store.DbContext, "stop", ELifecycleJobKind.Stop, stopping: true);
        var emulator = CreateEmulator(host, paused: false, new RecordingLogger());

        await emulator.StartAsync(CancellationToken.None);
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        Assert.Equal(TaskStatus.RanToCompletion, emulator.ExecuteTask.Status);
        var command = await store.DbContext.WorkerCommands.AsNoTracking().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Succeeded, command.Status);
        Assert.NotNull(command.TimeStampCompleted);
        Assert.Null(command.ErrorCode);
        var worker = await store.DbContext.LifecycleWorkers.AsNoTracking()
            .SingleAsync(q => q.Deployment!.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        Assert.Null(worker.ProcessId);
        await emulator.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// A command whose action the emulator cannot emulate is reported failed with the fixed unsupported-action code
    /// instead of staying queued.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnsupportedAction_IsReportedFailedWithTheFixedCode()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(store, stream);
        await SeedAsync(store);
        var commandId = await QueueCommandAsync(store.DbContext, "restart", ELifecycleJobKind.Restart, stopping: false);
        var logger = new RecordingLogger();
        var emulator = CreateEmulator(host, paused: false, logger);

        await emulator.StartAsync(CancellationToken.None);
        await logger.WaitForMessageAsync("reported command", Timeout);
        await emulator.StopAsync(CancellationToken.None);

        var command = await store.DbContext.WorkerCommands.AsNoTracking().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Failed, command.Status);
        Assert.Equal(UatEmulatedCommandReports.UnsupportedActionCode, command.ErrorCode);
    }

    /// <summary>
    /// The real Stop job of the seeded metrics server finishes while the emulator runs: the emulated worker acknowledges
    /// the stop command over the real channel and the executor releases the emulated provider resource with a verified
    /// non-billable release.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StopJob_CompletesThroughTheEmulatedWorkerAndTheVerifiedRelease()
    {
        await using var database = await SharedFileDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        var (serverId, seededHeartbeat) = await SeedAsync(seedContext);
        var server = await seedContext.GameServers.SingleAsync(q => q.PrimaryKey == serverId);
        var deployment = await seedContext.GameServerDeployments
            .SingleAsync(q => q.GameServerFk == serverId && q.TimeStampResolved == null);
        // Enqueueing a Stop blocks provisioning before the job starts; the job then runs under the lease of a worker.
        deployment.IsProvisioningBlocked = true;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = serverId,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = server.CreatedByFk,
            Kind = ELifecycleJobKind.Stop,
            ActionIdentifier = GameServerActions.Stop,
            Sequence = 1,
            Priority = 50,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        seedContext.LifecycleJobs.Add(job);
        await seedContext.SaveChangesAsync();
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(null, stream, database.CreateContext);
        // One heartbeat and one metrics frame only, so the emulator is idle on the database apart from claim polls.
        var emulator = CreateEmulator(
            host,
            paused: false,
            new RecordingLogger(),
            emulatorOptions =>
            {
                emulatorOptions.HeartbeatInterval = TimeSpan.FromHours(1);
                emulatorOptions.MetricsInterval = TimeSpan.FromHours(1);
            });
        await emulator.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await using var probe = database.CreateContext();
            return await probe.ServerMetricSnapshots.AnyAsync()
                   && await probe.LifecycleWorkers.AnyAsync(q => q.LastHeartbeatAt > seededHeartbeat);
        });

        await using var executorContext = database.CreateContext();
        using var executorTimeout = new CancellationTokenSource(Timeout);
        await CreateStopExecutor(executorContext).ExecuteAsync(job, executorTimeout.Token);
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        await using var verify = database.CreateContext();
        var persistedDeployment = await verify.GameServerDeployments.AsNoTracking()
            .SingleAsync(q => q.PrimaryKey == deployment.PrimaryKey);
        Assert.Equal(EDeploymentStatus.Stopped, persistedDeployment.Status);
        Assert.False(persistedDeployment.PossibleCost);
        Assert.NotNull(persistedDeployment.TimeStampResolved);
        var resource = await verify.DeploymentResources.AsNoTracking()
            .SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);
        Assert.StartsWith(EmulatedHetznerRuntimeProvider.SeededServerPrefix, resource.ProviderResourceIdentifier);
        Assert.Equal(EDeploymentResourceState.Released, resource.State);
        Assert.False(resource.PossibleCost);
        var command = await verify.WorkerCommands.AsNoTracking().SingleAsync(q => q.LifecycleJobFk == job.PrimaryKey);
        Assert.Equal(GameServerActions.Stop, command.ActionIdentifier);
        Assert.Equal(EWorkerCommandStatus.Succeeded, command.Status);
        Assert.Equal(TaskStatus.RanToCompletion, emulator.ExecuteTask.Status);
        await emulator.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Every listed server gets its own session with its own player feed: the metrics server follows the synthetic
    /// series while the two extra servers report zero and three players.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SeveralServers_EachReceiveSnapshotsOfTheirPlayerProfile()
    {
        await using var database = await SharedFileDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        await SeedAsync(seedContext);
        await AddPrimaryWorkerAsync(seedContext, EmptyServerTitle);
        await AddPrimaryWorkerAsync(seedContext, FewPlayersServerTitle);
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(null, stream, database.CreateContext);
        var emulator = CreateEmulator(
            host,
            paused: false,
            new RecordingLogger(),
            configure: null,
            emulatedWorkers: new Dictionary<string, EUatPlayerProfile>
            {
                [UatFleetScenarioSeeder.MetricsScenarioTitle] = EUatPlayerProfile.Synthetic,
                [EmptyServerTitle] = EUatPlayerProfile.NoPlayers,
                [FewPlayersServerTitle] = EUatPlayerProfile.FewPlayers,
            });

        await emulator.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await using var probe = database.CreateContext();
            return await probe.ServerMetricSnapshots.CountAsync() == 3;
        });
        await emulator.StopAsync(CancellationToken.None);

        await using var verify = database.CreateContext();
        var snapshots = await verify.ServerMetricSnapshots.AsNoTracking()
            .Include(q => q.GameServer)
            .ToDictionaryAsync(q => q.GameServer!.Title);
        Assert.NotNull(snapshots[UatFleetScenarioSeeder.MetricsScenarioTitle].PlayerCount);
        Assert.Equal(0, snapshots[EmptyServerTitle].PlayerCount);
        Assert.Null(snapshots[EmptyServerTitle].PlayersUnavailableReason);
        Assert.Equal(3, snapshots[FewPlayersServerTitle].PlayerCount);
        Assert.All(snapshots.Values, q => Assert.Equal(UatMetricHistorySeeder.PlayerCapacity, q.PlayerCapacity));
    }

    /// <summary>
    /// A listed title without a running deployment and primary worker is skipped with one information line while the
    /// other sessions run.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListedTitleWithoutWorker_IsSkippedWhileTheOtherSessionsRun()
    {
        await using var database = await SharedFileDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        await SeedAsync(seedContext);
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(null, stream, database.CreateContext);
        var logger = new RecordingLogger();
        var emulator = CreateEmulator(
            host,
            paused: false,
            logger,
            configure: null,
            emulatedWorkers: new Dictionary<string, EUatPlayerProfile>
            {
                [UatFleetScenarioSeeder.MetricsScenarioTitle] = EUatPlayerProfile.Synthetic,
                ["UAT 1 Off (no deployment)"] = EUatPlayerProfile.NoPlayers,
                ["Not seeded at all"] = EUatPlayerProfile.FewPlayers,
            });

        await emulator.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await using var probe = database.CreateContext();
            return await probe.ServerMetricSnapshots.AnyAsync();
        });
        await emulator.StopAsync(CancellationToken.None);

        Assert.Equal(2, logger.Count("skips it"));
        Assert.Equal(1, logger.Count("UAT 1 Off (no deployment)"));
        Assert.Equal(1, logger.Count("Not seeded at all"));
        Assert.Equal(0, logger.WarningCount);
    }

    /// <summary>
    /// With no server listed at all the emulator logs exactly one information line and completes.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NoListedServer_LogsOneInformationLineAndCompletes()
    {
        await using var store = await HostingTestStore.CreateAsync();
        await using var host = await CreateHostAsync(store, new GatedUpdateStream());
        await SeedAsync(store);
        var logger = new RecordingLogger();
        var emulator = CreateEmulator(
            host,
            paused: false,
            logger,
            configure: null,
            emulatedWorkers: new Dictionary<string, EUatPlayerProfile>());

        await emulator.StartAsync(CancellationToken.None);
        await emulator.ExecuteTask!.WaitAsync(Timeout);

        Assert.Equal(TaskStatus.RanToCompletion, emulator.ExecuteTask.Status);
        Assert.Equal(1, logger.InformationCount);
        Assert.Equal(0, logger.WarningCount);
        Assert.Equal(0, await store.DbContext.ServerMetricSnapshots.CountAsync());
    }

    /// <summary>
    /// When the stop command of one emulated server succeeded, only that session ends; the other session keeps sending
    /// heartbeats.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StoppingOneSession_DoesNotEndTheOtherSessions()
    {
        await using var database = await SharedFileDatabase.CreateAsync();
        await using var seedContext = database.CreateContext();
        await SeedAsync(seedContext);
        await AddPrimaryWorkerAsync(seedContext, EmptyServerTitle);
        var commandId = await QueueCommandAsync(seedContext, "stop", ELifecycleJobKind.Stop, stopping: true);
        var stream = new GatedUpdateStream();
        stream.Release();
        await using var host = await CreateHostAsync(null, stream, database.CreateContext);
        var logger = new RecordingLogger();
        var emulator = CreateEmulator(
            host,
            paused: false,
            logger,
            configure: null,
            emulatedWorkers: new Dictionary<string, EUatPlayerProfile>
            {
                [UatFleetScenarioSeeder.MetricsScenarioTitle] = EUatPlayerProfile.Synthetic,
                [EmptyServerTitle] = EUatPlayerProfile.NoPlayers,
            });

        await emulator.StartAsync(CancellationToken.None);
        await logger.WaitForMessageAsync("reported command", Timeout);
        Instant? heartbeatAfterStop = null;
        await WaitUntilAsync(async () =>
        {
            await using var probe = database.CreateContext();
            var heartbeat = (await probe.LifecycleWorkers.AsNoTracking()
                .SingleAsync(q => q.Deployment!.GameServer!.Title == EmptyServerTitle)).LastHeartbeatAt;
            heartbeatAfterStop ??= heartbeat;
            return heartbeat > heartbeatAfterStop;
        });

        Assert.False(emulator.ExecuteTask!.IsCompleted);
        await emulator.StopAsync(CancellationToken.None);
        await using var verify = database.CreateContext();
        var command = await verify.WorkerCommands.AsNoTracking().SingleAsync(q => q.PrimaryKey == commandId);
        Assert.Equal(EWorkerCommandStatus.Succeeded, command.Status);
    }

    /// <summary>
    /// Gives the seeded running server with the given title an online primary worker, as the seeder does for the
    /// metrics scenario.
    /// </summary>
    /// <param name="dbContext">The context the worker is saved through.</param>
    /// <param name="title">The title of the seeded server with a running deployment.</param>
    /// <returns>A task that completes when the worker is saved.</returns>
    private static async Task AddPrimaryWorkerAsync(DatabaseContext dbContext, string title)
    {
        var deployment = await dbContext.GameServerDeployments
            .SingleAsync(q => q.TimeStampResolved == null && q.GameServer!.Title == title);
        var now = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(10);
        dbContext.LifecycleWorkers.Add(new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ServiceIdentifier = "game-server",
            ServiceOrdinal = 0,
            Identity = "uat-emulated-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            BootstrapCredentialExpiresAt = now,
            BootstrapCredentialConsumedAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Polls a condition until it holds.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <returns>A task that completes once the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within the test timeout.</exception>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        while (!await condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }
        }
    }

    /// <summary>
    /// Creates a lifecycle executor over the context with the emulated Hetzner provider and the Arma 3 definition, the
    /// way the UAT stack runs it, with a fast worker poll.
    /// </summary>
    /// <param name="dbContext">The context the executor works on.</param>
    /// <returns>The executor.</returns>
    private static LifecycleJobExecutor CreateStopExecutor(DatabaseContext dbContext)
        => new(
            dbContext,
            new GameServerDefinitionCatalog([new Arma3GameServerDefinitionRegistration()]),
            [new EmulatedHetznerRuntimeProvider()],
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            new NullBootstrapRenderer(),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            new GameServerContentSnapshotResolver(dbContext),
            new GameServerConfigurationService(dbContext),
            new NullServerFileStorage(),
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            new NullCostSliceService(),
            new HostingAuditWriter(),
            new TestGameServerSecretService(),
            Options.Create(new LifecycleWorkerOptions { IdlePollInterval = TimeSpan.FromMilliseconds(20) }),
            Options.Create(new HostingFinanceOptions()));

    /// <summary>
    /// A file-backed SQLite database with its own connection per context, so the control plane and the emulator can run
    /// concurrently without sharing one connection or one transaction.
    /// </summary>
    private sealed class SharedFileDatabase : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<NodaTimeSqliteApiDbContext> _options;

        /// <summary>
        /// Wraps the database file and the options every context is created with.
        /// </summary>
        /// <param name="path">The database file path.</param>
        /// <param name="options">The context options pointing at the file.</param>
        private SharedFileDatabase(string path, DbContextOptions<NodaTimeSqliteApiDbContext> options)
        {
            _path = path;
            _options = options;
        }

        /// <summary>
        /// Creates a database file with the schema ensured and write-ahead logging enabled.
        /// </summary>
        /// <returns>A task resolving to the database; dispose it to delete the file.</returns>
        public static async Task<SharedFileDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"utf-uat-emulator-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                DefaultTimeout = 30,
                Pooling = false,
            }.ToString();
            var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(connectionString).Options;
            var database = new SharedFileDatabase(path, options);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            return database;
        }

        /// <summary>
        /// Creates a context on a connection of its own.
        /// </summary>
        /// <returns>A new context; dispose it when done.</returns>
        public DatabaseContext CreateContext() => new NodaTimeSqliteApiDbContext(_options);

        /// <summary>
        /// Deletes the database file and its write-ahead log.
        /// </summary>
        /// <returns>A completed task.</returns>
        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A bootstrap renderer double; the Stop job never renders a payload.</summary>
    private sealed class NullBootstrapRenderer : IWorkerBootstrapPayloadRenderer
    {
        /// <inheritdoc />
        public WorkerBootstrapPayload Render(
            WorkerBootstrapConfiguration bootstrap,
            Guid serverId,
            string gameIdentifier,
            IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
            => new("#cloud-config\nwrite_files: []");
    }

    /// <summary>A server file storage double; the Stop job never touches files.</summary>
    private sealed class NullServerFileStorage : IServerFileStorage
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServerFileEntry>>([]);

        /// <inheritdoc />
        public Task<ServerFileDownload> OpenReadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ServerFileEntry> UploadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, Stream content,
            bool overwrite, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ServerFileEntry> CreateDirectoryAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, bool recursive,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A cost slice service double; the Stop job charges nothing.</summary>
    private sealed class NullCostSliceService : IHostingCostSliceService
    {
        /// <inheritdoc />
        public Task ReserveInitialSliceAsync(
            Guid deploymentId, string providerIdentifier, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task<bool> CanReserveInitialSliceAsync(
            Guid costOwnerId, decimal? estimatedHourlyCostEur, string providerIdentifier, Instant now,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        /// <inheritdoc />
        public Task CommitInitialReservationAsync(
            Guid deploymentId, string providerIdentifier, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task ReleaseInitialReservationAsync(
            Guid deploymentId, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task AuthorizeNextSliceAsync(
            Guid deploymentId, string providerIdentifier, bool enforceAffordability,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// The emulator is registered only in the Development environment with the UAT scenarios flag set to true.
    /// </summary>
    /// <param name="environmentName">The hosting environment name.</param>
    /// <param name="flag">The configured flag value, or <see langword="null"/> when absent.</param>
    /// <param name="registered">Whether the emulator must be registered.</param>
    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Development", null, false)]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    public void Registration_RequiresDevelopmentAndTheUatFlag(string environmentName, string? flag, bool registered)
    {
        var values = new Dictionary<string, string?> { [UatFleetScenarioSeeder.ConfigurationKey] = flag };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = new TestHostEnvironment(environmentName);
        var services = new ServiceCollection();

        var result = UatWorkerChannelEmulator.AddTo(services, configuration, environment);

        Assert.Equal(registered, result);
        Assert.Equal(
            registered,
            services.Any(q => q.ServiceType == typeof(IHostedService)
                              && q.ImplementationType == typeof(UatWorkerChannelEmulator)));
    }

    /// <summary>
    /// Creates a channel host over the store with the metrics route, service and publisher registered.
    /// </summary>
    /// <param name="store">The store the host works on; null when <paramref name="contextFactory"/> supplies contexts.</param>
    /// <param name="stream">The update stream double the publisher sends to.</param>
    /// <param name="contextFactory">
    /// An optional factory that replaces the shared singleton context with one context per scope, for tests whose
    /// control plane and emulator run concurrently.
    /// </param>
    /// <returns>A task resolving to the host.</returns>
    private static Task<WorkerChannelTestHost> CreateHostAsync(
        HostingTestStore? store,
        GatedUpdateStream stream,
        Func<DatabaseContext>? contextFactory = null)
        => WorkerChannelTestHost.CreateAsync(store, configure: services =>
        {
            if (contextFactory is not null)
                services.AddScoped<DatabaseContext>(_ => contextFactory());
            services.AddSingleton(Options.Create(new ServerMetricsOptions()));
            services.AddScoped<IHostingAuthorizationService>(
                q => new HostingAuthorizationService(q.GetRequiredService<DatabaseContext>()));
            services.AddScoped<IServerMetricsService, ServerMetricsService>();
            services.AddScoped<IServerMetricsPublisher, ServerMetricsPublisher>();
            services.AddScoped<IWorkerChannelFrameRoute, MetricsFrameRoute>();
            services.AddSingleton<IUpdateStreamService>(stream);
        });

    /// <summary>
    /// Seeds the UAT scenarios ten minutes in the past and lets the operator view the metrics server, so the publisher
    /// has an audience.
    /// </summary>
    /// <param name="store">The store to seed.</param>
    /// <returns>A task resolving to the metrics server id and the seeded worker heartbeat.</returns>
    private static Task<(Guid ServerId, Instant SeededHeartbeat)> SeedAsync(HostingTestStore store)
        => SeedAsync(store.DbContext);

    /// <summary>
    /// Seeds the UAT scenarios ten minutes in the past and lets the operator view the metrics server, so the publisher
    /// has an audience.
    /// </summary>
    /// <param name="dbContext">The context to seed through.</param>
    /// <returns>A task resolving to the metrics server id and the seeded worker heartbeat.</returns>
    private static async Task<(Guid ServerId, Instant SeededHeartbeat)> SeedAsync(DatabaseContext dbContext)
    {
        var seed = await HostingTestData.SeedServerAsync(dbContext);
        var seededAt = NodaSystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(10);
        await UatFleetScenarioSeeder.SeedAsync(
            dbContext,
            seed.User.PrimaryKey,
            new TestGameServerDefinitionCatalog(),
            new TestGameServerSecretService(),
            now: seededAt);
        var serverId = (await dbContext.GameServers
            .SingleAsync(q => q.Title == UatFleetScenarioSeeder.MetricsScenarioTitle)).PrimaryKey;
        seed.User.Claims!.Add(new X39.UnitedTacticalForces.Database.Authority.Claim
        {
            Identifier = Claims.ResourceBased.Server.View,
            Value = serverId.ToString(),
            Category = "test",
            Title = Claims.ResourceBased.Server.View,
            Description = "test claim",
        });
        await dbContext.SaveChangesAsync();
        return (serverId, seededAt);
    }

    /// <summary>
    /// Creates the emulator under test with fast intervals over the real handler of the host.
    /// </summary>
    /// <param name="host">The host that provides the scope factory and the channel handler.</param>
    /// <param name="paused">Whether the control plane is paused.</param>
    /// <param name="logger">The logger double that records the emulator's log entries.</param>
    /// <param name="configure">An optional hook that adjusts the emulator options after the fast defaults.</param>
    /// <param name="emulatedWorkers">
    /// The servers to emulate; only the metrics scenario when null, because these tests seed one primary worker per
    /// server they use and the automation scenarios would add sessions they do not expect.
    /// </param>
    /// <returns>The emulator, not yet started.</returns>
    private static UatWorkerChannelEmulator CreateEmulator(
        WorkerChannelTestHost host,
        bool paused,
        ILogger<UatWorkerChannelEmulator> logger,
        Action<UatWorkerChannelEmulatorOptions>? configure = null,
        IReadOnlyDictionary<string, EUatPlayerProfile>? emulatedWorkers = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostingControlPlaneState.ConfigurationKey] = paused ? "true" : "false",
            })
            .Build();
        var emulatorOptions = new UatWorkerChannelEmulatorOptions
        {
            StartDelay = TimeSpan.Zero,
            HeartbeatInterval = Interval,
            MetricsInterval = Interval,
            ClaimPollInterval = Interval,
        };
        configure?.Invoke(emulatorOptions);
        return new UatWorkerChannelEmulator(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Services.GetRequiredService<IWorkerChannelHandler>(),
            new HostingControlPlaneState(configuration),
            Options.Create(emulatorOptions),
            logger)
        {
            EmulatedWorkers = emulatedWorkers ?? MetricsOnly,
        };
    }

    /// <summary>
    /// Persists a Running lifecycle job with one Queued worker command for the primary worker of the seeded metrics
    /// server, the way the control plane leaves them for the worker to claim.
    /// </summary>
    /// <param name="dbContext">The context the rows are saved through.</param>
    /// <param name="action">The action identifier of the job and the command.</param>
    /// <param name="kind">The lifecycle job kind matching the action.</param>
    /// <param name="stopping">
    /// Whether the deployment is already blocked and Stopping, as the Stop job leaves it before it waits for the command.
    /// </param>
    /// <returns>A task resolving to the persisted command id.</returns>
    private static async Task<Guid> QueueCommandAsync(
        DatabaseContext dbContext,
        string action,
        ELifecycleJobKind kind,
        bool stopping)
    {
        var deployment = await dbContext.GameServerDeployments
            .Include(q => q.GameServer)
            .SingleAsync(q => q.TimeStampResolved == null
                              && q.GameServer!.Title == UatFleetScenarioSeeder.MetricsScenarioTitle);
        var worker = await dbContext.LifecycleWorkers.SingleAsync(q => q.DeploymentFk == deployment.PrimaryKey);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (stopping)
        {
            deployment.IsProvisioningBlocked = true;
            deployment.Status = EDeploymentStatus.Stopping;
            deployment.Stage = "worker-stop-queued";
        }

        var payload = JsonSerializer.Serialize(
            new WorkerCommandPayload
            {
                ServerId = deployment.GameServerFk,
                DeploymentId = deployment.PrimaryKey,
                GameIdentifier = deployment.GameServer!.GameIdentifier,
                ActionIdentifier = action,
                Workloads =
                [
                    new WorkerWorkloadAssignment
                    {
                        ServiceIdentifier = "game-server",
                        ServiceOrdinal = 0,
                        HostServiceIdentifier = "game-server",
                        HostServiceOrdinal = 0,
                        IsPrimary = true,
                    },
                ],
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = deployment.GameServerFk,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = deployment.GameServer.CreatedByFk,
            Kind = kind,
            ActionIdentifier = action,
            Sequence = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "claimed",
            Attempt = 1,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJobFk = job.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = action,
            Payload = payload,
            Status = EWorkerCommandStatus.Queued,
            Stage = "queued",
            TimeStampCreated = now,
        };
        dbContext.AddRange(job, command);
        await dbContext.SaveChangesAsync();
        return command.PrimaryKey;
    }

    /// <summary>
    /// An update stream double that parks the first publication until it is released, so a test can inspect the
    /// database while the channel handler is idle inside the publisher.
    /// </summary>
    private sealed class GatedUpdateStream : IUpdateStreamService
    {
        private readonly TaskCompletionSource _firstPublished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets a task that completes once the first publication arrived.
        /// </summary>
        /// <value>The task awaited by tests before they inspect the database.</value>
        public Task FirstPublished => _firstPublished.Task;

        /// <summary>
        /// Lets the parked publication and every later one pass.
        /// </summary>
        public void Release() => _released.TrySetResult();

        /// <inheritdoc />
        public ValueTask RegisterAsync(string path, WebSocket webSocket, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <inheritdoc />
        public async ValueTask SendUpdateAsync<T>(string path, T package)
        {
            _firstPublished.TrySetResult();
            await _released.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A logger double that counts entries per level.
    /// </summary>
    private sealed class RecordingLogger : ILogger<UatWorkerChannelEmulator>
    {
        private readonly ConcurrentQueue<string> _messages = new();
        private int _information;
        private int _warning;

        /// <summary>
        /// Counts the entries logged so far that contain the fragment.
        /// </summary>
        /// <param name="fragment">The text the formatted entry must contain.</param>
        /// <returns>The number of matching entries.</returns>
        public int Count(string fragment) => _messages.Count(q => q.Contains(fragment, StringComparison.Ordinal));

        /// <summary>
        /// Waits until an entry that contains the fragment was logged.
        /// </summary>
        /// <param name="fragment">The text the formatted entry must contain.</param>
        /// <param name="timeout">How long to wait before the wait fails.</param>
        /// <returns>A task that completes once such an entry exists.</returns>
        /// <exception cref="TimeoutException">No entry contained the fragment in time.</exception>
        public async Task WaitForMessageAsync(string fragment, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            while (!_messages.Any(q => q.Contains(fragment, StringComparison.Ordinal)))
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(20), cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"No log entry contained '{fragment}'.");
                }
            }
        }

        /// <summary>
        /// Gets the number of information entries logged.
        /// </summary>
        /// <value>The count of entries at <see cref="LogLevel.Information"/>.</value>
        public int InformationCount => Volatile.Read(ref _information);

        /// <summary>
        /// Gets the number of warning entries logged.
        /// </summary>
        /// <value>The count of entries at <see cref="LogLevel.Warning"/>.</value>
        public int WarningCount => Volatile.Read(ref _warning);

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
            if (logLevel == LogLevel.Information)
                Interlocked.Increment(ref _information);
            else if (logLevel == LogLevel.Warning)
                Interlocked.Increment(ref _warning);
        }
    }

    /// <summary>
    /// A hosting environment double that only carries a name.
    /// </summary>
    /// <param name="environmentName">The environment name.</param>
    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = environmentName;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Api.Tests";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = string.Empty;

        /// <inheritdoc />
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
