using System.Security.Claims;
using System.Data;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<GameServerLifecycleService, IGameServerLifecycleService>]
internal sealed class GameServerLifecycleService(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IEnumerable<IRuntimeProvider> runtimeProviders,
    IHostingAuthorizationService authorizationService,
    IHostingAuditWriter auditWriter) : IGameServerLifecycleService
{
    public async Task<GameServer> CreateAsync(
        GameServerCreationPayload payload,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId))
            throw new HostingForbiddenException("An authenticated user is required.");

        var title = payload.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new HostingValidationException("A server title is required.");

        var capability = gameDefinitions.GetDefinitions()
            .SingleOrDefault(q => string.Equals(
                q.GameIdentifier,
                payload.GameIdentifier.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (capability is null)
            throw new HostingValidationException("The selected game is not supported.");
        var providerIdentifier = payload.RuntimeProviderIdentifier.Trim();
        var provider = runtimeProviders.SingleOrDefault(q => string.Equals(
            q.ProviderIdentifier,
            providerIdentifier,
            StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            throw new HostingValidationException("The selected cloud runtime provider is unavailable.");

        var actor = await dbContext.Users
            .Include(q => q.Claims)
            .SingleOrDefaultAsync(q => q.PrimaryKey == actorId && !q.IsDeleted && !q.IsBanned, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingForbiddenException("The requesting user is no longer eligible.");
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var costOwner = payload.CostOwnerId is { } requestedOwnerId
            ? await dbContext.CostOwners
                .SingleOrDefaultAsync(q => q.PrimaryKey == requestedOwnerId, cancellationToken)
                .ConfigureAwait(false)
            : await dbContext.CostOwners
                .SingleOrDefaultAsync(q => q.UserFk == actorId, cancellationToken)
                .ConfigureAwait(false);

        if (payload.CostOwnerId is { } ownerId
            && !await authorizationService.CanUseCostOwnerAsync(principal, ownerId, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingForbiddenException("The selected cost owner cannot be used by this user.");
        if (payload.CostOwnerId is not null && costOwner is null)
            throw new HostingValidationException("The selected cost owner does not exist.");

        costOwner ??= new CostOwner
        {
            PrimaryKey      = Guid.NewGuid(),
            Kind            = ECostOwnerKind.User,
            UserFk          = actorId,
            DisplayName     = actor.Nickname,
            Currency        = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        if (costOwner.IsFrozen)
            throw new HostingValidationException("The selected cost owner is frozen.");
        if (dbContext.Entry(costOwner).State is EntityState.Detached)
            dbContext.CostOwners.Add(costOwner);

        var server = new GameServer
        {
            PrimaryKey               = Guid.NewGuid(),
            Title                    = title,
            GameIdentifier           = capability.GameIdentifier,
            RuntimeProviderIdentifier = provider.ProviderIdentifier,
            CreatedByFk              = actorId,
            CurrentCostOwner         = costOwner,
            CurrentCostOwnerFk       = costOwner.PrimaryKey,
            IsLaunchDisabled         = payload.IsLaunchDisabled,
            LaunchDisabledReason     = payload.IsLaunchDisabled ? "Disabled when the server was created." : null,
            TimeStampCreated         = now,
            TimeStampUpdated         = now,
        };
        dbContext.GameServers.Add(server);
        dbContext.GameServerCostOwnerAssignments.Add(new GameServerCostOwnerAssignment
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServer       = server,
            GameServerFk     = server.PrimaryKey,
            CostOwner        = costOwner,
            CostOwnerFk      = costOwner.PrimaryKey,
            AssignedByFk     = actorId,
            TimeStampAssigned = now,
        });
        foreach (var seed in capability.PreservationSeeds)
        {
            dbContext.PreservationSelections.Add(new PreservationSelection
            {
                PrimaryKey      = Guid.NewGuid(),
                GameServer      = server,
                GameServerFk    = server.PrimaryKey,
                Path            = CanonicalizePreservationPath(seed.RelativePath, seed.Scope),
                Scope           = seed.Scope,
                Mode            = seed.Mode,
                IsGameSeeded    = true,
                TimeStampCreated = now,
                TimeStampUpdated = now,
            });
        }

        actor.Claims ??= [];
        actor.Claims.Add(new X39.UnitedTacticalForces.Database.Authority.Claim
        {
            Identifier  = Claims.ResourceBased.Server.All,
            Value       = server.PrimaryKey.ToString(),
            Category    = "Game servers",
            Title       = Claims.ResourceBased.Server.All,
            Description = "Server-scoped hosting permission.",
        });

        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ServerCreated,
            "Game server created.",
            actorId,
            server.PrimaryKey,
            costOwnerId: costOwner.PrimaryKey,
            details: $"Game={server.GameIdentifier}; Provider={server.RuntimeProviderIdentifier}");
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.AccessGranted,
            "Server access granted.",
            actorId,
            server.PrimaryKey,
            details: $"User {actorId}; permission {Claims.ResourceBased.Server.All}.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return server;
    }

    public async Task<LifecycleJob> EnqueueActionAsync(
        Guid gameServerId,
        string actionId,
        string? confirmation,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Operate))
            throw new HostingForbiddenException("Server Operate permission is required.");

        actionId = actionId.Trim().ToLowerInvariant();
        if (!GameServerActions.TryGetKind(actionId, out var kind))
            throw new HostingValidationException("The requested server action is not supported.");

        await using var actionTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        IQueryable<GameServer> serverQuery = dbContext.GameServers;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            serverQuery = dbContext.GameServers.FromSqlInterpolated(
                $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {gameServerId} FOR UPDATE""");
        }
        var server = await serverQuery
            .Include(q => q.CurrentCostOwner)
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"Game server {gameServerId} does not exist.");
        var supported = gameDefinitions.GetDefinitions()
            .SingleOrDefault(q => string.Equals(q.GameIdentifier, server.GameIdentifier, StringComparison.OrdinalIgnoreCase));
        var projectedAction = supported?.Actions.SingleOrDefault(q =>
            string.Equals(q.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (supported is null
            || kind is not ELifecycleJobKind.Terminate && projectedAction is null)
            throw new HostingValidationException("The selected game implementation does not expose this action.");
        if (projectedAction?.RequiresConfirmation == true
            && !string.Equals(confirmation, server.Title, StringComparison.Ordinal))
            throw new HostingValidationException("This action requires the exact server title as confirmation.");

        var activeDeployment = await dbContext.GameServerDeployments
            .SingleOrDefaultAsync(
                q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        GameServerDeployment deployment;
        if (kind is ELifecycleJobKind.Start)
        {
            if (server.IsLaunchDisabled)
                throw new HostingConflictException(server.LaunchDisabledReason ?? "Server launch is disabled.");
            if (server.CurrentCostOwner is null || server.CurrentCostOwner.IsFrozen)
                throw new HostingConflictException("The assigned cost owner is unavailable.");
            if (!await authorizationService
                    .CanUseCostOwnerAsync(principal, server.CurrentCostOwnerFk, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new HostingForbiddenException("The assigned cost owner cannot be used by this user.");
            }
            if (activeDeployment is not null)
                throw new HostingConflictException("The server already has an unresolved deployment.");
            if (await dbContext.DeploymentResources.AnyAsync(
                    q => q.Deployment!.GameServerFk == gameServerId && q.TimeStampReleased == null,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new HostingConflictException(
                    "A previous deployment still has unresolved provider resources.");
            }

            deployment = new GameServerDeployment
            {
                PrimaryKey      = Guid.NewGuid(),
                GameServerFk    = server.PrimaryKey,
                CostOwnerFk     = server.CurrentCostOwnerFk,
                Status          = EDeploymentStatus.Provisioning,
                Stage           = "queued",
                TimeStampCreated = now,
                TimeStampUpdated = now,
            };
            dbContext.GameServerDeployments.Add(deployment);
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.DeploymentCreated,
                "Deployment intent created.",
                actorId,
                server.PrimaryKey,
                deployment.PrimaryKey,
                costOwnerId: deployment.CostOwnerFk,
                costImpact: ECostImpact.NoCostImpact);
        }
        else
        {
            deployment = activeDeployment
                         ?? throw new HostingConflictException("The server has no active deployment.");
            var hasPendingAction = await dbContext.LifecycleJobs.AnyAsync(
                    q => q.DeploymentFk == deployment.PrimaryKey
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker),
                    cancellationToken)
                .ConfigureAwait(false);
            if (kind is ELifecycleJobKind.Terminate)
            {
                if (deployment.Status is EDeploymentStatus.Terminating
                    or EDeploymentStatus.TerminatingDueToFault)
                {
                    throw new HostingConflictException("Deployment termination is already in progress.");
                }
                if (!string.Equals(confirmation, server.Title, StringComparison.Ordinal))
                    throw new HostingValidationException("Terminate requires the exact server title as confirmation.");
                deployment.IsProvisioningBlocked = true;
                deployment.Status = EDeploymentStatus.Terminating;
                deployment.Stage = "termination-requested";
                deployment.TimeStampUpdated = now;
                await CancelPendingWorkAsync(deployment.PrimaryKey, now, true, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (kind is ELifecycleJobKind.Stop)
            {
                if (deployment.Status is not EDeploymentStatus.Running)
                    throw new HostingConflictException("Stop is available only while the deployment is running.");
                if (hasPendingAction)
                    throw new HostingConflictException("Another lifecycle action is already pending.");
                deployment.IsProvisioningBlocked = true;
                await CancelPendingWorkAsync(deployment.PrimaryKey, now, false, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (deployment.Status is not EDeploymentStatus.Running)
                    throw new HostingConflictException("This action is available only while the deployment is running.");
                if (hasPendingAction)
                    throw new HostingConflictException("Another lifecycle action is already pending.");
            }
        }

        var nextSequence = (await dbContext.LifecycleJobs
            .Where(q => q.GameServerFk == gameServerId)
            .MaxAsync(q => (long?) q.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;
        var job = new LifecycleJob
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = gameServerId,
            Deployment       = deployment,
            DeploymentFk     = deployment.PrimaryKey,
            RequestedByFk    = actorId,
            Kind             = kind,
            ActionIdentifier = actionId,
            Sequence         = nextSequence,
            Priority         = kind is ELifecycleJobKind.Terminate ? 100 : kind is ELifecycleJobKind.Stop ? 50 : 0,
            Status           = ELifecycleJobStatus.Queued,
            Stage            = "queued",
            Progress         = 0,
            MaxAttempts      = kind is ELifecycleJobKind.Terminate ? int.MaxValue : 3,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.LifecycleJobs.Add(job);
        server.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            kind is ELifecycleJobKind.Terminate
                ? EHostingAuditEventKind.TerminateRequested
                : EHostingAuditEventKind.LifecycleJobQueued,
            $"Lifecycle action '{actionId}' queued.",
            actorId,
            server.PrimaryKey,
            deployment.PrimaryKey,
            job.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            costImpact: CostImpact(kind));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await actionTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return job;
    }

    public async Task<LifecycleJob> RetryAsync(
        Guid gameServerId,
        Guid lifecycleJobId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Operate))
            throw new HostingForbiddenException("Server Operate permission is required.");

        await using var retryTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await dbContext.GameServers.FromSqlInterpolated(
                    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {gameServerId} FOR UPDATE""")
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        IQueryable<LifecycleJob> failedQuery = dbContext.LifecycleJobs;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            failedQuery = dbContext.LifecycleJobs.FromSqlInterpolated(
                $"""SELECT * FROM "LifecycleJobs" WHERE "PrimaryKey" = {lifecycleJobId} FOR UPDATE""");
        }
        var failed = await failedQuery
            .Include(q => q.WorkerCommands)
            .Include(q => q.Deployment)
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == lifecycleJobId && q.GameServerFk == gameServerId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"Lifecycle job {lifecycleJobId} does not exist.");
        if (failed.Status is not ELifecycleJobStatus.Failed)
            throw new HostingConflictException("Only a failed lifecycle job can be retried.");
        if (failed.FailureCode == "permanent-execution-failed"
            || failed.Kind is ELifecycleJobKind.Start && failed.Deployment?.TimeStampResolved is not null)
        {
            throw new HostingConflictException(
                "This failure cannot be retried; correct the server state and queue a fresh Start.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var originalSequence = failed.Sequence;
        var nextSequence = (await dbContext.LifecycleJobs
            .Where(q => q.GameServerFk == gameServerId)
            .MaxAsync(q => (long?) q.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;
        foreach (var command in failed.WorkerCommands.Where(q => q.Status is EWorkerCommandStatus.Failed))
            command.Status = EWorkerCommandStatus.Retried;
        // Lifecycle jobs do not have a separate Retried terminal enum. The original
        // job remains in history as an explicitly superseded/skipped attempt while
        // its worker command carries the Retried outcome and a new job gets its own id.
        failed.Status = ELifecycleJobStatus.Skipped;
        failed.Stage = "retried-as-new-job";
        // Move the superseded historical row out of its queue slot. Reusing that slot
        // makes the retry a dependency predecessor of commands that were already queued.
        failed.Sequence = nextSequence;
        failed.TimeStampUpdated = now;
        failed.TimeStampCompleted ??= now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var retry = new LifecycleJob
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = failed.GameServerFk,
            DeploymentFk     = failed.DeploymentFk,
            RequestedByFk    = actorId,
            Kind             = failed.Kind,
            ActionIdentifier = failed.ActionIdentifier,
            Sequence         = originalSequence,
            Priority         = failed.Priority,
            Status           = ELifecycleJobStatus.Queued,
            Stage            = "queued-after-retry",
            MaxAttempts      = failed.MaxAttempts,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.LifecycleJobs.Add(retry);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.LifecycleRetried,
            "Failed lifecycle job explicitly retried.",
            actorId,
            gameServerId,
            failed.DeploymentFk,
            retry.PrimaryKey,
            details: $"OriginalJob={failed.PrimaryKey}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await retryTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return retry;
    }

    public async Task<LifecycleJob> SkipAsync(
        Guid gameServerId,
        Guid lifecycleJobId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Operate))
            throw new HostingForbiddenException("Server Operate permission is required.");
        var job = await dbContext.LifecycleJobs
            .Include(q => q.WorkerCommands)
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == lifecycleJobId && q.GameServerFk == gameServerId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"Lifecycle job {lifecycleJobId} does not exist.");
        if (job.Status is not ELifecycleJobStatus.Failed)
            throw new HostingConflictException("Only a failed lifecycle job can be skipped.");
        if (job.FailureCode == "permanent-execution-failed")
            throw new HostingConflictException("This permanent failure is already terminal and does not block later work.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        job.Status = ELifecycleJobStatus.Skipped;
        job.Stage = "skipped-by-user";
        job.TimeStampUpdated = now;
        job.TimeStampCompleted = now;
        foreach (var command in job.WorkerCommands.Where(q => q.Status is EWorkerCommandStatus.Failed))
        {
            command.Status = EWorkerCommandStatus.Skipped;
            command.TimeStampCompleted = now;
        }
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.LifecycleSkipped,
            "Failed lifecycle job explicitly skipped.",
            actorId,
            gameServerId,
            job.DeploymentFk,
            job.PrimaryKey);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return job;
    }

    public async Task ChangeCostOwnerAsync(
        Guid gameServerId,
        Guid costOwnerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Modify))
            throw new HostingForbiddenException("Server Modify permission is required.");
        if (!await authorizationService.CanUseCostOwnerAsync(principal, costOwnerId, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingForbiddenException("The selected cost owner cannot be used by this user.");
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        IQueryable<GameServer> serverQuery = dbContext.GameServers;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            serverQuery = dbContext.GameServers.FromSqlInterpolated(
                $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {gameServerId} FOR UPDATE""");
        }
        var server = await serverQuery
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException($"Game server {gameServerId} does not exist.");
        if (await dbContext.GameServerDeployments.AnyAsync(
                q => q.GameServerFk == gameServerId && q.TimeStampResolved == null,
                cancellationToken).ConfigureAwait(false))
            throw new HostingConflictException("Cost ownership cannot change while a deployment is unresolved.");
        var owner = await dbContext.CostOwners
            .SingleOrDefaultAsync(q => q.PrimaryKey == costOwnerId && !q.IsFrozen, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingValidationException("The selected cost owner is unavailable.");
        if (server.CurrentCostOwnerFk == owner.PrimaryKey)
            return;

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var priorAssignment = await dbContext.GameServerCostOwnerAssignments
            .SingleAsync(q => q.GameServerFk == gameServerId && q.TimeStampUnassigned == null, cancellationToken)
            .ConfigureAwait(false);
        priorAssignment.TimeStampUnassigned = now;
        priorAssignment.UnassignedByFk = actorId;
        priorAssignment.ChangeReason = "Cost owner changed by an authorized user.";
        dbContext.GameServerCostOwnerAssignments.Add(new GameServerCostOwnerAssignment
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = server.PrimaryKey,
            CostOwnerFk      = owner.PrimaryKey,
            AssignedByFk     = actorId,
            TimeStampAssigned = now,
        });
        server.CurrentCostOwnerFk = owner.PrimaryKey;
        server.TimeStampUpdated = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.CostOwnerChanged,
            "Server cost owner changed.",
            actorId,
            server.PrimaryKey,
            costOwnerId: owner.PrimaryKey,
            details: $"PreviousCostOwner={priorAssignment.CostOwnerFk}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ArchiveAsync(
        Guid gameServerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!principal.TryGetUserId(out var actorId)
            || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Modify))
            throw new HostingForbiddenException("Server Modify permission is required.");

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        IQueryable<GameServer> serverQuery = dbContext.GameServers;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            serverQuery = dbContext.GameServers.FromSqlInterpolated(
                $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {gameServerId} FOR UPDATE""");
        }
        var server = await serverQuery
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HostingNotFoundException("The server does not exist or is already archived.");
        if (await dbContext.GameServerDeployments
                .AnyAsync(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingConflictException("Fully stop the server and verify provider cleanup before deleting it.");
        if (await dbContext.LifecycleJobs
                .AnyAsync(
                    q => q.GameServerFk == gameServerId
                         && (q.Status == ELifecycleJobStatus.Queued
                             || q.Status == ELifecycleJobStatus.Running
                             || q.Status == ELifecycleJobStatus.RetryPending
                             || q.Status == ELifecycleJobStatus.WaitingForWorker
                             || q.Status == ELifecycleJobStatus.Failed
                             && q.FailureCode != "permanent-execution-failed"),
                    cancellationToken)
                .ConfigureAwait(false))
            throw new HostingConflictException("Wait for pending lifecycle work to finish before deleting the server.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        server.IsArchived = true;
        server.IsLaunchDisabled = true;
        server.LaunchDisabledReason = "The server record was deleted.";
        server.TimeStampArchived = now;
        server.TimeStampUpdated = now;

        var activeOwnerAssignment = await dbContext.GameServerCostOwnerAssignments
            .SingleOrDefaultAsync(
                q => q.GameServerFk == gameServerId && q.TimeStampUnassigned == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (activeOwnerAssignment is not null)
        {
            activeOwnerAssignment.TimeStampUnassigned = now;
            activeOwnerAssignment.UnassignedByFk = actorId;
            activeOwnerAssignment.ChangeReason = "Server record archived.";
        }

        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ServerArchived,
            "Game server archived.",
            actorId,
            server.PrimaryKey,
            costOwnerId: server.CurrentCostOwnerFk);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CancelPendingWorkAsync(
        Guid deploymentId,
        Instant now,
        bool includeRunningAndFailed,
        CancellationToken cancellationToken)
    {
        var jobs = await dbContext.LifecycleJobs
            .Where(q => q.DeploymentFk == deploymentId
                        && (q.Status == ELifecycleJobStatus.Queued
                            || q.Status == ELifecycleJobStatus.RetryPending
                            || q.Status == ELifecycleJobStatus.WaitingForWorker
                            || includeRunningAndFailed
                            && (q.Status == ELifecycleJobStatus.Running
                                || q.Status == ELifecycleJobStatus.Failed)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var pending in jobs)
        {
            pending.Status = ELifecycleJobStatus.CancelledByCostStop;
            pending.Stage = "cancelled-by-cost-stop";
            pending.LeaseOwner = null;
            pending.LeaseExpires = null;
            pending.TimeStampUpdated = now;
            pending.TimeStampCompleted = now;
        }
        var jobIds = jobs.Select(q => q.PrimaryKey).ToArray();
        var attempts = await dbContext.LifecycleJobAttempts
            .Where(q => jobIds.Contains(q.LifecycleJobFk) && q.TimeStampCompleted == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var attempt in attempts)
        {
            attempt.Status = ELifecycleJobStatus.CancelledByCostStop;
            attempt.Stage = "cancelled-by-cost-stop";
            attempt.TimeStampCompleted = now;
        }
        var commands = await dbContext.WorkerCommands
            .Where(q => q.DeploymentFk == deploymentId
                        && (q.Status == EWorkerCommandStatus.Queued
                            || q.Status == EWorkerCommandStatus.Received
                            || includeRunningAndFailed
                            && (q.Status == EWorkerCommandStatus.Running
                                || q.Status == EWorkerCommandStatus.Failed)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var pending in commands)
        {
            pending.Status = EWorkerCommandStatus.CancelledByCostStop;
            pending.Stage = "cancelled-by-cost-stop";
            pending.LeaseOwner = null;
            pending.LeaseExpires = null;
            pending.TimeStampCompleted = now;
        }
    }

    private static ECostImpact CostImpact(ELifecycleJobKind kind)
        => kind switch
        {
            ELifecycleJobKind.Start => ECostImpact.StartsCost,
            ELifecycleJobKind.Stop or ELifecycleJobKind.Terminate => ECostImpact.StopsCost,
            _ => ECostImpact.KeepsCostRunning,
        };

    private static string CanonicalizePreservationPath(string path, EPreservationScope scope)
    {
        var normalized = SafeServerPath.Normalize(path, scope is not EPreservationScope.File);
        if (normalized.Length == 0)
            throw new HostingValidationException("The runtime root cannot be selected for preservation.");
        return OperatingSystem.IsWindows() ? normalized.ToLowerInvariant() : normalized;
    }
}
