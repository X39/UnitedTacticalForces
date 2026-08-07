using Microsoft.EntityFrameworkCore;
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
            DiagnosticOutput = "redacted collected output",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });

        var operatorJob = projection.ProjectJob(job, operatorPrincipal);
        var viewerJob = projection.ProjectJob(job, viewerPrincipal);

        Assert.True(operatorJob.CanRetry);
        Assert.True(operatorJob.CanSkip);
        Assert.Equal("redacted collected output", Assert.Single(operatorJob.Commands).DiagnosticOutput);
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
                ModPackRevisions = [],
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

    private static GameServerProjectionService Projection(
        X39.UnitedTacticalForces.Database.DatabaseContext dbContext,
        params IRuntimeProvider[] providers)
        => Projection(dbContext, providers, NullLogger<GameServerProjectionService>.Instance);

    private static GameServerProjectionService Projection(
        X39.UnitedTacticalForces.Database.DatabaseContext dbContext,
        IReadOnlyCollection<IRuntimeProvider> providers,
        ILogger<GameServerProjectionService> logger)
        => new(
            dbContext,
            new TestGameServerDefinitionCatalog(),
            providers,
            new TestStorageDemandService(),
            new HostingAuthorizationService(dbContext),
            new AdministrativeSshAccess(Options.Create(new WorkerProvisioningOptions())),
            Options.Create(new StorageDemandOptions()),
            logger);

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

        public Task<IReadOnlyCollection<RuntimeProfile>> GetProfilesAsync(
            CancellationToken cancellationToken = default)
            => profileFailure is null
                ? Task.FromResult(profiles)
                : Task.FromException<IReadOnlyCollection<RuntimeProfile>>(profileFailure);

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
