using X39.UnitedTacticalForces.Database.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Turns the weekly schedule rules of a server into recorded occurrences: it queues consented and affordable
/// scheduled starts, records why an occurrence was skipped, and turns due stop occurrences into pending stops that the
/// automation evaluator warns about and executes.
/// </summary>
/// <remarks>
/// Every occurrence is recorded exactly once through the unique run row of its rule and instant, so a repeated pass, a
/// crash between two steps or a second API instance never acts on an occurrence twice.
/// </remarks>
internal interface IServerScheduleRunner
{
    /// <summary>
    /// Records every occurrence of the server's rules that came due since the persisted watermark (and not before the
    /// rule's effective instant), each in its own transaction, and advances the watermark.
    /// </summary>
    /// <param name="gameServerId">The server to scan.</param>
    /// <param name="now">The instant the pass is evaluated at; the runner never reads a clock itself.</param>
    /// <param name="cancellationToken">A token that cancels the scan.</param>
    /// <returns>
    /// A task resolving to the alerts of warnings that a scheduled stop superseded and the number of queued starts.
    /// </returns>
    Task<ScheduleScanResult> ProcessDueAsync(Guid gameServerId, Instant now, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the earliest stop occurrence whose warning window is open: <c>O - warningLead &lt;= now &lt; O</c> and
    /// <c>O &gt;= EffectiveFrom</c>.
    /// </summary>
    /// <param name="settings">The settings of the server with their schedule rules loaded.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="warningLead">The time between a warning and its stop.</param>
    /// <param name="handled">
    /// The rule and occurrence pairs that already have a pending stop or a recorded run, which are never proposed
    /// again, or <see langword="null" /> when none is known.
    /// </param>
    /// <returns>The stop to warn about, or <see langword="null" /> when no window is open.</returns>
    PendingStopCandidate? NextScheduledStop(
        ServerAutomationSettings settings,
        Instant now,
        Duration warningLead,
        IReadOnlySet<(Guid ScheduleId, Instant OccurrenceAt)>? handled = null);
}
