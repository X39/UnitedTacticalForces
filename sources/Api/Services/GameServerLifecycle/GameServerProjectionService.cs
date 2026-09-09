using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Api.Services.StorageDemand;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<GameServerProjectionService, IGameServerProjectionService>]
internal sealed class GameServerProjectionService(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IEnumerable<IRuntimeProvider> runtimeProviders,
    IStorageDemandService storageDemandService,
    IHostingAuthorizationService authorizationService,
    AdministrativeSshAccess administrativeSshAccess,
    IOptions<StorageDemandOptions> storageDemandOptions,
    ILogger<GameServerProjectionService> logger) : IGameServerProjectionService
{
    private readonly StorageDemandOptions _storageDemandOptions = storageDemandOptions.Value;
    public async Task<IReadOnlyCollection<GameServerCapabilityDto>> GetCapabilitiesAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var providers = new List<RuntimeProviderDto>();
        foreach (var provider in runtimeProviders
                     .OrderBy(q => q.ProviderIdentifier, StringComparer.OrdinalIgnoreCase))
        {
            if (!provider.Capabilities.IsReady)
            {
                providers.Add(new RuntimeProviderDto
                {
                    Identifier = provider.ProviderIdentifier,
                    AvailabilityStatus = RuntimeProviderAvailabilityStatuses.ConfigurationRequired,
                });
                continue;
            }

            IReadOnlyCollection<RuntimeProfile> profiles;
            try
            {
                profiles = await provider.GetProfilesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Runtime profile catalog discovery failed for provider {ProviderIdentifier}: {Failure}",
                    provider.ProviderIdentifier,
                    HostingFailureSanitizer.Sanitize(exception.Message, 512));
                providers.Add(new RuntimeProviderDto
                {
                    Identifier = provider.ProviderIdentifier,
                    AvailabilityStatus = RuntimeProviderAvailabilityStatuses.CatalogUnavailable,
                });
                continue;
            }

            providers.Add(new RuntimeProviderDto
            {
                Identifier = provider.ProviderIdentifier,
                AvailabilityStatus = profiles.Count is 0
                    ? RuntimeProviderAvailabilityStatuses.NoProfiles
                    : RuntimeProviderAvailabilityStatuses.Available,
                Profiles = profiles.Select(q => new RuntimeProfileDto
                {
                    Identifier = q.Identifier,
                    DisplayName = q.DisplayName,
                    Description = q.Description,
                    VirtualCpuCount = q.VirtualCpuCount,
                    MemoryGiB = q.MemoryGiB,
                    DiskGiB = q.DiskGiB,
                    IncludedStorageBytes = IncludedStorageBytes(q),
                    UsableStorageBytes = UsableStorageBytes(q),
                    EstimatedHourlyCostEur = q.EstimatedHourlyCostEur,
                    EstimatedMonthlyCostEur = q.EstimatedMonthlyCostEur,
                }).ToArray(),
                Storage = provider.Capabilities.Storage is { } storage
                    ? new RuntimeStorageCapabilitiesDto
                    {
                        SupportsExternalBlockStorage = storage.SupportsExternalBlockStorage,
                        MinimumCapacityGiB = storage.MinimumCapacityGiB,
                        MaximumCapacityGiB = storage.MaximumCapacityGiB,
                        CapacityIncrementGiB = storage.CapacityIncrementGiB,
                        EstimatedHourlyCostPerGiBEur = storage.EstimatedHourlyCostPerGiBEur,
                        DeploymentMountPath = storage.DeploymentMountPath,
                        ReadinessFailure = storage.ReadinessFailure,
                    }
                    : null,
            });
        }
        return gameDefinitions.GetDefinitions()
            .Select(capability => new GameServerCapabilityDto
            {
                GameIdentifier = capability.GameIdentifier,
                DisplayName = capability.DisplayName,
                Description = $"Durable {capability.DisplayName} hosting through a dedicated cloud worker.",
                RuntimeProviders = providers
                    .Where(q => q.AvailabilityStatus == RuntimeProviderAvailabilityStatuses.Available)
                    .Select(q => q.Identifier)
                    .ToArray(),
                RuntimeProviderProfiles = providers,
                Actions = capability.Actions
                    .OrderBy(q => q.Order)
                    .Select(q => ProjectStaticAction(q))
                    .ToArray(),
                RequiresTerrain = capability.ContentCapabilities.RequiresTerrain,
                TerrainLabel = capability.ContentCapabilities.TerrainLabel,
                RequiresScenario = capability.ContentCapabilities.RequiresScenario,
                ScenarioLabel = capability.ContentCapabilities.ScenarioLabel,
                SupportsModPacks = capability.ContentCapabilities.SupportsModPacks,
                RequiresModPack = capability.ContentCapabilities.RequiresModPack,
                TerrainSourceKinds = capability.ContentCapabilities.TerrainSourceKinds,
                ModInstallerIdentifiers = capability.ContentCapabilities.ModInstallerIdentifiers,
                ModProviderBindings = capability.ContentCapabilities.ModProviderBindings.Select(q => new ModProviderBindingDto
                {
                    ProviderIdentifier = q.ProviderIdentifier,
                    DisplayName = q.DisplayName,
                    AcceptedInput = q.AcceptedInput,
                    InstallerIdentifiers = q.InstallerIdentifiers,
                    SupportsVersionSelector = q.SupportsVersionSelector,
                }).ToArray(),
                ModPackFormats = capability.ContentCapabilities.ModPackFormats.Select(q => new ModPackFormatDto
                {
                    Identifier = q.Identifier,
                    DisplayName = q.DisplayName,
                    FileExtension = q.FileExtension,
                    MediaType = q.MediaType,
                    MaximumSizeBytes = q.MaximumSizeBytes,
                }).ToArray(),
                DefaultTerrainSourceIdentifier = capability.ContentCapabilities.DefaultTerrainSourceIdentifier,
                DefaultScenarioSourceIdentifier = capability.ContentCapabilities.DefaultScenarioSourceIdentifier,
                Secrets = capability.Secrets.Select(q => new GameServerSecretCapabilityDto(
                    q.Key,
                    q.DisplayName,
                    q.RequiredForStart,
                    q.Generated,
                    q.Revealable,
                    q.ExternalProvider)).ToArray(),
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GameServerDto>> GetServersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var servers = await ServerQuery()
            .Where(q => !q.IsArchived)
            .OrderBy(q => q.Title)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new List<GameServerDto>(servers.Length);
        foreach (var server in servers)
        {
            if (!authorizationService.HasServerPermission(
                    principal,
                    server.PrimaryKey,
                    Claims.ResourceBased.Server.View))
                continue;
            result.Add(await ProjectServerAsync(server, principal, cancellationToken).ConfigureAwait(false));
        }
        return result;
    }

    public async Task<GameServerDto?> GetServerAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!authorizationService.HasServerPermission(
                principal,
                gameServerId,
                Claims.ResourceBased.Server.View))
            return null;
        var server = await ServerQuery()
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId, cancellationToken)
            .ConfigureAwait(false);
        return server is null
            ? null
            : await ProjectServerAsync(server, principal, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<GameServerModPackOptionDto>> GetModPackOptionsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!authorizationService.HasServerPermission(
                principal,
                gameServerId,
                Claims.ResourceBased.Server.View))
            return [];

        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId && !q.IsArchived)
            .Select(q => q.GameIdentifier)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (gameIdentifier is null)
            return [];

        return await dbContext.ModPackDefinitions
            .AsNoTracking()
            .Where(q => q.IsActive && q.GameIdentifier == gameIdentifier)
            .OrderBy(q => q.Title)
            .ThenBy(q => q.PrimaryKey)
            .Select(q => new GameServerModPackOptionDto
            {
                PrimaryKey = q.PrimaryKey,
                Title = q.Title,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<GameServerModPackOptionDto>> GetCreationModPackOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default)
        => await dbContext.ModPackDefinitions
            .AsNoTracking()
            .Where(q => q.IsActive && q.GameIdentifier == gameIdentifier.Trim())
            .OrderBy(q => q.Title)
            .ThenBy(q => q.PrimaryKey)
            .Select(q => new GameServerModPackOptionDto
            {
                PrimaryKey = q.PrimaryKey,
                Title = q.Title,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyCollection<GameServerCostOwnerOptionDto>> GetCreationCostOwnerOptionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var userId))
            return [];

        var canUseEveryOwner = principal.HasAnyEmptyClaim(
            Claims.Administrative.All,
            Claims.Administrative.Server,
            Claims.Administrative.Hosting);
        var grantedOwnerIds = principal.Claims
            .Where(q => q.Type == Claims.ResourceBased.CostOwner.Use)
            .Select(q => Guid.TryParse(q.Value, out var ownerId)
                         && string.Equals(q.Value, ownerId.ToString(), StringComparison.Ordinal)
                ? ownerId
                : Guid.Empty)
            .Where(q => q != Guid.Empty)
            .Distinct()
            .ToArray();

        return await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => canUseEveryOwner || q.UserFk == userId || grantedOwnerIds.Contains(q.PrimaryKey))
            .OrderByDescending(q => q.UserFk == userId)
            .ThenBy(q => q.DisplayName)
            .ThenBy(q => q.PrimaryKey)
            .Select(q => new GameServerCostOwnerOptionDto
            {
                CostOwnerId = q.PrimaryKey,
                DisplayName = q.DisplayName,
                IsFrozen = q.IsFrozen,
                IsCurrentUser = q.UserFk == userId,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorageDemandDto?> GetStorageDemandAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!authorizationService.HasServerPermission(
                principal,
                gameServerId,
                Claims.ResourceBased.Server.View))
            return null;
        var server = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId && !q.IsArchived)
            .Select(q => new
            {
                q.GameIdentifier,
                q.SelectedModPackDefinitionFk,
                q.RuntimeProviderIdentifier,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (server is null)
            return null;
        var demand = await storageDemandService.EstimateAsync(
                server.GameIdentifier,
                server.SelectedModPackDefinitionFk,
                gameServerId,
                true,
                cancellationToken)
            .ConfigureAwait(false);
        return await ProjectStorageDemandAsync(demand, server.RuntimeProviderIdentifier, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorageDemandDto> EstimateStorageDemandAsync(
        DTO.Payloads.StorageDemandEstimatePayload payload,
        CancellationToken cancellationToken = default)
    {
        var demand = await storageDemandService.EstimateAsync(
                payload.GameIdentifier,
                payload.ModPackDefinitionId,
                null,
                true,
                cancellationToken)
            .ConfigureAwait(false);
        return await ProjectStorageDemandAsync(demand, payload.RuntimeProviderIdentifier, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<StorageDemandDto> ProjectStorageDemandAsync(
        StorageDemandResult demand,
        string runtimeProviderIdentifier,
        CancellationToken cancellationToken)
    {
        var provider = runtimeProviders.SingleOrDefault(q => string.Equals(
            q.ProviderIdentifier,
            runtimeProviderIdentifier,
            StringComparison.OrdinalIgnoreCase));
        IReadOnlyCollection<RuntimeProfile> profiles = provider is null
            ? []
            : await provider.GetProfilesAsync(cancellationToken).ConfigureAwait(false);
        return new StorageDemandDto
        {
            SteadyStateRequiredBytes = demand.SteadyStateRequiredBytes,
            PreparationWorkspaceBytes = demand.PreparationWorkspaceBytes,
            SafetyMarginPercent = demand.SafetyMarginPercent,
            SafetyMarginBytes = demand.SafetyMarginBytes,
            PeakPreparationRequiredBytes = demand.PeakPreparationRequiredBytes,
            Status = demand.Status,
            CalculatedAt = demand.CalculatedAt,
            MeasurementTimestamp = demand.MeasurementTimestamp,
            MissingComponents = demand.MissingComponents,
            Components = demand.Components.Select(q => new StorageDemandComponentDto
            {
                Identifier = q.Identifier,
                DisplayName = q.DisplayName,
                Kind = q.Kind,
                RequiredBytes = q.RequiredBytes,
                Status = q.Status,
                Source = q.Source,
                ObservedAt = q.ObservedAt,
                Detail = q.Detail,
            }).ToArray(),
            RuntimeProfiles = profiles.Select(q =>
            {
                var included = IncludedStorageBytes(q);
                var usable = UsableStorageBytes(q);
                var sufficient = usable.HasValue
                    ? usable.Value >= demand.PeakPreparationRequiredBytes
                    : (bool?) null;
                return new RuntimeProfileStorageFitDto
                {
                    RuntimeProviderIdentifier = runtimeProviderIdentifier,
                    RuntimeProfileIdentifier = q.Identifier,
                    DisplayName = q.DisplayName,
                    IncludedCapacityBytes = included,
                    UsableCapacityBytes = usable,
                    IsSufficient = sufficient,
                    ExternalStorageRequired = sufficient is not true,
                };
            }).ToArray(),
        };
    }

    private static long? IncludedStorageBytes(RuntimeProfile profile)
    {
        if (profile.DiskGiB is not { } diskGiB || diskGiB < 0)
            return null;
        try
        {
            return checked(diskGiB * StorageDemandOptions.GiB);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private long? UsableStorageBytes(RuntimeProfile profile)
        => IncludedStorageBytes(profile) is { } included
            ? Math.Max(0, included - Math.Max(0, _storageDemandOptions.RuntimeProfileSystemReserveBytes))
            : null;

    public async Task<IReadOnlyCollection<GameServerNotificationDto>> GetNotificationsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var servers = await ServerQuery()
            .Where(q => q.Deployments.Any(d => d.TimeStampResolved == null)
                        || q.LifecycleJobs.Any(j => j.Status == ELifecycleJobStatus.Queued
                                                   || j.Status == ELifecycleJobStatus.Running
                                                   || j.Status == ELifecycleJobStatus.RetryPending
                                                   || j.Status == ELifecycleJobStatus.WaitingForWorker
                                                   || j.Status == ELifecycleJobStatus.Failed
                                                   && j.FailureCode != "permanent-execution-failed"))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var notifications = new List<GameServerNotificationDto>();
        foreach (var server in servers)
        {
            if (!authorizationService.HasServerPermission(
                    principal,
                    server.PrimaryKey,
                    Claims.ResourceBased.Server.View))
                continue;
            var dto = await ProjectServerAsync(server, principal, cancellationToken).ConfigureAwait(false);
            var deployment = ActiveDeployment(server);
            notifications.Add(new GameServerNotificationDto
            {
                ServerId = server.PrimaryKey,
                Title = server.Title,
                StatusLabel = deployment?.Stage
                              ?? (dto.PendingJobCount > 0 ? "Lifecycle work pending" : "Stopped"),
                PossibleCost = deployment?.PossibleCost ?? false,
                CanOperate = authorizationService.HasServerPermission(
                    principal,
                    server.PrimaryKey,
                    Claims.ResourceBased.Server.Operate),
                CostStopAction = dto.Actions.SingleOrDefault(q => q.Id == GameServerActions.Terminate),
            });
        }
        return notifications;
    }

    public async Task<IReadOnlyCollection<LifecycleJobDto>> GetJobsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!authorizationService.HasServerPermission(
                principal,
                gameServerId,
                Claims.ResourceBased.Server.View))
            return [];
        var jobs = await dbContext.LifecycleJobs
            .AsNoTracking()
            .Include(q => q.WorkerCommands)
            .Where(q => q.GameServerFk == gameServerId)
            .OrderByDescending(q => q.Sequence)
            .Take(100)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return jobs.Select(q => ProjectJob(q, principal)).ToArray();
    }

    public async Task<IReadOnlyCollection<HostingOperationDto>> GetOperationsAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        int take = 80,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(take));
        if (!authorizationService.HasServerPermission(
                principal,
                gameServerId,
                Claims.ResourceBased.Server.View))
            return [];

        var providerIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId)
            .Select(q => q.RuntimeProviderIdentifier)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (providerIdentifier is null)
            return [];

        var operationalKinds = new[]
        {
            EHostingAuditEventKind.DeploymentCreated,
            EHostingAuditEventKind.DeploymentResourceProvisioningRequested,
            EHostingAuditEventKind.DeploymentResourceProvisioningFailed,
            EHostingAuditEventKind.DeploymentResourceObserved,
            EHostingAuditEventKind.DeploymentResourceAdopted,
            EHostingAuditEventKind.DeploymentResourceReleaseRequested,
            EHostingAuditEventKind.DeploymentResourceReleaseFailed,
            EHostingAuditEventKind.DeploymentResourceReleased,
            EHostingAuditEventKind.DeploymentStopped,
            EHostingAuditEventKind.DeploymentTerminated,
            EHostingAuditEventKind.LifecycleJobQueued,
            EHostingAuditEventKind.LifecycleJobClaimed,
            EHostingAuditEventKind.LifecycleJobRetryPending,
            EHostingAuditEventKind.LifecycleJobFailed,
            EHostingAuditEventKind.LifecycleJobSucceeded,
            EHostingAuditEventKind.LifecycleFailed,
            EHostingAuditEventKind.WorkerProvisioned,
            EHostingAuditEventKind.WorkerRegistered,
            EHostingAuditEventKind.WorkerDegraded,
            EHostingAuditEventKind.WorkerRecovered,
            EHostingAuditEventKind.WorkerStopped,
            EHostingAuditEventKind.WorkerCommandQueued,
            EHostingAuditEventKind.WorkerCommandFailed,
            EHostingAuditEventKind.WorkerCommandSucceeded,
            EHostingAuditEventKind.ProviderReconciliationDegraded,
            EHostingAuditEventKind.Recovery,
        };
        var records = await dbContext.HostingAuditRecords
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId && operationalKinds.Contains(q.Kind))
            .OrderByDescending(q => q.TimeStampCreated)
            .ThenByDescending(q => q.PrimaryKey)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return records.Select(q => ProjectOperation(q, providerIdentifier)).ToArray();
    }

    public LifecycleJobDto ProjectJob(LifecycleJob lifecycleJob, ClaimsPrincipal principal)
        => new()
        {
            PrimaryKey = lifecycleJob.PrimaryKey,
            GameServerId = lifecycleJob.GameServerFk,
            DeploymentId = lifecycleJob.DeploymentFk,
            Kind = lifecycleJob.Kind,
            ActionId = lifecycleJob.ActionIdentifier,
            Status = lifecycleJob.Status,
            Stage = lifecycleJob.Stage,
            ProgressPercent = lifecycleJob.Progress,
            AttemptCount = lifecycleJob.Attempt,
            FailureCode = lifecycleJob.FailureCode,
            FailureReason = lifecycleJob.FailureMessage,
            CanRetry = lifecycleJob.Status is ELifecycleJobStatus.Failed
                       && !(lifecycleJob.FailureCode?.StartsWith(
                           "permanent-",
                           StringComparison.OrdinalIgnoreCase) ?? false)
                       && authorizationService.HasServerPermission(
                           principal,
                           lifecycleJob.GameServerFk,
                           Claims.ResourceBased.Server.Operate),
            CanSkip = lifecycleJob.Status is ELifecycleJobStatus.Failed
                      && !(lifecycleJob.FailureCode?.StartsWith(
                          "permanent-",
                          StringComparison.OrdinalIgnoreCase) ?? false)
                      && authorizationService.HasServerPermission(
                          principal,
                          lifecycleJob.GameServerFk,
                          Claims.ResourceBased.Server.Operate),
            TimeStampCreated = lifecycleJob.TimeStampCreated,
            TimeStampUpdated = lifecycleJob.TimeStampUpdated,
            TimeStampStarted = lifecycleJob.TimeStampStarted,
            TimeStampCompleted = lifecycleJob.TimeStampCompleted,
            Commands = lifecycleJob.WorkerCommands
                .OrderBy(q => q.Sequence)
                .Select(q => new WorkerCommandDto
                {
                    PrimaryKey = q.PrimaryKey,
                    Sequence = q.Sequence,
                    ActionId = q.ActionIdentifier,
                    Status = q.Status,
                    Stage = q.Stage,
                    FailureCode = q.ErrorCode,
                    FailureReason = q.ErrorMessage,
                    DiagnosticOutput = q.DiagnosticOutput,
                    TimeStampCreated = q.TimeStampCreated,
                    TimeStampStarted = q.TimeStampStarted,
                    TimeStampCompleted = q.TimeStampCompleted,
                })
                .ToArray(),
        };

    private IQueryable<GameServer> ServerQuery()
        => dbContext.GameServers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.CurrentCostOwner)
            .Include(q => q.SelectedModPackDefinition)
            .Include(q => q.SelectedTerrain)
            .Include(q => q.SelectedScenario)
            .Include(q => q.SelectedConfigurationProfile)
            .Include(q => q.Deployments)
            .ThenInclude(q => q.Workers)
            .Include(q => q.Deployments)
            .ThenInclude(q => q.Resources)
            .Include(q => q.LifecycleJobs)
            .ThenInclude(q => q.WorkerCommands);

    private static HostingOperationDto ProjectOperation(
        HostingAuditRecord record,
        string defaultProviderIdentifier)
    {
        var detail = ExtractDetail(record.Details);
        var fields = ParseDetailFields(detail);
        return new HostingOperationDto
        {
            PrimaryKey = record.PrimaryKey,
            Kind = record.Kind,
            Status = OperationStatus(record.Kind),
            Summary = record.Summary,
            Detail = detail,
            ProviderIdentifier = fields.GetValueOrDefault("Provider") ?? defaultProviderIdentifier,
            ResourceKind = fields.GetValueOrDefault("Kind"),
            ResourceIdentifier = fields.GetValueOrDefault("Identity"),
            ProviderState = fields.GetValueOrDefault("State") ?? fields.GetValueOrDefault("Stage"),
            DeploymentId = record.DeploymentFk,
            LifecycleJobId = record.LifecycleJobFk,
            WorkerId = record.WorkerFk,
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            TimeStampCreated = record.TimeStampCreated,
        };
    }

    private static string OperationStatus(EHostingAuditEventKind kind)
        => kind switch
        {
            EHostingAuditEventKind.DeploymentResourceProvisioningRequested
                or EHostingAuditEventKind.DeploymentResourceReleaseRequested
                or EHostingAuditEventKind.DeploymentCreated
                or EHostingAuditEventKind.LifecycleJobQueued
                or EHostingAuditEventKind.LifecycleJobClaimed
                or EHostingAuditEventKind.WorkerProvisioned
                or EHostingAuditEventKind.WorkerCommandQueued => "pending",
            EHostingAuditEventKind.DeploymentResourceProvisioningFailed
                or EHostingAuditEventKind.DeploymentResourceReleaseFailed
                or EHostingAuditEventKind.LifecycleJobFailed
                or EHostingAuditEventKind.LifecycleFailed
                or EHostingAuditEventKind.WorkerCommandFailed => "failed",
            EHostingAuditEventKind.ProviderReconciliationDegraded
                or EHostingAuditEventKind.WorkerDegraded
                or EHostingAuditEventKind.LifecycleJobRetryPending
                or EHostingAuditEventKind.Recovery => "warning",
            _ => "succeeded",
        };

    private static string? ExtractDetail(string details)
    {
        if (string.IsNullOrWhiteSpace(details) || details == "{}")
            return null;
        try
        {
            using var document = JsonDocument.Parse(details);
            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseDetailFields(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return detail.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(q => q.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(q => q.Length == 2 && q[0].Length > 0)
            .GroupBy(q => q[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(q => q.Key, q => q.Last()[1], StringComparer.OrdinalIgnoreCase);
    }

    private async Task<GameServerDto> ProjectServerAsync(
        GameServer server,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var deployment = ActiveDeployment(server);
        var pendingStatuses = new[]
        {
            ELifecycleJobStatus.Queued,
            ELifecycleJobStatus.Running,
            ELifecycleJobStatus.RetryPending,
            ELifecycleJobStatus.WaitingForWorker,
        };
        var pending = server.LifecycleJobs.Count(q => pendingStatuses.Contains(q.Status));
        var canSeeOwner = await authorizationService
            .CanViewCostOwnerAsync(principal, server.CurrentCostOwnerFk, cancellationToken)
            .ConfigureAwait(false);
        var gameDefinition = gameDefinitions.GetDefinitions()
            .SingleOrDefault(q => string.Equals(
                q.GameIdentifier,
                server.GameIdentifier,
                StringComparison.OrdinalIgnoreCase));
        var primaryServiceIdentifier = gameDefinition?.Nodes.Single(q => q.IsPrimary).ServiceIdentifier
                                       ?? GameServerNodeDefinition.PrimaryServiceIdentifier;
        var worker = deployment?.Workers
            .Where(q => q.TimeStampStopped == null && q.ServiceIdentifier == primaryServiceIdentifier)
            .OrderByDescending(q => q.TimeStampCreated)
            .FirstOrDefault();
        var publicAddress = deployment?.Resources
            .Where(q => q.State == EDeploymentResourceState.Allocated
                        && q.TimeStampReleased == null
                        && (worker == null
                            || q.WorkerFk == worker.PrimaryKey
                            || deployment!.Resources.All(resource => resource.WorkerFk == null))
                        && !string.IsNullOrWhiteSpace(q.PublicAddress))
            .OrderBy(q => string.Equals(q.Kind, "primary-ip", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(q => q.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(q => q.PublicAddress)
            .FirstOrDefault();
        var connection = gameDefinition?.PrimaryConnection;
        return new GameServerDto
        {
            PrimaryKey = server.PrimaryKey,
            Title = server.Title,
            GameIdentifier = server.GameIdentifier,
            RuntimeProviderIdentifier = server.RuntimeProviderIdentifier,
            RuntimeProfileIdentifier = server.RuntimeProfileIdentifier,
            RuntimeProfileDisplayName = server.RuntimeProfileDisplayName,
            RuntimeStorageStrategy = server.RuntimeStorageStrategy,
            ExternalStorageSizeGiB = server.ExternalStorageSizeGiB,
            EstimatedHourlyCostEur = server.EstimatedHourlyCostEur,
            EstimatedMonthlyCostEur = server.EstimatedMonthlyCostEur,
            SelectedModPackDefinitionId = server.SelectedModPackDefinitionFk,
            SelectedModPackDefinitionTitle = server.SelectedModPackDefinition?.Title,
            PinnedModPackRevisionId = server.PinnedModPackRevisionFk,
            SelectedTerrainId = server.SelectedTerrainFk,
            SelectedTerrainTitle = server.SelectedTerrain?.Title,
            PinnedTerrainVersionId = server.PinnedTerrainVersionFk,
            SelectedScenarioId = server.SelectedScenarioFk,
            SelectedScenarioTitle = server.SelectedScenario?.Title,
            PinnedScenarioVersionId = server.PinnedScenarioVersionFk,
            SelectedConfigurationProfileId = server.SelectedConfigurationProfileFk,
            SelectedConfigurationProfileTitle = server.SelectedConfigurationProfile?.Title,
            PinnedConfigurationProfileRevisionId = server.PinnedConfigurationProfileRevisionFk,
            CreatedById = server.CreatedByFk,
            CostOwnerId = server.CurrentCostOwnerFk,
            CostOwnerName = canSeeOwner
                ? server.CurrentCostOwner?.DisplayName ?? "Unknown owner"
                : "Assigned owner",
            IsLaunchDisabled = server.IsLaunchDisabled,
            IsArchived = server.IsArchived,
            CanModify = authorizationService.HasServerPermission(
                principal,
                server.PrimaryKey,
                Claims.ResourceBased.Server.Modify),
            CanManageSecrets = authorizationService.HasServerPermission(
                principal,
                server.PrimaryKey,
                Claims.ResourceBased.Server.ManageSecrets),
            CanRevealSecrets = authorizationService.HasServerPermission(
                principal,
                server.PrimaryKey,
                Claims.ResourceBased.Server.RevealSecrets),
            TimeStampCreated = server.TimeStampCreated,
            TimeStampUpdated = server.TimeStampUpdated,
            PendingJobCount = pending,
            Deployment = deployment is null
                ? null
                : new GameServerDeploymentDto
                {
                    PrimaryKey = deployment.PrimaryKey,
                    RuntimeProfileIdentifier = deployment.RuntimeProfileIdentifier,
                    RuntimeProfileDisplayName = deployment.RuntimeProfileDisplayName,
                    EstimatedHourlyCostEur = deployment.EstimatedHourlyCostEur,
                    RuntimeStorageStrategy = deployment.RuntimeStorageStrategy,
                    PeakStorageRequiredBytes = deployment.PeakStorageRequiredBytes,
                    IncludedStorageBytes = deployment.IncludedStorageBytes,
                    SelectedExternalStorageBytes = deployment.SelectedExternalStorageBytes,
                    DeploymentRoot = deployment.DeploymentRoot,
                    EstimatedComputeHourlyCostEur = deployment.EstimatedComputeHourlyCostEur,
                    EstimatedStorageHourlyCostEur = deployment.EstimatedStorageHourlyCostEur,
                    Status = deployment.Status,
                    Stage = deployment.Stage,
                    PossibleCost = deployment.PossibleCost,
                    ProvisioningBlocked = deployment.IsProvisioningBlocked,
                    CostOwnerId = deployment.CostOwnerFk,
                    CostOwnerName = canSeeOwner
                        ? server.CurrentCostOwner?.DisplayName ?? "Unknown owner"
                        : "Assigned owner",
                    WorkerId = worker?.PrimaryKey,
                    WorkerStatus = worker?.Status,
                    LastHeartbeat = worker?.LastHeartbeatAt,
                    DiskTotalBytes = worker?.DiskTotalBytes,
                    DiskAvailableBytes = worker?.DiskAvailableBytes,
                    DiskTimeStampObserved = worker?.DiskTimeStampObserved,
                    PublicAddress = publicAddress,
                    ConnectionEndpoint = deployment.Status == EDeploymentStatus.Running
                                         && publicAddress is not null
                                         && connection is not null
                        ? FormatConnectionEndpoint(publicAddress, connection.Port)
                        : null,
                    ConnectionProtocol = connection?.Protocol.ToString().ToUpperInvariant(),
                    ConnectionPort = connection?.Port,
                    AdministrativeSshCommand = principal.HasClaim(Claims.Administrative.All, string.Empty)
                        ? administrativeSshAccess.GetCommand(publicAddress ?? string.Empty)
                        : null,
                    Resources = deployment.Resources
                        .OrderBy(q => q.Kind, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(q => q.ProviderResourceIdentifier, StringComparer.Ordinal)
                        .Select(q => new GameServerDeploymentResourceDto
                        {
                            PrimaryKey = q.PrimaryKey,
                            ProviderIdentifier = q.ProviderIdentifier,
                            ProviderResourceIdentifier = q.ProviderResourceIdentifier,
                            Kind = q.Kind,
                            State = q.State,
                            PossibleCost = q.PossibleCost,
                            ProvisionedCapacityBytes = q.ProvisionedCapacityBytes,
                            EstimatedHourlyCostEur = q.EstimatedHourlyCostEur,
                            ProviderState = q.ProviderState,
                            PublicAddress = q.PublicAddress,
                            TimeStampObserved = q.TimeStampObserved,
                            TimeStampReleased = q.TimeStampReleased,
                        })
                        .ToArray(),
                    TimeStampCreated = deployment.TimeStampCreated,
                    TimeStampStarted = deployment.TimeStampStarted,
                    TimeStampResolved = deployment.TimeStampResolved,
                    FaultSummary = deployment.FaultMessage,
                },
            Actions = ProjectActions(server, deployment, pending > 0, principal, gameDefinition),
        };
    }

    private IReadOnlyCollection<GameServerActionDto> ProjectActions(
        GameServer server,
        GameServerDeployment? deployment,
        bool pending,
        ClaimsPrincipal principal,
        GameDefinition? capability)
    {
        if (capability is null)
            return [];
        var canOperate = authorizationService.HasServerPermission(
            principal,
            server.PrimaryKey,
            Claims.ResourceBased.Server.Operate);
        var result = capability.Actions
            .Select(action => ProjectAction(action, server, deployment, pending, canOperate))
            .ToList();
        if (deployment is not null)
        {
            var terminating = deployment.Status is EDeploymentStatus.Terminating
                or EDeploymentStatus.TerminatingDueToFault;
            result.Add(new GameServerActionDto
            {
                Id = GameServerActions.Terminate,
                Label = "Terminate",
                Description = "Immediately release runtime resources without publishing live data.",
                Icon = "warning",
                Group = "Emergency",
                Order = 1000,
                Enabled = canOperate && !terminating,
                UnavailableReason = !canOperate
                    ? "Server Operate permission is required."
                    : terminating ? "Termination is already in progress." : null,
                Dangerous = true,
                ConfirmationText = "Live unpublished data will be lost.",
                ConfirmationChallenge = server.Title,
                Pending = terminating,
                CostImpact = ECostImpact.StopsCost,
                CostDescription = "Stops all attributable runtime cost after release is verified.",
            });
        }
        return result.OrderBy(q => q.Order).ToArray();
    }

    private static GameServerActionDto ProjectAction(
        GameServerActionDefinition action,
        GameServer server,
        GameServerDeployment? deployment,
        bool pending,
        bool canOperate)
    {
        var isStart = string.Equals(action.ActionId, GameServerActions.Start, StringComparison.OrdinalIgnoreCase);
        var isStop = string.Equals(action.ActionId, GameServerActions.Stop, StringComparison.OrdinalIgnoreCase);
        var stateAllows = isStart
            ? deployment is null && !server.IsLaunchDisabled && !server.IsArchived
            : isStop
                ? deployment?.Status is EDeploymentStatus.Running
                : deployment?.Status is EDeploymentStatus.Running;
        var enabled = canOperate && stateAllows && !pending;
        var reason = !canOperate
            ? "Server Operate permission is required."
            : pending ? "Another lifecycle operation is pending."
            : stateAllows ? null
            : isStart && server.IsLaunchDisabled
                ? server.LaunchDisabledReason ?? "Server launch is disabled."
                : "This action is unavailable in the current deployment state.";
        return new GameServerActionDto
        {
            Id = action.ActionId,
            Label = action.Label,
            Description = action.Description,
            Icon = action.Icon,
            Group = action.Group,
            Order = action.Order,
            Enabled = enabled,
            UnavailableReason = reason,
            Dangerous = action.IsDangerous,
            ConfirmationText = action.RequiresConfirmation ? "Confirm this server action." : null,
            ConfirmationChallenge = action.RequiresConfirmation ? server.Title : null,
            Pending = pending,
            ExpectedDurationSeconds = action.ExpectedDuration is null
                ? null
                : (int) action.ExpectedDuration.Value.TotalSeconds,
            CostImpact = action.CostImpact,
            CostDescription = action.CostEffectDescription,
        };
    }

    private static GameServerActionDto ProjectStaticAction(GameServerActionDefinition action)
        => new()
        {
            Id = action.ActionId,
            Label = action.Label,
            Description = action.Description,
            Icon = action.Icon,
            Group = action.Group,
            Order = action.Order,
            Enabled = true,
            Dangerous = action.IsDangerous,
            ExpectedDurationSeconds = action.ExpectedDuration is null
                ? null
                : (int) action.ExpectedDuration.Value.TotalSeconds,
            CostImpact = action.CostImpact,
            CostDescription = action.CostEffectDescription,
        };

    private static GameServerDeployment? ActiveDeployment(GameServer server)
        => server.Deployments.SingleOrDefault(q => q.TimeStampResolved == null);

    private static string FormatConnectionEndpoint(string address, int port)
        => address.Contains(':')
            ? $"[{address}]:{port}"
            : $"{address}:{port}";
}
