using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Withdraws an open automatic stop warning: the pending stop becomes cancelled or superseded, its alert is resolved
/// and the audit entry is written. A superseded scheduled stop also records its run, so its occurrence is accounted
/// for exactly once.
/// </summary>
internal static class PendingStopWithdrawal
{
    /// <summary>
    /// Withdraws an open warning inside the caller's transaction.
    /// </summary>
    /// <param name="dbContext">The database context that receives the changes.</param>
    /// <param name="alertWriter">The writer that resolves the warning alert.</param>
    /// <param name="auditWriter">The writer that appends the audit entries.</param>
    /// <param name="stop">The tracked pending stop.</param>
    /// <param name="status">The terminal status, cancelled or superseded.</param>
    /// <param name="reason">The machine-readable reason.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>A task resolving to the resolved alert, or <see langword="null" /> when no alert was open.</returns>
    public static async Task<ResolvedCostAlert?> ApplyAsync(
        DatabaseContext dbContext,
        IAutoStopAlertWriter alertWriter,
        IHostingAuditWriter auditWriter,
        AutomationPendingStop stop,
        EAutoStopStatus status,
        string reason,
        Instant now,
        CancellationToken cancellationToken)
    {
        stop.Status = status;
        stop.CancelReason = reason;
        stop.ResolvedAt = now;
        var resolved = await alertWriter.ResolveAsync(stop.PrimaryKey, now, cancellationToken).ConfigureAwait(false);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.AutomaticStopCancelled,
            "Automatic stop warning withdrawn.",
            serverId: stop.GameServerFk,
            deploymentId: stop.DeploymentFk,
            details: $"Reason={stop.Reason}; CancelReason={reason}; Status={status}",
            costImpact: ECostImpact.NoCostImpact);

        // A cancelled scheduled stop (rule changed or removed) records no run; a superseded one did not happen.
        if (status == EAutoStopStatus.Superseded
            && stop is { Reason: EAutoStopReason.Schedule, ScheduleFk: { } scheduleId, ScheduleOccurrenceAt: { } occurrence }
            && await dbContext.ServerSchedules
                .AsNoTracking()
                .AnyAsync(q => q.PrimaryKey == scheduleId, cancellationToken)
                .ConfigureAwait(false)
            && !await ScheduledRunLedger.ExistsAsync(dbContext, scheduleId, occurrence, cancellationToken)
                .ConfigureAwait(false))
        {
            var deploymentGone = reason == PendingStopPlanner.DeploymentNotRunningReason;
            ScheduledRunLedger.AddSkip(
                dbContext,
                auditWriter,
                scheduleId,
                stop.GameServerFk,
                null,
                occurrence,
                deploymentGone ? EServerScheduleRunOutcome.SkippedOff : EServerScheduleRunOutcome.SkippedBusy,
                deploymentGone
                    ? "the server was not running any more"
                    : "another stop already applies to the server",
                now);
        }

        return resolved;
    }
}
