using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<GameServerProjectionService, IGameServerProjectionService>]
internal sealed class GameServerProjectionService(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IEnumerable<IRuntimeProvider> runtimeProviders,
    IHostingAuthorizationService authorizationService) : IGameServerProjectionService
{
    public IReadOnlyCollection<GameServerCapabilityDto> GetCapabilities(ClaimsPrincipal principal)
    {
        var providers = runtimeProviders
            .Where(q => q.Capabilities.IsReady)
            .Select(q => q.ProviderIdentifier)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(q => q, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return gameDefinitions.GetDefinitions()
            .Select(capability => new GameServerCapabilityDto
            {
                GameIdentifier = capability.GameIdentifier,
                DisplayName = capability.DisplayName,
                Description = $"Durable {capability.DisplayName} hosting through a dedicated cloud worker.",
                RuntimeProviders = providers,
                Actions = capability.Actions
                    .OrderBy(q => q.Order)
                    .Select(q => ProjectStaticAction(q))
                    .ToArray(),
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
            FailureReason = lifecycleJob.FailureMessage,
            CanRetry = lifecycleJob.Status is ELifecycleJobStatus.Failed
                       && lifecycleJob.FailureCode != "permanent-execution-failed"
                       && authorizationService.HasServerPermission(
                           principal,
                           lifecycleJob.GameServerFk,
                           Claims.ResourceBased.Server.Operate),
            CanSkip = lifecycleJob.Status is ELifecycleJobStatus.Failed
                      && lifecycleJob.FailureCode != "permanent-execution-failed"
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
                    FailureReason = q.ErrorMessage,
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
            .Include(q => q.Deployments)
            .ThenInclude(q => q.Workers)
            .Include(q => q.Deployments)
            .ThenInclude(q => q.Resources)
            .Include(q => q.LifecycleJobs)
            .ThenInclude(q => q.WorkerCommands);

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
        var worker = deployment?.Workers
            .OrderByDescending(q => q.TimeStampCreated)
            .FirstOrDefault();
        var gameDefinition = gameDefinitions.GetDefinitions()
            .SingleOrDefault(q => string.Equals(
                q.GameIdentifier,
                server.GameIdentifier,
                StringComparison.OrdinalIgnoreCase));
        var publicAddress = deployment?.Resources
            .Where(q => q.State == EDeploymentResourceState.Allocated
                        && q.TimeStampReleased == null
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
            TimeStampCreated = server.TimeStampCreated,
            TimeStampUpdated = server.TimeStampUpdated,
            PendingJobCount = pending,
            Deployment = deployment is null
                ? null
                : new GameServerDeploymentDto
                {
                    PrimaryKey = deployment.PrimaryKey,
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
                    PublicAddress = publicAddress,
                    ConnectionEndpoint = deployment.Status == EDeploymentStatus.Running
                                         && publicAddress is not null
                                         && connection is not null
                        ? FormatConnectionEndpoint(publicAddress, connection.Port)
                        : null,
                    ConnectionProtocol = connection?.Protocol.ToString().ToUpperInvariant(),
                    ConnectionPort = connection?.Port,
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
