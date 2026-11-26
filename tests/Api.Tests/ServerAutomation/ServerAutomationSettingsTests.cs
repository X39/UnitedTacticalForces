using Api.Tests.GameServerHosting;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Proves that automation settings are saved through the real <see cref="GameServerAutomationController" />:
/// authorization per claim, server-side validation with field-addressable errors, optimistic concurrency, the
/// evaluator state a first save creates, and the audit trail. The tests run against in-memory SQLite.
/// </summary>
public sealed class ServerAutomationSettingsTests
{
    private static ClaimsPrincipal Manager(HostingTestSeed seed)
        => HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Modify);

    private static ServerAutomationUpdate IdleAndRuntime(int version, int idleMinutes = 45)
        => new()
        {
            Version             = version,
            IdleShutdownEnabled = true,
            IdleShutdownMinutes = idleMinutes,
            MaxRuntimeEnabled   = true,
            MaxRuntimeHours     = 6,
            TimeZone            = "UTC",
        };

    /// <summary>The first save creates the settings, the evaluator state at the save instant and one audit entry.</summary>
    /// <returns>A task completing after the first save was verified.</returns>
    [Fact]
    public async Task FirstSave_ReturnsVersionOne_AndCreatesSettingsStateAndAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var before = NodaSystemClock.Instance.GetCurrentInstant();

        var result = await Put(store, seed, Manager(seed), IdleAndRuntime(0));

        var dto = Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(result).Value);
        var after = NodaSystemClock.Instance.GetCurrentInstant();
        Assert.Equal(1, dto.Settings.Version);
        Assert.True(dto.Settings.IdleShutdownEnabled);
        Assert.Equal(45, dto.Settings.IdleShutdownMinutes);
        Assert.True(dto.Settings.MaxRuntimeEnabled);
        Assert.Equal(6, dto.Settings.MaxRuntimeHours);
        Assert.Equal("UTC", dto.Settings.TimeZone);
        Assert.Single(await store.DbContext.ServerAutomationSettings.AsNoTracking().ToListAsync());
        var state = Assert.Single(await store.DbContext.ServerAutomationStates.AsNoTracking().ToListAsync());
        Assert.InRange(state.SchedulesEvaluatedThrough!.Value, before, after);
        var audit = Assert.Single(await store.DbContext.HostingAuditRecords
            .AsNoTracking()
            .Where(q => q.Kind == EHostingAuditEventKind.AutomationSettingsChanged)
            .ToListAsync());
        Assert.Equal(seed.User.PrimaryKey, audit.ActorFk);
        Assert.Equal(seed.Server.PrimaryKey, audit.GameServerFk);
        Assert.Equal("Automation settings changed.", audit.Summary);
    }

    /// <summary>A changed save bumps the version; repeating the same content bumps nothing and audits nothing.</summary>
    /// <returns>A task completing after both saves were verified.</returns>
    [Fact]
    public async Task SecondSave_BumpsVersion_AndAnUnchangedSaveWritesNothing()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = Manager(seed);
        await Put(store, seed, principal, IdleAndRuntime(0));

        var changed = Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(
            await Put(store, seed, principal, IdleAndRuntime(1, 60))).Value);
        var repeated = Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(
            await Put(store, seed, principal, IdleAndRuntime(2, 60))).Value);

        Assert.Equal(2, changed.Settings.Version);
        Assert.Equal(60, changed.Settings.IdleShutdownMinutes);
        Assert.Equal(2, repeated.Settings.Version);
        Assert.Equal(2, await store.DbContext.HostingAuditRecords
            .CountAsync(q => q.Kind == EHostingAuditEventKind.AutomationSettingsChanged));
        Assert.Single(await store.DbContext.ServerAutomationStates.AsNoTracking().ToListAsync());
    }

    /// <summary>A stale version, and a first save that claims an existing version, are conflicts.</summary>
    /// <returns>A task completing after both conflicts were verified.</returns>
    [Fact]
    public async Task StaleVersion_AndFirstSaveWithAVersion_AreConflicts()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = Manager(seed);

        var firstWithVersion = await Put(store, seed, principal, IdleAndRuntime(5));
        await Put(store, seed, principal, IdleAndRuntime(0));
        var stale = await Put(store, seed, principal, IdleAndRuntime(0, 90));

        Assert.IsType<ConflictObjectResult>(firstWithVersion);
        Assert.IsType<ConflictObjectResult>(stale);
        Assert.Equal(1, (await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync()).Version);
    }

    /// <summary>A caller who may not view the server cannot tell it exists.</summary>
    /// <returns>A task completing after the refusal was verified.</returns>
    [Fact]
    public async Task CallerWithoutViewPermission_GetsNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(seed.User, seed.Server, Claims.ResourceBased.Server.Modify);

        var result = await Put(store, seed, principal, IdleAndRuntime(0));

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(await store.DbContext.ServerAutomationSettings.AsNoTracking().ToListAsync());
    }

    /// <summary>A viewer without the manage right is forbidden and nothing is stored.</summary>
    /// <param name="permission">The permission the viewer holds besides view.</param>
    /// <returns>A task completing after the refusal was verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.View)]
    [InlineData(Claims.ResourceBased.Server.Operate)]
    public async Task ViewerWithoutManageRight_IsForbidden(string permission)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = HostingTestData.ServerPrincipal(
            seed.User,
            seed.Server,
            Claims.ResourceBased.Server.View,
            permission);

        var result = await Put(store, seed, principal, IdleAndRuntime(0));

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(await store.DbContext.ServerAutomationSettings.AsNoTracking().ToListAsync());
    }

    /// <summary>A modifier who may not use the current cost owner is forbidden.</summary>
    /// <returns>A task completing after the refusal was verified.</returns>
    [Fact]
    public async Task ModifierWithoutTheRightToUseTheCostOwner_IsForbidden()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var stranger = new User { PrimaryKey = Guid.NewGuid() };
        var principal = HostingTestData.ServerPrincipal(
            stranger,
            seed.Server,
            Claims.ResourceBased.Server.View,
            Claims.ResourceBased.Server.Modify);

        var result = await Put(store, seed, principal, IdleAndRuntime(0));

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>A hosting administrator may save.</summary>
    /// <returns>A task completing after the save was verified.</returns>
    [Fact]
    public async Task Administrator_MaySave()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var administrator = HostingTestData.Principal(seed.User, (Claims.Administrative.Hosting, string.Empty));

        var result = await Put(store, seed, administrator, IdleAndRuntime(0));

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>An archived server cannot be changed.</summary>
    /// <returns>A task completing after the refusal was verified.</returns>
    [Fact]
    public async Task ArchivedServer_IsAConflict()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.IsArchived = true;
        await store.DbContext.SaveChangesAsync();

        var result = await Put(store, seed, Manager(seed), IdleAndRuntime(0));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("The server is archived.", ErrorOf(conflict.Value));
    }

    /// <summary>An enabled idle shutdown below the minimum is rejected with its field and stores nothing.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task IdleEnabledBelowTheMinimum_IsRejectedWithItsField()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(store, seed, Manager(seed), IdleAndRuntime(0, 2));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(["idleShutdownMinutes"], FieldsOf(bad.Value));
        Assert.Empty(await store.DbContext.ServerAutomationSettings.AsNoTracking().ToListAsync());
        Assert.Empty(await store.DbContext.HostingAuditRecords.AsNoTracking().ToListAsync());
    }

    /// <summary>A disabled idle shutdown never rejects its number and keeps the stored minutes.</summary>
    /// <returns>A task completing after the stored minutes were verified.</returns>
    [Fact]
    public async Task IdleDisabledWithAnOutOfRangeNumber_SavesAndKeepsTheStoredMinutes()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = Manager(seed);
        await Put(store, seed, principal, IdleAndRuntime(0, 45));

        var result = await Put(
            store,
            seed,
            principal,
            IdleAndRuntime(1, 2) with { IdleShutdownEnabled = false });

        var dto = Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(dto.Settings.IdleShutdownEnabled);
        Assert.Equal(45, dto.Settings.IdleShutdownMinutes);
        Assert.Equal(2, dto.Settings.Version);
    }

    /// <summary>A game that cannot report players does not accept idle shutdown.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task IdleEnabledForAGameWithoutPlayerCount_IsRejectedWithItsField()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(store, seed, Manager(seed), IdleAndRuntime(0), reportsPlayerCount: false);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(["idleShutdownEnabled"], FieldsOf(bad.Value));
    }

    /// <summary>A maximum runtime outside the limits is rejected only while the limit is enabled.</summary>
    /// <returns>A task completing after both cases were verified.</returns>
    [Fact]
    public async Task MaxRuntimeOutOfRange_IsRejectedOnlyWhenEnabled()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var principal = Manager(seed);

        var enabled = await Put(store, seed, principal, IdleAndRuntime(0) with { MaxRuntimeHours = 500 });
        var disabled = await Put(
            store,
            seed,
            principal,
            IdleAndRuntime(0) with { MaxRuntimeEnabled = false, MaxRuntimeHours = 500 });

        Assert.Equal(["maxRuntimeHours"], FieldsOf(Assert.IsType<BadRequestObjectResult>(enabled).Value));
        var dto = Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(disabled).Value);
        Assert.False(dto.Settings.MaxRuntimeEnabled);
        Assert.Equal(6, dto.Settings.MaxRuntimeHours);
    }

    private static ServerAutomationUpdate WithRules(
        int version,
        string? timeZone,
        params ServerScheduleRuleUpdate[] rules)
        => new()
        {
            Version             = version,
            IdleShutdownEnabled = false,
            IdleShutdownMinutes = 30,
            MaxRuntimeEnabled   = false,
            MaxRuntimeHours     = 6,
            TimeZone            = timeZone,
            Schedules           = rules,
        };

    private static ServerScheduleRuleUpdate Rule(
        EServerScheduleAction action,
        int[] days,
        string time,
        Guid? id = null)
        => new() { Id = id, Action = action, Days = days, Time = time };

    private static ServerAutomationDto Saved(IActionResult result)
        => Assert.IsType<ServerAutomationDto>(Assert.IsType<OkObjectResult>(result).Value);

    /// <summary>Two rules are saved in payload order with new ids, ascending days, HH:mm times and the save instant.</summary>
    /// <returns>A task completing after the rules were verified.</returns>
    [Fact]
    public async Task Rules_AreSavedInPayloadOrder_WithIdsAndEffectiveFromAtTheSave()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var before = NodaSystemClock.Instance.GetCurrentInstant();

        var dto = Saved(await Put(
            store,
            seed,
            Manager(seed),
            WithRules(
                0,
                "Europe/Berlin",
                Rule(EServerScheduleAction.Start, [5, 1], "18:00"),
                Rule(EServerScheduleAction.Stop, [6], "00:00"))));

        var after = NodaSystemClock.Instance.GetCurrentInstant();
        Assert.Equal("Europe/Berlin", dto.Settings.TimeZone);
        Assert.Equal(2, dto.Settings.Schedules.Count);
        Assert.Equal(EServerScheduleAction.Start, dto.Settings.Schedules[0].Action);
        Assert.Equal([1, 5], dto.Settings.Schedules[0].Days);
        Assert.Equal("18:00", dto.Settings.Schedules[0].Time);
        Assert.Equal(EServerScheduleAction.Stop, dto.Settings.Schedules[1].Action);
        Assert.Equal([6], dto.Settings.Schedules[1].Days);
        Assert.Equal("00:00", dto.Settings.Schedules[1].Time);
        Assert.NotEqual(Guid.Empty, dto.Settings.Schedules[0].Id);
        var stored = await store.DbContext.ServerSchedules.AsNoTracking().OrderBy(q => q.Position).ToListAsync();
        Assert.All(stored, q => Assert.InRange(q.EffectiveFrom, before, after));
        var state = await store.DbContext.ServerAutomationStates.AsNoTracking().SingleAsync();
        Assert.Equal(stored[0].EffectiveFrom, state.SchedulesEvaluatedThrough);
    }

    /// <summary>A changed rule restarts its own effectiveness, an unchanged rule keeps it, a missing rule is deleted.</summary>
    /// <returns>A task completing after the diff was verified.</returns>
    [Fact]
    public async Task Rules_AreDiffedById_ChangedResetsEffectiveFrom_MissingIsDeletedWithItsRuns()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext,
            seed.Server,
            q => q.TimeZone = "Europe/Berlin");
        var past = Instant.FromUnixTimeSeconds(1_700_000_000);
        var kept = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [1], 600, past);
        var changed = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [2], 600, past);
        var removed = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [3], 600, past);
        await ServerAutomationTestData.AddRunAsync(
            store.DbContext, removed, past, EServerScheduleRunOutcome.Stopped);
        var before = NodaSystemClock.Instance.GetCurrentInstant();

        var dto = Saved(await Put(
            store,
            seed,
            Manager(seed),
            WithRules(
                1,
                "Europe/Berlin",
                Rule(EServerScheduleAction.Stop, [1], "10:00", kept.PrimaryKey),
                Rule(EServerScheduleAction.Stop, [2], "11:00", changed.PrimaryKey))));

        Assert.Equal(2, dto.Settings.Version);
        var stored = await store.DbContext.ServerSchedules.AsNoTracking().OrderBy(q => q.Position).ToListAsync();
        Assert.Equal([kept.PrimaryKey, changed.PrimaryKey], stored.Select(q => q.PrimaryKey));
        Assert.Equal(past, stored[0].EffectiveFrom);
        Assert.True(stored[1].EffectiveFrom >= before);
        Assert.Equal(11 * 60, stored[1].MinuteOfDay);
        Assert.Empty(await store.DbContext.ServerScheduleRuns.AsNoTracking().ToListAsync());
    }

    /// <summary>Changing the time zone makes every rule effective from the save so nothing fires from the past.</summary>
    /// <returns>A task completing after the effective instants were verified.</returns>
    [Fact]
    public async Task ChangingTheTimeZone_ResetsEveryRulesEffectiveFrom()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext,
            seed.Server,
            q => q.TimeZone = "Europe/Berlin");
        var past = Instant.FromUnixTimeSeconds(1_700_000_000);
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [1], 600, past);
        var before = NodaSystemClock.Instance.GetCurrentInstant();

        Saved(await Put(
            store,
            seed,
            Manager(seed),
            WithRules(1, "America/New_York", Rule(EServerScheduleAction.Stop, [1], "10:00", rule.PrimaryKey))));

        Assert.True((await store.DbContext.ServerSchedules.AsNoTracking().SingleAsync()).EffectiveFrom >= before);
    }

    /// <summary>A rule id that does not belong to this server is rejected with the rule's field.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task RuleIdOfAnotherServer_IsRejected()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(
            store,
            seed,
            Manager(seed),
            WithRules(0, "UTC", Rule(EServerScheduleAction.Stop, [1], "10:00", Guid.NewGuid())));

        Assert.Equal(["schedules[0]"], FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
    }

    /// <summary>An unknown zone and rules without a zone are rejected on the time zone field.</summary>
    /// <param name="timeZone">The zone sent with one rule.</param>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Theory]
    [InlineData("Mars/Base")]
    [InlineData(null)]
    public async Task UnknownOrMissingTimeZone_IsRejectedWithItsField(string? timeZone)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(
            store,
            seed,
            Manager(seed),
            WithRules(0, timeZone, Rule(EServerScheduleAction.Stop, [1], "10:00")));

        Assert.Equal(["timeZone"], FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
    }

    /// <summary>More rules than the configured maximum are rejected on the list.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task MoreRulesThanTheMaximum_AreRejectedWithTheListField()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var rules = Enumerable.Range(0, 9)
            .Select(q => Rule(EServerScheduleAction.Stop, [1], $"{q:00}:00"))
            .ToArray();

        var result = await Put(store, seed, Manager(seed), WithRules(0, "UTC", rules));

        Assert.Equal(["schedules"], FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
    }

    /// <summary>Empty or out-of-range days and malformed times are rejected on the rule's own field.</summary>
    /// <param name="days">The weekdays of the rule.</param>
    /// <param name="time">The time of the rule.</param>
    /// <param name="expectedField">The field that must be reported.</param>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Theory]
    [InlineData(new int[0], "10:00", "schedules[0].days")]
    [InlineData(new[] { 0 }, "10:00", "schedules[0].days")]
    [InlineData(new[] { 8 }, "10:00", "schedules[0].days")]
    [InlineData(new[] { 1, 1 }, "10:00", "schedules[0].days")]
    [InlineData(new[] { 1 }, "25:00", "schedules[0].time")]
    [InlineData(new[] { 1 }, "7:5", "schedules[0].time")]
    [InlineData(new[] { 1 }, "10:60", "schedules[0].time")]
    [InlineData(new[] { 1 }, "", "schedules[0].time")]
    public async Task MalformedRule_IsRejectedWithItsField(int[] days, string time, string expectedField)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(
            store,
            seed,
            Manager(seed),
            WithRules(0, "UTC", Rule(EServerScheduleAction.Stop, days, time)));

        Assert.Equal([expectedField], FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
        Assert.Empty(await store.DbContext.ServerSchedules.AsNoTracking().ToListAsync());
    }

    /// <summary>Two rules that share a weekday and a time are both reported on their time fields.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task TwoRulesOnTheSameDayAndTime_AreBothReported()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(
            store,
            seed,
            Manager(seed),
            WithRules(
                0,
                "UTC",
                Rule(EServerScheduleAction.Start, [4, 5], "18:00"),
                Rule(EServerScheduleAction.Stop, [5, 6], "18:00")));

        Assert.Equal(
            ["schedules[0].time", "schedules[1].time"],
            FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
    }

    /// <summary>Confirming starts records the cost owner, user, time and estimate and keeps the version.</summary>
    /// <returns>A task completing after the consent was verified.</returns>
    [Fact]
    public async Task StartConsent_IsRecordedForTheCostOwner_WithoutChangingTheVersion()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        seed.Server.EstimatedHourlyCostEur = 0.5m;
        await store.DbContext.SaveChangesAsync();
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext,
            seed.Server,
            q => q.TimeZone = "UTC");
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext,
            settings,
            EServerScheduleAction.Start,
            [1],
            600,
            Instant.FromUnixTimeSeconds(1_700_000_000));
        var before = NodaSystemClock.Instance.GetCurrentInstant();

        var dto = Saved(await Put(
            store,
            seed,
            Manager(seed),
            WithRules(1, "UTC", Rule(EServerScheduleAction.Start, [1], "10:00", rule.PrimaryKey))
                with { StartConsent = true }));

        Assert.Equal(EStartConsentState.Given, dto.StartConsent.State);
        Assert.Equal(1, dto.Settings.Version);
        var stored = await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync();
        Assert.Equal(seed.CostOwner.PrimaryKey, stored.StartConsentCostOwnerFk);
        Assert.Equal(seed.User.PrimaryKey, stored.StartConsentByFk);
        Assert.True(stored.StartConsentAt >= before);
        Assert.Equal(0.5m, stored.StartConsentHourlyEstimateEur);
        var audit = Assert.Single(await store.DbContext.HostingAuditRecords.AsNoTracking().ToListAsync());
        Assert.Equal(EHostingAuditEventKind.ScheduledStartsConfirmed, audit.Kind);
        Assert.Equal(seed.CostOwner.PrimaryKey, audit.CostOwnerFk);
    }

    /// <summary>Confirming starts without any Start rule is rejected on the consent field.</summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task StartConsentWithoutAStartRule_IsRejectedWithItsField()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await Put(
            store,
            seed,
            Manager(seed),
            WithRules(0, "UTC", Rule(EServerScheduleAction.Stop, [1], "10:00")) with { StartConsent = true });

        Assert.Equal(["startConsent"], FieldsOf(Assert.IsType<BadRequestObjectResult>(result).Value));
    }

    /// <summary>A consent that was given for another cost owner is recorded again for the current one.</summary>
    /// <returns>A task completing after the new consent was verified.</returns>
    [Fact]
    public async Task StartConsentOfAPreviousCostOwner_IsReplacedForTheCurrentOne()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var past = Instant.FromUnixTimeSeconds(1_700_000_000);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext,
            seed.Server,
            q =>
            {
                q.TimeZone = "UTC";
                q.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey;
                q.StartConsentByFk = seed.User.PrimaryKey;
                q.StartConsentAt = past;
            });
        var rule = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [1], 600, past);
        var newOwner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = ECostOwnerKind.Verein,
            DisplayName      = "New owner",
            Currency         = "EUR",
            TimeStampCreated = past,
            TimeStampUpdated = past,
        };
        store.DbContext.CostOwners.Add(newOwner);
        await store.DbContext.SaveChangesAsync();
        seed.Server.CurrentCostOwnerFk = newOwner.PrimaryKey;
        await store.DbContext.SaveChangesAsync();
        var administrator = HostingTestData.Principal(seed.User, (Claims.Administrative.Hosting, string.Empty));
        var unchanged = WithRules(1, "UTC", Rule(EServerScheduleAction.Start, [1], "10:00", rule.PrimaryKey));

        var needed = Saved(await Put(store, seed, administrator, unchanged));
        var given = Saved(await Put(store, seed, administrator, unchanged with { StartConsent = true }));

        Assert.Equal(EStartConsentState.Needed, needed.StartConsent.State);
        Assert.Equal(EStartConsentState.Given, given.StartConsent.State);
        Assert.Equal(
            newOwner.PrimaryKey,
            (await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync()).StartConsentCostOwnerFk);
    }

    /// <summary>Adding or changing a Start rule without consent clears it; a Stop-only change keeps it.</summary>
    /// <returns>A task completing after the consent states were verified.</returns>
    [Fact]
    public async Task ChangingAStartRuleClearsTheConsent_ChangingOnlyStopRulesKeepsIt()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var past = Instant.FromUnixTimeSeconds(1_700_000_000);
        var settings = await ServerAutomationTestData.SeedSettingsAsync(
            store.DbContext,
            seed.Server,
            q =>
            {
                q.TimeZone = "UTC";
                q.StartConsentCostOwnerFk = seed.CostOwner.PrimaryKey;
                q.StartConsentByFk = seed.User.PrimaryKey;
                q.StartConsentAt = past;
            });
        var start = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Start, [1], 600, past);
        var stop = await ServerAutomationTestData.AddScheduleAsync(
            store.DbContext, settings, EServerScheduleAction.Stop, [1], 900, past);
        var principal = Manager(seed);

        var stopChanged = Saved(await Put(
            store,
            seed,
            principal,
            WithRules(
                1,
                "UTC",
                Rule(EServerScheduleAction.Start, [1], "10:00", start.PrimaryKey),
                Rule(EServerScheduleAction.Stop, [1], "16:00", stop.PrimaryKey))));
        var startChanged = Saved(await Put(
            store,
            seed,
            principal,
            WithRules(
                2,
                "UTC",
                Rule(EServerScheduleAction.Start, [1], "11:00", start.PrimaryKey),
                Rule(EServerScheduleAction.Stop, [1], "16:00", stop.PrimaryKey))));

        Assert.Equal(EStartConsentState.Given, stopChanged.StartConsent.State);
        Assert.Equal(EStartConsentState.Needed, startChanged.StartConsent.State);
        Assert.Equal(3, startChanged.Settings.Version);
        Assert.Null((await store.DbContext.ServerAutomationSettings.AsNoTracking().SingleAsync())
            .StartConsentCostOwnerFk);
    }

    /// <summary>
    /// Creates the real controller and service on the store and puts an update as the given caller.
    /// </summary>
    /// <param name="store">The test store the services read from.</param>
    /// <param name="seed">The seeded server.</param>
    /// <param name="principal">The caller.</param>
    /// <param name="update">The update to put.</param>
    /// <param name="reportsPlayerCount">Whether the test game reports its player count.</param>
    /// <returns>A task resolving to the controller result.</returns>
    private static Task<IActionResult> Put(
        HostingTestStore store,
        HostingTestSeed seed,
        ClaimsPrincipal principal,
        ServerAutomationUpdate update,
        bool reportsPlayerCount = true)
    {
        var db = store.DbContext;
        var service = new ServerAutomationSettingsService(
            db,
            new HostingAuthorizationService(db),
            ServerAutomationTestData.CreateProjector(db, reportsPlayerCount: reportsPlayerCount),
            new TestGameServerDefinitionCatalog(reportsPlayerCount: reportsPlayerCount),
            new HostingAuditWriter(),
            Options.Create(new ServerAutomationOptions()),
            NullLogger<ServerAutomationSettingsService>.Instance);
        var controller = new GameServerAutomationController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
        return controller.UpdateAutomationAsync(seed.Server.PrimaryKey, update, CancellationToken.None);
    }

    private static string[] FieldsOf(object? body)
        => JsonSerializer.SerializeToElement(body).GetProperty("fields").EnumerateArray()
            .Select(q => q.GetString()!)
            .ToArray();

    private static string ErrorOf(object? body)
        => JsonSerializer.SerializeToElement(body).GetProperty("error").GetString()!;
}
