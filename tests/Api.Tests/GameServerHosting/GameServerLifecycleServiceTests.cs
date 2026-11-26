using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Core;
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
            new TestStorageDemandService(),
            new HostingAuthorizationService(store.DbContext),
            new HostingAuditWriter(),
            new TestGameServerSecretService(),
            new GameServerConfigurationService(store.DbContext),
            new GameServerContentSnapshotResolver(store.DbContext),
            Options.Create(new HostingFinanceOptions()));

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
        Assert.Contains(
            await store.DbContext.Users
                .Where(q => q.PrimaryKey == user.PrimaryKey)
                .SelectMany(q => q.Claims!)
                .ToArrayAsync(),
            q => q.Identifier == Claims.ResourceBased.Server.All
                 && q.Value == server.PrimaryKey.ToString());
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
        Assert.Equal("default", deployment.RuntimeProfileIdentifier);
        Assert.Equal("Default", deployment.RuntimeProfileDisplayName);
        Assert.Equal("[]", deployment.ContentSnapshot);
        Assert.Equal(0, deployment.PeakStorageRequiredBytes);
        Assert.Equal(EStorageEstimateStatus.Incomplete.ToString(), deployment.StorageEstimateStatus);
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
    public async Task ChangeRuntimeProfile_WithUnresolvedDeployment_UpdatesDesiredProfileOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deploymentId = seed.Deployment!.PrimaryKey;
        seed.Server.RuntimeProfileIdentifier = "legacy";
        seed.Server.RuntimeProfileDisplayName = "Legacy profile";
        seed.Server.EstimatedHourlyCostEur = 9m;
        seed.Server.EstimatedMonthlyCostEur = 900m;
        await store.DbContext.SaveChangesAsync();
        await store.DbContext.GameServerDeployments
            .Where(q => q.PrimaryKey == deploymentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(q => q.RuntimeProfileIdentifier, "legacy")
                .SetProperty(q => q.RuntimeProfileDisplayName, "Legacy profile")
                .SetProperty(q => q.EstimatedHourlyCostEur, 9m));
        store.DbContext.ChangeTracker.Clear();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);

        await service.ChangeRuntimeProfileAsync(seed.Server.PrimaryKey, "default", principal);

        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        var deployment = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == deploymentId);
        Assert.Equal("default", server.RuntimeProfileIdentifier);
        Assert.Equal("Default", server.RuntimeProfileDisplayName);
        Assert.Equal("legacy", deployment.RuntimeProfileIdentifier);
        Assert.Equal("Legacy profile", deployment.RuntimeProfileDisplayName);
        Assert.Equal(9m, deployment.EstimatedHourlyCostEur);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.ServerUpdated
                 && q.GameServerFk == seed.Server.PrimaryKey
                 && (q.Details?.Contains("AppliesTo=next-deployment", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public async Task ChangeModPack_ValidatesActiveGameScopedSelectionAndAllowsRunningDesiredState()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var matching = Definition("Matching pack", HostingTestData.GameIdentifier, isActive: true);
        var inactive = Definition("Inactive pack", HostingTestData.GameIdentifier, isActive: false);
        var otherGame = Definition("Other game pack", "another-game", isActive: true);
        store.DbContext.ModPackDefinitions.AddRange(matching, inactive, otherGame);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Modify);

        await Assert.ThrowsAsync<HostingValidationException>(() => service.ChangeModPackAsync(
            seed.Server.PrimaryKey,
            inactive.PrimaryKey,
            principal));
        await Assert.ThrowsAsync<HostingValidationException>(() => service.ChangeModPackAsync(
            seed.Server.PrimaryKey,
            otherGame.PrimaryKey,
            principal));
        await service.ChangeModPackAsync(seed.Server.PrimaryKey, matching.PrimaryKey, principal);

        store.DbContext.ChangeTracker.Clear();
        var selected = await store.DbContext.GameServers
            .Where(q => q.PrimaryKey == seed.Server.PrimaryKey)
            .Select(q => q.SelectedModPackDefinitionFk)
            .SingleAsync();
        Assert.Equal(matching.PrimaryKey, selected);
        Assert.Null(seed.Deployment!.TimeStampResolved);

        await service.ChangeModPackAsync(seed.Server.PrimaryKey, null, principal);
        store.DbContext.ChangeTracker.Clear();
        Assert.Null(await store.DbContext.GameServers
            .Where(q => q.PrimaryKey == seed.Server.PrimaryKey)
            .Select(q => q.SelectedModPackDefinitionFk)
            .SingleAsync());

        ModPackDefinition Definition(string title, string gameIdentifier, bool isActive)
        {
            var definition = new ModPackDefinition
            {
                Title = title,
                GameIdentifier = gameIdentifier,
                IsActive = isActive,
                OwnerFk = seed.User.PrimaryKey,
                TimeStampCreated = now,
                ModPackRevisionsOwned = [],
            };
            definition.ModPackRevisionsOwned.Add(new ModPackRevision
            {
                Definition = definition,
                UpdatedByFk = seed.User.PrimaryKey,
                IsActive = true,
                TimeStampCreated = now,
                Mods = [],
            });
            return definition;
        }
    }

    /// <summary>Verifies that a pack change clears the pinned revision so the Start snapshot resolves the new pack.</summary>
    [Fact]
    public async Task ChangeModPack_ClearsPinnedRevisionAndMarksModPackPending()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var packA = NewPack("Pack A", seed, isActive: true, withActiveRevision: true);
        var packB = NewPack("Pack B", seed, isActive: true, withActiveRevision: true);
        store.DbContext.ModPackDefinitions.AddRange(packA, packB);
        await store.DbContext.SaveChangesAsync();
        seed.Server.SelectedModPackDefinitionFk = packA.PrimaryKey;
        seed.Server.PinnedModPackRevisionFk = packA.ModPackRevisionsOwned!.Single().PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        await service.ChangeModPackAsync(seed.Server.PrimaryKey, packB.PrimaryKey, principal);

        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(packB.PrimaryKey, server.SelectedModPackDefinitionFk);
        Assert.Null(server.PinnedModPackRevisionFk);
        Assert.Equal(EGameServerChangeArea.ModPack, server.PendingStartChangeAreas);
        Assert.NotNull(server.PendingStartSince);
        Assert.Contains(
            await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.GameServerFk == seed.Server.PrimaryKey
                 && (q.Details?.Contains("PinCleared=True", StringComparison.Ordinal) ?? false));
        var references = await new GameServerContentSnapshotResolver(store.DbContext)
            .ResolveAsync(seed.Server.PrimaryKey);
        Assert.Empty(references);
    }

    /// <summary>Verifies that unusable packs are rejected with the reason the options show and nothing changes.</summary>
    [Fact]
    public async Task ChangeModPack_RejectsUnusablePacksWithSharedReasons()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var inactive = NewPack("Inactive", seed, isActive: false, withActiveRevision: true);
        var withoutRevision = NewPack("No revision", seed, isActive: true, withActiveRevision: false);
        var brokenMod = new Mod
        {
            PrimaryKey = Guid.NewGuid(),
            GameIdentifier = HostingTestData.GameIdentifier,
            ProviderIdentifier = "steam-workshop",
            CanonicalSourceIdentifier = "1",
            Title = "Retired mod",
            IsActive = false,
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        var unresolvable = NewPack("Unresolvable", seed, isActive: true, withActiveRevision: true);
        unresolvable.ModPackRevisionsOwned!.Single().Mods =
            [new ModPackRevisionMod { Mod = brokenMod, ModFk = brokenMod.PrimaryKey, Order = 0 }];
        store.DbContext.ModPackDefinitions.AddRange(inactive, withoutRevision, unresolvable);
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        var inactiveError = await Assert.ThrowsAsync<HostingValidationException>(
            () => service.ChangeModPackAsync(seed.Server.PrimaryKey, inactive.PrimaryKey, principal));
        var revisionError = await Assert.ThrowsAsync<HostingValidationException>(
            () => service.ChangeModPackAsync(seed.Server.PrimaryKey, withoutRevision.PrimaryKey, principal));
        var unresolvableError = await Assert.ThrowsAsync<HostingValidationException>(
            () => service.ChangeModPackAsync(seed.Server.PrimaryKey, unresolvable.PrimaryKey, principal));

        Assert.Equal(ModPackAvailabilityReasons.Inactive, inactiveError.Message);
        Assert.Equal(ModPackAvailabilityReasons.NoActiveRevision, revisionError.Message);
        Assert.Equal(ModPackAvailabilityReasons.Unresolvable, unresolvableError.Message);
        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Null(server.SelectedModPackDefinitionFk);
        Assert.Equal(EGameServerChangeArea.None, server.PendingStartChangeAreas);
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    /// <summary>Verifies that re-selecting the saved pack is a no-op even when that pack is inactive now.</summary>
    [Fact]
    public async Task ChangeModPack_ReselectingSavedPack_ChangesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var saved = NewPack("Saved", seed, isActive: false, withActiveRevision: true);
        store.DbContext.ModPackDefinitions.Add(saved);
        await store.DbContext.SaveChangesAsync();
        seed.Server.SelectedModPackDefinitionFk = saved.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        await service.ChangeModPackAsync(seed.Server.PrimaryKey, saved.PrimaryKey, principal);

        store.DbContext.ChangeTracker.Clear();
        var server = await store.DbContext.GameServers.SingleAsync(q => q.PrimaryKey == seed.Server.PrimaryKey);
        Assert.Equal(saved.PrimaryKey, server.SelectedModPackDefinitionFk);
        Assert.Equal(EGameServerChangeArea.None, server.PendingStartChangeAreas);
        Assert.Null(server.PendingStartSince);
        Assert.Empty(await store.DbContext.HostingAuditRecords.ToArrayAsync());
    }

    private static ModPackDefinition NewPack(
        string title,
        HostingTestSeed seed,
        bool isActive,
        bool withActiveRevision)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var definition = new ModPackDefinition
        {
            Title = title,
            GameIdentifier = HostingTestData.GameIdentifier,
            IsActive = isActive,
            OwnerFk = seed.User.PrimaryKey,
            TimeStampCreated = now,
            ModPackRevisionsOwned = [],
        };
        if (withActiveRevision)
        {
            definition.ModPackRevisionsOwned.Add(new ModPackRevision
            {
                Definition = definition,
                UpdatedByFk = seed.User.PrimaryKey,
                IsActive = true,
                TimeStampCreated = now,
                Mods = [],
            });
        }
        return definition;
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
    public async Task AutomaticCostStop_WithExistingUserTerminate_PreservesUserTerminateSemantics()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            deploymentStatus: EDeploymentStatus.Running);
        var service = HostingTestData.CreateLifecycleService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);
        var terminate = await service.EnqueueActionAsync(
            seed.Server.PrimaryKey,
            GameServerActions.Terminate,
            seed.Server.Title,
            principal);
        var automaticCostStop = new AutomaticCostStopService(store.DbContext, new HostingAuditWriter());

        var queuedAutomaticTermination = await automaticCostStop.TriggerAsync(
            seed.Deployment!.PrimaryKey,
            "worker-control-lost",
            "The worker heartbeat became stale.");

        store.DbContext.ChangeTracker.Clear();
        var deployment = await store.DbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == seed.Deployment.PrimaryKey);
        var persistedTerminate = await store.DbContext.LifecycleJobs
            .SingleAsync(q => q.PrimaryKey == terminate.PrimaryKey);
        Assert.False(queuedAutomaticTermination);
        Assert.Equal(EDeploymentStatus.Terminating, deployment.Status);
        Assert.Equal("termination-requested", deployment.Stage);
        Assert.Null(deployment.FaultCode);
        Assert.Null(deployment.FaultMessage);
        Assert.Equal(seed.User.PrimaryKey, persistedTerminate.RequestedByFk);
        Assert.Equal(ELifecycleJobStatus.Queued, persistedTerminate.Status);
        Assert.Single(await store.DbContext.LifecycleJobs
            .Where(q => q.DeploymentFk == deployment.PrimaryKey && q.Kind == ELifecycleJobKind.Terminate)
            .ToArrayAsync());
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
