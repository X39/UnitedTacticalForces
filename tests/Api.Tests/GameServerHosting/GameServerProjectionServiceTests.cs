using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerProjectionServiceTests
{
    [Fact]
    public async Task StoppedServer_ProjectsStartAsAvailableOnlyToOperator()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var operatorPrincipal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Operate);
        var viewerPrincipal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View);

        var operatorView = await projection.GetServerAsync(seed.Server.PrimaryKey, operatorPrincipal);
        var viewerView = await projection.GetServerAsync(seed.Server.PrimaryKey, viewerPrincipal);

        Assert.NotNull(operatorView);
        Assert.True(operatorView.Actions.Single(q => q.Id == GameServerActions.Start).Enabled);
        Assert.False(operatorView.Actions.Single(q => q.Id == GameServerActions.Stop).Enabled);
        Assert.NotNull(viewerView);
        Assert.All(viewerView.Actions, action => Assert.False(action.Enabled));
        Assert.All(
            viewerView.Actions,
            action => Assert.Equal("Server Operate permission is required.", action.UnavailableReason));
    }

    [Fact]
    public async Task PendingWork_DisablesOrdinaryActionsButKeepsEmergencyCostStopAvailable()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.LifecycleJobs.Add(new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        });
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Operate);

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(dto);
        Assert.Equal(1, dto.PendingJobCount);
        Assert.False(dto.Actions.Single(q => q.Id == GameServerActions.Stop).Enabled);
        Assert.Equal(
            "Another lifecycle operation is pending.",
            dto.Actions.Single(q => q.Id == GameServerActions.Stop).UnavailableReason);
        Assert.True(dto.Actions.Single(q => q.Id == GameServerActions.Terminate).Enabled);
        Assert.Equal(ECostImpact.StopsCost, dto.Actions.Single(q => q.Id == GameServerActions.Terminate).CostImpact);
    }

    [Fact]
    public async Task RunningDeployment_ProjectsDurableProviderGraphAndConnectionEndpoint()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.DeploymentResources.AddRange(
            new DeploymentResource
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = seed.Deployment!.PrimaryKey,
                ProviderIdentifier = TestRuntimeProvider.Identifier,
                ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
                ProviderResourceIdentifier = "primary-ip:91",
                Kind = "primary-ip",
                ProviderState = "assigned",
                PublicAddress = "203.0.113.91",
                State = EDeploymentResourceState.Allocated,
                PossibleCost = true,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                TimeStampObserved = now,
            },
            new DeploymentResource
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = seed.Deployment.PrimaryKey,
                ProviderIdentifier = TestRuntimeProvider.Identifier,
                ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
                ProviderResourceIdentifier = "server:42",
                Kind = "server",
                ProviderState = "running",
                PublicAddress = "203.0.113.91",
                State = EDeploymentResourceState.Allocated,
                PossibleCost = true,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                TimeStampObserved = now,
            });
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View);

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(dto?.Deployment);
        Assert.Equal("203.0.113.91", dto.Deployment.PublicAddress);
        Assert.Equal("203.0.113.91:32000", dto.Deployment.ConnectionEndpoint);
        Assert.Equal("UDP", dto.Deployment.ConnectionProtocol);
        Assert.Equal(32000, dto.Deployment.ConnectionPort);
        Assert.Equal(2, dto.Deployment.Resources.Count);
        var primaryIp = dto.Deployment.Resources.Single(q => q.Kind == "primary-ip");
        Assert.Equal("assigned", primaryIp.ProviderState);
        Assert.Equal("203.0.113.91", primaryIp.PublicAddress);
    }

    [Fact]
    public void FailedJob_ProjectsRetryAndSkipOnlyForOperator()
    {
        var serverId = Guid.NewGuid();
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = serverId,
            DeploymentFk = Guid.NewGuid(),
            RequestedByFk = Guid.NewGuid(),
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Status = ELifecycleJobStatus.Failed,
            Stage = "failed",
            MaxAttempts = 3,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new NodaTimeSqliteApiDbContext(options);
        var projection = HostingTestData.CreateProjectionService(dbContext);
        var user = new X39.UnitedTacticalForces.Database.Authority.User
        {
            PrimaryKey = job.RequestedByFk!.Value,
        };
        var operatorPrincipal = HostingTestData.Principal(
            user,
            (Claims.ResourceBased.Server.Operate, serverId.ToString()));
        var viewerPrincipal = HostingTestData.Principal(user);

        var operatorJob = projection.ProjectJob(job, operatorPrincipal);
        var viewerJob = projection.ProjectJob(job, viewerPrincipal);

        Assert.True(operatorJob.CanRetry);
        Assert.True(operatorJob.CanSkip);
        Assert.False(viewerJob.CanRetry);
        Assert.False(viewerJob.CanSkip);

        job.FailureCode = "permanent-execution-failed";
        var permanentJob = projection.ProjectJob(job, operatorPrincipal);
        Assert.False(permanentJob.CanRetry);
        Assert.False(permanentJob.CanSkip);
    }

    [Fact]
    public async Task PermanentlyRejectedStart_DoesNotRemainAnOperationalNotification()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Failed);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        seed.Deployment!.PossibleCost = false;
        seed.Deployment.TimeStampResolved = now;
        store.DbContext.LifecycleJobs.Add(new LifecycleJob
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
            FailureCode = "permanent-execution-failed",
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampCompleted = now,
        });
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Operate);

        var notifications = await projection.GetNotificationsAsync(principal);

        Assert.Empty(notifications);
    }
}
