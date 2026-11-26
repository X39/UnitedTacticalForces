using Api.Tests.GameServerHosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Builders for server automation tests. The projector is constructed only here, so a later constructor change
/// touches one place.
/// </summary>
internal static class ServerAutomationTestData
{
    private static readonly Instant SeedInstant = Instant.FromUnixTimeSeconds(1_700_000_000);

    /// <summary>
    /// Saves an automation settings row for a server with both limits off and the default values.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="server">The server the settings belong to.</param>
    /// <param name="configure">Changes applied to the row before it is saved, or <see langword="null"/>.</param>
    /// <returns>A task resolving to the saved settings.</returns>
    public static async Task<ServerAutomationSettings> SeedSettingsAsync(
        DatabaseContext db,
        GameServer server,
        Action<ServerAutomationSettings>? configure = null)
    {
        var settings = new ServerAutomationSettings
        {
            PrimaryKey          = Guid.NewGuid(),
            GameServerFk        = server.PrimaryKey,
            IdleShutdownMinutes = 30,
            MaxRuntimeHours     = 6,
            Version             = 1,
            TimeStampCreated    = SeedInstant,
            TimeStampUpdated    = SeedInstant,
        };
        configure?.Invoke(settings);
        db.ServerAutomationSettings.Add(settings);
        await db.SaveChangesAsync();
        return settings;
    }

    /// <summary>
    /// Saves a weekly schedule rule at the end of the rule list of a settings row.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="settings">The settings that own the rule.</param>
    /// <param name="action">The action the rule performs.</param>
    /// <param name="isoDays">The ISO weekdays, Monday is 1 and Sunday is 7.</param>
    /// <param name="minuteOfDay">The local time of day in minutes after midnight.</param>
    /// <param name="effectiveFrom">The instant from which the rule may fire.</param>
    /// <returns>A task resolving to the saved rule.</returns>
    public static async Task<ServerSchedule> AddScheduleAsync(
        DatabaseContext db,
        ServerAutomationSettings settings,
        EServerScheduleAction action,
        int[] isoDays,
        int minuteOfDay,
        Instant effectiveFrom)
    {
        var position = await db.ServerSchedules.CountAsync(q => q.SettingsFk == settings.PrimaryKey);
        var schedule = new ServerSchedule
        {
            PrimaryKey       = Guid.NewGuid(),
            SettingsFk       = settings.PrimaryKey,
            GameServerFk     = settings.GameServerFk,
            Action           = action,
            DaysMask         = ScheduleCalculator.ToDaysMask(isoDays),
            MinuteOfDay      = minuteOfDay,
            Position         = position,
            EffectiveFrom    = effectiveFrom,
            TimeStampCreated = effectiveFrom,
        };
        db.ServerSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule;
    }

    /// <summary>
    /// Saves a pending automatic stop warning for a deployment.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="deployment">The deployment the warning applies to.</param>
    /// <param name="reason">Why the warning was opened.</param>
    /// <param name="stopsAt">The instant the stop follows.</param>
    /// <param name="limit">The limit that applied, or <see langword="null"/>.</param>
    /// <returns>A task resolving to the saved pending stop.</returns>
    public static async Task<AutomationPendingStop> AddPendingStopAsync(
        DatabaseContext db,
        GameServerDeployment deployment,
        EAutoStopReason reason,
        Instant stopsAt,
        int? limit = null)
    {
        var pending = new AutomationPendingStop
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = deployment.GameServerFk,
            DeploymentFk     = deployment.PrimaryKey,
            Reason           = reason,
            Limit            = limit,
            WarnedAt         = stopsAt - Duration.FromMinutes(10),
            StopsAt          = stopsAt,
            Status           = EAutoStopStatus.Pending,
            TimeStampCreated = stopsAt - Duration.FromMinutes(10),
        };
        db.AutomationPendingStops.Add(pending);
        await db.SaveChangesAsync();
        return pending;
    }

    /// <summary>
    /// Saves the recorded outcome of one schedule occurrence.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="schedule">The rule the occurrence belongs to.</param>
    /// <param name="occurrence">The instant the occurrence was scheduled for.</param>
    /// <param name="outcome">The recorded outcome.</param>
    /// <param name="jobId">The lifecycle job queued for the occurrence, or <see langword="null"/>.</param>
    /// <returns>A task resolving to the saved run.</returns>
    public static async Task<ServerScheduleRun> AddRunAsync(
        DatabaseContext db,
        ServerSchedule schedule,
        Instant occurrence,
        EServerScheduleRunOutcome outcome,
        Guid? jobId = null)
    {
        var run = new ServerScheduleRun
        {
            PrimaryKey     = Guid.NewGuid(),
            ScheduleFk     = schedule.PrimaryKey,
            GameServerFk   = schedule.GameServerFk,
            OccurrenceAt   = occurrence,
            Outcome        = outcome,
            LifecycleJobFk = jobId,
            RecordedAt     = occurrence,
        };
        db.ServerScheduleRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    /// <summary>
    /// Saves a lifecycle job of a deployment in the given status.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="deployment">The deployment the job belongs to.</param>
    /// <param name="status">The job status.</param>
    /// <param name="kind">The job kind.</param>
    /// <returns>A task resolving to the saved job.</returns>
    public static async Task<LifecycleJob> AddJobAsync(
        DatabaseContext db,
        GameServerDeployment deployment,
        ELifecycleJobStatus status,
        ELifecycleJobKind kind = ELifecycleJobKind.Stop)
    {
        var job = new LifecycleJob
        {
            PrimaryKey       = Guid.NewGuid(),
            GameServerFk     = deployment.GameServerFk,
            DeploymentFk     = deployment.PrimaryKey,
            Kind             = kind,
            ActionIdentifier = kind.ToString(),
            Sequence         = 1,
            Status           = status,
            Stage            = status.ToString().ToLowerInvariant(),
            MaxAttempts      = 3,
            TimeStampCreated = SeedInstant,
            TimeStampUpdated = SeedInstant,
        };
        db.LifecycleJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    /// <summary>
    /// Constructs the projector over a test database context with the real metrics service and authorization rules.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="options">The automation options, or <see langword="null"/> for the defaults.</param>
    /// <param name="reportsPlayerCount">Whether the test game reports its player count.</param>
    /// <returns>The projector under test.</returns>
    public static ServerAutomationProjector CreateProjector(
        DatabaseContext db,
        ServerAutomationOptions? options = null,
        bool reportsPlayerCount = true)
        => new(
            db,
            new TestGameServerDefinitionCatalog(reportsPlayerCount: reportsPlayerCount),
            ServerMetricsTestData.Create(db),
            new HostingAuthorizationService(db),
            Options.Create(options ?? new ServerAutomationOptions()));

    /// <summary>
    /// Constructs the evaluator over a test database context with the real lifecycle service, alert writer, metrics
    /// service and audit writer. This is the only construction site of the evaluator in tests.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="options">The automation options, or <see langword="null"/> for the defaults.</param>
    /// <param name="reportsPlayerCount">Whether the test game reports its player count.</param>
    /// <returns>The evaluator under test.</returns>
    internal static ServerAutomationEvaluator CreateEvaluator(
        DatabaseContext db,
        ServerAutomationOptions? options = null,
        bool reportsPlayerCount = true)
        => new(
            db,
            new TestGameServerDefinitionCatalog(reportsPlayerCount: reportsPlayerCount),
            ServerMetricsTestData.Create(db),
            HostingTestData.CreateLifecycleService(db),
            CreateScheduleRunner(db, options),
            new AutoStopAlertWriter(db, new CostAlertRecipientResolver(db)),
            new HostingAuditWriter(),
            Options.Create(options ?? new ServerAutomationOptions()),
            NullLogger<ServerAutomationEvaluator>.Instance);

    /// <summary>
    /// Constructs the schedule runner over a test database context with the real lifecycle service, cost slice
    /// service, alert writer and audit writer.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="options">The automation options, or <see langword="null"/> for the defaults.</param>
    /// <param name="hourlyCostEur">The hourly cost the cost slice service assumes for the test provider.</param>
    /// <returns>The schedule runner under test.</returns>
    internal static ServerScheduleRunner CreateScheduleRunner(
        DatabaseContext db,
        ServerAutomationOptions? options = null,
        decimal hourlyCostEur = 0.10m)
    {
        var auditWriter = new HostingAuditWriter();
        return new ServerScheduleRunner(
            db,
            HostingTestData.CreateLifecycleService(db),
            new HostingCostSliceService(
                db,
                auditWriter,
                Options.Create(new HostingFinanceOptions
                {
                    MinimumSafetyRangeEur = 5m,
                    ReservationSlice = TimeSpan.FromMinutes(15),
                    ReconciliationInterval = TimeSpan.FromSeconds(30),
                    EstimatedHourlyCostEur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        [TestRuntimeProvider.Identifier] = hourlyCostEur,
                    },
                })),
            new AutoStopAlertWriter(db, new CostAlertRecipientResolver(db)),
            auditWriter,
            Options.Create(options ?? new ServerAutomationOptions()),
            NullLogger<ServerScheduleRunner>.Instance);
    }

    /// <summary>
    /// Saves the schedule watermark of a server, as the first settings save does.
    /// </summary>
    /// <param name="db">The test database context.</param>
    /// <param name="server">The server the state belongs to.</param>
    /// <param name="through">The instant through which the schedules count as evaluated.</param>
    /// <returns>A task resolving to the saved state.</returns>
    public static async Task<ServerAutomationState> SeedWatermarkAsync(
        DatabaseContext db,
        GameServer server,
        Instant through)
    {
        var state = new ServerAutomationState
        {
            PrimaryKey                = Guid.NewGuid(),
            GameServerFk              = server.PrimaryKey,
            SchedulesEvaluatedThrough = through,
            TimeStampUpdated          = through,
        };
        db.ServerAutomationStates.Add(state);
        await db.SaveChangesAsync();
        return state;
    }
}
