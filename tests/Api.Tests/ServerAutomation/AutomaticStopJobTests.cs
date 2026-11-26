using Api.Tests.GameServerHosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-05 proof across the whole pipeline: the stop job the evaluator queues is claimed from the real queue and run
/// by the real lifecycle executor. It completes only through the verified non-billable provider release, and a failed
/// automatic stop stays a failed ordinary stop without ever escalating to a terminate job (D-AUTO-2).
/// </summary>
public sealed class AutomaticStopJobTests
{
    /// <summary>The instant the seeded deployment started.</summary>
    private static readonly Instant Started = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>The first idle reading instant of the idle scenario.</summary>
    private static readonly Instant IdleStart = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>The default warning lead of the automation options.</summary>
    private static readonly Duration Lead = Duration.FromMinutes(10);

    /// <summary>The idle limit in minutes used by the idle scenario.</summary>
    private const int IdleMinutes = 5;

    /// <summary>The lease owner standing in for the control-plane processor.</summary>
    private const string LeaseOwner = "automatic-stop-test-processor";

    /// <summary>
    /// A seeded running deployment with its primary worker, its provider resources and the provider double that
    /// models them.
    /// </summary>
    /// <param name="Seed">The seeded server, owner and deployment.</param>
    /// <param name="Worker">The primary worker of the deployment.</param>
    /// <param name="Provider">The provider double that discovers and releases the seeded resources.</param>
    private sealed record StopFixture(HostingTestSeed Seed, LifecycleWorker Worker, ReleasingRuntimeProvider Provider);

    /// <summary>
    /// Seeds a running deployment with a primary worker and three provider resources (server, primary address and
    /// firewall) that all belong to it.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="configure">The settings change that switches the limit under test on.</param>
    /// <returns>A task resolving to the fixture.</returns>
    private static async Task<StopFixture> SeedAsync(HostingTestStore store, Action<ServerAutomationSettings> configure)
    {
        var db = store.DbContext;
        var seed = await HostingTestData.SeedServerAsync(db, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        await ServerAutomationTestData.SeedSettingsAsync(db, seed.Server, configure);
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            db,
            deployment,
            IdleStart - Duration.FromSeconds(5),
            IdleStart - Duration.FromSeconds(60));
        var resources = new List<RuntimeResource>();
        foreach (var (kind, billable) in new[] { ("server", true), ("primary-ip", true), ("firewall", false) })
        {
            var intent = Guid.NewGuid();
            var identifier = $"{kind}:1";
            db.DeploymentResources.Add(new DeploymentResource
            {
                PrimaryKey                   = Guid.NewGuid(),
                DeploymentFk                 = deployment.PrimaryKey,
                WorkerFk                     = worker.PrimaryKey,
                ProviderIdentifier           = TestRuntimeProvider.Identifier,
                ProvisioningIntentIdentifier = intent.ToString("N"),
                ProviderResourceIdentifier   = identifier,
                Kind                         = kind,
                State                        = EDeploymentResourceState.Allocated,
                PossibleCost                 = billable,
                TimeStampCreated             = Started,
                TimeStampUpdated             = Started,
                TimeStampProvisioningRequested = Started,
                TimeStampProvisioningResolved  = Started,
            });
            resources.Add(new RuntimeResource(
                TestRuntimeProvider.Identifier,
                identifier,
                IsAllocated: true,
                IsPotentiallyCostGenerating: billable,
                ObservedAt: DateTimeOffset.UtcNow,
                ResourceType: kind,
                ProviderState: "allocated",
                Attribution: new RuntimeResourceAttribution(
                    seed.Server.PrimaryKey,
                    deployment.PrimaryKey,
                    intent,
                    worker.PrimaryKey),
                IsPlatformManaged: true,
                HasTrustworthyAttribution: true,
                ProvisioningReadiness: ERuntimeResourceProvisioningReadiness.Ready,
                OperationalState: ERuntimeResourceOperationalState.Operational));
        }

        await db.SaveChangesAsync();
        return new StopFixture(seed, worker, new ReleasingRuntimeProvider(resources));
    }

    /// <summary>Replaces the latest reading and refreshes the heartbeat, like a worker report at the given instant.</summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="fixture">The seeded fixture.</param>
    /// <param name="at">The report instant.</param>
    /// <param name="players">The reported player count.</param>
    /// <returns>A task completing after the reading was saved.</returns>
    private static async Task ReportAsync(HostingTestStore store, StopFixture fixture, Instant at, int players)
    {
        var db = store.DbContext;
        db.ChangeTracker.Clear();
        var worker = await db.LifecycleWorkers.SingleAsync();
        worker.LastHeartbeatAt = at - Duration.FromSeconds(5);
        db.ServerMetricSnapshots.RemoveRange(await db.ServerMetricSnapshots.ToArrayAsync());
        await db.SaveChangesAsync();
        await ServerMetricsTestData.AddSnapshotAsync(
            db,
            fixture.Seed.Deployment!,
            worker.PrimaryKey,
            at,
            ServerMetricsTestData.Sample(
                fixture.Seed.Deployment!.PrimaryKey,
                at.ToDateTimeOffset(),
                10d,
                1,
                8,
                players,
                32));
    }

    /// <summary>
    /// Runs the maximum runtime scenario up to the queued, claimed stop job and seeds the worker outcome of its stop
    /// command.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="fixture">The seeded fixture.</param>
    /// <param name="commandStatus">The outcome the worker reported for the stop command.</param>
    /// <returns>A task resolving to the claimed job.</returns>
    private static async Task<LifecycleJob> QueueMaxRuntimeStopAsync(
        HostingTestStore store,
        StopFixture fixture,
        EWorkerCommandStatus commandStatus)
    {
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        var warnedAt = Started + Duration.FromMinutes(60);
        var warned = await evaluator.EvaluateAsync(warnedAt, CancellationToken.None);
        Assert.Single(warned.RaisedAlerts);
        var stopped = await evaluator.EvaluateAsync(warnedAt + Lead, CancellationToken.None);
        Assert.Equal(1, stopped.JobsQueued);
        return await ClaimAndSeedCommandAsync(store, fixture, commandStatus);
    }

    /// <summary>
    /// Claims the queued stop job from the real queue and seeds the worker outcome of its stop command.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="fixture">The seeded fixture.</param>
    /// <param name="commandStatus">The outcome the worker reported for the stop command.</param>
    /// <returns>A task resolving to the claimed job.</returns>
    private static async Task<LifecycleJob> ClaimAndSeedCommandAsync(
        HostingTestStore store,
        StopFixture fixture,
        EWorkerCommandStatus commandStatus)
    {
        var claimed = await CreateQueue(store.Connection).ClaimNextAsync(LeaseOwner, TimeSpan.FromMinutes(5));
        Assert.NotNull(claimed);
        Assert.Equal(ELifecycleJobKind.Stop, claimed.Kind);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var failed = commandStatus is EWorkerCommandStatus.Failed;
        store.DbContext.ChangeTracker.Clear();
        store.DbContext.WorkerCommands.Add(new WorkerCommand
        {
            PrimaryKey        = Guid.NewGuid(),
            LifecycleJobFk    = claimed.PrimaryKey,
            DeploymentFk      = fixture.Seed.Deployment!.PrimaryKey,
            WorkerFk          = fixture.Worker.PrimaryKey,
            Sequence          = 1,
            Priority          = claimed.Priority,
            ActionIdentifier  = GameServerActions.Stop,
            Status            = commandStatus,
            Stage             = failed ? "failed" : "completed",
            ErrorCode         = failed ? "preservation-failed" : null,
            ErrorMessage      = failed ? "The worker could not preserve the server data." : null,
            TimeStampCreated  = now,
            TimeStampStarted  = now,
            TimeStampCompleted = now,
        });
        await store.DbContext.SaveChangesAsync();
        return claimed;
    }

    /// <summary>
    /// Processes a claimed job like the durable control-plane processor: the executor runs on a fresh context, a clean
    /// run completes the job and a failure is recorded on it, retrying until the job reaches a final status.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="fixture">The seeded fixture.</param>
    /// <param name="claimed">The claimed job.</param>
    /// <returns>A task resolving to the final job status.</returns>
    private static async Task<ELifecycleJobStatus> ProcessAsync(
        HostingTestStore store,
        StopFixture fixture,
        LifecycleJob claimed)
    {
        var queue = CreateQueue(store.Connection);
        var job = claimed;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using (var executorDb = CreateDbContext(store.Connection))
            {
                Exception? failure = null;
                try
                {
                    await CreateExecutor(executorDb, fixture.Provider).ExecuteAsync(job);
                }
                catch (LifecycleExecutionException ex)
                {
                    failure = ex;
                }

                if (failure is null)
                {
                    await queue.CompleteAsync(job.PrimaryKey, LeaseOwner);
                }
                else
                {
                    await queue.FailAsync(
                        job.PrimaryKey,
                        LeaseOwner,
                        failure.Message,
                        TimeSpan.Zero,
                        failure is not PermanentLifecycleExecutionException,
                        (failure as LifecycleExecutionException)?.FailureCode);
                }
            }

            await using var verification = CreateDbContext(store.Connection);
            var status = await verification.LifecycleJobs
                .Where(q => q.PrimaryKey == claimed.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync();
            if (status is ELifecycleJobStatus.Succeeded or ELifecycleJobStatus.Failed)
                return status;
            job = await queue.ClaimNextAsync(LeaseOwner, TimeSpan.FromMinutes(5))
                  ?? throw new InvalidOperationException("The retried job could not be claimed again.");
        }

        throw new InvalidOperationException("The job did not reach a final status.");
    }

    /// <summary>
    /// Asserts the verified non-billable end state every successful automatic stop must reach.
    /// </summary>
    /// <param name="store">The test store that owns the database.</param>
    /// <param name="fixture">The seeded fixture.</param>
    /// <returns>A task completing after the assertions ran.</returns>
    private static async Task AssertStoppedWithVerifiedReleaseAsync(HostingTestStore store, StopFixture fixture)
    {
        await using var db = CreateDbContext(store.Connection);
        var deployment = await db.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == fixture.Seed.Deployment!.PrimaryKey);
        Assert.Equal(EDeploymentStatus.Stopped, deployment.Status);
        Assert.False(deployment.PossibleCost);
        Assert.NotNull(deployment.TimeStampResolved);
        var resources = await db.DeploymentResources
            .Where(q => q.DeploymentFk == deployment.PrimaryKey)
            .ToArrayAsync();
        Assert.Equal(3, resources.Length);
        Assert.All(resources, q =>
        {
            Assert.NotNull(q.TimeStampReleased);
            Assert.Equal(EDeploymentResourceState.Released, q.State);
            Assert.False(q.PossibleCost);
        });
        Assert.Equal(3, fixture.Provider.VerifiedReleases.Count);
        Assert.Equal(
            resources.Select(q => q.ProviderResourceIdentifier!).Order(StringComparer.Ordinal),
            fixture.Provider.VerifiedReleases.Order(StringComparer.Ordinal));
        Assert.Equal(
            3,
            await db.HostingAuditRecords.CountAsync(q => q.Kind == EHostingAuditEventKind.DeploymentResourceReleased));
        var stopped = Assert.Single(
            await db.HostingAuditRecords
                .Where(q => q.Kind == EHostingAuditEventKind.DeploymentStopped)
                .ToArrayAsync());
        Assert.Equal(ECostImpact.StopsCost, stopped.CostImpact);
        var worker = await db.LifecycleWorkers.SingleAsync(q => q.PrimaryKey == fixture.Worker.PrimaryKey);
        Assert.NotNull(worker.TimeStampStopped);
    }

    /// <summary>
    /// A maximum runtime stop completes only once every provider resource is released and verified non-billable.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task MaxRuntimeStop_CompletesThroughVerifiedNonBillableRelease()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var fixture = await SeedAsync(store, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
            q.TimeStampUpdated = Started - Duration.FromDays(1);
        });
        var claimed = await QueueMaxRuntimeStopAsync(store, fixture, EWorkerCommandStatus.Succeeded);
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, claimed.Trigger);

        var status = await ProcessAsync(store, fixture, claimed);

        Assert.Equal(ELifecycleJobStatus.Succeeded, status);
        await AssertStoppedWithVerifiedReleaseAsync(store, fixture);
        await using var db = CreateDbContext(store.Connection);
        var job = await db.LifecycleJobs.SingleAsync();
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.MaxRuntime, job.Trigger);
        Assert.Equal(1, job.TriggerLimit);
        Assert.Null(job.RequestedByFk);
        Assert.Equal(EAutoStopStatus.Executed, (await db.AutomationPendingStops.SingleAsync()).Status);
    }

    /// <summary>
    /// An idle stop completes the same way and its job keeps the idle trigger and the limit that applied.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task IdleStop_CompletesThroughVerifiedNonBillableReleaseAndKeepsItsTrigger()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var fixture = await SeedAsync(store, q =>
        {
            q.IdleShutdownEnabled = true;
            q.IdleShutdownMinutes = IdleMinutes;
        });
        var evaluator = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        await ReportAsync(store, fixture, IdleStart, 0);
        await evaluator.EvaluateAsync(IdleStart, CancellationToken.None);
        var warnAt = IdleStart + Duration.FromMinutes(IdleMinutes);
        await ReportAsync(store, fixture, warnAt, 0);
        var warned = await evaluator.EvaluateAsync(warnAt, CancellationToken.None);
        Assert.Single(warned.RaisedAlerts);
        await ReportAsync(store, fixture, warnAt + Lead, 0);
        var stopped = await evaluator.EvaluateAsync(warnAt + Lead, CancellationToken.None);
        Assert.Equal(1, stopped.JobsQueued);
        var claimed = await ClaimAndSeedCommandAsync(store, fixture, EWorkerCommandStatus.Succeeded);

        var status = await ProcessAsync(store, fixture, claimed);

        Assert.Equal(ELifecycleJobStatus.Succeeded, status);
        await AssertStoppedWithVerifiedReleaseAsync(store, fixture);
        await using var db = CreateDbContext(store.Connection);
        var job = await db.LifecycleJobs.SingleAsync();
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobTrigger.Idle, job.Trigger);
        Assert.Equal(IdleMinutes, job.TriggerLimit);
    }

    /// <summary>
    /// A failed worker stop leaves a failed ordinary stop job: nothing is torn down, no terminate job is queued, the
    /// pending stop stays executed and later passes queue no further stop (D-AUTO-2).
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task FailedAutomaticStop_StaysAFailedStopAndIsNeverEscalated()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var fixture = await SeedAsync(store, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
            q.TimeStampUpdated = Started - Duration.FromDays(1);
        });
        var claimed = await QueueMaxRuntimeStopAsync(store, fixture, EWorkerCommandStatus.Failed);

        var status = await ProcessAsync(store, fixture, claimed);

        Assert.Equal(ELifecycleJobStatus.Failed, status);
        var stopInstant = Started + Duration.FromMinutes(60) + Lead;
        var later = ServerAutomationTestData.CreateEvaluator(store.DbContext);
        var firstPass = await later.EvaluateAsync(stopInstant + Duration.FromHours(1), CancellationToken.None);
        var secondPass = await later.EvaluateAsync(stopInstant + Duration.FromHours(2), CancellationToken.None);

        Assert.Equal(0, firstPass.JobsQueued);
        Assert.Equal(0, secondPass.JobsQueued);
        await using var db = CreateDbContext(store.Connection);
        var jobs = await db.LifecycleJobs.ToArrayAsync();
        var job = Assert.Single(jobs);
        Assert.Equal(ELifecycleJobKind.Stop, job.Kind);
        Assert.Equal(ELifecycleJobStatus.Failed, job.Status);
        Assert.DoesNotContain(jobs, q => q.Kind == ELifecycleJobKind.Terminate);
        Assert.Equal(EAutoStopStatus.Executed, (await db.AutomationPendingStops.SingleAsync()).Status);
        var deployment = await db.GameServerDeployments.SingleAsync();
        Assert.Equal(EDeploymentStatus.PreservationFailed, deployment.Status);
        Assert.Null(deployment.TimeStampResolved);
        Assert.Empty(fixture.Provider.VerifiedReleases);
        Assert.DoesNotContain(
            await db.DeploymentResources.ToArrayAsync(),
            q => q.TimeStampReleased is not null);
    }

    /// <summary>
    /// Creates a context on the shared connection, like a freshly started API process sharing the database.
    /// </summary>
    /// <param name="connection">The open in-memory connection that keeps the database alive.</param>
    /// <returns>A new context; the caller disposes it.</returns>
    private static NodaTimeSqliteApiDbContext CreateDbContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// Creates the real job queue over the shared connection.
    /// </summary>
    /// <param name="connection">The open in-memory connection that keeps the database alive.</param>
    /// <returns>The queue under test.</returns>
    private static LifecycleJobQueue CreateQueue(SqliteConnection connection)
        => new(new ConnectionDbContextFactory(connection), new HostingAuditWriter());

    /// <summary>
    /// Constructs the real lifecycle executor over a context with the given provider as the only runtime provider.
    /// </summary>
    /// <param name="db">The context the executor works on.</param>
    /// <param name="provider">The provider double.</param>
    /// <returns>The executor under test.</returns>
    private static LifecycleJobExecutor CreateExecutor(DatabaseContext db, IRuntimeProvider provider)
    {
        var auditWriter = new HostingAuditWriter();
        return new LifecycleJobExecutor(
            db,
            new TestGameServerDefinitionCatalog(),
            [provider],
            new WorkerCredentialService(Options.Create(new LifecycleWorkerOptions())),
            new UnusedBootstrapRenderer(),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            new GameServerContentSnapshotResolver(db),
            new GameServerConfigurationService(db),
            new UnusedServerFileStorage(),
            new TestStorageDemandService(),
            new HostingAuthorizationService(db),
            new HostingCostSliceService(
                db,
                auditWriter,
                Options.Create(new HostingFinanceOptions
                {
                    MinimumSafetyRangeEur = 5m,
                    ReservationSlice = TimeSpan.FromMinutes(15),
                    ReconciliationInterval = TimeSpan.FromSeconds(30),
                })),
            auditWriter,
            new TestGameServerSecretService(),
            Options.Create(new LifecycleWorkerOptions { IdlePollInterval = TimeSpan.FromMilliseconds(5) }),
            Options.Create(new HostingFinanceOptions()));
    }

    /// <summary>
    /// Creates contexts on one shared in-memory connection for the job queue.
    /// </summary>
    /// <param name="connection">The open in-memory connection that keeps the database alive.</param>
    private sealed class ConnectionDbContextFactory(SqliteConnection connection) : IDbContextFactory<DatabaseContext>
    {
        /// <inheritdoc />
        public DatabaseContext CreateDbContext() => AutomaticStopJobTests.CreateDbContext(connection);

        /// <inheritdoc />
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    /// <summary>
    /// A bootstrap renderer the stop path never calls.
    /// </summary>
    private sealed class UnusedBootstrapRenderer : IWorkerBootstrapPayloadRenderer
    {
        /// <inheritdoc />
        public WorkerBootstrapPayload Render(
            WorkerBootstrapConfiguration bootstrap,
            Guid serverId,
            string gameIdentifier,
            IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements)
            => throw new NotSupportedException("A stop never renders a worker bootstrap payload.");
    }

    /// <summary>
    /// A file storage the stop path never calls.
    /// </summary>
    private sealed class UnusedServerFileStorage : IServerFileStorage
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ServerFileDownload> OpenReadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ServerFileEntry> UploadAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, Stream content,
            bool overwrite, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<ServerFileEntry> CreateDirectoryAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAsync(
            Guid serverId, Guid? deploymentId, EFileContext context, string relativePath, bool recursive,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// A provider double that discovers the resources it has not released yet and verifies every release as
    /// non-billable, recording each release.
    /// </summary>
    /// <param name="resources">The attributed resources the provider currently holds.</param>
    private sealed class ReleasingRuntimeProvider(IReadOnlyList<RuntimeResource> resources) : IRuntimeProvider
    {
        /// <summary>The identifiers of the resources released with a verified non-billable result.</summary>
        private readonly List<string> _verifiedReleases = [];

        /// <inheritdoc />
        public string ProviderIdentifier => TestRuntimeProvider.Identifier;

        /// <inheritdoc />
        public RuntimeProviderCapabilities Capabilities { get; } = new(true, true, true, EstimatedHourlyCostEur: 0.10m);

        /// <summary>Gets the identifiers released with a verified non-billable result, in release order.</summary>
        /// <value>A snapshot of the recorded releases.</value>
        public IReadOnlyList<string> VerifiedReleases => _verifiedReleases.ToArray();

        /// <inheritdoc />
        public Task<RuntimeProvisionResult> ProvisionAsync(
            RuntimeProvisionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("A stop never provisions.");

        /// <inheritdoc />
        public Task<RuntimeObservationResult> ObserveAsync(
            RuntimeObservationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<RuntimeReleaseResult> ReleaseAsync(
            RuntimeReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            _verifiedReleases.Add(request.ResourceIdentifier);
            return Task.FromResult(new RuntimeReleaseResult(
                ERuntimeProviderOutcome.Succeeded,
                "released",
                null,
                IsVerifiedNonBillable: true));
        }

        /// <inheritdoc />
        public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
            RuntimeDanglingReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeDiscoveryResult(
                ERuntimeProviderOutcome.Succeeded,
                "discovered",
                null,
                resources.Where(q => !_verifiedReleases.Contains(q.ResourceIdentifier)).ToArray()));
    }
}
