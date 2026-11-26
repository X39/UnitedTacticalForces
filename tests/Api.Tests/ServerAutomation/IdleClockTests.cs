using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers the pure idle clock transition: arming, resetting, and holding on unavailable readings.
/// </summary>
public sealed class IdleClockTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 20, 18, 0);
    private static readonly Guid DeploymentA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeploymentB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IdleObservation Armed(int? playerCount, Guid? deployment = null, bool activeJob = false)
        => new(true, deployment ?? DeploymentA, playerCount, activeJob);

    /// <summary>A clock that is not armed is cleared whatever it held before.</summary>
    [Fact]
    public void Advance_NotArmed_ClearsStateAndReportsNotRunning()
    {
        var previous = new IdleClockState(DeploymentA, Now - Duration.FromMinutes(5), Now - Duration.FromMinutes(1));

        var result = IdleClock.Advance(previous, new IdleObservation(false, DeploymentA, 0, false), Now);

        Assert.Equal(IdleClockState.Empty, result.State);
        Assert.Equal(EAutomationIdleState.NotRunning, result.DisplayState);
    }

    /// <summary>An armed observation without a deployment cannot run the clock.</summary>
    [Fact]
    public void Advance_ArmedWithoutDeployment_ClearsStateAndReportsNotRunning()
    {
        var previous = new IdleClockState(DeploymentA, Now - Duration.FromMinutes(5), null);

        var result = IdleClock.Advance(previous, new IdleObservation(true, null, 0, false), Now);

        Assert.Equal(IdleClockState.Empty, result.State);
        Assert.Equal(EAutomationIdleState.NotRunning, result.DisplayState);
    }

    /// <summary>The first known zero starts the clock at the evaluation instant.</summary>
    [Fact]
    public void Advance_KnownZeroOnEmptyState_StartsClockNow()
    {
        var result = IdleClock.Advance(IdleClockState.Empty, Armed(0), Now);

        Assert.Equal(new IdleClockState(DeploymentA, Now, null), result.State);
        Assert.Equal(EAutomationIdleState.Idle, result.DisplayState);
    }

    /// <summary>A known zero keeps an existing idle start of the same deployment.</summary>
    [Fact]
    public void Advance_KnownZeroWithExistingIdleSince_KeepsIdleSince()
    {
        var since = Now - Duration.FromMinutes(12);

        var result = IdleClock.Advance(new IdleClockState(DeploymentA, since, null), Armed(0), Now);

        Assert.Equal(since, result.State.IdleSince);
        Assert.Equal(DeploymentA, result.State.IdleDeploymentId);
        Assert.Equal(EAutomationIdleState.Idle, result.DisplayState);
    }

    /// <summary>The clock of another deployment never carries over.</summary>
    [Fact]
    public void Advance_KnownZeroForAnotherDeployment_RestartsClockNow()
    {
        var previous = new IdleClockState(DeploymentB, Now - Duration.FromMinutes(40), null);

        var result = IdleClock.Advance(previous, Armed(0, DeploymentA), Now);

        Assert.Equal(new IdleClockState(DeploymentA, Now, null), result.State);
        Assert.Equal(EAutomationIdleState.Idle, result.DisplayState);
    }

    /// <summary>Online players reset the clock.</summary>
    [Fact]
    public void Advance_PlayersOnline_ClearsStateAndReportsPlayersOnline()
    {
        var previous = new IdleClockState(DeploymentA, Now - Duration.FromMinutes(20), null);

        var result = IdleClock.Advance(previous, Armed(3), Now);

        Assert.Equal(IdleClockState.Empty, result.State);
        Assert.Equal(EAutomationIdleState.PlayersOnline, result.DisplayState);
    }

    /// <summary>An active lifecycle job counts as activity even when nobody is online.</summary>
    [Fact]
    public void Advance_KnownZeroWithActiveJob_ClearsStateAndReportsPlayersOnline()
    {
        var previous = new IdleClockState(DeploymentA, Now - Duration.FromMinutes(20), null);

        var result = IdleClock.Advance(previous, Armed(0, activeJob: true), Now);

        Assert.Equal(IdleClockState.Empty, result.State);
        Assert.Equal(EAutomationIdleState.PlayersOnline, result.DisplayState);
    }

    /// <summary>An unavailable reading holds the clock and starts the hold.</summary>
    [Fact]
    public void Advance_UnavailableReading_KeepsIdleSinceAndStartsHold()
    {
        var since = Now - Duration.FromMinutes(10);

        var result = IdleClock.Advance(new IdleClockState(DeploymentA, since, null), Armed(null), Now);

        Assert.Equal(since, result.State.IdleSince);
        Assert.Equal(Now, result.State.IdleHeldSince);
        Assert.Equal(EAutomationIdleState.Unknown, result.DisplayState);
    }

    /// <summary>An already running hold keeps its original start.</summary>
    [Fact]
    public void Advance_UnavailableReadingWhileHeld_KeepsHoldStart()
    {
        var since = Now - Duration.FromMinutes(10);
        var heldSince = Now - Duration.FromMinutes(2);

        var result = IdleClock.Advance(new IdleClockState(DeploymentA, since, heldSince), Armed(null), Now);

        Assert.Equal(since, result.State.IdleSince);
        Assert.Equal(heldSince, result.State.IdleHeldSince);
        Assert.Equal(EAutomationIdleState.Unknown, result.DisplayState);
    }

    /// <summary>An unavailable reading never starts a clock that is not running.</summary>
    [Fact]
    public void Advance_UnavailableReadingOnEmptyState_DoesNotStartClock()
    {
        var result = IdleClock.Advance(IdleClockState.Empty, Armed(null), Now);

        Assert.Null(result.State.IdleSince);
        Assert.Null(result.State.IdleHeldSince);
        Assert.Equal(EAutomationIdleState.Unknown, result.DisplayState);
    }

    /// <summary>A known zero after a hold shifts the idle start forward by the held time.</summary>
    [Fact]
    public void Advance_KnownZeroAfterHold_ShiftsIdleSinceByHeldDuration()
    {
        var since = Now - Duration.FromMinutes(10);
        var heldSince = Now - Duration.FromMinutes(3);

        var result = IdleClock.Advance(new IdleClockState(DeploymentA, since, heldSince), Armed(0), Now);

        Assert.Equal(since + Duration.FromMinutes(3), result.State.IdleSince);
        Assert.Null(result.State.IdleHeldSince);
        Assert.Equal(EAutomationIdleState.Idle, result.DisplayState);
    }

    /// <summary>Players returning after a hold reset the clock.</summary>
    [Fact]
    public void Advance_PlayersOnlineAfterHold_ClearsState()
    {
        var previous = new IdleClockState(DeploymentA, Now - Duration.FromMinutes(10), Now - Duration.FromMinutes(3));

        var result = IdleClock.Advance(previous, Armed(2), Now);

        Assert.Equal(IdleClockState.Empty, result.State);
        Assert.Equal(EAutomationIdleState.PlayersOnline, result.DisplayState);
    }

    /// <summary>A flapping signal never postpones idle time: held time is excluded, known idle time accumulates.</summary>
    [Fact]
    public void Advance_RepeatedHolds_AccumulateOnlyKnownIdleTime()
    {
        var start = Now;
        var state = IdleClock.Advance(IdleClockState.Empty, Armed(0), start).State;
        state = IdleClock.Advance(state, Armed(null), start + Duration.FromMinutes(2)).State;
        state = IdleClock.Advance(state, Armed(0), start + Duration.FromMinutes(5)).State;
        state = IdleClock.Advance(state, Armed(null), start + Duration.FromMinutes(6)).State;
        state = IdleClock.Advance(state, Armed(0), start + Duration.FromMinutes(8)).State;

        // 5 minutes held in total out of 8 elapsed: the clock reads 3 known idle minutes.
        Assert.Equal(start + Duration.FromMinutes(5), state.IdleSince);
        Assert.Null(state.IdleHeldSince);
    }
}
