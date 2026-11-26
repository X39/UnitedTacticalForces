using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class GameServerProjectionServiceTests
{
    /// <summary>External recommendations expose the raw allocation and its distinct usable-capacity estimate.</summary>
    /// <param name="ready">Whether external storage is configured and available.</param>
    /// <returns>A task completing after the projected capacity recommendation is verified.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StorageDemand_ProjectsUsableCapacityRecommendation(bool ready)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var provider = new CapabilityRuntimeProvider(
            "capacity-cloud",
            new RuntimeProviderCapabilities(true, true, true, Storage: new RuntimeStorageCapabilities(
                true, 10, 100, 1, ReadinessFailure: ready ? null : "Storage unavailable.")),
            [],
            null);
        var projection = Projection(store.DbContext, provider);

        var demand = await projection.EstimateStorageDemandAsync(new()
        {
            GameIdentifier = "arma3",
            RuntimeProviderIdentifier = provider.ProviderIdentifier,
        });

        if (ready)
        {
            Assert.Equal(10, demand.MinimumExternalCapacityGiB);
            Assert.Equal(8 * RuntimeStoragePlanResolver.GiB, demand.RecommendedExternalUsableBytes);
        }
        else
        {
            Assert.Null(demand.MinimumExternalCapacityGiB);
            Assert.Null(demand.RecommendedExternalUsableBytes);
        }
    }

    /// <summary>The real pause state controls whether provider profile discovery is attempted.</summary>
    /// <param name="paused">Whether the operator has suspended hosting operations.</param>
    /// <returns>A task completing after provider discovery and capability projection are verified.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitPause_ControlsProviderProfileDiscovery(bool paused)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var provider = (CapabilityRuntimeProvider)Provider(
            "available-cloud",
            profiles: [new RuntimeProfile("standard", "Standard", "General-purpose test profile.")]);
        var projection = Projection(
            store.DbContext,
            [provider],
            NullLogger<GameServerProjectionService>.Instance,
            PauseState(paused));

        var capability = Assert.Single(
            await projection.GetCapabilitiesAsync(new System.Security.Claims.ClaimsPrincipal()));

        Assert.Equal(paused ? 0 : 1, provider.ProfileCalls);
        Assert.Equal(
            paused ? RuntimeProviderAvailabilityStatuses.Paused : RuntimeProviderAvailabilityStatuses.Available,
            Assert.Single(capability.RuntimeProviderProfiles).AvailabilityStatus);
        Assert.All(capability.Actions, action => Assert.Equal(!paused, action.Enabled));
        if (paused)
        {
            Assert.Empty(capability.RuntimeProviders);
            Assert.All(
                capability.Actions,
                action => Assert.Equal(HostingControlPlaneState.PausedReason, action.UnavailableReason));
        }
        else
        {
            Assert.Equal(["available-cloud"], capability.RuntimeProviders);
        }
    }

    /// <summary>Paused projection disables even normally available Start and emergency Terminate actions.</summary>
    /// <param name="paused">Whether the operator has suspended hosting operations.</param>
    /// <param name="activeDeployment">Whether a running deployment makes Terminate applicable instead of Start.</param>
    /// <returns>A task completing after the operator-facing action has been projected.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task ExplicitPause_ControlsStartAndTerminateActions(bool paused, bool activeDeployment)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: activeDeployment);
        var projection = Projection(
            store.DbContext,
            [new TestRuntimeProvider()],
            NullLogger<GameServerProjectionService>.Instance,
            PauseState(paused));
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Operate);

        var server = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(server);
        var action = Assert.Single(
            server.Actions,
            q => q.Id == (activeDeployment ? GameServerActions.Terminate : GameServerActions.Start));
        Assert.Equal(!paused, action.Enabled);
        if (paused)
            Assert.Equal(HostingControlPlaneState.PausedReason, action.UnavailableReason);
    }

    /// <summary>Retained failed jobs keep their state while operator retry and skip are disabled by the pause.</summary>
    /// <param name="paused">Whether the operator has suspended hosting operations.</param>
    /// <returns>A task completing after the failed-job projection is verified.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitPause_ControlsRetryAndSkipWithoutChangingJob(bool paused)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var projection = Projection(
            store.DbContext,
            [new TestRuntimeProvider()],
            NullLogger<GameServerProjectionService>.Instance,
            PauseState(paused));
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.Operate);
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Kind = ELifecycleJobKind.Start,
            ActionIdentifier = GameServerActions.Start,
            Status = ELifecycleJobStatus.Failed,
            Stage = "failed",
            Attempt = 1,
            FailureCode = "transient-provider-failure",
        };

        var dto = projection.ProjectJob(job, principal);

        Assert.Equal(!paused, dto.CanRetry);
        Assert.Equal(!paused, dto.CanSkip);
        Assert.Equal(ELifecycleJobStatus.Failed, dto.Status);
        Assert.Equal(1, dto.AttemptCount);
        Assert.Equal("transient-provider-failure", dto.FailureCode);
        Assert.Equal(ELifecycleJobStatus.Failed, job.Status);
    }

    [Fact]
    public async Task Capabilities_KeepEveryRegisteredProviderButSelectOnlyProvidersWithProfiles()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var available = Provider(
            "available-cloud",
            profiles:
            [
                new RuntimeProfile("standard", "Standard", "General-purpose test profile."),
            ]);
        var configurationRequired = Provider(
            "configuration-required-cloud",
            isReady: false,
            readinessFailure: "secret provider configuration detail");
        var noProfiles = Provider("no-profiles-cloud");
        var projection = Projection(store.DbContext, available, configurationRequired, noProfiles);

        var capability = Assert.Single(
            await projection.GetCapabilitiesAsync(new System.Security.Claims.ClaimsPrincipal()));

        Assert.Equal(["available-cloud"], capability.RuntimeProviders);
        Assert.Equal(
            ["available-cloud", "configuration-required-cloud", "no-profiles-cloud"],
            capability.RuntimeProviderProfiles.Select(q => q.Identifier));
        Assert.Equal(
            [
                RuntimeProviderAvailabilityStatuses.Available,
                RuntimeProviderAvailabilityStatuses.ConfigurationRequired,
                RuntimeProviderAvailabilityStatuses.NoProfiles,
            ],
            capability.RuntimeProviderProfiles.Select(q => q.AvailabilityStatus));
    }

    [Fact]
    public async Task Capabilities_IsolateCatalogFailureFromOtherProviders()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var healthy = Provider(
            "healthy-cloud",
            profiles:
            [
                new RuntimeProfile("standard", "Standard", "General-purpose test profile."),
            ]);
        var failing = Provider(
            "unavailable-catalog-cloud",
            profileFailure: new InvalidOperationException("token=secret-catalog-token"));
        var logger = new RecordingLogger<GameServerProjectionService>();
        var projection = Projection(store.DbContext, [healthy, failing], logger);

        var capability = Assert.Single(
            await projection.GetCapabilitiesAsync(new System.Security.Claims.ClaimsPrincipal()));

        Assert.Equal(["healthy-cloud"], capability.RuntimeProviders);
        Assert.Equal(
            ["healthy-cloud", "unavailable-catalog-cloud"],
            capability.RuntimeProviderProfiles.Select(q => q.Identifier));
        Assert.Equal(
            [
                RuntimeProviderAvailabilityStatuses.Available,
                RuntimeProviderAvailabilityStatuses.CatalogUnavailable,
            ],
            capability.RuntimeProviderProfiles.Select(q => q.AvailabilityStatus));
        var warning = Assert.Single(logger.Messages);
        Assert.Contains("token=<redacted>", warning);
        Assert.DoesNotContain("secret-catalog-token", warning);
    }

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
    public async Task CreatorFullAccess_KeepsEmergencyCostStopAvailableWhileWaitingForWorker()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(
            store.DbContext,
            withActiveDeployment: true,
            EDeploymentStatus.WaitingForWorker);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var creatorPrincipal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.All);

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, creatorPrincipal);

        Assert.NotNull(dto);
        var terminate = dto.Actions.Single(q => q.Id == GameServerActions.Terminate);
        Assert.True(terminate.Enabled);
        Assert.False(terminate.Pending);
        Assert.Equal(seed.Server.Title, terminate.ConfirmationChallenge);
        Assert.Equal(ECostImpact.StopsCost, terminate.CostImpact);
    }

    /// <summary>Uses legacy worker and connection metadata when placement is missing or unreadable.</summary>
    /// <param name="snapshot">The stored placement data for the legacy deployment.</param>
    /// <returns>A task completing after the provider graph and connection have been projected.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("""{"version":99}""")]
    public async Task RunningDeployment_ProjectsDurableProviderGraphAndConnectionEndpoint(string? snapshot)
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
        store.DbContext.LifecycleWorkers.Add(new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = seed.Deployment.PrimaryKey,
            Identity = "projection-worker",
            Status = EWorkerStatus.Online,
            LastHeartbeatAt = now,
            DiskTotalBytes = 1_000,
            DiskAvailableBytes = 375,
            ControlledDeploymentBytes = 500,
            DiskTimeStampObserved = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        });
        await store.DbContext.SaveChangesAsync();
        await store.DbContext.GameServerDeployments
            .Where(q => q.PrimaryKey == seed.Deployment.PrimaryKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(q => q.RuntimeProfileIdentifier, "active-profile")
                .SetProperty(q => q.RuntimeProfileDisplayName, "Active profile")
                .SetProperty(q => q.PlacementSnapshot, snapshot)
                .SetProperty(q => q.EstimatedHourlyCostEur, 0.42m));
        var projection = HostingTestData.CreateProjectionService(
            store.DbContext,
            new WorkerProvisioningOptions
            {
                AdministrativeSshEnabled = true,
                AdministrativeSshAuthorizedKeys =
                [
                    "ssh-ed25519 MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDE= utf-admin",
                ],
                AdministrativeSshSourceCidrs = ["203.0.113.10/32"],
            });
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
        Assert.Null(dto.Deployment.AdministrativeSshCommand);
        Assert.Equal("active-profile", dto.Deployment.RuntimeProfileIdentifier);
        Assert.Equal("Active profile", dto.Deployment.RuntimeProfileDisplayName);
        Assert.Equal(0.42m, dto.Deployment.EstimatedHourlyCostEur);
        Assert.Equal(1_000, dto.Deployment.DiskTotalBytes);
        Assert.Equal(375, dto.Deployment.DiskAvailableBytes);
        Assert.Equal(now, dto.Deployment.DiskTimeStampObserved);
        Assert.Equal(2, dto.Deployment.Resources.Count);
        var primaryIp = dto.Deployment.Resources.Single(q => q.Kind == "primary-ip");
        Assert.Equal("assigned", primaryIp.ProviderState);
        Assert.Equal("203.0.113.91", primaryIp.PublicAddress);

        var administrator = HostingTestData.Principal(
            seed.User,
            (Claims.Administrative.All, string.Empty));
        var administrativeDto = await projection.GetServerAsync(seed.Server.PrimaryKey, administrator);
        Assert.Equal("ssh utf-admin@203.0.113.91", administrativeDto?.Deployment?.AdministrativeSshCommand);
    }

    /// <summary>Projects the persisted primary host by identifier and ordinal with its stored connection.</summary>
    /// <returns>A task completing after the selected worker and endpoint have been verified.</returns>
    [Fact]
    public async Task RunningDeployment_ProjectsPrimaryHostAndConnectionFromPlacement()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var primaryKey = new WorkloadPlacementKey("game-server", 1);
        var auxiliaryKey = new WorkloadPlacementKey("game-server", 0);
        var primary = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "placement-primary",
            ServiceIdentifier = primaryKey.ServiceIdentifier,
            ServiceOrdinal = primaryKey.ServiceOrdinal,
            Status = EWorkerStatus.Online,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var auxiliary = new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            Identity = "placement-auxiliary",
            ServiceIdentifier = auxiliaryKey.ServiceIdentifier,
            ServiceOrdinal = auxiliaryKey.ServiceOrdinal,
            Status = EWorkerStatus.Online,
            TimeStampCreated = now + NodaTime.Duration.FromSeconds(1),
            TimeStampUpdated = now,
        };
        store.DbContext.LifecycleWorkers.AddRange(primary, auxiliary);
        foreach (var (worker, address) in new[] { (primary, "203.0.113.91"), (auxiliary, "203.0.113.92") })
        {
            store.DbContext.DeploymentResources.Add(new DeploymentResource
            {
                PrimaryKey = Guid.NewGuid(),
                DeploymentFk = deployment.PrimaryKey,
                WorkerFk = worker.PrimaryKey,
                ProviderIdentifier = TestRuntimeProvider.Identifier,
                ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
                ProviderResourceIdentifier = $"server:{address}",
                Kind = "server",
                PublicAddress = address,
                State = EDeploymentResourceState.Allocated,
                TimeStampCreated = now,
                TimeStampUpdated = now,
            });
        }

        var storage = new RuntimeStoragePlan(ERuntimeStorageStrategy.IncludedDisk, 0, 0, "/srv/runtime", 0m);
        deployment.PlacementSnapshot = new WorkloadPlacementPlan(
            WorkloadPlacementPlan.CurrentVersion,
            primaryKey,
            new(ERuntimeNetworkProtocol.Tcp, 34000, "Primary connection"),
            [
                new(primaryKey, [new(ERuntimeNetworkProtocol.Tcp, 34000, 34000, "Primary ingress")],
                    "standard", storage, 0.1m, null, 0),
                new(auxiliaryKey, [], "standard", storage, 0.1m, null, 0),
            ],
            [new(primaryKey, primaryKey), new(auxiliaryKey, auxiliaryKey)]).Serialize();
        await store.DbContext.SaveChangesAsync();

        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);
        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, principal);

        Assert.NotNull(dto?.Deployment);
        Assert.Equal(primary.PrimaryKey, dto.Deployment.WorkerId);
        Assert.Equal("203.0.113.91", dto.Deployment.PublicAddress);
        Assert.Equal("203.0.113.91:34000", dto.Deployment.ConnectionEndpoint);
        Assert.Equal("TCP", dto.Deployment.ConnectionProtocol);
        Assert.Equal(34000, dto.Deployment.ConnectionPort);
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
        job.WorkerCommands.Add(new WorkerCommand
        {
            PrimaryKey = Guid.NewGuid(),
            LifecycleJobFk = job.PrimaryKey,
            DeploymentFk = job.DeploymentFk,
            WorkerFk = Guid.NewGuid(),
            Sequence = 1,
            ActionIdentifier = GameServerActions.CollectLogs,
            Status = EWorkerCommandStatus.Succeeded,
            Stage = "logs-collected",
            ErrorCode = "synthetic-diagnostic-code",
            DiagnosticOutput = "redacted collected output",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });

        var operatorJob = projection.ProjectJob(job, operatorPrincipal);
        var viewerJob = projection.ProjectJob(job, viewerPrincipal);

        Assert.True(operatorJob.CanRetry);
        Assert.True(operatorJob.CanSkip);
        var operatorCommand = Assert.Single(operatorJob.Commands);
        Assert.Equal("synthetic-diagnostic-code", operatorCommand.FailureCode);
        Assert.Equal("redacted collected output", operatorCommand.DiagnosticOutput);
        Assert.False(viewerJob.CanRetry);
        Assert.False(viewerJob.CanSkip);

        job.FailureCode = "permanent-firewall-create-failed";
        var permanentJob = projection.ProjectJob(job, operatorPrincipal);
        Assert.Equal("permanent-firewall-create-failed", permanentJob.FailureCode);
        Assert.False(permanentJob.CanRetry);
        Assert.False(permanentJob.CanSkip);
    }

    [Fact]
    public async Task ModPackOptions_ProjectOnlyActiveDefinitionsForTheServerGame()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var selected = Definition("Selected", HostingTestData.GameIdentifier, true);
        var inactive = Definition("Inactive", HostingTestData.GameIdentifier, false);
        var otherGame = Definition("Other game", "other-game", true);
        store.DbContext.ModPackDefinitions.AddRange(selected, inactive, otherGame);
        seed.Server.SelectedModPackDefinition = selected;
        seed.Server.SelectedModPackDefinitionFk = selected.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View);

        var options = await projection.GetModPackOptionsAsync(seed.Server.PrimaryKey, viewer);
        var server = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);

        var option = Assert.Single(options);
        Assert.Equal(selected.PrimaryKey, option.PrimaryKey);
        Assert.Equal("Selected", option.Title);
        Assert.NotNull(server);
        Assert.Equal(selected.PrimaryKey, server.SelectedModPackDefinitionId);
        Assert.Equal(selected.Title, server.SelectedModPackDefinitionTitle);

        ModPackDefinition Definition(string title, string gameIdentifier, bool isActive)
            => new()
            {
                Title = title,
                GameIdentifier = gameIdentifier,
                IsActive = isActive,
                OwnerFk = seed.User.PrimaryKey,
                TimeStampCreated = now,
                ModPackRevisionsOwned = [],
            };
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

    [Fact]
    public async Task Operations_ReturnOnlyOperationalEvidenceWithResourceAndTraceContext()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.HostingAuditRecords.AddRange(
            new HostingAuditRecord
            {
                PrimaryKey = Guid.NewGuid(),
                Kind = EHostingAuditEventKind.DeploymentResourceProvisioningFailed,
                GameServerFk = seed.Server.PrimaryKey,
                Summary = "Provider resource provisioning did not complete.",
                Details = """{"detail":"Provider=hetzner; Kind=primary-ip; Identity=primary-ip:91; Stage=create; Failure=rate-limited"}""",
                TraceId = "0123456789abcdef0123456789abcdef",
                SpanId = "0123456789abcdef",
                TimeStampCreated = now,
            },
            new HostingAuditRecord
            {
                PrimaryKey = Guid.NewGuid(),
                Kind = EHostingAuditEventKind.AccessGranted,
                GameServerFk = seed.Server.PrimaryKey,
                Summary = "Access granted.",
                TimeStampCreated = now + Duration.FromSeconds(1),
            });
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View);

        var operations = await projection.GetOperationsAsync(seed.Server.PrimaryKey, viewer);
        var hidden = await projection.GetOperationsAsync(
            seed.Server.PrimaryKey,
            HostingTestData.Principal(seed.User));

        var operation = Assert.Single(operations);
        Assert.Equal("failed", operation.Status);
        Assert.Equal("hetzner", operation.ProviderIdentifier);
        Assert.Equal("primary-ip", operation.ResourceKind);
        Assert.Equal("primary-ip:91", operation.ResourceIdentifier);
        Assert.Equal("create", operation.ProviderState);
        Assert.Equal("0123456789abcdef0123456789abcdef", operation.TraceId);
        Assert.Empty(hidden);
    }

    /// <summary>The user behind a user-kind cost owner receives their own user id and the owner display name.</summary>
    /// <returns>A task completing after the owning viewer's projection is verified.</returns>
    [Fact]
    public async Task CostOwnerUserId_IsProjectedForTheOwningUser()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);

        Assert.NotNull(dto);
        Assert.Equal(seed.User.PrimaryKey, dto.CostOwnerUserId);
        Assert.Equal("Hosting operator", dto.CostOwnerName);
    }

    /// <summary>A viewer who may not see the cost owner learns neither its name nor its user id.</summary>
    /// <returns>A task completing after the hidden-owner projection is verified.</returns>
    [Fact]
    public async Task CostOwnerUserId_IsWithheldWhenTheViewerCannotSeeTheOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(other.User, seed.Server, Claims.ResourceBased.Server.View);

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);

        Assert.NotNull(dto);
        Assert.Null(dto.CostOwnerUserId);
        Assert.Equal("Assigned owner", dto.CostOwnerName);
    }

    /// <summary>A different viewer holding the cost owner view claim receives the owner's user id.</summary>
    /// <returns>A task completing after the visible-owner projection is verified.</returns>
    [Fact]
    public async Task CostOwnerUserId_IsProjectedWhenTheViewerMayViewTheOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.Principal(
            other.User,
            (Claims.ResourceBased.Server.View, seed.Server.PrimaryKey.ToString()),
            (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString()));

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);

        Assert.NotNull(dto);
        Assert.Equal(seed.User.PrimaryKey, dto.CostOwnerUserId);
        Assert.Equal("Hosting operator", dto.CostOwnerName);
    }

    /// <summary>A visible owner that is not a user projects its display name but no user id.</summary>
    /// <returns>A task completing after the Verein-owner projection is verified.</returns>
    [Fact]
    public async Task CostOwnerUserId_IsNullForANonUserOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.Kind = ECostOwnerKind.Verein;
        seed.CostOwner.UserFk = null;
        seed.CostOwner.User = null;
        seed.CostOwner.DisplayName = "Verein";
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var administrator = HostingTestData.Principal(seed.User, (Claims.Administrative.All, string.Empty));

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, administrator);

        Assert.NotNull(dto);
        Assert.Null(dto.CostOwnerUserId);
        Assert.Equal("Verein", dto.CostOwnerName);
    }

    /// <summary>A hosting administrator viewing a user-kind owner receives the owner's user id.</summary>
    /// <returns>A task completing after the administrator projection is verified.</returns>
    [Fact]
    public async Task CostOwnerUserId_IsProjectedForAHostingAdministrator()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var administrator = HostingTestData.Principal(other.User, (Claims.Administrative.All, string.Empty));

        var dto = await projection.GetServerAsync(seed.Server.PrimaryKey, administrator);

        Assert.NotNull(dto);
        Assert.Equal(seed.User.PrimaryKey, dto.CostOwnerUserId);
        Assert.Equal("Hosting operator", dto.CostOwnerName);
    }

    /// <summary>
    /// The manage-access flag follows the exact rule of the manage-access requirement in the detail and in the list.
    /// </summary>
    /// <param name="scenario">The named claim set the viewer holds.</param>
    /// <param name="expected">Whether the viewer may manage access.</param>
    /// <returns>A task completing after both projections are verified.</returns>
    [Theory]
    [InlineData("adm", true)]
    [InlineData("adm:sv", true)]
    [InlineData("adm:hosting", true)]
    [InlineData("sv", true)]
    [InlineData("view+manage-access", true)]
    [InlineData("view+modify", false)]
    [InlineData("view", false)]
    [InlineData("manage-access-of-another-server", false)]
    public async Task CanManageAccess_MirrorsTheManageAccessRequirement(string scenario, bool expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var serverId = seed.Server.PrimaryKey.ToString();
        (string type, string value)[] claims = scenario switch
        {
            "adm" => [(Claims.Administrative.All, string.Empty)],
            "adm:sv" => [(Claims.Administrative.Server, string.Empty)],
            "adm:hosting" => [(Claims.Administrative.Hosting, string.Empty)],
            "sv" => [(Claims.ResourceBased.Server.All, serverId)],
            "view+manage-access" =>
            [
                (Claims.ResourceBased.Server.View, serverId),
                (Claims.ResourceBased.Server.ManageAccess, serverId),
            ],
            "view+modify" =>
            [
                (Claims.ResourceBased.Server.View, serverId),
                (Claims.ResourceBased.Server.Modify, serverId),
            ],
            "view" => [(Claims.ResourceBased.Server.View, serverId)],
            "manage-access-of-another-server" =>
            [
                (Claims.ResourceBased.Server.View, serverId),
                (Claims.ResourceBased.Server.ManageAccess, other.Server.PrimaryKey.ToString()),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
        var viewer = HostingTestData.Principal(other.User, claims);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);
        var list = await projection.GetServersAsync(viewer);

        Assert.NotNull(detail);
        Assert.Equal(expected, detail.CanManageAccess);
        Assert.Equal(expected, list.Single(q => q.PrimaryKey == seed.Server.PrimaryKey).CanManageAccess);
    }

    /// <summary>The frozen flag mirrors the current owner for a viewer who may see the owner.</summary>
    /// <param name="frozen">Whether the current owner is frozen.</param>
    /// <returns>A task completing after the detail and list projections are verified.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CostOwnerIsFrozen_MirrorsTheOwnerForAViewerWhoMaySeeIt(bool frozen)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.IsFrozen = frozen;
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);
        var list = await projection.GetServersAsync(viewer);

        Assert.NotNull(detail);
        Assert.Equal(frozen, detail.CostOwnerIsFrozen);
        Assert.Equal(frozen, list.Single().CostOwnerIsFrozen);
    }

    /// <summary>A viewer who may not see the owner learns neither the frozen state nor the assignment time.</summary>
    /// <returns>A task completing after the hidden-owner projection is verified.</returns>
    [Fact]
    public async Task CostOwnerFacts_AreWithheldWhenTheViewerCannotSeeTheOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.CostOwner.IsFrozen = true;
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(other.User, seed.Server, Claims.ResourceBased.Server.View);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);
        var list = await projection.GetServersAsync(viewer);

        Assert.NotNull(detail);
        Assert.Null(detail.CostOwnerIsFrozen);
        Assert.Null(detail.CostOwnerAssignedAt);
        Assert.Null(list.Single(q => q.PrimaryKey == seed.Server.PrimaryKey).CostOwnerIsFrozen);
    }

    /// <summary>
    /// The detail reports the time of the open assignment, ignoring closed ones, and the list leaves it empty.
    /// </summary>
    /// <returns>A task completing after the assignment time projection is verified.</returns>
    [Fact]
    public async Task CostOwnerAssignedAt_IsTheOpenAssignmentInTheDetailOnly()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var openAssignment = await store.DbContext.GameServerCostOwnerAssignments
            .SingleAsync(q => q.GameServerFk == seed.Server.PrimaryKey);
        var closedAt = Instant.FromUnixTimeSeconds(1_600_000_000);
        var openAt = Instant.FromUnixTimeSeconds(1_700_000_500);
        openAssignment.TimeStampAssigned = openAt;
        store.DbContext.GameServerCostOwnerAssignments.Add(new GameServerCostOwnerAssignment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            AssignedByFk = seed.User.PrimaryKey,
            UnassignedByFk = seed.User.PrimaryKey,
            TimeStampAssigned = closedAt,
            TimeStampUnassigned = Instant.FromUnixTimeSeconds(1_650_000_000),
        });
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var detail = await projection.GetServerAsync(seed.Server.PrimaryKey, viewer);
        var list = await projection.GetServersAsync(viewer);

        Assert.NotNull(detail);
        Assert.Equal(openAt, detail.CostOwnerAssignedAt);
        Assert.Null(list.Single().CostOwnerAssignedAt);
    }

    /// <summary>A job requested by a user projects that user's nickname for a viewer of the server.</summary>
    /// <returns>A task completing after the requester projection is verified.</returns>
    [Fact]
    public async Task RequestedByName_IsTheRequesterNickname()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddJobAsync(store, seed, seed.User.PrimaryKey);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var jobs = await projection.GetJobsAsync(seed.Server.PrimaryKey, viewer);

        var job = Assert.Single(jobs);
        Assert.Equal("hosting-operator", job.RequestedByName);
    }

    /// <summary>A job without a requesting user, such as an automatic cost-stop, projects no requester name.</summary>
    /// <returns>A task completing after the platform-job projection is verified.</returns>
    [Fact]
    public async Task RequestedByName_IsNullForPlatformJobs()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddJobAsync(store, seed, requestedByFk: null);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var jobs = await projection.GetJobsAsync(seed.Server.PrimaryKey, viewer);

        var job = Assert.Single(jobs);
        Assert.Null(job.RequestedByName);
    }

    /// <summary>A deleted requesting user is withheld behind the hidden-member placeholder.</summary>
    /// <returns>A task completing after the deleted-requester projection is verified.</returns>
    [Fact]
    public async Task RequestedByName_HidesDeletedUsers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddJobAsync(store, seed, seed.User.PrimaryKey);
        seed.User.IsDeleted = true;
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var jobs = await projection.GetJobsAsync(seed.Server.PrimaryKey, viewer);

        var job = Assert.Single(jobs);
        Assert.Equal("Hidden member", job.RequestedByName);
    }

    /// <summary>A requester reference without a loaded user never reads as the platform.</summary>
    /// <returns>A task completing after the unloaded-navigation projection is verified.</returns>
    [Fact]
    public async Task RequestedByName_UsesPlaceholderWhenNavigationIsMissing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = seed.User.PrimaryKey,
            RequestedBy = null,
            Kind = ELifecycleJobKind.Restart,
            ActionIdentifier = GameServerActions.Restart,
            Sequence = 1,
            Status = ELifecycleJobStatus.Queued,
            Stage = "queued",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var dto = projection.ProjectJob(job, viewer);

        Assert.Equal("Hidden member", dto.RequestedByName);
    }

    /// <summary>A viewer without the server view permission still receives no jobs and therefore no actor names.</summary>
    /// <returns>A task completing after the unauthorized read is verified.</returns>
    [Fact]
    public async Task RequestedByName_IsNotExposedWithoutTheViewPermission()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await AddJobAsync(store, seed, seed.User.PrimaryKey);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);

        var jobs = await projection.GetJobsAsync(seed.Server.PrimaryKey, HostingTestData.Principal(seed.User));

        Assert.Empty(jobs);
    }

    /// <summary>An operational audit record caused by a user projects that user's nickname.</summary>
    /// <returns>A task completing after the actor projection is verified.</returns>
    [Fact]
    public async Task ActorName_IsTheActorNickname()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await AddOperationAsync(store, seed, seed.User.PrimaryKey);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var operations = await projection.GetOperationsAsync(seed.Server.PrimaryKey, viewer);

        var operation = Assert.Single(operations);
        Assert.Equal("hosting-operator", operation.ActorName);
    }

    /// <summary>An operational audit record without an actor projects no actor name.</summary>
    /// <returns>A task completing after the platform-record projection is verified.</returns>
    [Fact]
    public async Task ActorName_IsNullForPlatformRecords()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await AddOperationAsync(store, seed, actorFk: null);
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var operations = await projection.GetOperationsAsync(seed.Server.PrimaryKey, viewer);

        var operation = Assert.Single(operations);
        Assert.Null(operation.ActorName);
    }

    /// <summary>A deleted actor is withheld behind the hidden-member placeholder.</summary>
    /// <returns>A task completing after the deleted-actor projection is verified.</returns>
    [Fact]
    public async Task ActorName_HidesDeletedUsers()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await AddOperationAsync(store, seed, seed.User.PrimaryKey);
        seed.User.IsDeleted = true;
        await store.DbContext.SaveChangesAsync();
        var projection = HostingTestData.CreateProjectionService(store.DbContext);
        var viewer = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.View);

        var operations = await projection.GetOperationsAsync(seed.Server.PrimaryKey, viewer);

        var operation = Assert.Single(operations);
        Assert.Equal("Hidden member", operation.ActorName);
    }

    /// <summary>Persists one operational audit record for the seeded server with the given actor.</summary>
    /// <param name="store">The isolated test database.</param>
    /// <param name="seed">The seeded server.</param>
    /// <param name="actorFk">The acting user, or <see langword="null"/> for a platform event.</param>
    /// <returns>A task completing after the record is saved.</returns>
    private static async Task AddOperationAsync(HostingTestStore store, HostingTestSeed seed, Guid? actorFk)
    {
        store.DbContext.HostingAuditRecords.Add(new HostingAuditRecord
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = EHostingAuditEventKind.LifecycleJobQueued,
            ActorFk = actorFk,
            GameServerFk = seed.Server.PrimaryKey,
            Summary = "Lifecycle job queued.",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });
        await store.DbContext.SaveChangesAsync();
    }

    /// <summary>Persists one queued restart job for the seeded server with the given requester.</summary>
    /// <param name="store">The isolated test database.</param>
    /// <param name="seed">The seeded server and active deployment.</param>
    /// <param name="requestedByFk">The requesting user, or <see langword="null"/> for a platform action.</param>
    /// <returns>A task completing after the job is saved.</returns>
    private static async Task AddJobAsync(HostingTestStore store, HostingTestSeed seed, Guid? requestedByFk)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        store.DbContext.LifecycleJobs.Add(new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = seed.Server.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            RequestedByFk = requestedByFk,
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
    }

    private static GameServerProjectionService Projection(
        X39.UnitedTacticalForces.Database.DatabaseContext dbContext,
        params IRuntimeProvider[] providers)
        => Projection(dbContext, providers, NullLogger<GameServerProjectionService>.Instance);

    /// <summary>Builds a projection with controlled providers and optional production pause state.</summary>
    /// <param name="dbContext">The isolated test database.</param>
    /// <param name="providers">The provider catalog available to this projection.</param>
    /// <param name="logger">The logger receiving sanitized discovery failures.</param>
    /// <param name="state">An explicit pause state, or the legacy fixture default when omitted.</param>
    /// <returns>The service used to project hosting capabilities and operations.</returns>
    private static GameServerProjectionService Projection(
        X39.UnitedTacticalForces.Database.DatabaseContext dbContext,
        IReadOnlyCollection<IRuntimeProvider> providers,
        ILogger<GameServerProjectionService> logger,
        HostingControlPlaneState? state = null)
        => new(
            dbContext,
            new TestGameServerDefinitionCatalog(),
            providers,
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            logger,
            new GameServerConfigurationService(dbContext),
            Options.Create(new HostingFinanceOptions()),
            Api.Tests.ServerAutomation.ServerAutomationTestData.CreateProjector(dbContext),
            state);

    /// <summary>Creates the production state from an explicit public operator setting.</summary>
    /// <param name="paused">Whether hosting operations must be paused.</param>
    /// <returns>The validated production pause state.</returns>
    private static HostingControlPlaneState PauseState(bool paused)
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HostingControlPlaneState.ConfigurationKey] = paused.ToString(),
        }).Build());

    private static IRuntimeProvider Provider(
        string identifier,
        bool isReady = true,
        string? readinessFailure = null,
        IReadOnlyCollection<RuntimeProfile>? profiles = null,
        Exception? profileFailure = null)
        => new CapabilityRuntimeProvider(
            identifier,
            new RuntimeProviderCapabilities(
                true,
                true,
                true,
                IsReady: isReady,
                ReadinessFailure: readinessFailure),
            profiles ?? [],
            profileFailure);

    private sealed class CapabilityRuntimeProvider(
        string identifier,
        RuntimeProviderCapabilities capabilities,
        IReadOnlyCollection<RuntimeProfile> profiles,
        Exception? profileFailure) : IRuntimeProvider
    {
        public string ProviderIdentifier => identifier;

        public RuntimeProviderCapabilities Capabilities => capabilities;

        /// <summary>Gets the number of profile requests made by the capability projection.</summary>
        /// <value>Zero until profile discovery is invoked.</value>
        public int ProfileCalls { get; private set; }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<RuntimeProfile>> GetProfilesAsync(
            CancellationToken cancellationToken = default)
        {
            ProfileCalls++;
            return profileFailure is null
                ? Task.FromResult(profiles)
                : Task.FromException<IReadOnlyCollection<RuntimeProfile>>(profileFailure);
        }

        public Task<RuntimeProvisionResult> ProvisionAsync(
            RuntimeProvisionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeObservationResult> ObserveAsync(
            RuntimeObservationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeReleaseResult> ReleaseAsync(
            RuntimeReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeReleaseResult> ReleaseDanglingAsync(
            RuntimeDanglingReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimeDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
