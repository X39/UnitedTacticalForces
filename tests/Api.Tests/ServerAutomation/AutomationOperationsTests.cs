using Api.Tests.GameServerHosting;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers how automatic jobs and automation decisions surface in the activity feeds of a server: the job trigger and
/// the seven automation audit kinds with their fixed tone. The tests run against in-memory SQLite.
/// </summary>
public sealed class AutomationOperationsTests
{
    private static async Task<LifecycleJob> AddJobAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        long sequence,
        ELifecycleJobTrigger trigger,
        int? triggerLimit,
        Guid? requestedBy)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = requestedBy,
            Kind = ELifecycleJobKind.Stop,
            ActionIdentifier = GameServerActions.Stop,
            Sequence = sequence,
            Status = ELifecycleJobStatus.Succeeded,
            Stage = "completed",
            Trigger = trigger,
            TriggerLimit = triggerLimit,
            MaxAttempts = 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        store.DbContext.LifecycleJobs.Add(job);
        await store.DbContext.SaveChangesAsync();
        return job;
    }

    /// <summary>An automatic job carries its trigger and limit while a manual job reports a manual trigger.</summary>
    /// <returns>A task completing after the job projection was verified.</returns>
    [Fact]
    public async Task GetJobs_ProjectsTriggerAndLimit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var automatic = await AddJobAsync(store, seed, 1, ELifecycleJobTrigger.Idle, 30, null);
        var manual = await AddJobAsync(store, seed, 2, ELifecycleJobTrigger.Manual, null, seed.User.PrimaryKey);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var jobs = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetJobsAsync(seed.Server.PrimaryKey, viewer);

        var automaticDto = Assert.Single(jobs, q => q.PrimaryKey == automatic.PrimaryKey);
        Assert.Equal(ELifecycleJobTrigger.Idle, automaticDto.Trigger);
        Assert.Equal(30, automaticDto.TriggerLimit);
        Assert.Null(automaticDto.RequestedByName);
        var manualDto = Assert.Single(jobs, q => q.PrimaryKey == manual.PrimaryKey);
        Assert.Equal(ELifecycleJobTrigger.Manual, manualDto.Trigger);
        Assert.Null(manualDto.TriggerLimit);
        Assert.NotNull(manualDto.RequestedByName);
    }

    /// <summary>All seven automation audit kinds are listed in the operations feed with their fixed tone.</summary>
    /// <param name="kind">The automation audit kind.</param>
    /// <param name="expectedStatus">The tone the feed must report for it.</param>
    /// <returns>A task completing after the operation projection was verified.</returns>
    [Theory]
    [InlineData(EHostingAuditEventKind.ScheduledRunSkipped, "warning")]
    [InlineData(EHostingAuditEventKind.AutomaticStopWarned, "pending")]
    [InlineData(EHostingAuditEventKind.AutomaticStopRequested, "pending")]
    [InlineData(EHostingAuditEventKind.ScheduledStartRequested, "pending")]
    [InlineData(EHostingAuditEventKind.AutomaticStopCancelled, "succeeded")]
    [InlineData(EHostingAuditEventKind.AutomationSettingsChanged, "succeeded")]
    [InlineData(EHostingAuditEventKind.ScheduledStartsConfirmed, "succeeded")]
    public async Task GetOperations_ListsAutomationKindsWithFixedTone(
        EHostingAuditEventKind kind,
        string expectedStatus)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        new HostingAuditWriter().Add(
            store.DbContext,
            kind,
            "Automation decision.",
            serverId: seed.Server.PrimaryKey);
        await store.DbContext.SaveChangesAsync();
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var operations = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetOperationsAsync(seed.Server.PrimaryKey, viewer);

        var operation = Assert.Single(operations);
        Assert.Equal(kind, operation.Kind);
        Assert.Equal(expectedStatus, operation.Status);
    }

    /// <summary>An automation audit entry is not returned to a principal without view permission on the server.</summary>
    /// <returns>A task completing after the isolation was verified.</returns>
    [Fact]
    public async Task GetOperations_DoesNotListAutomationEntriesWithoutViewPermission()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        new HostingAuditWriter().Add(
            store.DbContext,
            EHostingAuditEventKind.AutomaticStopRequested,
            "Automatic stop requested.",
            serverId: seed.Server.PrimaryKey);
        await store.DbContext.SaveChangesAsync();

        var operations = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetOperationsAsync(seed.Server.PrimaryKey, HostingTestData.Principal(seed.User));

        Assert.Empty(operations);
    }
}
