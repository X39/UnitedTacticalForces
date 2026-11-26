using Api.Tests.GameServerHosting;
using System.Security.Claims;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers the automation read model of the server detail: saved settings, limits, the manage flag, and the live
/// status the control plane calculates for the browser. The tests run against in-memory SQLite.
/// </summary>
public sealed class ServerAutomationProjectionTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 20, 18, 0);

    private static ClaimsPrincipal Viewer(HostingTestSeed seed, params string[] permissions)
        => HostingTestData.ServerPrincipal(seed.User, seed.Server, permissions);

    /// <summary>A saved settings row is returned with its values and the limits come from the options.</summary>
    /// <returns>A task completing after the detail was verified.</returns>
    [Fact]
    public async Task Detail_WithSettingsRow_ReturnsSavedSettingsAndLimits()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.IdleShutdownEnabled = true;
            q.IdleShutdownMinutes = 45;
            q.MaxRuntimeEnabled = false;
            q.MaxRuntimeHours = 6;
            q.TimeZone = "Europe/Berlin";
            q.Version = 3;
        });

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, Viewer(seed, Claims.ResourceBased.Server.View));

        var automation = Assert.IsType<X39.UnitedTacticalForces.Api.DTO.ServerAutomationDto>(dto?.Automation);
        Assert.True(automation.Settings.IdleShutdownEnabled);
        Assert.Equal(45, automation.Settings.IdleShutdownMinutes);
        Assert.False(automation.Settings.MaxRuntimeEnabled);
        Assert.Equal(6, automation.Settings.MaxRuntimeHours);
        Assert.Equal("Europe/Berlin", automation.Settings.TimeZone);
        Assert.Equal(3, automation.Settings.Version);
        Assert.Equal(5, automation.Limits.IdleMinutesMin);
        Assert.Equal(1440, automation.Limits.IdleMinutesMax);
        Assert.Equal(1, automation.Limits.MaxRuntimeHoursMin);
        Assert.Equal(168, automation.Limits.MaxRuntimeHoursMax);
        Assert.Equal(8, automation.Limits.MaxSchedules);
        Assert.Equal(10, automation.Limits.WarningLeadMinutes);
    }

    /// <summary>Without a settings row both limits are off and the proposed defaults are returned.</summary>
    /// <returns>A task completing after the defaults were verified.</returns>
    [Fact]
    public async Task Detail_WithoutSettingsRow_ReturnsDefaults()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, Viewer(seed, Claims.ResourceBased.Server.View));

        var settings = Assert.IsType<X39.UnitedTacticalForces.Api.DTO.ServerAutomationDto>(dto?.Automation).Settings;
        Assert.False(settings.IdleShutdownEnabled);
        Assert.Equal(30, settings.IdleShutdownMinutes);
        Assert.False(settings.MaxRuntimeEnabled);
        Assert.Equal(6, settings.MaxRuntimeHours);
        Assert.Null(settings.TimeZone);
        Assert.Equal(0, settings.Version);
        Assert.Empty(settings.Schedules);
    }

    /// <summary>Only a viewer with modify permission who may use the cost owner may manage the automation.</summary>
    /// <param name="permission">The server permission the viewer holds.</param>
    /// <param name="expected">Whether the viewer may manage the automation.</param>
    /// <returns>A task completing after the flag was verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.View, false)]
    [InlineData(Claims.ResourceBased.Server.Operate, false)]
    [InlineData(Claims.ResourceBased.Server.Modify, true)]
    public async Task Detail_CanManageAutomation_FollowsModifyPermissionForTheCostOwner(
        string permission,
        bool expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, Viewer(seed, Claims.ResourceBased.Server.View, permission));

        Assert.Equal(expected, dto?.CanManageAutomation);
    }

    /// <summary>A viewer with modify permission who may not use the current cost owner cannot manage the automation.</summary>
    /// <returns>A task completing after the flag was verified.</returns>
    [Fact]
    public async Task Detail_CanManageAutomation_IsFalseWithoutTheRightToUseTheCostOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var stranger = new User { PrimaryKey = Guid.NewGuid() };

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(
                seed.Server.PrimaryKey,
                HostingTestData.ServerPrincipal(
                    stranger,
                    seed.Server,
                    Claims.ResourceBased.Server.View,
                    Claims.ResourceBased.Server.Modify));

        Assert.False(dto?.CanManageAutomation);
    }

    /// <summary>A hosting administrator may manage the automation.</summary>
    /// <returns>A task completing after the flag was verified.</returns>
    [Fact]
    public async Task Detail_CanManageAutomation_IsTrueForAnAdministrator()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var administrator = HostingTestData.Principal(
            new User { PrimaryKey = Guid.NewGuid() },
            (Claims.Administrative.Hosting, string.Empty));

        var dto = await HostingTestData.CreateProjectionService(store.DbContext)
            .GetServerAsync(seed.Server.PrimaryKey, administrator);

        Assert.True(dto?.CanManageAutomation);
    }

    /// <summary>The fleet list and the notifications do not compute the automation block.</summary>
    /// <returns>A task completing after the list paths were verified.</returns>
    [Fact]
    public async Task ListAndNotifications_DoNotCarryTheAutomationBlock()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var viewer = Viewer(seed, Claims.ResourceBased.Server.View, Claims.ResourceBased.Server.Modify);
        var service = HostingTestData.CreateProjectionService(store.DbContext);

        var listed = Assert.Single(await service.GetServersAsync(viewer));
        var detail = await service.GetServerAsync(seed.Server.PrimaryKey, viewer);
        _ = Assert.Single(await service.GetNotificationsAsync(viewer));

        Assert.Null(listed.Automation);
        Assert.Null(listed.CanManageAutomation);
        Assert.NotNull(detail?.Automation);
        Assert.NotNull(detail.CanManageAutomation);
    }

    /// <summary>A game that cannot report players has no idle shutdown; an unknown game has no automation block.</summary>
    /// <returns>A task completing after both cases were verified.</returns>
    [Fact]
    public async Task Projection_GameCapabilityAndUnknownGame_AreReflected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var withoutPlayers = await ServerAutomationTestData
            .CreateProjector(store.DbContext, reportsPlayerCount: false)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);
        var withPlayers = await ServerAutomationTestData
            .CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);
        seed.Server.GameIdentifier = "unknown-game";
        var unknown = await ServerAutomationTestData
            .CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        Assert.False(withoutPlayers?.IdleShutdown.Available);
        Assert.True(withPlayers?.IdleShutdown.Available);
        Assert.Null(unknown);
    }

    /// <summary>Schedules keep their rule order and carry ISO weekdays in ascending order and the time as HH:mm.</summary>
    /// <returns>A task completing after the rules were verified.</returns>
    [Fact]
    public async Task Projection_Schedules_AreMappedInRuleOrder()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        var first = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [7, 1, 5], 23 * 60 + 5, Now);
        var second = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [2], 9 * 60, Now);

        var automation = await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        var schedules = automation!.Settings.Schedules;
        Assert.Equal([first.PrimaryKey, second.PrimaryKey], schedules.Select(q => q.Id));
        Assert.Equal(EServerScheduleAction.Stop, schedules[0].Action);
        Assert.Equal([1, 5, 7], schedules[0].Days);
        Assert.Equal("23:05", schedules[0].Time);
        Assert.Equal(EServerScheduleAction.Start, schedules[1].Action);
        Assert.Equal([2], schedules[1].Days);
        Assert.Equal("09:00", schedules[1].Time);
    }

    /// <summary>Idle with a persisted clock start: the stop is one warning lead after the idle limit (D-AUTO-1).</summary>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Fact]
    public async Task Idle_WithZeroPlayers_StopsOneWarningLeadAfterTheLimit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 0);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        var idleSince = MetricsNow - Duration.FromMinutes(20);
        await SeedIdleClockAsync(store, seed, idleSince);

        var idle = (await ProjectAsync(store, seed, MetricsNow)).Status.Idle;

        Assert.Equal(EAutomationIdleState.Idle, idle.State);
        Assert.Equal(idleSince, idle.Since);
        // The limit is idleSince + 30 minutes = now + 10 minutes; the stop follows one 10 minute lead later.
        Assert.Equal(idleSince + Duration.FromMinutes(30) + Duration.FromMinutes(10), idle.StopsAt);
    }

    /// <summary>An idle limit that already passed still leaves the full warning lead before the stop.</summary>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Fact]
    public async Task Idle_LimitAlreadyPassed_StopsOneWarningLeadFromNow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 0);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        await SeedIdleClockAsync(store, seed, MetricsNow - Duration.FromHours(2));

        var idle = (await ProjectAsync(store, seed, MetricsNow)).Status.Idle;

        Assert.Equal(EAutomationIdleState.Idle, idle.State);
        Assert.Equal(MetricsNow + Duration.FromMinutes(10), idle.StopsAt);
    }

    /// <summary>Connected players keep the server out of the idle clock.</summary>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Fact]
    public async Task Idle_WithPlayersOnline_ReportsPlayersOnlineWithoutInstants()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 4);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        await SeedIdleClockAsync(store, seed, MetricsNow - Duration.FromMinutes(20));

        var idle = (await ProjectAsync(store, seed, MetricsNow)).Status.Idle;

        Assert.Equal(EAutomationIdleState.PlayersOnline, idle.State);
        Assert.Null(idle.Since);
        Assert.Null(idle.StopsAt);
    }

    /// <summary>An active lifecycle job of the deployment counts as activity even without players.</summary>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Fact]
    public async Task Idle_WithQueuedJobAndZeroPlayers_ReportsPlayersOnline()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 0);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        await ServerAutomationTestData.AddJobAsync(store.DbContext, seed.Deployment!, ELifecycleJobStatus.Queued);

        var idle = (await ProjectAsync(store, seed, MetricsNow)).Status.Idle;

        Assert.Equal(EAutomationIdleState.PlayersOnline, idle.State);
    }

    /// <summary>A stale reading holds the clock: the start is kept and no stop instant is promised.</summary>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Fact]
    public async Task Idle_WithStaleReading_IsUnknownAndKeepsSince()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 0, sampleAgeSeconds: 600);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        var idleSince = MetricsNow - Duration.FromMinutes(20);
        await SeedIdleClockAsync(store, seed, idleSince);

        var idle = (await ProjectAsync(store, seed, MetricsNow)).Status.Idle;

        Assert.Equal(EAutomationIdleState.Unknown, idle.State);
        Assert.Equal(idleSince, idle.Since);
        Assert.Null(idle.StopsAt);
    }

    /// <summary>Idle shutdown that is off, unsupported or without a running deployment is not running.</summary>
    /// <param name="enabled">Whether idle shutdown is switched on.</param>
    /// <param name="running">Whether the server has a running deployment.</param>
    /// <param name="reportsPlayerCount">Whether the game reports its player count.</param>
    /// <returns>A task completing after the idle status was verified.</returns>
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Idle_WhenNotArmed_IsNotRunning(bool enabled, bool running, bool reportsPlayerCount)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = running
            ? await SeedRunningAsync(store, players: 0)
            : await HostingTestData.SeedServerAsync(store.DbContext);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = enabled);

        var automation = await ServerAutomationTestData
            .CreateProjector(store.DbContext, reportsPlayerCount: reportsPlayerCount)
            .ProjectAsync(seed.Server, MetricsNow, CancellationToken.None);

        var idle = automation!.Status.Idle;
        Assert.Equal(EAutomationIdleState.NotRunning, idle.State);
        Assert.Null(idle.Since);
        Assert.Null(idle.StopsAt);
    }

    /// <summary>The maximum runtime stop is one warning lead after the session limit.</summary>
    /// <returns>A task completing after the stop instant was verified.</returns>
    [Fact]
    public async Task MaxRuntime_Enabled_StopsOneWarningLeadAfterTheLimit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 3);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 6;
        });

        var status = (await ProjectAsync(store, seed, MetricsNow)).Status;

        Assert.Equal(
            seed.Deployment!.TimeStampStarted!.Value + Duration.FromHours(6) + Duration.FromMinutes(10),
            status.MaxRuntimeStopsAt);
    }

    /// <summary>A passed maximum runtime limit still leaves the full warning lead before the stop.</summary>
    /// <returns>A task completing after the stop instant was verified.</returns>
    [Fact]
    public async Task MaxRuntime_LimitAlreadyPassed_StopsOneWarningLeadFromNow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 3);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
        });
        var later = seed.Deployment!.TimeStampStarted!.Value + Duration.FromHours(3);

        var status = (await ProjectAsync(store, seed, later)).Status;

        Assert.Equal(later + Duration.FromMinutes(10), status.MaxRuntimeStopsAt);
    }

    /// <summary>The maximum runtime stop is absent when the limit is off or the server is not running.</summary>
    /// <param name="enabled">Whether the runtime limit is switched on.</param>
    /// <param name="running">Whether the server has a running deployment.</param>
    /// <returns>A task completing after the stop instant was verified.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MaxRuntime_WhenOffOrNotRunning_HasNoStopInstant(bool enabled, bool running)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = running
            ? await SeedRunningAsync(store, players: 3)
            : await HostingTestData.SeedServerAsync(store.DbContext);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.MaxRuntimeEnabled = enabled);

        var status = (await ProjectAsync(store, seed, MetricsNow)).Status;

        Assert.Null(status.MaxRuntimeStopsAt);
    }

    /// <summary>A pending stop reaches the block with its reason, instant and limit, and fixes the matching stop instant.</summary>
    /// <returns>A task completing after the pending stop was verified.</returns>
    [Fact]
    public async Task PendingStop_Idle_IsExposedAndFixesTheIdleStopInstant()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 0);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q => q.IdleShutdownEnabled = true);
        await SeedIdleClockAsync(store, seed, MetricsNow - Duration.FromMinutes(40));
        var stopsAt = MetricsNow + Duration.FromMinutes(4);
        await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext, seed.Deployment!, EAutoStopReason.Idle, stopsAt, limit: 30);

        var status = (await ProjectAsync(store, seed, MetricsNow)).Status;

        var pending = Assert.IsType<X39.UnitedTacticalForces.Api.DTO.ServerPendingStopDto>(status.PendingStop);
        Assert.Equal(EAutoStopReason.Idle, pending.Reason);
        Assert.Equal(stopsAt, pending.StopsAt);
        Assert.Equal(30, pending.Limit);
        Assert.Equal(stopsAt, status.Idle.StopsAt);
    }

    /// <summary>A pending maximum runtime stop fixes the maximum runtime stop instant.</summary>
    /// <returns>A task completing after the stop instant was verified.</returns>
    [Fact]
    public async Task PendingStop_MaxRuntime_FixesTheMaxRuntimeStopInstant()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 3);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
        });
        var stopsAt = MetricsNow + Duration.FromMinutes(7);
        await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext, seed.Deployment!, EAutoStopReason.MaxRuntime, stopsAt, limit: 1);

        var status = (await ProjectAsync(store, seed, MetricsNow)).Status;

        Assert.Equal(EAutoStopReason.MaxRuntime, status.PendingStop?.Reason);
        Assert.Equal(stopsAt, status.MaxRuntimeStopsAt);
    }

    /// <summary>A cancelled warning is no longer pending.</summary>
    /// <returns>A task completing after the pending stop was verified.</returns>
    [Fact]
    public async Task PendingStop_Cancelled_IsIgnored()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await SeedRunningAsync(store, players: 3);
        var pending = await ServerAutomationTestData.AddPendingStopAsync(
            store.DbContext, seed.Deployment!, EAutoStopReason.Idle, MetricsNow + Duration.FromMinutes(4), limit: 30);
        pending.Status = EAutoStopStatus.Cancelled;
        await store.DbContext.SaveChangesAsync();

        var status = (await ProjectAsync(store, seed, MetricsNow)).Status;

        Assert.Null(status.PendingStop);
    }

    /// <summary>The next scheduled action is the earliest occurrence across rules in the saved zone.</summary>
    /// <returns>A task completing after the next action was verified.</returns>
    [Fact]
    public async Task NextScheduled_IsTheEarliestOccurrenceInTheSavedZone()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext, seed.Server, q => q.TimeZone = "Europe/Berlin");
        var effective = Now - Duration.FromDays(1);
        // Friday 2026-11-20 18:00 UTC is 19:00 in Berlin: the Friday 20:00 rule runs at 19:00 UTC, the daily 22:00
        // rule two hours later.
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [1, 2, 3, 4, 5, 6, 7], 22 * 60, effective);
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [5], 20 * 60, effective);

        var status = (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None))!.Status;

        Assert.Equal(EServerScheduleAction.Stop, status.NextScheduled?.Action);
        Assert.Equal(Instant.FromUtc(2026, 11, 20, 19, 0), status.NextScheduled?.At);
    }

    /// <summary>Without a schedule time zone no occurrence is calculated.</summary>
    /// <returns>A task completing after the schedule was verified.</returns>
    [Fact]
    public async Task NextScheduled_WithoutTimeZone_HasNoNextRun()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [5], 20 * 60, Now - Duration.FromDays(1));

        var automation = await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        Assert.Null(automation!.Status.NextScheduled);
        Assert.Null(Assert.Single(automation.Settings.Schedules).NextRunAt);
    }

    /// <summary>A start rule is skipped while consent is needed and is the next action once consent was given.</summary>
    /// <param name="consentGiven">Whether the consent of the current cost owner is recorded.</param>
    /// <returns>A task completing after the next action was verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextScheduled_StartRule_RunsOnlyWithConsent(bool consentGiven)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.TimeZone = "Europe/Berlin";
            q.StartConsentCostOwnerFk = consentGiven ? seed.CostOwner.PrimaryKey : null;
        });
        var effective = Now - Duration.FromDays(1);
        // Friday 19:30 Berlin (18:30 UTC) is before the Friday 20:00 stop (19:00 UTC).
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [5], 19 * 60 + 30, effective);
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [5], 20 * 60, effective);

        var automation = (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None))!;

        var startRule = automation.Settings.Schedules.Single(q => q.Action == EServerScheduleAction.Start);
        if (consentGiven)
        {
            Assert.Equal(Instant.FromUtc(2026, 11, 20, 18, 30), startRule.NextRunAt);
            Assert.Equal(EServerScheduleAction.Start, automation.Status.NextScheduled?.Action);
        }
        else
        {
            Assert.Null(startRule.NextRunAt);
            Assert.Equal(EServerScheduleAction.Stop, automation.Status.NextScheduled?.Action);
        }
    }

    /// <summary>Consent is not required without a start rule.</summary>
    /// <returns>A task completing after the consent state was verified.</returns>
    [Fact]
    public async Task StartConsent_WithoutStartRule_IsNotRequired()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [5], 20 * 60, Now);

        var automation = await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        Assert.Equal(EStartConsentState.NotRequired, automation!.StartConsent.State);
    }

    /// <summary>Consent counts only for the cost owner that currently pays for the server.</summary>
    /// <param name="mode">None for no consent, Current for the paying owner, Other for a previous owner.</param>
    /// <param name="expected">The expected consent state.</param>
    /// <returns>A task completing after the consent state was verified.</returns>
    [Theory]
    [InlineData("None", EStartConsentState.Needed)]
    [InlineData("Current", EStartConsentState.Given)]
    [InlineData("Other", EStartConsentState.Needed)]
    public async Task StartConsent_WithStartRule_DependsOnTheCurrentCostOwner(string mode, EStartConsentState expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.ExternalOrganization,
            DisplayName      = "Previous owner",
            Currency         = "EUR",
            TimeStampCreated = Now,
            TimeStampUpdated = Now,
        };
        store.DbContext.CostOwners.Add(other);
        await store.DbContext.SaveChangesAsync();
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server, q =>
        {
            q.StartConsentCostOwnerFk = mode switch
            {
                "Current" => seed.CostOwner.PrimaryKey,
                "Other" => other.PrimaryKey,
                _ => null,
            };
        });
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [5], 9 * 60, Now);

        var automation = await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        Assert.Equal(expected, automation!.StartConsent.State);
    }

    /// <summary>The last run is the latest occurrence of the rule and keeps its stored outcome.</summary>
    /// <returns>A task completing after the last run was verified.</returns>
    [Fact]
    public async Task LastRun_IsTheLatestOccurrenceWithItsStoredOutcome()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [5], 20 * 60, Now - Duration.FromDays(10));
        var latest = Now - Duration.FromDays(1);
        await ServerAutomationTestData.AddRunAsync(
            store.DbContext, rule, latest, EServerScheduleRunOutcome.SkippedOff);
        await ServerAutomationTestData.AddRunAsync(
            store.DbContext, rule, Now - Duration.FromDays(8), EServerScheduleRunOutcome.Stopped);

        var lastRun = (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None))!.Settings.Schedules.Single().LastRun;

        Assert.Equal(latest, lastRun?.At);
        Assert.Equal(EServerScheduleRunOutcome.SkippedOff, lastRun?.Outcome);
    }

    /// <summary>A start that waits for a stop projects as waiting at its occurrence, not as failed.</summary>
    /// <returns>A task completing after the last run was verified.</returns>
    [Fact]
    public async Task LastRun_ShowsAWaitingStartAsWaitingForStop()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [5], 9 * 60, Now - Duration.FromDays(10));
        var occurrence = Now - Duration.FromHours(1);
        await ServerAutomationTestData.AddRunAsync(
            store.DbContext, rule, occurrence, EServerScheduleRunOutcome.WaitingForStop);

        var lastRun = (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None))!.Settings.Schedules.Single().LastRun;

        Assert.Equal(occurrence, lastRun?.At);
        Assert.Equal(EServerScheduleRunOutcome.WaitingForStop, lastRun?.Outcome);
    }

    /// <summary>A run reads Failed when the job it queued ended failed, and keeps the stored outcome otherwise.</summary>
    /// <param name="jobStatus">The status of the job queued for the occurrence.</param>
    /// <param name="expected">The expected last run outcome.</param>
    /// <returns>A task completing after the last run was verified.</returns>
    [Theory]
    [InlineData(ELifecycleJobStatus.Failed, EServerScheduleRunOutcome.Failed)]
    [InlineData(ELifecycleJobStatus.Succeeded, EServerScheduleRunOutcome.Started)]
    public async Task LastRun_DerivesFailedFromTheQueuedJob(
        ELifecycleJobStatus jobStatus,
        EServerScheduleRunOutcome expected)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, seed.Server);
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [5], 9 * 60, Now - Duration.FromDays(10));
        var job = await ServerAutomationTestData.AddJobAsync(
            store.DbContext, seed.Deployment!, jobStatus, ELifecycleJobKind.Start);
        await ServerAutomationTestData.AddRunAsync(
            store.DbContext, rule, Now - Duration.FromDays(1), EServerScheduleRunOutcome.Started, job.PrimaryKey);

        var lastRun = (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None))!.Settings.Schedules.Single().LastRun;

        Assert.Equal(expected, lastRun?.Outcome);
    }

    /// <summary>The block carries no cost owner identity: consent is a state only.</summary>
    /// <returns>A task completing after the serialized block was verified.</returns>
    [Fact]
    public async Task Block_DoesNotCarryCostOwnerIdentity()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext, seed.Server, q => q.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey);
        await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [5], 9 * 60, Now);

        var automation = await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, Now, CancellationToken.None);

        var json = System.Text.Json.JsonSerializer.Serialize(automation);
        Assert.DoesNotContain(seed.CostOwner.PrimaryKey.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seed.CostOwner.DisplayName, json, StringComparison.Ordinal);
    }

    private static readonly Instant MetricsNow = Instant.FromUnixTimeSeconds(1_700_000_100);

    /// <summary>
    /// Seeds a server with a running deployment, its primary worker and a fresh (or aged) player reading.
    /// </summary>
    /// <param name="store">The test database.</param>
    /// <param name="players">The reported player count.</param>
    /// <param name="sampleAgeSeconds">The age of the reading relative to <see cref="MetricsNow" />.</param>
    /// <returns>The seeded server with its running deployment.</returns>
    private static async Task<HostingTestSeed> SeedRunningAsync(
        HostingTestStore store,
        int players,
        int sampleAgeSeconds = 10)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var deployment = seed.Deployment!;
        var worker = await ServerMetricsTestData.AddPrimaryWorkerAsync(
            store.DbContext,
            deployment,
            MetricsNow - Duration.FromSeconds(5),
            MetricsNow - Duration.FromSeconds(70));
        var receivedAt = MetricsNow - Duration.FromSeconds(sampleAgeSeconds);
        await ServerMetricsTestData.AddSnapshotAsync(
            store.DbContext,
            deployment,
            worker.PrimaryKey,
            receivedAt,
            ServerMetricsTestData.Sample(deployment.PrimaryKey, receivedAt.ToDateTimeOffset(), 10d, 1, 8, players, 32));
        return seed;
    }

    private static async Task SeedIdleClockAsync(HostingTestStore store, HostingTestSeed seed, Instant idleSince)
    {
        store.DbContext.ServerAutomationStates.Add(new ServerAutomationState
        {
            PrimaryKey         = Guid.NewGuid(),
            GameServerFk       = seed.Server.PrimaryKey,
            IdleDeploymentFk   = seed.Deployment!.PrimaryKey,
            IdleSince          = idleSince,
            TimeStampUpdated   = idleSince,
        });
        await store.DbContext.SaveChangesAsync();
    }

    private static async Task<X39.UnitedTacticalForces.Api.DTO.ServerAutomationDto> ProjectAsync(
        HostingTestStore store,
        HostingTestSeed seed,
        Instant now)
        => (await ServerAutomationTestData.CreateProjector(store.DbContext)
            .ProjectAsync(seed.Server, now, CancellationToken.None))!;
}
