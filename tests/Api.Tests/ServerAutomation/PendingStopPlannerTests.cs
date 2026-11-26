using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers the pure decision matrix of <see cref="PendingStopPlanner" />: warn, withdraw, replace, execute and hold,
/// with explicit instants and no database.
/// </summary>
public sealed class PendingStopPlannerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 20, 18, 0);
    private static readonly Duration Lead = Duration.FromMinutes(10);
    private static readonly Guid DeploymentId = Guid.NewGuid();

    private static IdleClockResult Clock(EAutomationIdleState state, Instant? since)
        => new(
            since is null ? IdleClockState.Empty : new IdleClockState(DeploymentId, since, null),
            state);

    private static PendingStopPlanInput Input(
        EAutomationIdleState idle = EAutomationIdleState.Idle,
        Instant? idleSince = null,
        PendingStopSnapshot? open = null,
        bool armed = true)
        => new(
            Now,
            Lead,
            true,
            Now - Duration.FromHours(1),
            false,
            armed,
            30,
            armed ? Clock(idle, idleSince ?? Now - Duration.FromMinutes(30)) : null,
            false,
            6,
            Instant.MinValue,
            open,
            null);

    private static PendingStopSnapshot OpenIdle(Instant stopsAt)
        => new(EAutoStopReason.Idle, 30, stopsAt, null, null);

    /// <summary>Idle for the full limit warns with a stop one lead later.</summary>
    [Fact]
    public void Idle_AtTheLimit_Warns()
    {
        var plan = PendingStopPlanner.Plan(Input());

        Assert.Equal(EPendingStopDecision.Warn, plan.Decision);
        Assert.Equal(EAutoStopReason.Idle, plan.Candidate!.Reason);
        Assert.Equal(30, plan.Candidate.Limit);
        Assert.Equal(Now + Lead, plan.Candidate.StopsAt);
    }

    /// <summary>Idle for less than the limit does nothing.</summary>
    [Fact]
    public void Idle_BeforeTheLimit_DoesNothing()
    {
        var plan = PendingStopPlanner.Plan(Input(idleSince: Now - Duration.FromMinutes(29)));

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>Connected players never start a warning.</summary>
    [Fact]
    public void PlayersOnline_DoesNothing()
    {
        var plan = PendingStopPlanner.Plan(Input(EAutomationIdleState.PlayersOnline));

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>An unavailable reading never starts a warning.</summary>
    [Fact]
    public void Unknown_NeverWarns()
    {
        var plan = PendingStopPlanner.Plan(Input(EAutomationIdleState.Unknown));

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>Players joining withdraw an open idle warning with the activity reason.</summary>
    [Fact]
    public void OpenIdle_WithPlayersOnline_CancelsForActivity()
    {
        var plan = PendingStopPlanner.Plan(Input(
            EAutomationIdleState.PlayersOnline,
            open: OpenIdle(Now + Duration.FromMinutes(5))));

        Assert.Equal(EPendingStopDecision.Cancel, plan.Decision);
        Assert.Equal("activity", plan.CancelReason);
    }

    /// <summary>An unavailable reading holds an open idle warning, even when its stop is due.</summary>
    /// <param name="minutesToStop">The minutes until the stop; negative when it is overdue.</param>
    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    public void OpenIdle_WithUnknownReading_IsHeld(int minutesToStop)
    {
        var plan = PendingStopPlanner.Plan(Input(
            EAutomationIdleState.Unknown,
            open: OpenIdle(Now + Duration.FromMinutes(minutesToStop))));

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
        Assert.Null(plan.CancelReason);
    }

    /// <summary>A due idle warning with a known idle reading executes with the stored limit.</summary>
    [Fact]
    public void OpenIdle_WhenDueAndIdle_Executes()
    {
        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now - Duration.FromSeconds(1))));

        Assert.Equal(EPendingStopDecision.Execute, plan.Decision);
        Assert.Equal(EAutoStopReason.Idle, plan.Candidate!.Reason);
        Assert.Equal(30, plan.Candidate.Limit);
    }

    /// <summary>An idle warning that is not due yet keeps waiting.</summary>
    [Fact]
    public void OpenIdle_WhenNotDue_Waits()
    {
        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now + Duration.FromMinutes(1))));

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>Switching idle shutdown off withdraws the warning as a settings change.</summary>
    [Fact]
    public void OpenIdle_WhenIdleShutdownDisarmed_CancelsForSettingsChange()
    {
        var plan = PendingStopPlanner.Plan(Input(armed: false, open: OpenIdle(Now + Duration.FromMinutes(5))));

        Assert.Equal(EPendingStopDecision.Cancel, plan.Decision);
        Assert.Equal("settings-changed", plan.CancelReason);
    }

    /// <summary>Raising the idle limit above the elapsed idle time withdraws the warning.</summary>
    [Fact]
    public void OpenIdle_WhenTheLimitWasRaised_CancelsForSettingsChange()
    {
        var plan = PendingStopPlanner.Plan(Input(
            idleSince: Now - Duration.FromMinutes(10),
            open: OpenIdle(Now + Duration.FromMinutes(5))));

        Assert.Equal(EPendingStopDecision.Cancel, plan.Decision);
        Assert.Equal("settings-changed", plan.CancelReason);
    }

    /// <summary>A deployment that is not running supersedes an open warning.</summary>
    [Fact]
    public void Open_WhenTheDeploymentIsNotRunning_Supersedes()
    {
        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now - Duration.FromMinutes(1))) with
        {
            DeploymentRunning = false,
        });

        Assert.Equal(EPendingStopDecision.Supersede, plan.Decision);
        Assert.Equal("deployment-not-running", plan.CancelReason);
    }

    /// <summary>A queued stop job supersedes an open warning.</summary>
    [Fact]
    public void Open_WhenAStopIsAlreadyQueued_Supersedes()
    {
        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now - Duration.FromMinutes(1))) with
        {
            StopAlreadyQueued = true,
        });

        Assert.Equal(EPendingStopDecision.Supersede, plan.Decision);
        Assert.Equal("stop-already-queued", plan.CancelReason);
    }

    /// <summary>Nothing is warned about while a stop is queued or the deployment is not running.</summary>
    /// <param name="running">Whether the deployment runs.</param>
    /// <param name="stopQueued">Whether a stop is queued.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void New_WhenNotRunningOrStopQueued_DoesNothing(bool running, bool stopQueued)
    {
        var plan = PendingStopPlanner.Plan(Input() with { DeploymentRunning = running, StopAlreadyQueued = stopQueued });

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>A maximum runtime stop that is already past its warning window executes without a warning.</summary>
    [Fact]
    public void MaxRuntime_PastTheWarningWindow_ExecutesWithoutWarning()
    {
        var plan = PendingStopPlanner.Plan(Input(EAutomationIdleState.PlayersOnline) with
        {
            MaxRuntimeEnabled = true,
            MaxRuntimeHours = 1,
            DeploymentStartedAt = Now - Duration.FromMinutes(70),
        });

        Assert.Equal(EPendingStopDecision.ExecuteWithoutWarning, plan.Decision);
        Assert.Equal(EAutoStopReason.MaxRuntime, plan.Candidate!.Reason);
        Assert.Equal(1, plan.Candidate.Limit);
    }

    /// <summary>The limit never lies before the instant the setting was saved.</summary>
    [Fact]
    public void MaxRuntime_SavedAfterTheLimit_WarnsInsteadOfExecuting()
    {
        var plan = PendingStopPlanner.Plan(Input(EAutomationIdleState.PlayersOnline) with
        {
            MaxRuntimeEnabled = true,
            MaxRuntimeHours = 1,
            DeploymentStartedAt = Now - Duration.FromHours(3),
            MaxRuntimeEffectiveFrom = Now - Duration.FromSeconds(15),
        });

        Assert.Equal(EPendingStopDecision.Warn, plan.Decision);
        Assert.Equal(Now + Lead, plan.Candidate!.StopsAt);
    }

    /// <summary>An immediate maximum runtime stop wins over an idle warning.</summary>
    [Fact]
    public void ExecuteWithoutWarning_WinsOverWarn()
    {
        var plan = PendingStopPlanner.Plan(Input() with
        {
            MaxRuntimeEnabled = true,
            MaxRuntimeHours = 1,
            DeploymentStartedAt = Now - Duration.FromMinutes(70),
        });

        Assert.Equal(EPendingStopDecision.ExecuteWithoutWarning, plan.Decision);
    }

    private static PendingStopCandidate ScheduledAt(Instant occurrence)
        => new(EAutoStopReason.Schedule, null, occurrence, Guid.NewGuid(), occurrence);

    private static PendingStopSnapshot OpenSchedule(Instant occurrence)
        => new(EAutoStopReason.Schedule, null, occurrence, Guid.NewGuid(), occurrence);

    /// <summary>A scheduled stop with an open window warns with its own occurrence as the stop instant.</summary>
    [Fact]
    public void ScheduledStop_WithOpenWindow_WarnsAtItsOccurrence()
    {
        var occurrence = Now + Lead;

        var plan = PendingStopPlanner.Plan(Input(EAutomationIdleState.PlayersOnline) with
        {
            ScheduledStop = ScheduledAt(occurrence),
        });

        Assert.Equal(EPendingStopDecision.Warn, plan.Decision);
        Assert.Equal(EAutoStopReason.Schedule, plan.Candidate!.Reason);
        Assert.Equal(occurrence, plan.Candidate.StopsAt);
    }

    /// <summary>An idle warning that would stop earlier than the scheduled stop wins the new warning.</summary>
    [Fact]
    public void ScheduledStop_LaterThanTheIdleCandidate_KeepsTheIdleWarning()
    {
        var plan = PendingStopPlanner.Plan(Input() with { ScheduledStop = ScheduledAt(Now + Lead + Lead) });

        Assert.Equal(EPendingStopDecision.Warn, plan.Decision);
        Assert.Equal(EAutoStopReason.Idle, plan.Candidate!.Reason);
    }

    /// <summary>An open idle warning is replaced by a scheduled stop that comes earlier.</summary>
    [Fact]
    public void OpenIdleWarning_WithEarlierScheduledStop_IsReplaced()
    {
        var earlier = Now + Duration.FromMinutes(5);

        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now + Duration.FromMinutes(8))) with
        {
            ScheduledStop = ScheduledAt(earlier),
        });

        Assert.Equal(EPendingStopDecision.Replace, plan.Decision);
        Assert.Equal(earlier, plan.Candidate!.StopsAt);
        Assert.Equal(EAutoStopReason.Schedule, plan.Candidate.Reason);
        Assert.Equal(PendingStopPlanner.EarlierStopReason, plan.CancelReason);
    }

    /// <summary>An open idle warning that stops earlier keeps its place.</summary>
    [Fact]
    public void OpenIdleWarning_WithLaterScheduledStop_IsKept()
    {
        var plan = PendingStopPlanner.Plan(Input(open: OpenIdle(Now + Duration.FromMinutes(5))) with
        {
            ScheduledStop = ScheduledAt(Now + Duration.FromMinutes(8)),
        });

        Assert.Equal(EPendingStopDecision.None, plan.Decision);
    }

    /// <summary>An open schedule stop whose rule is gone or changed is cancelled as a settings change.</summary>
    [Fact]
    public void OpenScheduleStop_WithInvalidRule_IsCancelled()
    {
        var plan = PendingStopPlanner.Plan(Input(
                EAutomationIdleState.PlayersOnline,
                open: OpenSchedule(Now + Duration.FromMinutes(5)))
            with
            {
                OpenScheduleRuleValid = false,
            });

        Assert.Equal(EPendingStopDecision.Cancel, plan.Decision);
        Assert.Equal(PendingStopPlanner.SettingsChangedReason, plan.CancelReason);
    }

    /// <summary>An open schedule stop with a valid rule waits and then executes at its occurrence.</summary>
    [Fact]
    public void OpenScheduleStop_WithValidRule_WaitsThenExecutes()
    {
        var waiting = PendingStopPlanner.Plan(Input(
            EAutomationIdleState.PlayersOnline,
            open: OpenSchedule(Now + Duration.FromMinutes(5))));
        var due = PendingStopPlanner.Plan(Input(EAutomationIdleState.PlayersOnline, open: OpenSchedule(Now)));

        Assert.Equal(EPendingStopDecision.None, waiting.Decision);
        Assert.Equal(EPendingStopDecision.Execute, due.Decision);
        Assert.Equal(EAutoStopReason.Schedule, due.Candidate!.Reason);
    }

    /// <summary>An open schedule stop is replaced by another rule's earlier stop.</summary>
    [Fact]
    public void OpenScheduleStop_WithEarlierScheduledStop_IsReplaced()
    {
        var plan = PendingStopPlanner.Plan(Input(
                EAutomationIdleState.PlayersOnline,
                open: OpenSchedule(Now + Duration.FromMinutes(8)))
            with
            {
                ScheduledStop = ScheduledAt(Now + Duration.FromMinutes(3)),
            });

        Assert.Equal(EPendingStopDecision.Replace, plan.Decision);
    }
}
