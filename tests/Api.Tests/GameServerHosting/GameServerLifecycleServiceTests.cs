using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerLifecycleServiceTests
{
    [Fact]
    public async Task Create_AsAdministratorWithoutPersonalCostOwner_CreatesArmaServerAndOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var user = new User
        {
            PrimaryKey = Guid.NewGuid(),
            Nickname = "administrator",
            AvatarMimeType = string.Empty,
            Claims = [],
            Roles = [],
            Discord = new DiscordUser { Username = "administrator" },
            Steam = new SteamUser(),
        };
        store.DbContext.Users.Add(user);
        await store.DbContext.SaveChangesAsync();
        var service = new GameServerLifecycleService(
            store.DbContext,
            new GameServerDefinitionCatalog([new Arma3GameServerDefinitionRegistration()]),
            [new TestRuntimeProvider()],
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter());

        var server = await service.CreateAsync(
            new GameServerCreationPayload
            {
                Title = "Test",
                GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
                RuntimeProviderIdentifier = TestRuntimeProvider.Identifier,
                CostOwnerId = null,
                IsLaunchDisabled = false,
            },
            HostingTestData.Principal(user, (Claims.Administrative.All, string.Empty)));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers
            .Include(q => q.CurrentCostOwner)
            .Include(q => q.PreservationSelections)
            .SingleAsync(q => q.PrimaryKey == server.PrimaryKey);
        Assert.Equal(user.PrimaryKey, persisted.CurrentCostOwner!.UserFk);
        Assert.Equal(3, persisted.PreservationSelections.Count);
    }

    [Fact]
    public async Task Archive_FullyStoppedServer_ArchivesRecordAndClosesOwnerAssignment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await service.ArchiveAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify));

        store.DbContext.ChangeTracker.Clear();
        var persisted = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        var assignment = await store.DbContext.GameServerCostOwnerAssignments
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey);
        Assert.True(persisted.IsArchived);
        Assert.True(persisted.IsLaunchDisabled);
        Assert.NotNull(persisted.TimeStampArchived);
        Assert.NotNull(assignment.TimeStampUnassigned);
        Assert.Equal(seed.User.PrimaryKey, assignment.UnassignedByFk);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.ServerArchived && q.GameServerFk == seed.Server.PrimaryKey);
    }

    [Fact]
    public async Task Archive_WithUnresolvedDeployment_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.ArchiveAsync(
            seed.Server.PrimaryKey,
            HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify)));

        store.DbContext.ChangeTracker.Clear();
        Assert.False(await store.DbContext.GameServers
            .Where(q => q.PrimaryKey == seed.Server.PrimaryKey)
            .Select(q => q.IsArchived)
            .SingleAsync());
    }

    [Fact]
    public async Task Archive_WithoutModifyPermission_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.ArchiveAsync(
            seed.Server.PrimaryKey,
            HostingTestData.Principal(seed.User)));

        Assert.False(seed.Server.IsArchived);
    }

    [Fact]
    public async Task Start_PersistsDeploymentOwnerSnapshotJobAndAuditEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        var job = await service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            confirmation: null,
            principal);
        store.DbContext.ChangeTracker.Clear();

        var persistedJob = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == job.PrimaryKey);
        var deployment = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == persistedJob.DeploymentFk);
        Assert.Equal(seed.CostOwner.PrimaryKey, deployment.CostOwnerFk);
        Assert.Equal(EDeploymentStatus.Provisioning, deployment.Status);
        Assert.Equal(ELifecycleJobStatus.Queued, persistedJob.Status);
        Assert.Equal(ELifecycleJobKind.Start, persistedJob.Kind);
        Assert.Equal(GameServerActions.Start, persistedJob.ActionIdentifier);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.DeploymentCreated
                 && q.CostOwnerFk == seed.CostOwner.PrimaryKey
                 && q.CostImpact == ECostImpact.NoCostImpact);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.LifecycleJobQueued
                 && q.LifecycleJobFk == persistedJob.PrimaryKey);
    }

    [Fact]
    public async Task Start_WithoutServerOperatePermission_DoesNotCreateDurableWork()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            confirmation: null,
            HostingTestData.Principal(seed.User)));

        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    [Fact]
    public async Task Start_WithoutPermissionToUseAssignedOwner_DoesNotCreateDurableWork()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var externalOwner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = "External owner",
            Currency = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.CostOwners.Add(externalOwner);
        seed.Server.CurrentCostOwner = externalOwner;
        seed.Server.CurrentCostOwnerFk = externalOwner.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Start,
            confirmation: null,
            principal));

        Assert.Empty(await store.DbContext.GameServerDeployments.ToArrayAsync());
        Assert.Empty(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    [Fact]
    public async Task ChangeCostOwner_IsRejectedWhileDeploymentIsUnresolved()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var alternateOwner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = "Alternate owner",
            Currency = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.CostOwners.Add(alternateOwner);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var administrator = HostingTestData.Principal(
            seed.User,
            (Claims.Administrative.Hosting, string.Empty));

        await Assert.ThrowsAsync<HostingConflictException>(() => service.ChangeCostOwnerAsync(
            seed.Server.PrimaryKey,
            alternateOwner.PrimaryKey,
            administrator));

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            seed.CostOwner.PrimaryKey,
            await store.DbContext.GameServers
                .Where(q => q.PrimaryKey == seed.Server.PrimaryKey)
                .Select(q => q.CurrentCostOwnerFk)
                .SingleAsync());
        Assert.Equal(
            seed.CostOwner.PrimaryKey,
            await store.DbContext.GameServerDeployments
                .Where(q => q.PrimaryKey == seed.Deployment!.PrimaryKey)
                .Select(q => q.CostOwnerFk)
                .SingleAsync());
    }

    [Fact]
    public async Task Stop_DuringProvisioning_IsRejectedWithoutCancellingStart()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            EDeploymentStatus.Provisioning);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var start = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Sequence = 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.LifecycleJobs.Add(start);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Stop,
            seed.Server.Title,
            principal));

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            ELifecycleJobStatus.Queued,
            await store.DbContext.LifecycleJobs
                .Where(q => q.PrimaryKey == start.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync());
        Assert.False(await store.DbContext.GameServerDeployments
            .Where(q => q.PrimaryKey == seed.Deployment.PrimaryKey)
            .Select(q => q.IsProvisioningBlocked)
            .SingleAsync());
    }

    [Fact]
    public async Task Retry_SupersedesFailedJobAndRetainsAttemptHistory()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, failed, command) = await SeedFailedJobAsync(store);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);
        var originalSequence = failed.Sequence;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var queuedSuccessor = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.CollectLogs,
            ActionIdentifier = GameServerActions.CollectLogs,
            Sequence = originalSequence + 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.LifecycleJobs.Add(queuedSuccessor);
        await store.DbContext.SaveChangesAsync();

        var retry = await service.RetryAsync(seed.Server.PrimaryKey, failed.PrimaryKey, principal);
        store.DbContext.ChangeTracker.Clear();

        var original = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == failed.PrimaryKey);
        var persistedRetry = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == retry.PrimaryKey);
        var persistedCommand = await store.DbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == command.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Skipped, original.Status);
        Assert.Equal("retried-as-new-job", original.Stage);
        Assert.Equal(EWorkerCommandStatus.Retried, persistedCommand.Status);
        Assert.Equal(ELifecycleJobStatus.Queued, persistedRetry.Status);
        Assert.Equal(originalSequence, persistedRetry.Sequence);
        Assert.True(persistedRetry.Sequence < queuedSuccessor.Sequence);
        Assert.True(queuedSuccessor.Sequence < original.Sequence);
        Assert.NotEqual(original.PrimaryKey, persistedRetry.PrimaryKey);
    }

    [Fact]
    public async Task Retry_PermanentResolvedStart_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Deployment!.Status = EDeploymentStatus.Failed;
        seed.Deployment.IsProvisioningBlocked = true;
        seed.Deployment.TimeStampResolved = now;
        var failed = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Sequence = 1,
            Status = ELifecycleJobStatus.Failed,
            Stage = "failed",
            Attempt = 1,
            MaxAttempts = 3,
            FailureCode = "permanent-execution-failed",
            FailureMessage = "The actor is no longer authorized.",
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampCompleted = now,
        };
        store.DbContext.LifecycleJobs.Add(failed);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        await Assert.ThrowsAsync<HostingConflictException>(() => service.RetryAsync(
            seed.Server.PrimaryKey,
            failed.PrimaryKey,
            principal));

        Assert.Single(await store.DbContext.LifecycleJobs.ToArrayAsync());
    }

    [Fact]
    public async Task Skip_ClosesFailedJobAndFailedWorkerCommand()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, failed, command) = await SeedFailedJobAsync(store);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);

        await service.SkipAsync(seed.Server.PrimaryKey, failed.PrimaryKey, principal);
        store.DbContext.ChangeTracker.Clear();

        var persistedJob = await store.DbContext.LifecycleJobs.SingleAsync(q => q.PrimaryKey == failed.PrimaryKey);
        var persistedCommand = await store.DbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == command.PrimaryKey);
        Assert.Equal(ELifecycleJobStatus.Skipped, persistedJob.Status);
        Assert.Equal("skipped-by-user", persistedJob.Stage);
        Assert.NotNull(persistedJob.TimeStampCompleted);
        Assert.Equal(EWorkerCommandStatus.Skipped, persistedCommand.Status);
        Assert.NotNull(persistedCommand.TimeStampCompleted);
    }

    [Fact]
    public async Task Retry_WithoutServerOperatePermission_LeavesFailureUntouched()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var (seed, failed, command) = await SeedFailedJobAsync(store);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);

        await Assert.ThrowsAsync<HostingForbiddenException>(() => service.RetryAsync(
            seed.Server.PrimaryKey,
            failed.PrimaryKey,
            HostingTestData.Principal(seed.User)));

        store.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            ELifecycleJobStatus.Failed,
            await store.DbContext.LifecycleJobs
                .Where(q => q.PrimaryKey == failed.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync());
        Assert.Equal(
            EWorkerCommandStatus.Failed,
            await store.DbContext.WorkerCommands
                .Where(q => q.PrimaryKey == command.PrimaryKey)
                .Select(q => q.Status)
                .SingleAsync());
    }

    private static async Task<(HostingTestSeed seed, LifecycleJob job, WorkerCommand command)> SeedFailedJobAsync(
        HostingTestStore store)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var worker = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Identity = "test-worker",
            Status = EWorkerStatus.Degraded,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        };
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = 4,
            Status = ELifecycleJobStatus.Failed,
            Stage = "failed",
            Progress = 30,
            Attempt = 1,
            MaxAttempts = 3,
            FailureCode = "test-failure",
            FailureMessage = "Sanitized test failure.",
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now,
            TimeStampCompleted = now,
        };
        var command = new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJob = job,
            LifecycleJobFk = job.PrimaryKey,
            DeploymentFk = seed.Deployment.PrimaryKey,
            Worker = worker,
            WorkerFk = worker.PrimaryKey,
            Sequence = 1,
            ActionIdentifier = GameServerActions.Restart,
            Payload = "{}",
            Status = EWorkerCommandStatus.Failed,
            Stage = "failed",
            ErrorCode = "test-failure",
            ErrorMessage = "Sanitized test failure.",
            TimeStampCreated = now,
            TimeStampStarted = now,
            TimeStampCompleted = now,
        };
        store.DbContext.AddRange(worker, job, command);
        await store.DbContext.SaveChangesAsync();
        return (seed, job, command);
    }
}
