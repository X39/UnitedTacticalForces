using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Tests.ServerAutomation;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Behavior matrix of the <see cref="FleetChangeInterceptor"/> against in-memory SQLite stores: which committed
/// changes signal, which stay silent, and how saves inside explicit transactions, rollbacks, failed saves and
/// concurrent contexts are handled.
/// </summary>
public sealed class FleetChangeInterceptorTests
{
    /// <summary>
    /// A fixed instant used for timestamps of rows created by these tests.
    /// </summary>
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// Adding a game server and later changing its title, archive flag, launch flag or current cost owner notifies
    /// that server exactly once per save.
    /// </summary>
    /// <returns>A task completing after every save was verified.</returns>
    [Fact]
    public async Task GameServerAddAndFleetVisibleEdits_NotifyServerOncePerSave()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        notifier.Clear();

        var added = await AddServerAsync(store, seed);
        Assert.Equal([added.PrimaryKey], notifier.TakeAll());

        var server = seed.Server;
        server.Title = "Renamed server";
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([server.PrimaryKey], notifier.TakeAll());

        server.IsArchived = true;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([server.PrimaryKey], notifier.TakeAll());

        server.IsLaunchDisabled = true;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([server.PrimaryKey], notifier.TakeAll());

        var secondOwner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.ExternalOrganization,
            DisplayName      = "Second owner",
            Currency         = "EUR",
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };
        store.DbContext.Add(secondOwner);
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());

        server.CurrentCostOwnerFk = secondOwner.PrimaryKey;
        server.CurrentCostOwner   = secondOwner;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Changing the pending-start areas of a game server notifies it so other open workspaces refetch the server,
    /// while a change of the since-timestamp alone stays silent.
    /// </summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task PendingStartAreas_Notify_PendingStartSinceAlone_StaysSilent()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        notifier.Clear();

        seed.Server.PendingStartChangeAreas = EGameServerChangeArea.Configuration;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        seed.Server.PendingStartSince = Now;
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// Changing a deployment's status or possible-cost flag notifies its server; stage-only and timestamp-only
    /// changes notify nothing.
    /// </summary>
    /// <returns>A task completing after every save was verified.</returns>
    [Fact]
    public async Task DeploymentStatusAndPossibleCost_Notify_StageAndTimestamp_StaySilent()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        notifier.Clear();

        deployment.Status = EDeploymentStatus.Stopping;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        deployment.PossibleCost = false;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        deployment.Stage = "draining";
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());

        deployment.TimeStampUpdated = Now.Plus(Duration.FromMinutes(5));
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// Adding a lifecycle job and changing its status notify the job's server; progress-only and stage-only
    /// changes notify nothing.
    /// </summary>
    /// <returns>A task completing after every save was verified.</returns>
    [Fact]
    public async Task JobAddAndStatus_Notify_ProgressAndStage_StaySilent()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        notifier.Clear();

        var job = CreateJob(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, sequence: 1);
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        job.Status = ELifecycleJobStatus.Running;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        job.Progress = 40;
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());

        job.Stage = "installing";
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// One save touching two servers notifies each once; one save touching the same server through a deployment and
    /// a job notifies it once.
    /// </summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task OneSave_NotifiesEachDistinctServerOnce()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddServerAsync(store, seed);
        notifier.Clear();

        seed.Server.Title = "First renamed";
        other.Title       = "Second renamed";
        await store.DbContext.SaveChangesAsync();
        Assert.Equal(
            new[] { seed.Server.PrimaryKey, other.PrimaryKey }.Order(),
            notifier.TakeAll().Order());

        seed.Deployment!.Status = EDeploymentStatus.Stopping;
        store.DbContext.LifecycleJobs.Add(CreateJob(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, sequence: 1));
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// A save with no fleet-relevant change notifies nothing.
    /// </summary>
    /// <returns>A task completing after the save was verified.</returns>
    [Fact]
    public async Task SaveWithoutRelevantChange_NotifiesNothing()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        notifier.Clear();

        seed.Server.GracefulStopAttempts = 5;
        seed.Server.TimeStampUpdated     = Now.Plus(Duration.FromMinutes(1));
        seed.CostOwner.DisplayName           = "Renamed owner";
        await store.DbContext.SaveChangesAsync();
        await store.DbContext.SaveChangesAsync();

        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// Inside an explicit transaction nothing is notified until the commit returns; then every affected server is
    /// notified once, including the union of two saves in the same transaction.
    /// </summary>
    /// <returns>A task completing after the commit was verified.</returns>
    [Fact]
    public async Task ExplicitTransaction_DefersUntilCommit_AndPublishesUnionOnce()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddServerAsync(store, seed);
        notifier.Clear();

        await using (var transaction = await store.DbContext.Database.BeginTransactionAsync())
        {
            seed.Deployment!.Status = EDeploymentStatus.Stopping;
            await store.DbContext.SaveChangesAsync();
            Assert.Empty(notifier.TakeAll());

            seed.Server.Title = "Renamed inside transaction";
            other.IsArchived  = true;
            await store.DbContext.SaveChangesAsync();
            Assert.Empty(notifier.TakeAll());

            await transaction.CommitAsync();
            Assert.Equal(
                new[] { seed.Server.PrimaryKey, other.PrimaryKey }.Order(),
                notifier.TakeAll().Order());
        }

        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// A rolled-back transaction never notifies, and a failed save (duplicate server and sequence on a job)
    /// notifies nothing and does not leak its identifiers into the next successful save.
    /// </summary>
    /// <returns>A task completing after the rollback and the failed save were verified.</returns>
    [Fact]
    public async Task RollbackAndFailedSave_NeverNotify_AndDoNotLeak()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var other = await AddServerAsync(store, seed);
        store.DbContext.LifecycleJobs.Add(CreateJob(seed.Server.PrimaryKey, seed.Deployment!.PrimaryKey, sequence: 1));
        await store.DbContext.SaveChangesAsync();
        notifier.Clear();

        await using (var transaction = await store.DbContext.Database.BeginTransactionAsync())
        {
            seed.Server.Title = "Rolled back";
            await store.DbContext.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.Empty(notifier.TakeAll());
        store.DbContext.ChangeTracker.Clear();

        var duplicate = CreateJob(seed.Server.PrimaryKey, seed.Deployment.PrimaryKey, sequence: 1);
        store.DbContext.LifecycleJobs.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.DbContext.SaveChangesAsync());
        Assert.Empty(notifier.TakeAll());

        store.DbContext.Entry(duplicate).State = EntityState.Detached;
        var tracked = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == other.PrimaryKey);
        tracked.Title = "Next successful save";
        await store.DbContext.SaveChangesAsync();

        Assert.Equal([other.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Two contexts sharing one interceptor instance stay isolated: a change deferred in an open transaction of one
    /// context is published only after that commit, while the other context's save is published immediately.
    /// </summary>
    /// <returns>A task completing after both contexts were verified.</returns>
    [Fact]
    public async Task TwoContexts_SharingInterceptor_DoNotExchangeIdentifiers()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var storeA = await HostingTestStore.CreateAsync([interceptor]);
        await using var storeB = await HostingTestStore.CreateAsync([interceptor]);
        var seedA = await HostingTestData.SeedServerAsync(storeA.DbContext, withActiveDeployment: true);
        var seedB = await HostingTestData.SeedServerAsync(storeB.DbContext, withActiveDeployment: true);
        notifier.Clear();

        await using var transaction = await storeA.DbContext.Database.BeginTransactionAsync();
        seedA.Deployment!.Status = EDeploymentStatus.Stopping;
        await storeA.DbContext.SaveChangesAsync();

        seedB.Server.Title = "Saved without transaction";
        await storeB.DbContext.SaveChangesAsync();

        Assert.Equal([seedB.Server.PrimaryKey], notifier.TakeAll());

        await transaction.CommitAsync();

        Assert.Equal([seedA.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Adding and modifying the automation settings of a server notifies that server.
    /// </summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task AutomationSettingsAddAndModify_NotifyServer()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        notifier.Clear();

        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        settings.IdleShutdownEnabled = true;
        settings.Version++;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Adding, modifying and deleting a schedule rule notifies the server of the rule.
    /// </summary>
    /// <returns>A task completing after every save was verified.</returns>
    [Fact]
    public async Task ScheduleAddModifyAndDelete_NotifyServer()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        notifier.Clear();

        var schedule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Stop,
            [1, 2],
            20 * 60,
            Now);
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        schedule.MinuteOfDay = 21 * 60;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        store.DbContext.ServerSchedules.Remove(schedule);
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Adding a pending automatic stop and later resolving it each notify the server.
    /// </summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task PendingStopAddAndStatusChange_NotifyServer()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        notifier.Clear();

        var pending = await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext,
            seed.Deployment!,
            EAutoStopReason.Idle,
            Now.Plus(Duration.FromMinutes(10)),
            30);
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        pending.Status     = EAutoStopStatus.Cancelled;
        pending.ResolvedAt = Now;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Adding a schedule run row and changing its outcome each notify the server, while a change of only the recorded
    /// instant or the details stays silent.
    /// </summary>
    /// <returns>A task completing after every save was verified.</returns>
    [Fact]
    public async Task ScheduleRunAddAndOutcomeChange_NotifyServer_RecordedAtAlone_StaysSilent()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        var schedule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Start,
            [1, 2],
            20 * 60,
            Now);
        notifier.Clear();

        var run = await ServerAutomationTestData.AddRunAsync(
            store.DbContext,
            schedule,
            Now,
            EServerScheduleRunOutcome.WaitingForStop);
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());

        run.RecordedAt = Now.Plus(Duration.FromMinutes(1));
        run.Details    = "still waiting";
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());

        run.Outcome = EServerScheduleRunOutcome.SkippedStopUnfinished;
        await store.DbContext.SaveChangesAsync();
        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// The evaluator-owned automation state changes on every pass and never notifies.
    /// </summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task AutomationStateAddAndModify_StaySilent()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        notifier.Clear();

        var state = new ServerAutomationState
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = seed.Server.PrimaryKey,
            TimeStampUpdated = Now,
        };
        store.DbContext.ServerAutomationStates.Add(state);
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());

        state.IdleSince        = Now;
        state.TimeStampUpdated = Now.Plus(Duration.FromMinutes(1));
        await store.DbContext.SaveChangesAsync();
        Assert.Empty(notifier.TakeAll());
    }

    /// <summary>
    /// A save that touches the settings and a pending stop of the same server notifies that server once.
    /// </summary>
    /// <returns>A task completing after the combined save was verified.</returns>
    [Fact]
    public async Task SettingsAndPendingStopInOneSave_NotifyServerOnce()
    {
        var (notifier, interceptor) = CreateInterceptor();
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        notifier.Clear();

        var deployment = seed.Deployment!;
        store.DbContext.ServerAutomationSettings.Add(new ServerAutomationSettings
        {
            PrimaryKey          = Guid.NewGuid(),
            GameServerFk        = seed.Server.PrimaryKey,
            IdleShutdownMinutes = 30,
            MaxRuntimeHours     = 6,
            Version             = 1,
            TimeStampCreated    = Now,
            TimeStampUpdated    = Now,
        });
        store.DbContext.AutomationPendingStops.Add(new AutomationPendingStop
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = seed.Server.PrimaryKey,
            DeploymentFk     = deployment.PrimaryKey,
            Reason           = EAutoStopReason.MaxRuntime,
            WarnedAt         = Now,
            StopsAt          = Now.Plus(Duration.FromMinutes(10)),
            Status           = EAutoStopStatus.Pending,
            TimeStampCreated = Now,
        });
        await store.DbContext.SaveChangesAsync();

        Assert.Equal([seed.Server.PrimaryKey], notifier.TakeAll());
    }

    /// <summary>
    /// Creates a recording notifier and an interceptor reporting to it.
    /// </summary>
    /// <returns>The recording notifier and the interceptor under test.</returns>
    private static (RecordingNotifier Notifier, FleetChangeInterceptor Interceptor) CreateInterceptor()
    {
        var notifier = new RecordingNotifier();
        return (notifier, new FleetChangeInterceptor(notifier, NullLogger<FleetChangeInterceptor>.Instance));
    }

    /// <summary>
    /// Adds and saves a second game server created by the seeded user and owned by the seeded cost owner.
    /// </summary>
    /// <param name="store">The store to add the server to.</param>
    /// <param name="seed">The seed supplying the creator and the cost owner.</param>
    /// <returns>The saved, tracked game server.</returns>
    private static async Task<GameServer> AddServerAsync(HostingTestStore store, HostingTestSeed seed)
    {
        var server = new GameServer
        {
            PrimaryKey                = Guid.NewGuid(),
            Title                     = "Second server",
            GameIdentifier            = HostingTestData.GameIdentifier,
            RuntimeProviderIdentifier = TestRuntimeProvider.Identifier,
            CreatedByFk               = seed.User.PrimaryKey,
            CurrentCostOwnerFk        = seed.CostOwner.PrimaryKey,
            TimeStampCreated          = Now,
            TimeStampUpdated          = Now,
        };
        store.DbContext.GameServers.Add(server);
        await store.DbContext.SaveChangesAsync();
        return server;
    }

    /// <summary>
    /// Creates a queued start job for the specified server and deployment.
    /// </summary>
    /// <param name="serverId">The primary key of the job's game server.</param>
    /// <param name="deploymentId">The primary key of the job's deployment.</param>
    /// <param name="sequence">The per-server sequence number; unique together with the server.</param>
    /// <returns>The untracked job.</returns>
    private static LifecycleJob CreateJob(Guid serverId, Guid deploymentId, long sequence)
        => new()
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = serverId,
            DeploymentFk     = deploymentId,
            Kind             = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Status           = ELifecycleJobStatus.Queued,
            Stage            = "queued",
            Attempt          = 0,
            Sequence         = sequence,
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };

    /// <summary>
    /// A thread-safe notifier double that records every notified server identifier in call order.
    /// </summary>
    private sealed class RecordingNotifier : IFleetChangeNotifier
    {
        /// <summary>
        /// Guards <see cref="_serverIds"/>.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// The notified server identifiers in call order since the last take or clear.
        /// </summary>
        private readonly List<Guid> _serverIds = [];

        /// <inheritdoc />
        public void Notify(Guid gameServerId)
        {
            lock (_lock)
            {
                _serverIds.Add(gameServerId);
            }
        }

        /// <summary>
        /// Returns every identifier recorded since the last take or clear and empties the recording.
        /// </summary>
        /// <returns>The recorded identifiers in call order.</returns>
        public Guid[] TakeAll()
        {
            lock (_lock)
            {
                var taken = _serverIds.ToArray();
                _serverIds.Clear();
                return taken;
            }
        }

        /// <summary>
        /// Discards every recorded identifier, for example after seeding.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _serverIds.Clear();
            }
        }
    }
}
