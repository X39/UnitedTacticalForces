using System.Data;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Converts a confirmed unattended deployment fault into the same durable Terminate path used for verified cost-stop.
/// </summary>
[Scoped<AutomaticCostStopService, IAutomaticCostStopService>]
internal sealed class AutomaticCostStopService(
    DatabaseContext dbContext,
    IHostingAuditWriter auditWriter) : IAutomaticCostStopService
{
    public async Task<bool> TriggerAsync(
        Guid deploymentId,
        string faultCode,
        string faultMessage,
        CancellationToken cancellationToken = default)
    {
        var sanitizedCode = HostingFailureSanitizer.Sanitize(faultCode, 128);
        var sanitizedMessage = HostingFailureSanitizer.Sanitize(faultMessage, 2048);
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        var serverId = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.PrimaryKey == deploymentId)
            .Select(q => (Guid?)q.GameServerFk)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (serverId is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (IsPostgreSql)
        {
            await dbContext.GameServers
                .FromSqlInterpolated($"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {serverId.Value} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            await dbContext.GameServerDeployments
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {deploymentId} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var deployment = await dbContext.GameServerDeployments
            .Include(q => q.GameServer)
            .SingleAsync(q => q.PrimaryKey == deploymentId, cancellationToken)
            .ConfigureAwait(false);
        if (deployment.TimeStampResolved is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var existingTermination = await dbContext.LifecycleJobs.AnyAsync(
                q => q.DeploymentFk == deploymentId
                     && q.Kind == ELifecycleJobKind.Terminate
                     && (q.Status == ELifecycleJobStatus.Queued
                         || q.Status == ELifecycleJobStatus.Running
                         || q.Status == ELifecycleJobStatus.RetryPending
                         || q.Status == ELifecycleJobStatus.WaitingForWorker),
                cancellationToken)
            .ConfigureAwait(false);

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.TerminatingDueToFault;
        deployment.Stage = "automatic-fault-cost-stop";
        deployment.PossibleCost = true;
        deployment.FaultCode ??= sanitizedCode;
        deployment.FaultMessage ??= sanitizedMessage;
        deployment.TimeStampFaulted ??= now;
        deployment.TimeStampUpdated = now;
        if (existingTermination)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await CancelNormalWorkAsync(deploymentId, now, cancellationToken).ConfigureAwait(false);
        var nextSequence = (await dbContext.LifecycleJobs
                .Where(q => q.GameServerFk == deployment.GameServerFk)
                .MaxAsync(q => (long?)q.Sequence, cancellationToken)
                .ConfigureAwait(false) ?? 0) + 1;
        var job = new LifecycleJob
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = deployment.GameServerFk,
            DeploymentFk = deployment.PrimaryKey,
            RequestedByFk = null,
            Kind = ELifecycleJobKind.Terminate,
            ActionIdentifier = GameServerActions.Terminate,
            Sequence = nextSequence,
            Priority = int.MaxValue,
            Status = ELifecycleJobStatus.Queued,
            Stage = "automatic-fault-cost-stop",
            Progress = 0,
            MaxAttempts = int.MaxValue,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.LifecycleJobs.Add(job);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.Recovery,
            "Confirmed deployment fault queued automatic provider cost-stop.",
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            lifecycleJobId: job.PrimaryKey,
            costOwnerId: deployment.CostOwnerFk,
            details: $"FaultCode={sanitizedCode}; Fault={sanitizedMessage}",
            costImpact: ECostImpact.StopsCost);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task CancelNormalWorkAsync(
        Guid deploymentId,
        Instant now,
        CancellationToken cancellationToken)
    {
        var jobs = await dbContext.LifecycleJobs
            .Where(q => q.DeploymentFk == deploymentId
                        && q.Kind != ELifecycleJobKind.Terminate
                        && (q.Status == ELifecycleJobStatus.Queued
                            || q.Status == ELifecycleJobStatus.Running
                            || q.Status == ELifecycleJobStatus.RetryPending
                            || q.Status == ELifecycleJobStatus.WaitingForWorker
                            || q.Status == ELifecycleJobStatus.Failed))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var job in jobs)
        {
            job.Status = ELifecycleJobStatus.CancelledByCostStop;
            job.Stage = "cancelled-by-cost-stop";
            job.LeaseOwner = null;
            job.LeaseExpires = null;
            job.TimeStampNextAttempt = null;
            job.TimeStampUpdated = now;
            job.TimeStampCompleted ??= now;
        }

        var jobIds = jobs.Select(q => q.PrimaryKey).ToArray();
        var attempts = await dbContext.LifecycleJobAttempts
            .Where(q => jobIds.Contains(q.LifecycleJobFk) && q.TimeStampCompleted == null)
            .ToArrayAsync(cancellationToken)
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
                            || q.Status == EWorkerCommandStatus.Running
                            || q.Status == EWorkerCommandStatus.Failed))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var command in commands)
        {
            command.Status = EWorkerCommandStatus.CancelledByCostStop;
            command.Stage = "cancelled-by-cost-stop";
            command.LeaseOwner = null;
            command.LeaseExpires = null;
            command.TimeStampCompleted ??= now;
        }
    }

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
