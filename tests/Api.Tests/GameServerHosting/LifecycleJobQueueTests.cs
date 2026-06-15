using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class LifecycleJobQueueTests
{
    [Fact]
    public async Task ClaimNext_ExecutesOneDeploymentInRequestOrderBeforePriority()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var first = CreateJob(seed, sequence: 1, priority: 0);
        var laterHighPriority = CreateJob(seed, sequence: 2, priority: 100);
        store.DbContext.LifecycleJobs.AddRange(first, laterHighPriority);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var claimedFirst = await queue.ClaimNextAsync("processor-a", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimedFirst);
        Assert.Equal(first.PrimaryKey, claimedFirst.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Running, claimedFirst.Status);
        Assert.Equal("claimed", claimedFirst.Stage);
        Assert.Equal(1, claimedFirst.Attempt);
        Assert.Equal("processor-a", claimedFirst.LeaseOwner);
        Assert.NotNull(claimedFirst.LeaseExpires);
        Assert.Null(await queue.ClaimNextAsync("processor-b", TimeSpan.FromMinutes(1)));

        await queue.CompleteAsync(first.PrimaryKey, "processor-a");
        var claimedSecond = await queue.ClaimNextAsync("processor-b", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimedSecond);
        Assert.Equal(laterHighPriority.PrimaryKey, claimedSecond.PrimaryKey);
        await using var verification = CreateDbContext(store.Connection);
        var attemptNumbers = await verification.LifecycleJobs
            .OrderBy(q => q.Sequence)
            .Select(q => q.Attempt)
            .ToArrayAsync();
        Assert.Equal([1, 1], attemptNumbers);
        Assert.Equal(2, await verification.LifecycleJobAttempts.CountAsync());
        Assert.Equal(
            1,
            await verification.LifecycleJobAttempts.CountAsync(q => q.Status == ELifecycleJobStatus.Running));
    }

    [Fact]
    public async Task ClaimNext_RecoversExpiredLeaseAndClosesAbandonedAttempt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var startedAt = Instant.FromUnixTimeSeconds(1_700_000_100);
        var expired = CreateJob(seed, sequence: 1, priority: 0);
        expired.Status = ELifecycleJobStatus.Running;
        expired.Stage = "executing";
        expired.Attempt = 1;
        expired.LeaseOwner = "dead-processor";
        expired.LeaseExpires = Instant.FromUnixTimeSeconds(1);
        expired.TimeStampStarted = startedAt;
        var abandonedAttempt = new LifecycleJobAttempt
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = expired,
            LifecycleJobFk = expired.PrimaryKey,
            AttemptNumber = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "executing",
            TimeStampStarted = startedAt,
        };
        store.DbContext.AddRange(expired, abandonedAttempt);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var recovered = await queue.ClaimNextAsync("replacement-processor", TimeSpan.FromMinutes(1));

        Assert.NotNull(recovered);
        Assert.Equal(expired.PrimaryKey, recovered.PrimaryKey);
        Assert.Equal(2, recovered.Attempt);
        Assert.Equal("recovered", recovered.Stage);
        Assert.Equal("replacement-processor", recovered.LeaseOwner);
        Assert.Equal(startedAt, recovered.TimeStampStarted);

        await using var verification = CreateDbContext(store.Connection);
        var attempts = await verification.LifecycleJobAttempts
            .Where(q => q.LifecycleJobFk == expired.PrimaryKey)
            .OrderBy(q => q.AttemptNumber)
            .ToArrayAsync();
        Assert.Equal(2, attempts.Length);
        Assert.Equal(ELifecycleJobStatus.RetryPending, attempts[0].Status);
        Assert.Equal("lease-expired", attempts[0].Stage);
        Assert.NotNull(attempts[0].TimeStampCompleted);
        Assert.Equal(ELifecycleJobStatus.Running, attempts[1].Status);
        Assert.Equal("recovered", attempts[1].Stage);
    }

    [Fact]
    public async Task ClaimNext_TerminalizesExpiredFinalAttemptBeforeSelectingOtherWork()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var exhaustedSeed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var availableSeed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var startedAt = Instant.FromUnixTimeSeconds(1_700_000_100);
        var exhausted = CreateJob(exhaustedSeed, sequence: 1, priority: 100);
        exhausted.Status = ELifecycleJobStatus.Running;
        exhausted.Stage = "executing";
        exhausted.Attempt = 1;
        exhausted.MaxAttempts = 1;
        exhausted.LeaseOwner = "dead-processor";
        exhausted.LeaseExpires = Instant.FromUnixTimeSeconds(1);
        exhausted.TimeStampStarted = startedAt;
        var abandonedAttempt = new LifecycleJobAttempt
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = exhausted,
            LifecycleJobFk = exhausted.PrimaryKey,
            AttemptNumber = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "executing",
            TimeStampStarted = startedAt,
        };
        var available = CreateJob(availableSeed, sequence: 1, priority: 0);
        store.DbContext.AddRange(exhausted, abandonedAttempt, available);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var claimed = await queue.ClaimNextAsync("replacement-processor", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimed);
        Assert.Equal(available.PrimaryKey, claimed.PrimaryKey);
        await using var verification = CreateDbContext(store.Connection);
        var terminalJob = await verification.LifecycleJobs.SingleAsync(q => q.PrimaryKey == exhausted.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Failed, terminalJob.Status);
        Assert.Equal("lease-expired", terminalJob.Stage);
        Assert.Equal(1, terminalJob.Attempt);
        Assert.Null(terminalJob.LeaseOwner);
        Assert.NotNull(terminalJob.TimeStampCompleted);
        var terminalAttempt = await verification.LifecycleJobAttempts
            .SingleAsync(q => q.PrimaryKey == abandonedAttempt.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Failed, terminalAttempt.Status);
        Assert.Equal("lease-expired", terminalAttempt.Stage);
        Assert.NotNull(terminalAttempt.TimeStampCompleted);
    }

    [Fact]
    public async Task ClaimNext_DoesNotTakeUnexpiredLeaseOrEarlyRetry()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var delayedSeed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var running = CreateJob(seed, sequence: 1, priority: 0);
        running.Status = ELifecycleJobStatus.Running;
        running.Attempt = 1;
        running.LeaseOwner = "active-processor";
        running.LeaseExpires = now + Duration.FromMinutes(10);
        running.TimeStampStarted = now;
        var attempt = new LifecycleJobAttempt
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = running,
            LifecycleJobFk = running.PrimaryKey,
            AttemptNumber = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "executing",
            TimeStampStarted = now,
        };
        var delayed = CreateJob(delayedSeed, sequence: 1, priority: 100);
        delayed.Status = ELifecycleJobStatus.RetryPending;
        delayed.TimeStampNextAttempt = now + Duration.FromMinutes(10);
        store.DbContext.AddRange(running, attempt, delayed);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        Assert.Null(await queue.ClaimNextAsync("processor-b", TimeSpan.FromMinutes(1)));

        await using var verification = CreateDbContext(store.Connection);
        Assert.Equal(
            "active-processor",
            await verification.LifecycleJobs
                .Where(q => q.PrimaryKey == running.PrimaryKey)
                .Select(q => q.LeaseOwner)
                .SingleAsync());
        Assert.Equal(
            0,
            await verification.LifecycleJobs
                .Where(q => q.PrimaryKey == delayed.PrimaryKey)
                .Select(q => q.Attempt)
                .SingleAsync());
    }

    [Fact]
    public async Task ClaimNext_TerminateBypassesFailureAndCancelsNormalQueue()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var failed = CreateJob(seed, sequence: 1, priority: 0);
        failed.Status = ELifecycleJobStatus.Failed;
        failed.Stage = "failed";
        failed.TimeStampCompleted = failed.TimeStampUpdated;
        var blockedNormal = CreateJob(seed, sequence: 2, priority: 0);
        var terminate = CreateJob(seed, sequence: 3, priority: 100);
        terminate.Kind = ELifecycleJobKind.Terminate;
        terminate.ActionIdentifier = GameServerActions.Terminate;
        store.DbContext.LifecycleJobs.AddRange(failed, blockedNormal, terminate);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var claimed = await queue.ClaimNextAsync("cost-stop-processor", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimed);
        Assert.Equal(terminate.PrimaryKey, claimed.PrimaryKey);
        Assert.Equal(ELifecycleJobKind.Terminate, claimed.Kind);
        await using var verification = CreateDbContext(store.Connection);
        var supersededStatuses = await verification.LifecycleJobs
            .Where(q => q.PrimaryKey == failed.PrimaryKey || q.PrimaryKey == blockedNormal.PrimaryKey)
            .Select(q => q.Status)
            .ToArrayAsync();
        Assert.Equal(2, supersededStatuses.Length);
        Assert.All(supersededStatuses, q => Assert.Equal(ELifecycleJobStatus.CancelledByCostStop, q));
    }

    [Fact]
    public async Task ClaimNext_TerminateCancelsPriorActiveNormalJobAndAttempt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var active = CreateJob(seed, sequence: 1, priority: 0);
        active.Status = ELifecycleJobStatus.Running;
        active.Stage = "executing";
        active.Attempt = 1;
        active.LeaseOwner = "normal-processor";
        active.LeaseExpires = now + Duration.FromMinutes(10);
        active.TimeStampStarted = now;
        var activeAttempt = new LifecycleJobAttempt
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = active,
            LifecycleJobFk = active.PrimaryKey,
            AttemptNumber = 1,
            Status = ELifecycleJobStatus.Running,
            Stage = "executing",
            TimeStampStarted = now,
        };
        var terminate = CreateJob(seed, sequence: 2, priority: 100);
        terminate.Kind = ELifecycleJobKind.Terminate;
        terminate.ActionIdentifier = GameServerActions.Terminate;
        terminate.MaxAttempts = 10;
        store.DbContext.AddRange(active, activeAttempt, terminate);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var claimed = await queue.ClaimNextAsync("cost-stop-processor", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimed);
        Assert.Equal(terminate.PrimaryKey, claimed.PrimaryKey);
        Assert.Equal(int.MaxValue, claimed.MaxAttempts);
        await using var verification = CreateDbContext(store.Connection);
        var cancelledJob = await verification.LifecycleJobs.SingleAsync(q => q.PrimaryKey == active.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.CancelledByCostStop, cancelledJob.Status);
        Assert.Equal("cancelled-by-cost-stop", cancelledJob.Stage);
        Assert.Null(cancelledJob.LeaseOwner);
        Assert.Null(cancelledJob.LeaseExpires);
        Assert.NotNull(cancelledJob.TimeStampCompleted);
        var cancelledAttempt = await verification.LifecycleJobAttempts
            .SingleAsync(q => q.PrimaryKey == activeAttempt.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.CancelledByCostStop, cancelledAttempt.Status);
        Assert.Equal("cancelled-by-cost-stop", cancelledAttempt.Stage);
        Assert.NotNull(cancelledAttempt.TimeStampCompleted);
    }

    [Fact]
    public async Task Fail_TerminateAlwaysRetriesWithPracticalInfiniteAttemptLimit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var terminate = CreateJob(seed, sequence: 1, priority: 100);
        terminate.Kind = ELifecycleJobKind.Terminate;
        terminate.ActionIdentifier = GameServerActions.Terminate;
        terminate.MaxAttempts = 1;
        store.DbContext.LifecycleJobs.Add(terminate);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);
        var claimed = await queue.ClaimNextAsync("cost-stop-processor", TimeSpan.FromMinutes(1));
        Assert.NotNull(claimed);
        Assert.Equal(int.MaxValue, claimed.MaxAttempts);

        await queue.FailAsync(
            terminate.PrimaryKey,
            "cost-stop-processor",
            "provider still reports billable resources",
            TimeSpan.Zero,
            retryAllowed: false);

        await using var verification = CreateDbContext(store.Connection);
        var persisted = await verification.LifecycleJobs.SingleAsync(q => q.PrimaryKey == terminate.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.RetryPending, persisted.Status);
        Assert.Equal("retry-pending", persisted.Stage);
        Assert.Equal(int.MaxValue, persisted.MaxAttempts);
        Assert.NotNull(persisted.TimeStampNextAttempt);
        Assert.Null(persisted.TimeStampCompleted);
        var attempt = await verification.LifecycleJobAttempts
            .SingleAsync(q => q.LifecycleJobFk == terminate.PrimaryKey && q.AttemptNumber == 1);
        Assert.Equal(ELifecycleJobStatus.RetryPending, attempt.Status);
        Assert.NotNull(attempt.TimeStampCompleted);
    }

    [Fact]
    public async Task Fail_PermanentFailureIsTerminalWithoutConsumingRetryBudget()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var job = CreateJob(seed, sequence: 1, priority: 0);
        job.MaxAttempts = 3;
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);
        Assert.NotNull(await queue.ClaimNextAsync("processor-a", TimeSpan.FromMinutes(1)));

        await queue.FailAsync(
            job.PrimaryKey,
            "processor-a",
            "request is no longer authorized",
            TimeSpan.Zero,
            retryAllowed: false);

        await using var verification = CreateDbContext(store.Connection);
        var persisted = await verification.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Failed, persisted.Status);
        Assert.Equal("failed", persisted.Stage);
        Assert.Equal("permanent-execution-failed", persisted.FailureCode);
        Assert.Equal(1, persisted.Attempt);
        Assert.Null(persisted.TimeStampNextAttempt);
        Assert.NotNull(persisted.TimeStampCompleted);
        var attempt = await verification.LifecycleJobAttempts
            .SingleAsync(q => q.LifecycleJobFk == job.PrimaryKey && q.AttemptNumber == 1);
        Assert.Equal(ELifecycleJobStatus.Failed, attempt.Status);
        Assert.Equal("permanent-execution-failed", attempt.FailureCode);
        Assert.NotNull(attempt.TimeStampCompleted);
    }

    [Fact]
    public async Task Fail_RetryableFailureStopsAtConfiguredAttemptLimit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var job = CreateJob(seed, sequence: 1, priority: 0);
        job.MaxAttempts = 1;
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);
        Assert.NotNull(await queue.ClaimNextAsync("processor-a", TimeSpan.FromMinutes(1)));

        await queue.FailAsync(
            job.PrimaryKey,
            "processor-a",
            "transient provider failure",
            TimeSpan.Zero,
            retryAllowed: true);

        await using var verification = CreateDbContext(store.Connection);
        var persisted = await verification.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Failed, persisted.Status);
        Assert.Equal(1, persisted.Attempt);
        Assert.NotNull(persisted.TimeStampCompleted);
    }

    [Fact]
    public async Task RenewLease_RequiresCurrentOwnerAndRunningStatus()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var job = CreateJob(seed, sequence: 1, priority: 0);
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);
        var claimed = await queue.ClaimNextAsync("processor-a", TimeSpan.FromSeconds(5));
        Assert.NotNull(claimed);
        var originalExpiry = claimed.LeaseExpires;

        Assert.False(await queue.RenewLeaseAsync(job.PrimaryKey, "processor-b", TimeSpan.FromMinutes(5)));
        Assert.True(await queue.RenewLeaseAsync(job.PrimaryKey, "processor-a", TimeSpan.FromMinutes(5)));

        await using (var verification = CreateDbContext(store.Connection))
        {
            var renewedExpiry = await verification.LifecycleJobs
                .Where(q => q.PrimaryKey == job.PrimaryKey)
                .Select(q => q.LeaseExpires)
                .SingleAsync();
            Assert.True(renewedExpiry > originalExpiry);
        }

        await queue.CompleteAsync(job.PrimaryKey, "processor-a");
        Assert.False(await queue.RenewLeaseAsync(job.PrimaryKey, "processor-a", TimeSpan.FromMinutes(5)));
    }

    private static LifecycleJobQueue CreateQueue(SqliteConnection connection)
        => new(new SqliteApiDbContextFactory(connection), new HostingAuditWriter());

    private static NodaTimeSqliteApiDbContext CreateDbContext(SqliteConnection connection)
        => new(
            new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
                .UseSqlite(connection)
                .Options);

    private static LifecycleJob CreateJob(HostingTestSeed seed, long sequence, int priority)
    {
        var now = Instant.FromUnixTimeSeconds(1_700_000_000 + sequence);
        return new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = sequence,
            Priority = priority,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
    }

    private sealed class SqliteApiDbContextFactory(SqliteConnection connection) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => LifecycleJobQueueTests.CreateDbContext(connection);

        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
