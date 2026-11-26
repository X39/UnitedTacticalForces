using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// The pure transition of the idle shutdown clock. It reads neither a clock nor a database; every input is explicit
/// so the evaluator and the read model reach the same decision.
/// </summary>
/// <remarks>
/// <para>
/// An unavailable player reading holds the clock instead of resetting it. Right after an API restart the metrics
/// reading is stale until the worker reports its next sample; resetting on unknown would zero every idle clock at
/// every restart. When a known reading returns, the idle start shifts forward by the held duration, so time without
/// a reading never counts as idle time. A flapping signal therefore cannot postpone a shutdown forever, because
/// known idle time keeps accumulating across holds.
/// </para>
/// <para>
/// An unavailable reading never starts the clock, never resets it, and is never reported as players online or idle.
/// </para>
/// </remarks>
public static class IdleClock
{
    /// <summary>
    /// Computes the next clock state and the displayed idle state from the previous state and a new observation.
    /// </summary>
    /// <param name="state">The previously persisted clock state.</param>
    /// <param name="observation">The current observation.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <returns>The state to persist and the state to display.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state" /> or <paramref name="observation" /> is
    /// <see langword="null" />.</exception>
    public static IdleClockResult Advance(IdleClockState state, IdleObservation observation, Instant now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(observation);

        if (!observation.Armed || observation.DeploymentId is not { } deploymentId)
        {
            return new IdleClockResult(IdleClockState.Empty, EAutomationIdleState.NotRunning);
        }

        // A clock of another deployment never carries over.
        var current = state.IdleDeploymentId == deploymentId ? state : IdleClockState.Empty;

        if (observation.PlayerCount is not { } playerCount)
        {
            // Unavailable: hold. Without a running clock there is nothing to hold or to start.
            if (current.IdleSince is null)
            {
                return new IdleClockResult(IdleClockState.Empty, EAutomationIdleState.Unknown);
            }

            return new IdleClockResult(
                current with { IdleHeldSince = current.IdleHeldSince ?? now },
                EAutomationIdleState.Unknown);
        }

        if (playerCount > 0 || observation.HasActiveJob)
        {
            return new IdleClockResult(IdleClockState.Empty, EAutomationIdleState.PlayersOnline);
        }

        if (current.IdleSince is not { } idleSince)
        {
            return new IdleClockResult(new IdleClockState(deploymentId, now, null), EAutomationIdleState.Idle);
        }

        if (current.IdleHeldSince is { } heldSince)
        {
            var shifted = idleSince + (now - heldSince);
            return new IdleClockResult(new IdleClockState(deploymentId, shifted, null), EAutomationIdleState.Idle);
        }

        return new IdleClockResult(current, EAutomationIdleState.Idle);
    }
}
