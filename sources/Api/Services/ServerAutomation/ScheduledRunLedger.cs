using System.Globalization;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Writes the unique run row of a schedule occurrence and the audit entry of a skipped one. The run row is what makes
/// an occurrence act exactly once.
/// </summary>
internal static class ScheduledRunLedger
{
    /// <summary>
    /// The details of a run row whose start waits for the stop of its server to finish.
    /// </summary>
    public const string WaitingDetails = "waiting for the stop of the server to finish";

    /// <summary>
    /// The maximum length of the stored run details.
    /// </summary>
    private const int MaxDetailsLength = 256;

    /// <summary>
    /// The maximum length of an audit summary.
    /// </summary>
    private const int MaxSummaryLength = 512;

    /// <summary>
    /// Tells whether an occurrence already has a recorded run.
    /// </summary>
    /// <param name="dbContext">The database context to query.</param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>A task resolving to <see langword="true" /> when a run row exists.</returns>
    public static Task<bool> ExistsAsync(
        DatabaseContext dbContext,
        Guid scheduleId,
        Instant occurrence,
        CancellationToken cancellationToken)
        => dbContext.ServerScheduleRuns
            .AsNoTracking()
            .AnyAsync(q => q.ScheduleFk == scheduleId && q.OccurrenceAt == occurrence, cancellationToken);

    /// <summary>
    /// Loads the tracked run row of an occurrence.
    /// </summary>
    /// <param name="dbContext">The database context that tracks the row.</param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>A task resolving to the tracked run row, or <see langword="null" /> when none exists.</returns>
    public static Task<ServerScheduleRun?> FindAsync(
        DatabaseContext dbContext,
        Guid scheduleId,
        Instant occurrence,
        CancellationToken cancellationToken)
        => dbContext.ServerScheduleRuns
            .SingleOrDefaultAsync(q => q.ScheduleFk == scheduleId && q.OccurrenceAt == occurrence, cancellationToken);

    /// <summary>
    /// Adds the open run row of a start occurrence that waits for the stop of its server.
    /// </summary>
    /// <param name="dbContext">The database context that receives the row.</param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="gameServerId">The server the rule belongs to.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="now">The instant the occurrence began to wait.</param>
    /// <returns>The tracked run row.</returns>
    public static ServerScheduleRun AddWaiting(
        DatabaseContext dbContext,
        Guid scheduleId,
        Guid gameServerId,
        Instant occurrence,
        Instant now)
        => AddRun(
            dbContext,
            scheduleId,
            gameServerId,
            occurrence,
            EServerScheduleRunOutcome.WaitingForStop,
            null,
            WaitingDetails,
            now);

    /// <summary>
    /// Adds the run row and the <see cref="EHostingAuditEventKind.ScheduledRunSkipped" /> audit entry of a skipped
    /// occurrence.
    /// </summary>
    /// <param name="dbContext">The database context that receives the rows.</param>
    /// <param name="auditWriter">The writer that appends the audit entry.</param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="gameServerId">The server the rule belongs to.</param>
    /// <param name="costOwnerId">The cost owner the entry is attributed to, or <see langword="null" />.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="outcome">The skip outcome.</param>
    /// <param name="reason">The human readable reason that goes into the summary and the run details.</param>
    /// <param name="now">The instant the skip is recorded.</param>
    public static void AddSkip(
        DatabaseContext dbContext,
        IHostingAuditWriter auditWriter,
        Guid scheduleId,
        Guid gameServerId,
        Guid? costOwnerId,
        Instant occurrence,
        EServerScheduleRunOutcome outcome,
        string reason,
        Instant now)
        => RecordSkip(
            dbContext,
            auditWriter,
            null,
            scheduleId,
            gameServerId,
            costOwnerId,
            occurrence,
            outcome,
            reason,
            now);

    /// <summary>
    /// Records a skipped occurrence either on its open waiting row or, when there is none, on a new run row, and appends
    /// the <see cref="EHostingAuditEventKind.ScheduledRunSkipped" /> audit entry.
    /// </summary>
    /// <param name="dbContext">The database context that receives the changes.</param>
    /// <param name="auditWriter">The writer that appends the audit entry.</param>
    /// <param name="open">
    /// The tracked waiting run row of the occurrence, or <see langword="null" /> to add a new run row.
    /// </param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="gameServerId">The server the rule belongs to.</param>
    /// <param name="costOwnerId">The cost owner the entry is attributed to, or <see langword="null" />.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="outcome">The skip outcome.</param>
    /// <param name="reason">The human readable reason that goes into the summary and the run details.</param>
    /// <param name="now">The instant the skip is recorded.</param>
    public static void RecordSkip(
        DatabaseContext dbContext,
        IHostingAuditWriter auditWriter,
        ServerScheduleRun? open,
        Guid scheduleId,
        Guid gameServerId,
        Guid? costOwnerId,
        Instant occurrence,
        EServerScheduleRunOutcome outcome,
        string reason,
        Instant now)
    {
        if (open is null)
        {
            AddRun(dbContext, scheduleId, gameServerId, occurrence, outcome, null, reason, now);
        }
        else
        {
            open.Outcome = outcome;
            open.LifecycleJobFk = null;
            open.Details = Truncate(reason, MaxDetailsLength);
            open.RecordedAt = now;
        }

        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ScheduledRunSkipped,
            Truncate($"Scheduled run skipped: {reason}", MaxSummaryLength)!,
            serverId: gameServerId,
            costOwnerId: costOwnerId,
            details: $"ScheduleId={scheduleId}; Occurrence={occurrence.ToString("g", CultureInfo.InvariantCulture)}; "
                     + $"Outcome={outcome}",
            costImpact: ECostImpact.NoCostImpact);
    }

    /// <summary>
    /// Adds the run row of an occurrence to the context.
    /// </summary>
    /// <param name="dbContext">The database context that receives the row.</param>
    /// <param name="scheduleId">The rule of the occurrence.</param>
    /// <param name="gameServerId">The server the rule belongs to.</param>
    /// <param name="occurrence">The occurrence instant.</param>
    /// <param name="outcome">What happened with the occurrence.</param>
    /// <param name="lifecycleJobId">The lifecycle job queued for the occurrence, or <see langword="null" />.</param>
    /// <param name="details">A short sanitized reason, or <see langword="null" />.</param>
    /// <param name="now">The instant the run is recorded.</param>
    /// <returns>The tracked run row.</returns>
    public static ServerScheduleRun AddRun(
        DatabaseContext dbContext,
        Guid scheduleId,
        Guid gameServerId,
        Instant occurrence,
        EServerScheduleRunOutcome outcome,
        Guid? lifecycleJobId,
        string? details,
        Instant now)
    {
        var run = new ServerScheduleRun
        {
            PrimaryKey = Guid.NewGuid(),
            ScheduleFk = scheduleId,
            GameServerFk = gameServerId,
            OccurrenceAt = occurrence,
            Outcome = outcome,
            LifecycleJobFk = lifecycleJobId,
            Details = Truncate(details, MaxDetailsLength),
            RecordedAt = now,
        };
        dbContext.ServerScheduleRuns.Add(run);
        return run;
    }

    private static string? Truncate(string? value, int maxLength)
        => value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
