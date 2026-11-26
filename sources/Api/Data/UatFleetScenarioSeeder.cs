using Microsoft.EntityFrameworkCore;
using NodaTime;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Data;

/// <summary>
/// Development-only seeder that creates one game server per fleet state so manual UAT can open the fleet and
/// find every card variant without driving real lifecycle jobs. It also seeds one running session that has already
/// been charged a backdated provider-cost slice, so the accrued cost and the cost alerts can be observed immediately
/// instead of after waiting for real time to pass. A further running session has an online primary worker and a week
/// of tiered metric history, so the metrics charts can be checked right after a reseed. The UAT cost owner is funded
/// with one audited seed credit, because the UAT profile runs the real cost reconciler and an unfunded owner would have
/// every seeded possible-cost session cost-stopped within seconds of startup. Six further servers make every
/// automatic shutdown and schedule behavior observable within minutes: an idle server with a zero-player emulated
/// worker, a server that is already past one hour of runtime with players online, a running server for a scheduled
/// stop, an off server for a scheduled start, a scheduled start that is skipped because its cost owner cannot pay, and
/// a server whose scheduled starts are paused because the consent belongs to another cost owner. It is idempotent:
/// servers are matched by title and the credit is booked once.
/// </summary>
internal static class UatFleetScenarioSeeder
{
    /// <summary>The configuration key that enables this seeder.</summary>
    internal const string ConfigurationKey = "Testing:UatScenarios:Enabled";

    /// <summary>The address given to a UAT owner without an email so the email alert channel has a recipient.</summary>
    internal const string OwnerEmailAddress = "uat-owner@utf.test";

    /// <summary>The title of the scenario whose running session carries a backdated charged slice.</summary>
    internal const string CostingScenarioTitle = "UAT 9 Costing (accrued cost)";

    /// <summary>
    /// The title of the scenario whose running session has an online primary worker and a week of tiered metric
    /// history. The UAT worker emulation feeds this server live readings through the worker channel.
    /// </summary>
    internal const string MetricsScenarioTitle = "UAT 10 Metrics";

    /// <summary>
    /// The title of the running scenario with a zero-player emulated worker, for the idle shutdown. The UAT sets the
    /// idle shutdown through the panel and observes the warning and the automatic stop.
    /// </summary>
    internal const string IdleScenarioTitle = "UAT 11 Idle shutdown";

    /// <summary>
    /// The title of the running scenario whose deployment started 70 minutes before seeding and whose emulated worker
    /// reports players online. A maximum runtime of one hour set through the panel is already exceeded, so the stop
    /// happens although players are online.
    /// </summary>
    internal const string MaxRuntimeScenarioTitle = "UAT 12 Maximum runtime";

    /// <summary>
    /// The title of the running scenario for a scheduled stop. The UAT adds a stop rule a few minutes ahead through the
    /// panel and observes the warning one lead before and the stop at the rule time.
    /// </summary>
    internal const string ScheduledStopScenarioTitle = "UAT 13 Scheduled stop";

    /// <summary>
    /// The title of the off scenario without settings for a scheduled start. The UAT adds a start rule a few minutes
    /// ahead through the panel, gives the consent and observes the queued start job.
    /// </summary>
    internal const string ScheduledStartScenarioTitle = "UAT 14 Scheduled start";

    /// <summary>
    /// The title of the off scenario whose daily start rule fires three minutes after seeding but whose cost owner
    /// cannot pay, so the occurrence is recorded as skipped for cost.
    /// </summary>
    internal const string StartSkippedScenarioTitle = "UAT 15 Scheduled start without credit";

    /// <summary>
    /// The title of the off scenario whose Sunday start rule is paused because the recorded consent belongs to a cost
    /// owner other than the one that pays for the server now.
    /// </summary>
    internal const string StartsPausedScenarioTitle = "UAT 16 Scheduled starts paused";

    /// <summary>The display name of the cost owner without credit that pays for the skipped-start scenario.</summary>
    internal const string UnfundedCostOwnerName = "UAT unfunded group";

    /// <summary>
    /// The seeded servers that get an emulated worker session, keyed by server title, with the player feed each session
    /// reports. Every listed seeded server with a running deployment and a primary worker is driven by the UAT worker
    /// emulation through the real worker channel; later UAT scenarios add their titles here.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, EUatPlayerProfile> EmulatedWorkers =
        new Dictionary<string, EUatPlayerProfile>(StringComparer.Ordinal)
        {
            [MetricsScenarioTitle] = EUatPlayerProfile.Synthetic,
            [IdleScenarioTitle] = EUatPlayerProfile.NoPlayers,
            [MaxRuntimeScenarioTitle] = EUatPlayerProfile.FewPlayers,
            [ScheduledStopScenarioTitle] = EUatPlayerProfile.FewPlayers,
        };

    /// <summary>The service identifier of the primary worker seeded for the metrics scenario.</summary>
    private const string PrimaryServiceIdentifier = "game-server";

    /// <summary>The process identifier the seeded primary worker reports as its game process.</summary>
    private const int MetricsProcessId = 4242;

    /// <summary>How long before seeding the game process of the metrics scenario started.</summary>
    private static readonly Duration MetricsProcessAge = Duration.FromMinutes(90);

    /// <summary>The hourly rate of the costing scenario in EUR.</summary>
    private const decimal CostingHourlyRateEur = 0.60m;

    /// <summary>
    /// The amount of the costing scenario slice in EUR: 130 minutes (115 elapsed plus 15 remaining) at the
    /// hourly rate.
    /// </summary>
    private const decimal CostingSliceAmountEur = 1.3000m;

    /// <summary>
    /// How long before seeding the costing session started to cost: 1 h 55 min, so the session sits five
    /// minutes before the start of its third billed hour. The UAT shows 1.20 EUR right after a reseed and
    /// steps to 1.80 EUR when the third started hour begins.
    /// </summary>
    private static readonly Duration CostingElapsed = Duration.FromMinutes(115);

    /// <summary>How long after seeding the costing slice authorization lasts.</summary>
    private static readonly Duration CostingRemaining = Duration.FromMinutes(15);

    /// <summary>
    /// The amount of the seed credit in EUR. The seeded possible-cost sessions together cost about 1.20 EUR per hour
    /// (six at 0.10 EUR per hour plus the costing scenario at 0.60 EUR per hour), so this keeps the owner far above
    /// the safety range of the real cost reconciler for hundreds of hours of a running UAT stack.
    /// </summary>
    internal const decimal CreditAmountEur = 1000.0000m;

    /// <summary>
    /// The fixed record identifier of the seed credit ledger entry. It is the idempotency key: the ledger has a unique
    /// filtered index on the record identifier, so the credit cannot be booked twice, not even by a race.
    /// </summary>
    internal static readonly Guid CreditRecordIdentifier = new("7f3c2a1e-5b8d-4c6f-9a0e-2d4b6c8e0f13");

    /// <summary>The external reference that marks the seed credit ledger entry as a UAT seed credit.</summary>
    internal const string CreditExternalReference = "UAT-SEED-CREDIT";

    /// <summary>The reason recorded on the seed credit ledger entry.</summary>
    private const string CreditReason =
        "UAT seed credit that lets the real cost reconciler fund the seeded sessions instead of cost-stopping them.";

    /// <summary>
    /// How long before the seed instant the credit is booked. The credit is booked one day earlier so a ledger
    /// ordered by effective time shows it before the costing slice, which starts 115 minutes before seeding.
    /// </summary>
    private static readonly Duration CreditAge = Duration.FromDays(1);

    /// <summary>How long before seeding the deployment of the maximum runtime scenario started.</summary>
    private static readonly Duration MaxRuntimeDeploymentAge = Duration.FromMinutes(70);

    /// <summary>The age of the game process of the automation scenarios that did not run for long.</summary>
    private static readonly Duration FreshProcessAge = Duration.FromMinutes(1);

    /// <summary>How many minutes after the seed instant the daily start rule of the skipped-start scenario fires.</summary>
    private const int SkippedStartDelayMinutes = 3;

    /// <summary>The minute of day, in UTC, of the Sunday start rule of the paused-starts scenario.</summary>
    private const int PausedStartMinuteOfDay = 3 * 60;

    /// <summary>The time zone the seeded schedule rules are interpreted in.</summary>
    private const string ScheduleTimeZone = "UTC";

    /// <summary>
    /// Selects which automation rows a seeded scenario gets beyond its deployment.
    /// </summary>
    private enum EAutomationSeed
    {
        /// <summary>The scenario gets no automation settings.</summary>
        None = 0,

        /// <summary>
        /// The scenario is billed to the cost owner without credit and has a consented daily start rule that fires
        /// shortly after seeding.
        /// </summary>
        StartSkippedForCost = 1,

        /// <summary>
        /// The scenario has a Sunday start rule whose consent belongs to another cost owner than the current one.
        /// </summary>
        StartsPaused = 2,
    }

    /// <summary>
    /// Describes one seeded fleet server.
    /// </summary>
    /// <param name="Title">The unique server title the seeder matches existing servers by.</param>
    /// <param name="Status">The status of the active deployment, or <see langword="null"/> for no deployment.</param>
    /// <param name="PossibleCost">Whether the deployment may still generate provider cost.</param>
    /// <param name="FaultCode">The fault code of the deployment, if it is faulted.</param>
    /// <param name="FaultMessage">The fault message of the deployment, if it is faulted.</param>
    /// <param name="AccruedCost">Whether the deployment gets a backdated charged provider-cost slice.</param>
    /// <param name="Metrics">
    /// Whether the deployment gets an online primary worker and a week of tiered metric history.
    /// </param>
    /// <param name="PrimaryWorker">
    /// Whether the deployment gets an online primary worker without metric history, so the UAT worker emulation can
    /// drive it.
    /// </param>
    /// <param name="DeploymentAge">How long before seeding the deployment started, or zero for a fresh start.</param>
    /// <param name="Automation">The automation rows the server gets.</param>
    private sealed record Scenario(
        string Title,
        EDeploymentStatus? Status,
        bool PossibleCost,
        string? FaultCode = null,
        string? FaultMessage = null,
        bool AccruedCost = false,
        bool Metrics = false,
        bool PrimaryWorker = false,
        Duration DeploymentAge = default,
        EAutomationSeed Automation = EAutomationSeed.None);

    private static readonly Scenario[] Scenarios =
    [
        new("UAT 1 Off (no deployment)", null, false),
        new("UAT 2 Running", EDeploymentStatus.Running, true),
        new("UAT 3 Starting (waiting for worker)", EDeploymentStatus.WaitingForWorker, true),
        new("UAT 4 Stopping", EDeploymentStatus.Stopping, true),
        new(
            "UAT 5 Attention (failed)",
            EDeploymentStatus.Failed,
            false,
            "uat-failed",
            "Emulated failure for UAT."),
        new(
            "UAT 6 Attention (preservation failed)",
            EDeploymentStatus.PreservationFailed,
            true,
            "uat-preservation-failed",
            "Emulated preservation failure for UAT."),
        new("UAT 7 Unconfirmed stop (stopped, cost possible)", EDeploymentStatus.Stopped, true),
        new("UAT 8 Off (terminated)", EDeploymentStatus.Terminated, false),
        new(CostingScenarioTitle, EDeploymentStatus.Running, true, AccruedCost: true),
        new(MetricsScenarioTitle, EDeploymentStatus.Running, true, Metrics: true),
        new(IdleScenarioTitle, EDeploymentStatus.Running, true, PrimaryWorker: true),
        new(
            MaxRuntimeScenarioTitle,
            EDeploymentStatus.Running,
            true,
            PrimaryWorker: true,
            DeploymentAge: MaxRuntimeDeploymentAge),
        new(ScheduledStopScenarioTitle, EDeploymentStatus.Running, true, PrimaryWorker: true),
        new(ScheduledStartScenarioTitle, null, false),
        new(StartSkippedScenarioTitle, null, false, Automation: EAutomationSeed.StartSkippedForCost),
        new(StartsPausedScenarioTitle, null, false, Automation: EAutomationSeed.StartsPaused),
    ];

    /// <summary>
    /// Credits the UAT cost owner once, then creates the missing UAT servers, owned by and billed to the given user.
    /// The owner is funded because the UAT profile runs the real cost reconciler.
    /// </summary>
    /// <param name="dbContext">The database context to write to.</param>
    /// <param name="ownerUserId">The identifier of the user that creates and pays for the servers.</param>
    /// <param name="definitions">The game catalog used to find the generated secrets of each server.</param>
    /// <param name="secrets">The secret service that creates the platform-generated secrets.</param>
    /// <param name="cancellationToken">A token that cancels the seeding.</param>
    /// <param name="now">The instant seeding is anchored to; defaults to the current instant.</param>
    /// <returns>A task that completes when the servers are saved.</returns>
    /// <exception cref="InvalidOperationException">The owner user or its cost owner does not exist.</exception>
    internal static async Task SeedAsync(
        DatabaseContext dbContext,
        Guid ownerUserId,
        IGameServerDefinitionCatalog definitions,
        IGameServerSecretService secrets,
        CancellationToken cancellationToken = default,
        Instant? now = null)
    {
        var costOwner = await dbContext.CostOwners
                            .SingleOrDefaultAsync(q => q.UserFk == ownerUserId, cancellationToken)
                            .ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            "The UAT owner user has no cost owner; seed the debug demo data first.");
        var owner = await dbContext.Users
                        .SingleOrDefaultAsync(q => q.PrimaryKey == ownerUserId, cancellationToken)
                        .ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The UAT owner user does not exist.");
        // The email alert channel needs a recipient during UAT; an address the user already has is kept.
        if (string.IsNullOrWhiteSpace(owner.EMail))
            owner.EMail = OwnerEmailAddress;
        var seedInstant = now ?? SystemClock.Instance.GetCurrentInstant();
        await EnsureCreditAsync(dbContext, costOwner, ownerUserId, seedInstant, cancellationToken)
            .ConfigureAwait(false);
        var existingTitles = await dbContext.GameServers
            .Select(q => q.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var unfundedOwner = await EnsureUnfundedCostOwnerAsync(dbContext, seedInstant, cancellationToken)
            .ConfigureAwait(false);
        foreach (var scenario in Scenarios.Where(q => !existingTitles.Contains(q.Title)))
        {
            AddServer(
                dbContext,
                scenario,
                ownerUserId,
                scenario.Automation is EAutomationSeed.StartSkippedForCost ? unfundedOwner : costOwner,
                unfundedOwner,
                definitions,
                secrets,
                seedInstant);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Books the seed credit for the UAT cost owner as a bank-transfer payment ledger entry together with its hosting
    /// audit record, unless it has been booked before.
    /// </summary>
    /// <param name="dbContext">The database context the ledger entry is added to.</param>
    /// <param name="costOwner">The tracked cost owner that receives the credit.</param>
    /// <param name="ownerUserId">The identifier of the user that is recorded as booking the credit.</param>
    /// <param name="now">The seeding instant the credit is placed before.</param>
    /// <param name="cancellationToken">A token that cancels the lookup of an existing credit.</param>
    /// <returns>A task that completes when the credit is added to the context or was already booked.</returns>
    private static async Task EnsureCreditAsync(
        DatabaseContext dbContext,
        CostOwner costOwner,
        Guid ownerUserId,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.HostingCostLedgerEntries
                .AnyAsync(q => q.RecordIdentifier == CreditRecordIdentifier, cancellationToken)
                .ConfigureAwait(false))
            return;

        costOwner.Balance += CreditAmountEur;
        costOwner.TimeStampUpdated = now;
        var bookedAt = now - CreditAge;
        var entryId = Guid.NewGuid();
        dbContext.HostingCostLedgerEntries.Add(new HostingCostLedgerEntry
        {
            PrimaryKey = entryId,
            CostOwnerFk = costOwner.PrimaryKey,
            Kind = EHostingLedgerEntryKind.BankTransferPayment,
            Amount = CreditAmountEur,
            BalanceAfter = costOwner.Balance,
            Currency = "EUR",
            RecordIdentifier = CreditRecordIdentifier,
            RecordedByFk = ownerUserId,
            ExternalReference = CreditExternalReference,
            Reason = CreditReason,
            TimeStampEffective = bookedAt,
            TimeStampCreated = bookedAt,
        });
        // The seeder is static; the audit writer is stateless and only sanitizes and adds the record.
        new HostingAuditWriter().Add(
            dbContext,
            EHostingAuditEventKind.BankTransferPaymentRecorded,
            "A UAT seed credit was recorded for the UAT cost owner.",
            actorId: ownerUserId,
            costOwnerId: costOwner.PrimaryKey,
            details: $"LedgerEntry={entryId}; Amount={CreditAmountEur:F4}; Currency=EUR; "
                     + $"Reference={CreditExternalReference}",
            costImpact: ECostImpact.NoCostImpact);
    }

    /// <summary>
    /// Finds or creates the cost owner without credit that pays for the skipped-start scenario and whose consent the
    /// paused-starts scenario carries. It is matched by display name, so reseeding creates it once.
    /// </summary>
    /// <param name="dbContext">The database context the cost owner is added to.</param>
    /// <param name="now">The seeding instant recorded as creation time of a new cost owner.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A task resolving to the tracked cost owner with a zero balance.</returns>
    private static async Task<CostOwner> EnsureUnfundedCostOwnerAsync(
        DatabaseContext dbContext,
        Instant now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.CostOwners
            .FirstOrDefaultAsync(q => q.DisplayName == UnfundedCostOwnerName, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var owner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = UnfundedCostOwnerName,
            Balance = 0m,
            Currency = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.CostOwners.Add(owner);
        return owner;
    }

    /// <summary>
    /// Adds the automation settings, schedule rule, consent and schedule watermark of an off automation scenario. The
    /// rule and the watermark start at the seeding instant so that nothing fires retroactively.
    /// </summary>
    /// <param name="dbContext">The database context the rows are added to.</param>
    /// <param name="server">The server the rows belong to.</param>
    /// <param name="automation">Which automation rows the server gets.</param>
    /// <param name="ownerUserId">The user recorded as having given the consent.</param>
    /// <param name="consentCostOwner">The cost owner the start consent is recorded for.</param>
    /// <param name="now">The seeding instant the rule becomes effective at.</param>
    private static void AddAutomation(
        DatabaseContext dbContext,
        GameServer server,
        EAutomationSeed automation,
        Guid ownerUserId,
        CostOwner consentCostOwner,
        Instant now)
    {
        var settings = new ServerAutomationSettings
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            IdleShutdownMinutes = 30,
            MaxRuntimeHours = 6,
            TimeZone = ScheduleTimeZone,
            Version = 1,
            StartConsentCostOwnerFk = consentCostOwner.PrimaryKey,
            StartConsentByFk = ownerUserId,
            StartConsentAt = now,
            UpdatedByFk = ownerUserId,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.ServerAutomationSettings.Add(settings);
        int[] days = automation is EAutomationSeed.StartsPaused ? [7] : [1, 2, 3, 4, 5, 6, 7];
        var utc = now.InUtc().TimeOfDay;
        var minuteOfDay = automation is EAutomationSeed.StartsPaused
            ? PausedStartMinuteOfDay
            : (utc.Hour * 60 + utc.Minute + SkippedStartDelayMinutes) % 1440;
        dbContext.ServerSchedules.Add(new ServerSchedule
        {
            PrimaryKey = Guid.NewGuid(),
            SettingsFk = settings.PrimaryKey,
            GameServerFk = server.PrimaryKey,
            Action = EServerScheduleAction.Start,
            DaysMask = ScheduleCalculator.ToDaysMask(days),
            MinuteOfDay = minuteOfDay,
            Position = 0,
            EffectiveFrom = now,
            TimeStampCreated = now,
        });
        dbContext.ServerAutomationStates.Add(new ServerAutomationState
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            SchedulesEvaluatedThrough = now,
            TimeStampUpdated = now,
        });
    }

    private static void AddServer(
        DatabaseContext dbContext,
        Scenario scenario,
        Guid ownerUserId,
        CostOwner costOwner,
        CostOwner unfundedOwner,
        IGameServerDefinitionCatalog definitions,
        IGameServerSecretService secrets,
        Instant now)
    {
        var costOwnerId = costOwner.PrimaryKey;
        var server = new GameServer
        {
            PrimaryKey = Guid.NewGuid(),
            Title = scenario.Title,
            GameIdentifier = Arma3GameServerDefinitionRegistration.GameIdentifier,
            RuntimeProviderIdentifier = HetznerRuntimeProvider.Identifier,
            RuntimeProfileIdentifier = "default",
            RuntimeProfileDisplayName = "Default",
            RuntimeStorageStrategy = ERuntimeStorageStrategy.IncludedDisk,
            EstimatedHourlyCostEur = 0.10m,
            EstimatedMonthlyCostEur = 73m,
            CreatedByFk = ownerUserId,
            CurrentCostOwnerFk = costOwnerId,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.GameServers.Add(server);
        // A server created through the API always owns its generated secrets, so a later start can resolve them.
        if (definitions.Find(server.GameIdentifier) is { } definition)
            secrets.EnsureGeneratedSecrets(server, definition, ownerUserId, now);
        dbContext.GameServerCostOwnerAssignments.Add(new GameServerCostOwnerAssignment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            CostOwnerFk = costOwnerId,
            AssignedByFk = ownerUserId,
            ChangeReason = "UAT scenario seed",
            TimeStampAssigned = now,
        });
        if (scenario.Automation is not EAutomationSeed.None)
        {
            // The paused scenario carries the consent of the cost owner that does not pay for the server.
            AddAutomation(
                dbContext,
                server,
                scenario.Automation,
                ownerUserId,
                scenario.Automation is EAutomationSeed.StartsPaused ? unfundedOwner : costOwner,
                now);
        }

        if (scenario.Status is not { } status)
            return;

        var isSettled = status is EDeploymentStatus.Terminated or EDeploymentStatus.Stopped;
        var deployment = new GameServerDeployment
        {
            PrimaryKey = Guid.NewGuid(),
            GameServerFk = server.PrimaryKey,
            CostOwnerFk = costOwnerId,
            RuntimeProfileIdentifier = "default",
            RuntimeProfileDisplayName = "Default",
            RuntimeStorageStrategy = ERuntimeStorageStrategy.IncludedDisk,
            DeploymentRoot = "/srv/united-tactical-forces/runtime",
            EstimatedHourlyCostEur = 0.10m,
            Status = status,
            Stage = "uat-emulated",
            PossibleCost = scenario.PossibleCost,
            FaultCode = scenario.FaultCode,
            FaultMessage = scenario.FaultMessage,
            TimeStampFaulted = scenario.FaultCode is null ? null : now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampStarted = now - scenario.DeploymentAge,
            TimeStampResolved = status is EDeploymentStatus.Terminated ? now : null,
        };
        dbContext.GameServerDeployments.Add(deployment);
        if (!scenario.PossibleCost)
            return;
        var resource = new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ProviderIdentifier = HetznerRuntimeProvider.Identifier,
            ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
            ProviderResourceIdentifier =
                (isSettled ? EmulatedHetznerRuntimeProvider.UnconfirmedPrefix : EmulatedHetznerRuntimeProvider.SeededServerPrefix)
                + deployment.PrimaryKey.ToString("N")[..8],
            Kind = HetznerRuntimeProvider.ServerResourceType,
            ProviderState = isSettled ? "unknown" : "running",
            PublicAddress = "203.0.113.10",
            State = isSettled ? EDeploymentResourceState.Unknown : EDeploymentResourceState.Allocated,
            PossibleCost = true,
            EstimatedHourlyCostEur = 0.10m,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampObserved = now,
        };
        dbContext.DeploymentResources.Add(resource);
        if (scenario.AccruedCost)
            AddChargedSlice(dbContext, deployment, resource, costOwner, now);
        if (scenario.PrimaryWorker)
            AddPrimaryWorker(dbContext, deployment, now, scenario.DeploymentAge + FreshProcessAge);
        if (!scenario.Metrics)
            return;
        AddPrimaryWorker(dbContext, deployment, now, MetricsProcessAge);
        UatMetricHistorySeeder.Add(dbContext, server, now);
    }

    /// <summary>
    /// Adds the online primary worker of a running deployment, with a heartbeat at the seeding instant and a game
    /// process that started the given time earlier, so the current readings can be live once samples arrive.
    /// </summary>
    /// <param name="dbContext">The database context the worker is added to.</param>
    /// <param name="deployment">The running deployment the worker belongs to.</param>
    /// <param name="now">The seeding instant the worker evidence is placed around.</param>
    /// <param name="processAge">How long before seeding the game process started.</param>
    private static void AddPrimaryWorker(
        DatabaseContext dbContext,
        GameServerDeployment deployment,
        Instant now,
        Duration processAge)
    {
        dbContext.LifecycleWorkers.Add(new LifecycleWorker
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = deployment.PrimaryKey,
            ServiceIdentifier = PrimaryServiceIdentifier,
            ServiceOrdinal = 0,
            Identity = "uat-emulated-worker",
            Status = EWorkerStatus.Online,
            ProcessId = MetricsProcessId,
            ProcessStartedAt = now - processAge,
            LastHeartbeatAt = now,
            BootstrapCredentialExpiresAt = now,
            BootstrapCredentialConsumedAt = now,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            TimeStampRegistered = now,
        });
    }

    /// <summary>
    /// Backdates the deployment and charges it one provider-cost slice the way the slice charging service would, so
    /// the regular cost reconciler and the accrual calculator treat the session like a real one.
    /// </summary>
    /// <param name="dbContext">The database context the ledger entry is added to.</param>
    /// <param name="deployment">The running deployment that receives the slice.</param>
    /// <param name="resource">The provider resource of the deployment.</param>
    /// <param name="costOwner">The tracked cost owner that is charged.</param>
    /// <param name="now">The seeding instant the slice is placed around.</param>
    private static void AddChargedSlice(
        DatabaseContext dbContext,
        GameServerDeployment deployment,
        DeploymentResource resource,
        CostOwner costOwner,
        Instant now)
    {
        var sliceStartsAt = now - CostingElapsed;
        var sliceEndsAt = now + CostingRemaining;
        deployment.EstimatedHourlyCostEur = CostingHourlyRateEur;
        deployment.TimeStampStarted = sliceStartsAt;
        deployment.CostAuthorizationExpiresAt = sliceEndsAt;
        resource.EstimatedHourlyCostEur = CostingHourlyRateEur;
        costOwner.Balance -= CostingSliceAmountEur;
        costOwner.TimeStampUpdated = now;
        dbContext.HostingCostLedgerEntries.Add(new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = costOwner.PrimaryKey,
            DeploymentFk = deployment.PrimaryKey,
            Kind = EHostingLedgerEntryKind.ProviderCostSlice,
            Amount = -CostingSliceAmountEur,
            BalanceAfter = costOwner.Balance,
            EstimatedHourlyRate = CostingHourlyRateEur,
            Currency = "EUR",
            ProviderIdentifier = HetznerRuntimeProvider.Identifier,
            SliceStartsAt = sliceStartsAt,
            SliceEndsAt = sliceEndsAt,
            TimeStampEffective = sliceStartsAt,
            TimeStampCreated = sliceStartsAt,
        });
    }
}
