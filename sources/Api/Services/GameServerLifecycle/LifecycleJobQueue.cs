using System.Data;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

[Scoped<LifecycleJobQueue, ILifecycleJobQueue>]
internal sealed class LifecycleJobQueue(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    IHostingAuditWriter auditWriter) : ILifecycleJobQueue
{
    public async Task<LifecycleJob?> ClaimNextAsync(
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default,
        bool terminateOnly = false)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var exhaustedExpiredJobs = await dbContext.LifecycleJobs
            .Include(q => q.Attempts)
            .Include(q => q.WorkerCommands)
            .Where(q => q.Kind != ELifecycleJobKind.Terminate
                        && q.Status == ELifecycleJobStatus.Running
                        && q.LeaseExpires != null
                        && q.LeaseExpires <= now
                        && q.Attempt >= q.MaxAttempts)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var exhausted in exhaustedExpiredJobs)
        {
            exhausted.Status = ELifecycleJobStatus.Failed;
            exhausted.Stage = "lease-expired";
            exhausted.LeaseOwner = null;
            exhausted.LeaseExpires = null;
            exhausted.TimeStampUpdated = now;
            exhausted.TimeStampCompleted = now;
            exhausted.FailureCode = "lease-expired";
            exhausted.FailureMessage = "The processor lease expired on the final permitted attempt.";
            var attempt = exhausted.Attempts.SingleOrDefault(q =>
                q.AttemptNumber == exhausted.Attempt && q.Status == ELifecycleJobStatus.Running);
            if (attempt is not null)
            {
                attempt.Status = ELifecycleJobStatus.Failed;
                attempt.Stage = exhausted.Stage;
                attempt.TimeStampCompleted = now;
                attempt.FailureCode = exhausted.FailureCode;
                attempt.FailureMessage = exhausted.FailureMessage;
            }
            foreach (var command in exhausted.WorkerCommands.Where(q =>
                         q.Status is EWorkerCommandStatus.Queued
                             or EWorkerCommandStatus.Received
                             or EWorkerCommandStatus.Running))
            {
                command.Status = EWorkerCommandStatus.Failed;
                command.Stage = exhausted.Stage;
                command.LeaseOwner = null;
                command.LeaseExpires = null;
                command.TimeStampCompleted = now;
                command.ErrorCode = exhausted.FailureCode;
                command.ErrorMessage = exhausted.FailureMessage;
            }
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.LifecycleJobFailed,
                "Lifecycle job failed after its final processor lease expired.",
                serverId: exhausted.GameServerFk,
                deploymentId: exhausted.DeploymentFk,
                lifecycleJobId: exhausted.PrimaryKey,
                details: exhausted.FailureMessage);
        }
        if (exhaustedExpiredJobs.Length > 0)
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var candidate = await dbContext.LifecycleJobs
            .Where(q =>
                q.Status == ELifecycleJobStatus.Queued
                || (q.Status == ELifecycleJobStatus.RetryPending
                    && (q.TimeStampNextAttempt == null || q.TimeStampNextAttempt <= now))
                || (q.Status == ELifecycleJobStatus.WaitingForWorker
                    && (q.TimeStampNextAttempt == null || q.TimeStampNextAttempt <= now))
                || (q.Status == ELifecycleJobStatus.Running
                    && q.LeaseExpires != null
                    && q.LeaseExpires <= now))
            .Where(q => !terminateOnly || q.Kind == ELifecycleJobKind.Terminate)
            .Where(q => !dbContext.LifecycleJobs.Any(previous =>
                previous.DeploymentFk == q.DeploymentFk
                && previous.Sequence < q.Sequence
                && (q.Kind != ELifecycleJobKind.Terminate || previous.Kind == ELifecycleJobKind.Terminate)
                && (previous.Status == ELifecycleJobStatus.Queued
                    || previous.Status == ELifecycleJobStatus.Running
                    || previous.Status == ELifecycleJobStatus.RetryPending
                    || previous.Status == ELifecycleJobStatus.WaitingForWorker
                    || previous.Status == ELifecycleJobStatus.Failed)))
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.TimeStampCreated)
            .ThenBy(q => q.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var recoveringExpiredLease = candidate.Status == ELifecycleJobStatus.Running;
        if (candidate.Kind == ELifecycleJobKind.Terminate)
        {
            // A cost-stop supersedes normal work for this deployment. Persist those cancellations
            // before claiming the terminate job so the active-job unique index is never violated.
            var supersededJobs = await dbContext.LifecycleJobs
                .Include(q => q.Attempts)
                .Include(q => q.WorkerCommands)
                .Where(q => q.DeploymentFk == candidate.DeploymentFk
                            && q.PrimaryKey != candidate.PrimaryKey
                            && q.Kind != ELifecycleJobKind.Terminate
                            && (q.Status == ELifecycleJobStatus.Queued
                                || q.Status == ELifecycleJobStatus.Running
                                || q.Status == ELifecycleJobStatus.RetryPending
                                || q.Status == ELifecycleJobStatus.WaitingForWorker
                                || q.Status == ELifecycleJobStatus.Failed))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var superseded in supersededJobs)
            {
                superseded.Status = ELifecycleJobStatus.CancelledByCostStop;
                superseded.Stage = "cancelled-by-cost-stop";
                superseded.LeaseOwner = null;
                superseded.LeaseExpires = null;
                superseded.TimeStampNextAttempt = null;
                superseded.TimeStampUpdated = now;
                superseded.TimeStampCompleted = now;
                foreach (var attempt in superseded.Attempts.Where(q => q.Status == ELifecycleJobStatus.Running))
                {
                    attempt.Status = ELifecycleJobStatus.CancelledByCostStop;
                    attempt.Stage = "cancelled-by-cost-stop";
                    attempt.TimeStampCompleted = now;
                }
                foreach (var command in superseded.WorkerCommands.Where(q =>
                             q.Status is EWorkerCommandStatus.Queued
                                 or EWorkerCommandStatus.Received
                                 or EWorkerCommandStatus.Running))
                {
                    command.Status = EWorkerCommandStatus.CancelledByCostStop;
                    command.Stage = "cancelled-by-cost-stop";
                    command.LeaseOwner = null;
                    command.LeaseExpires = null;
                    command.TimeStampCompleted = now;
                }
                auditWriter.Add(
                    dbContext,
                    EHostingAuditEventKind.LifecycleCancelledByCostStop,
                    "Lifecycle work cancelled because termination superseded it.",
                    serverId: superseded.GameServerFk,
                    deploymentId: superseded.DeploymentFk,
                    lifecycleJobId: superseded.PrimaryKey,
                    costImpact: ECostImpact.StopsCost);
            }
            candidate.MaxAttempts = int.MaxValue;
            if (supersededJobs.Length > 0)
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (recoveringExpiredLease)
        {
            var abandonedAttempt = await dbContext.LifecycleJobAttempts
                .SingleOrDefaultAsync(
                    q => q.LifecycleJobFk == candidate.PrimaryKey
                         && q.AttemptNumber == candidate.Attempt
                         && q.Status == ELifecycleJobStatus.Running,
                    cancellationToken)
                .ConfigureAwait(false);
            if (abandonedAttempt is not null)
            {
                abandonedAttempt.Status = ELifecycleJobStatus.RetryPending;
                abandonedAttempt.Stage = "lease-expired";
                abandonedAttempt.TimeStampCompleted = now;
                abandonedAttempt.FailureCode = "lease-expired";
                abandonedAttempt.FailureMessage = "The prior processor lease expired before completion.";
            }
        }

        candidate.Status = ELifecycleJobStatus.Running;
        candidate.Stage = recoveringExpiredLease ? "recovered" : "claimed";
        candidate.Attempt++;
        candidate.LeaseOwner = leaseOwner;
        candidate.LeaseExpires = now + Duration.FromTimeSpan(leaseDuration);
        candidate.TimeStampStarted ??= now;
        candidate.TimeStampUpdated = now;
        candidate.TimeStampNextAttempt = null;
        candidate.FailureCode = null;
        candidate.FailureMessage = null;
        dbContext.LifecycleJobAttempts.Add(new LifecycleJobAttempt
        {
            PrimaryKey      = Guid.NewGuid(),
            LifecycleJobFk  = candidate.PrimaryKey,
            AttemptNumber   = candidate.Attempt,
            Status          = ELifecycleJobStatus.Running,
            Stage           = candidate.Stage,
            TimeStampStarted = now,
        });
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.LifecycleJobClaimed,
            "Lifecycle job claimed by the durable processor.",
            serverId: candidate.GameServerFk,
            deploymentId: candidate.DeploymentFk,
            lifecycleJobId: candidate.PrimaryKey,
            details: $"Attempt={candidate.Attempt}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return candidate;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return await dbContext.LifecycleJobs
            .Where(q => q.PrimaryKey == lifecycleJobId
                        && q.Status == ELifecycleJobStatus.Running
                        && q.LeaseOwner == leaseOwner)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(q => q.LeaseExpires, now + Duration.FromTimeSpan(leaseDuration))
                    .SetProperty(q => q.TimeStampUpdated, now),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    public async Task CompleteAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var job = await dbContext.LifecycleJobs
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == lifecycleJobId
                     && q.Status == ELifecycleJobStatus.Running
                     && q.LeaseOwner == leaseOwner,
                cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
            return;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        job.Status = ELifecycleJobStatus.Succeeded;
        job.Stage = "completed";
        job.Progress = 100;
        job.LeaseOwner = null;
        job.LeaseExpires = null;
        job.TimeStampUpdated = now;
        job.TimeStampCompleted = now;
        var attempt = await dbContext.LifecycleJobAttempts
            .SingleAsync(
                q => q.LifecycleJobFk == lifecycleJobId && q.AttemptNumber == job.Attempt,
                cancellationToken)
            .ConfigureAwait(false);
        attempt.Status = ELifecycleJobStatus.Succeeded;
        attempt.Stage = job.Stage;
        attempt.TimeStampCompleted = now;
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.LifecycleJobSucceeded,
            "Lifecycle job completed.",
            serverId: job.GameServerFk,
            deploymentId: job.DeploymentFk,
            lifecycleJobId: job.PrimaryKey);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task FailAsync(
        Guid lifecycleJobId,
        string leaseOwner,
        string failureReason,
        TimeSpan retryDelay,
        bool retryAllowed = true,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var job = await dbContext.LifecycleJobs
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == lifecycleJobId
                     && q.Status == ELifecycleJobStatus.Running
                     && q.LeaseOwner == leaseOwner,
                cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
            return;

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var sanitized = HostingFailureSanitizer.Sanitize(failureReason, 2048);
        var retry = job.Kind == ELifecycleJobKind.Terminate
                    || (retryAllowed && job.Attempt < job.MaxAttempts);
        if (job.Kind == ELifecycleJobKind.Terminate)
            job.MaxAttempts = int.MaxValue;
        job.Status = retry ? ELifecycleJobStatus.RetryPending : ELifecycleJobStatus.Failed;
        job.Stage = retry ? "retry-pending" : "failed";
        job.LeaseOwner = null;
        job.LeaseExpires = null;
        job.TimeStampNextAttempt = retry ? now + Duration.FromTimeSpan(retryDelay) : null;
        job.TimeStampUpdated = now;
        job.TimeStampCompleted = retry ? null : now;
        job.FailureCode = retryAllowed ? "execution-failed" : "permanent-execution-failed";
        job.FailureMessage = sanitized;
        var attempt = await dbContext.LifecycleJobAttempts
            .SingleAsync(
                q => q.LifecycleJobFk == lifecycleJobId && q.AttemptNumber == job.Attempt,
                cancellationToken)
            .ConfigureAwait(false);
        attempt.Status = job.Status;
        attempt.Stage = job.Stage;
        attempt.TimeStampCompleted = now;
        attempt.FailureCode = job.FailureCode;
        attempt.FailureMessage = sanitized;
        auditWriter.Add(
            dbContext,
            retry ? EHostingAuditEventKind.LifecycleJobRetryPending : EHostingAuditEventKind.LifecycleJobFailed,
            retry ? "Lifecycle job scheduled for retry." : "Lifecycle job failed.",
            serverId: job.GameServerFk,
            deploymentId: job.DeploymentFk,
            lifecycleJobId: job.PrimaryKey,
            details: sanitized);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
