using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The pure decision of what happens to the automatic stop of one deployment: warn, withdraw, replace, execute, or
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// D-AUTO-1: an idle or maximum runtime stop is warned when its limit is reached and happens one warning lead later
/// (<c>StopsAt = now + WarningLead</c> at the moment the warning is created).
/// </para>
/// <para>
/// Downtime rule: a maximum runtime limit whose warning window elapsed completely while nobody evaluated (the
/// platform was down) is executed at the first pass without a warning, because the owner can no longer receive a
/// useful lead. Idle never does this: an idle stop always needs a fresh known reading and therefore always warns
/// first.
/// </para>
/// <para>
/// A scheduled stop warns when its window opens one lead before its occurrence and runs at the occurrence. The
/// earlier stop wins: an open warning that stops later than a scheduled stop whose window is open is replaced, and a
/// new warning is the earliest of all candidates. An open scheduled stop whose rule was removed or changed since the
/// warning was opened is withdrawn.
/// </para>
/// <para>
/// An unavailable player reading holds an open idle warning. It is neither executed nor withdrawn until a known
/// reading arrives, which avoids a withdraw-and-warn-again message pair after every API restart while still never
/// stopping a server on a guess.
/// </para>
/// </remarks>
public static class PendingStopPlanner
{
    /// <summary>
    /// The cancel code used when connected players or a running job ended the idle condition.
    /// </summary>
    public const string ActivityReason = "activity";

    /// <summary>
    /// The cancel code used when a limit was switched off, raised or otherwise changed.
    /// </summary>
    public const string SettingsChangedReason = "settings-changed";

    /// <summary>
    /// The supersede code used when the deployment is no longer running.
    /// </summary>
    public const string DeploymentNotRunningReason = "deployment-not-running";

    /// <summary>
    /// The supersede code used when a stop job is already pending.
    /// </summary>
    public const string StopAlreadyQueuedReason = "stop-already-queued";

    /// <summary>
    /// The supersede code used when an earlier stop replaced the open warning.
    /// </summary>
    public const string EarlierStopReason = "earlier-stop";

    /// <summary>
    /// Decides what to do with the automatic stop of a deployment.
    /// </summary>
    /// <param name="input">The facts of this evaluation pass.</param>
    /// <returns>The decision and, for a warning or an execution, the stop it applies to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
    public static PendingStopPlan Plan(PendingStopPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.OpenPending is { } open ? PlanOpen(input, open) : PlanNew(input);
    }

    private static PendingStopPlan PlanOpen(PendingStopPlanInput input, PendingStopSnapshot open)
    {
        if (!input.DeploymentRunning)
            return new PendingStopPlan(EPendingStopDecision.Supersede, null, DeploymentNotRunningReason);
        if (input.StopAlreadyQueued)
            return new PendingStopPlan(EPendingStopDecision.Supersede, null, StopAlreadyQueuedReason);

        if (open.Reason == EAutoStopReason.MaxRuntime
            && (!input.MaxRuntimeEnabled
                || MaxRuntimeLimit(input) is not { } limit
                || limit > input.Now))
        {
            return new PendingStopPlan(EPendingStopDecision.Cancel, null, SettingsChangedReason);
        }

        if (open.Reason == EAutoStopReason.Schedule && !input.OpenScheduleRuleValid)
            return new PendingStopPlan(EPendingStopDecision.Cancel, null, SettingsChangedReason);

        var plan = open.Reason == EAutoStopReason.Idle ? PlanOpenIdle(input, open) : DueOrWait(input, open);

        // The earlier stop wins: a scheduled stop whose window opened before the open warning's stop replaces it.
        return plan.Decision == EPendingStopDecision.None
               && input.ScheduledStop is { } earlier
               && earlier.StopsAt < open.StopsAt
            ? new PendingStopPlan(EPendingStopDecision.Replace, earlier, EarlierStopReason)
            : plan;
    }

    private static PendingStopPlan PlanOpenIdle(PendingStopPlanInput input, PendingStopSnapshot open)
    {
        var cancelled = new PendingStopPlan(EPendingStopDecision.Cancel, null, SettingsChangedReason);
        if (!input.IdleArmed || input.Idle is not { } idle)
            return cancelled;

        switch (idle.DisplayState)
        {
            case EAutomationIdleState.PlayersOnline:
                return new PendingStopPlan(EPendingStopDecision.Cancel, null, ActivityReason);
            case EAutomationIdleState.Unknown:
                // Held: never executed and never withdrawn on a guess; a known reading decides.
                return new PendingStopPlan(EPendingStopDecision.None, null, null);
            case EAutomationIdleState.Idle:
                // A raised idle limit means the server has not been idle long enough any more.
                return idle.State.IdleSince is { } since && since + IdleLimit(input) > input.Now
                    ? cancelled
                    : DueOrWait(input, open);
            default:
                return cancelled;
        }
    }

    private static Duration IdleLimit(PendingStopPlanInput input)
        => Duration.FromMinutes(input.IdleMinutes);

    private static PendingStopPlan DueOrWait(PendingStopPlanInput input, PendingStopSnapshot open)
        => open.StopsAt <= input.Now
            ? new PendingStopPlan(
                EPendingStopDecision.Execute,
                new PendingStopCandidate(
                    open.Reason,
                    open.Limit,
                    open.StopsAt,
                    open.ScheduleId,
                    open.ScheduleOccurrenceAt),
                null)
            : new PendingStopPlan(EPendingStopDecision.None, null, null);

    private static PendingStopPlan PlanNew(PendingStopPlanInput input)
    {
        if (!input.DeploymentRunning || input.StopAlreadyQueued)
            return new PendingStopPlan(EPendingStopDecision.None, null, null);

        var immediate = new List<PendingStopCandidate>();
        var warnings = new List<PendingStopCandidate>();
        if (input.MaxRuntimeEnabled && MaxRuntimeLimit(input) is { } limit)
        {
            if (input.Now >= limit + input.WarningLead)
            {
                immediate.Add(new PendingStopCandidate(
                    EAutoStopReason.MaxRuntime,
                    input.MaxRuntimeHours,
                    input.Now,
                    null,
                    null));
            }
            else if (input.Now >= limit)
            {
                warnings.Add(new PendingStopCandidate(
                    EAutoStopReason.MaxRuntime,
                    input.MaxRuntimeHours,
                    input.Now + input.WarningLead,
                    null,
                    null));
            }
        }

        // A scheduled stop warns one lead before its occurrence and runs at the occurrence.
        if (input.ScheduledStop is { } scheduled)
            warnings.Add(scheduled);

        // Idle always warns first: it needs a known reading and never executes without a complete warning window.
        if (input is { IdleArmed: true, Idle: { DisplayState: EAutomationIdleState.Idle, State.IdleSince: { } idleSince } }
            && input.Now >= idleSince + IdleLimit(input))
        {
            warnings.Add(new PendingStopCandidate(
                EAutoStopReason.Idle,
                input.IdleMinutes,
                input.Now + input.WarningLead,
                null,
                null));
        }

        // The earliest candidate wins, and a stop that is already due beats one that still needs its warning.
        if (immediate.Count > 0)
        {
            return new PendingStopPlan(
                EPendingStopDecision.ExecuteWithoutWarning,
                immediate.OrderBy(q => q.StopsAt).First(),
                null);
        }

        return warnings.Count > 0
            ? new PendingStopPlan(EPendingStopDecision.Warn, warnings.OrderBy(q => q.StopsAt).First(), null)
            : new PendingStopPlan(EPendingStopDecision.None, null, null);
    }

    /// <summary>
    /// Calculates the instant the maximum runtime limit applies: the deployment start plus the limit, but never
    /// before the setting was saved.
    /// </summary>
    /// <param name="input">The facts of this evaluation pass.</param>
    /// <returns>The limit instant, or <see langword="null" /> when the deployment has no recorded start.</returns>
    private static Instant? MaxRuntimeLimit(PendingStopPlanInput input)
        => input.DeploymentStartedAt is { } started
            ? Instant.Max(started + Duration.FromHours(input.MaxRuntimeHours), input.MaxRuntimeEffectiveFrom)
            : null;
}
