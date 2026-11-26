# Phase 68: Auto-Stop & Schedules - Pattern Map

**Mapped:** 2026-11-22
**Files analyzed:** 38 (new + modified, grouped where identical in shape)
**Analogs found:** 36 / 38

All analog paths below are git-tracked, except the seeded-stack UAT suite (`sources/ReactApp/tests/uat/*`,
`playwright.uat.config.ts`), which is still untracked in the working tree (see "No Analog Found").

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `sources/HostingContracts/EAutoStopReason.cs`, `EAutoStopStatus.cs`, `EServerScheduleAction.cs`, `EServerScheduleRunOutcome.cs`, `ELifecycleJobTrigger.cs`, `ECostAlertKind.cs` | model (enum) | n/a | `sources/HostingContracts/ELifecycleJobKind.cs` | exact |
| `sources/HostingContracts/EHostingAuditEventKind.cs` (modify, +83..89) | model (enum) | n/a | itself (last member `CostOwnerUnfrozen = 82`) | exact |
| `sources/Database/Hosting/ServerAutomationSettings.cs` | model (entity) | CRUD, optimistic concurrency | `sources/Database/Hosting/CostAlertSetting.cs` | exact |
| `sources/Database/Hosting/ServerAutomationState.cs` | model (entity) | evaluator-owned state | `CostAlertSetting.cs` (shape, no Version) | role-match |
| `sources/Database/Hosting/ServerSchedule.cs` | model (entity child) | CRUD | `sources/Database/Hosting/CostAlertThreshold.cs` | exact |
| `sources/Database/Hosting/ServerScheduleRun.cs`, `AutomationPendingStop.cs` | model (once-only record) | event / persist-intent | `sources/Database/Hosting/CostAlert.cs` | exact |
| `sources/Database/Hosting/CostAlert.cs`, `LifecycleJob.cs` (modify) | model | n/a | themselves | exact |
| `sources/Database/DatabaseContext.cs` (modify: DbSets + filtered unique indexes) | config | n/a | `DatabaseContext.cs` lines 84-88, 395-398 | exact |
| `sources/Database/Migrations/*_AddServerAutomation*` | migration | n/a | scaffold only (`dotnet ef migrations add`) | tool-generated |
| `sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs` | config | n/a | `sources/Api/Services/GameServerLifecycle/CostAlertOptions.cs` | exact |
| `sources/Api/Services/ServerAutomation/IServerAutomationSettingsService.cs` / `ServerAutomationSettingsService.cs` | service | request-response, CRUD | `sources/Api/Services/GameServerLifecycle/CostAlertSettingsService.cs` | exact |
| `sources/Api/Services/ServerAutomation/IServerAutomationEvaluator.cs` / `ServerAutomationEvaluator.cs` | service | batch (periodic) | `sources/Api/Services/GameServerLifecycle/CostAlertEvaluator.cs` | exact |
| `sources/Api/Services/ServerAutomation/IdleClock.cs`, `ScheduleCalculator.cs`, `PendingStopPlanner.cs` | utility (pure) | transform | `sources/Api/Services/GameServerLifecycle/CostAccrualCalculator.cs` | role-match |
| `sources/Api/Services/ServerAutomation/ServerAutomationProjector.cs` + decision records | utility | transform | `GameServerProjectionService.ProjectServerAsync` (line 822) | role-match |
| `sources/Api/HostedServices/ServerAutomationWorker.cs` | service (hosted) | batch loop | `sources/Api/HostedServices/CostAlertWorker.cs` | exact |
| `sources/Api/Controllers/GameServerAutomationController.cs` | controller | request-response | `sources/Api/Controllers/CostAlertSettingsController.cs` | exact |
| `sources/Api/DTO/ServerAutomationDto.cs` (+ `DTO/Updates/ServerAutomationUpdate.cs`, limits/status/pending/consent DTOs) | model (DTO) | n/a | `sources/Api/DTO/CostAlertSettingsDto.cs`, `DTO/Updates/CostAlertSettingsUpdate.cs` | exact |
| `sources/Api/DTO/GameServerDto.cs`, `LifecycleJobDto.cs`, `CostAlertDto.cs` (modify) | model (DTO) | n/a | themselves | exact |
| `GameServerLifecycleService.cs` (modify: shared enqueue core + `EnqueueAutomatedActionAsync`) | service | request-response | itself lines 302-585 | exact |
| `GameServerProjectionService.cs` (modify: `operationalKinds`, `OperationStatus`, `includeAutomation`) | service | read projection | itself lines 614-642, 772-792, 822-835 | exact |
| `FleetChangeInterceptor.cs` (modify: `Collect`) | middleware (EF interceptor) | event-driven | itself lines 230-268 | exact |
| `HostingControlPlaneServiceCollectionExtensions.cs` (modify) | config | n/a | itself lines 23-31 | exact |
| `HostingCostSliceService.cs` / `IHostingCostSliceService.cs` (+ `CanReserveInitialSliceAsync`) | service | read | itself lines 60-76 | exact |
| `CostAlertDeliveryProcessor.cs`, `CostAlertPublisher.cs`, `CostAlertService.cs`, `CostAlertDeliveryReasons.cs` (modify for `AutoStop`) | service | pub-sub / outbox | themselves | exact |
| `sources/Api/Services/Notifications/CostAlertMessageRenderer.cs` (+ `RenderAutoStop`), `Properties/Language.resx`, `Language.de.resx` | utility | transform | itself | exact |
| `sources/Api/Data/UatFleetScenarioSeeder.cs`, `scripts/Start-Uat.ps1` (modify) | config/seed | batch | itself (`Scenario` record, lines 106-137) | exact |
| `tests/Api.Tests/GameServerHosting/ServerAutomation*Tests.cs`, `IdleClockTests.cs`, `ScheduleCalculatorTests.cs`, ... + `ServerAutomationTestData.cs` | test | n/a | `CostAlertSettingsTests.cs`, `CostAlertEvaluatorTests.cs`, `ServerMetricsTestData.cs` | exact |
| `FleetChangeInterceptorTests.cs`, `GameServerProjectionServiceTests.cs` (extend) | test | n/a | themselves | exact |
| `sources/ReactApp/src/features/hosting/automationModel.ts` | utility (model) | transform/validation | `src/features/hosting/alertSettingsModel.ts` | exact |
| `sources/ReactApp/src/features/hosting/AutomationPanel.tsx` | component | request-response (draft/save) | `src/features/hosting/CostAlertSettingsPanel.tsx` | exact |
| `sources/ReactApp/src/features/hosting/useAutomationSettings.ts`, `useAutoStopNotice.ts` | hook | request-response / socket | `src/features/hosting/useCostAlerts.ts` | exact |
| `sources/ReactApp/src/components/server/AutoStopNotice.tsx` | component | display | `src/components/server/CostAlertBanner.tsx`, `PendingStartNotice.tsx` | exact |
| `sources/ReactApp/src/components/server/AutomationStatusList.tsx`, `ScheduleRuleFields.tsx`, `ScheduledStartConsentDialog.tsx`, `automation.css` | component | display/form | `CostAlertSettingsPanel.tsx` (rows), `CostConfirmDialog.tsx`, `cost-alerts.css` | role-match |
| `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` (modify: replace slot) | component | composition | itself lines 218-224 | exact |
| `sources/ReactApp/src/features/hosting/activityLabels.ts`, `types.ts`, `services.ts`, locales (modify) | utility | n/a | themselves | exact |
| `sources/ReactApp/tests/hosting/automation-*.test.ts(x)`, `tests/e2e/hosting-automation.spec.ts`, `tests/e2e/support/automationMocks.ts` | test | n/a | `tests/e2e/support/costMocks.ts`, existing cost-alert tests | exact |
| `sources/ReactApp/tests/uat/automation.spec.ts` | test (seeded-stack) | n/a | `tests/uat/cost-alerts.spec.ts` (UNTRACKED) | partial |

## Pattern Assignments

### `sources/Api/HostedServices/ServerAutomationWorker.cs` (hosted service, batch loop)

**Analog:** `sources/Api/HostedServices/CostAlertWorker.cs` (whole file, 71 lines) - copy almost verbatim.

```csharp
internal sealed class CostAlertWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertWorker> logger) : BackgroundService
{
    private readonly CostAlertOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<ICostAlertEvaluator>()
                    .EvaluateAsync(NodaSystemClock.Instance.GetCurrentInstant(), stoppingToken)
                    .ConfigureAwait(false);
                // Publishing happens after the evaluation transaction committed and is best effort
                var publisher = scope.ServiceProvider.GetRequiredService<ICostAlertPublisher>();
                await publisher.PublishRaisedAsync(result.RaisedAlerts, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Cost alert evaluation failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
            }
            try { await Task.Delay(_options.EvaluationInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
```
Keep the XML `<remarks>` stating "registered only while the hosting control plane is not paused". Publish raised
and dismissed AutoStop alerts after the pass (`PublishRaisedAsync` / dismissed counterpart).

**Registration** (`HostingControlPlaneServiceCollectionExtensions.cs` lines 23-31): add
`services.AddHostedService<ServerAutomationWorker>();` inside the existing `if (!state.IsPaused)` block, next to
`services.AddHostedService<CostAlertWorker>();`. Bind `ServerAutomationOptions` the same way `CostAlertOptions` is bound.

---

### `sources/Api/Services/ServerAutomation/ServerAutomationOptions.cs` (config)

**Analog:** `sources/Api/Services/GameServerLifecycle/CostAlertOptions.cs` lines 1-80.

```csharp
public sealed class CostAlertOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "Hosting:CostAlerts";

    /// <summary>Gets or sets how often the control plane evaluates thresholds against accrued cost.</summary>
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum number of thresholds one cost owner may hold.</summary>
    public int MaxThresholds { get; set; } = 10;
    ...
    /// <exception cref="InvalidOperationException">...</exception>
    public void Validate()
```
Use `SectionName = "Hosting:Automation"`; members `EvaluationInterval`, `WarningLead` (10 min), `StartGraceWindow`
(15 min), `IdleMinutesMin/Max` (5/1440), `MaxRuntimeHoursMin/Max` (1/168), `MaxSchedules` (8); `Validate()` throws
`InvalidOperationException`.

---

### `sources/Database/Hosting/ServerAutomationSettings.cs` (entity, optimistic concurrency)

**Analog:** `sources/Database/Hosting/CostAlertSetting.cs` (whole file, 75 lines).

```csharp
[Index(nameof(CostOwnerFk), IsUnique = true)]
public sealed class CostAlertSetting : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    [ForeignKey(nameof(CostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? CostOwner { get; set; }
    public Guid CostOwnerFk { get; set; }
    ...
    /// <summary>... revision counter that is incremented on every change, for optimistic concurrency ...</summary>
    public int Revision { get; set; }

    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }
    public Guid? UpdatedByFk { get; set; }

    public Instant TimeStampCreated { get; set; }
    public Instant TimeStampUpdated { get; set; }

    public ICollection<CostAlertThreshold> Thresholds { get; set; } = [];
}
```
Swap `CostOwnerFk` -> `GameServerFk` (unique), `Revision` -> `Version`, `Thresholds` -> `Schedules`
(`ICollection<ServerSchedule>`). `ServerAutomationState` copies the same header (unique `GameServerFk`) without
`Version`/`UpdatedBy`. `ServerSchedule` copies `CostAlertThreshold` (child with `SettingFk`-style FK). Store time of
day as `int MinuteOfDay`, never `LocalTime` (SQLite test context has no converter).

---

### `sources/Database/Hosting/AutomationPendingStop.cs`, `ServerScheduleRun.cs` (once-only records)

**Analog:** `sources/Database/Hosting/CostAlert.cs` lines 1-40.

```csharp
/// <remarks>
/// The unique index on the deployment and threshold amount makes the crossing once-only across API restarts,
/// duplicate evaluation passes and concurrent instances. ...
/// </remarks>
[Index(nameof(DeploymentFk), nameof(ThresholdAmount), IsUnique = true)]
[Index(nameof(CostOwnerFk), nameof(TimeStampCreated))]
public sealed class CostAlert : IPrimaryKey<Guid>, ITimeStampCreated
{
    [Key] public Guid PrimaryKey { get; set; }
    [ForeignKey(nameof(DeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? Deployment { get; set; }
    public Guid DeploymentFk { get; set; }
```
`ServerScheduleRun`: `[Index(nameof(ScheduleFk), nameof(OccurrenceAt), IsUnique = true)]` via attribute.
`AutomationPendingStop`: the partial unique index on `DeploymentFk WHERE Status = 0` needs the fluent API (next).

**Filtered index pattern** (`sources/Database/Hosting/LifecycleJob.cs` lines 17-18 and `DatabaseContext.cs` 395-398):
```csharp
public const string ActiveDeploymentIndexName = "IX_LifecycleJobs_DeploymentFk_Active";
public const string ActiveDeploymentIndexFilter = "\"Status\" IN (1, 2, 3)";
```
```csharp
entity.HasIndex(q => q.GameServerFk)
    .HasDatabaseName(GameServerDeployment.ActiveServerIndexName)
    .IsUnique()
    .HasFilter(GameServerDeployment.ActiveServerIndexFilter);
```
Add `PendingIndexName/PendingIndexFilter` constants on `AutomationPendingStop`; on `CostAlert` replace the
`[Index(DeploymentFk, ThresholdAmount, IsUnique)]` attribute with a fluent filtered index `"\"Kind\" = 0"` and add a
unique index on `PendingStopFk`. DbSets go next to `DatabaseContext.cs` lines 84-88.

**Migration:** scaffold with the AGENTS.md command only (`--output-dir Migrations`, name `AddServerAutomation`); do
not copy prior migrations.

---

### `ServerAutomationSettingsService.cs` (service, CRUD + concurrency)

**Analog:** `sources/Api/Services/GameServerLifecycle/CostAlertSettingsService.cs`.

**Header/DI** (lines 1-41):
```csharp
[Scoped<CostAlertSettingsService, ICostAlertSettingsService>]
internal sealed class CostAlertSettingsService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    ...
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertSettingsService> logger) : ICostAlertSettingsService
```
**Authorize then validate** (lines 80-104):
```csharp
ArgumentNullException.ThrowIfNull(update);
ArgumentNullException.ThrowIfNull(principal);
if (!await authorizationService.CanUseCostOwnerAsync(principal, costOwnerId, cancellationToken).ConfigureAwait(false))
    throw new HostingForbiddenException("The cost owner's alert settings may not be changed.");
var amounts = Validate(update, out var language);   // throws HostingValidationException
principal.TryGetUserId(out var userId);
var revision = await SaveAsync(...);
logger.LogInformation("Cost alert settings of cost owner {CostOwnerId} saved as revision {Revision} by user {UserId}.", ...);
```
For automation: `canManageAutomation = HasServerPermission(principal, serverId, Claims.ResourceBased.Server.Modify)
&& (admin || CanUseCostOwnerAsync(principal, server.CurrentCostOwnerFk))`. Zone check via
`DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)`.

**Claim-by-conditional-update** (lines ~170-255): first save requires `Version == 0` and inserts; later saves
```csharp
if (update.Revision != existing.Revision)
    throw new HostingConflictException("The alert settings changed; reload them and try again.");
var claimed = await dbContext.CostAlertSettings
    .Where(q => q.PrimaryKey == existing.PrimaryKey && q.Revision == update.Revision)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(q => q.Revision, q => q.Revision + 1)
        .SetProperty(q => q.UpdatedByFk, updatedBy)
        .SetProperty(q => q.TimeStampUpdated, now), cancellationToken)
    .ConfigureAwait(false);
if (claimed == 0) throw new HostingConflictException(...);
// then diff child rows: RemoveRange missing, Add new
...
catch (DbUpdateException)
{
    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
    dbContext.ChangeTracker.Clear();
    throw new HostingConflictException(...);
}
```
Only bump `Version` when normalized content differs (consent-only PUT keeps it). Reset `ServerSchedule.EffectiveFrom`
when action/days/time change; write audit `AutomationSettingsChanged` / `ScheduledStartsConfirmed` in the same
`SaveChanges`.

---

### `GameServerAutomationController.cs` (controller)

**Analog:** `sources/Api/Controllers/CostAlertSettingsController.cs` lines 13-108.

```csharp
[ApiController]
[Authorize]
[Route(Constants.Routes.CostOwners)]
public sealed class CostAlertSettingsController(ICostAlertSettingsService settingsService) : ControllerBase
{
    [HttpPut("{costOwnerId:guid}/alert-settings", Name = nameof(UpdateAlertSettingsAsync))]
    [ProducesResponseType<CostAlertSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAlertSettingsAsync([FromRoute] Guid costOwnerId,
        [FromBody] CostAlertSettingsUpdate update, CancellationToken cancellationToken)
    {
        try { return Ok(await settingsService.UpdateAsync(costOwnerId, update, User, cancellationToken).ConfigureAwait(false)); }
        catch (Exception ex) when (TryMapException(ex, out var result)) { return result; }
    }

    private static bool TryMapException(Exception exception, out IActionResult result)
    {
        result = exception switch
        {
            HostingNotFoundException   => new NotFoundObjectResult(new { error = exception.Message }),
            HostingForbiddenException  => new ForbidResult(),
            HostingValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            HostingConflictException   => new ConflictObjectResult(new { error = exception.Message }),
            _                          => null!,
        };
        return result is not null;
    }
}
```
Route `game-servers/{gameServerId:guid}/automation`; add `[ProducesResponseType(503)]` (pause middleware) and a
`fields` array to the 400 body for field-addressable errors.

---

### `ServerAutomationEvaluator.cs` (service, periodic batch)

**Analog:** `sources/Api/Services/GameServerLifecycle/CostAlertEvaluator.cs` lines 1-80.

```csharp
[Scoped<CostAlertEvaluator, ICostAlertEvaluator>]
internal sealed class CostAlertEvaluator(
    DatabaseContext dbContext, ICostAccrualService accrualService,
    ICostAlertRecipientResolver recipientResolver, IOptions<CostAlertOptions> options,
    ILogger<CostAlertEvaluator> logger) : ICostAlertEvaluator
{
    public async Task<CostAlertEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.GameServerDeployments.AsNoTracking()
            .Include(q => q.GameServer).Include(q => q.CostOwner)
            .Where(q => (q.TimeStampResolved == null && q.PossibleCost) || ...)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0) return new CostAlertEvaluationResult([], []);
```
Remarks to mirror: "inserted in one transaction before any side effect (persist intent first) ... unique index makes
the pass idempotent". Return a result record (`ServerAutomationEvaluationResult`, one file) carrying raised and
dismissed alerts for the worker to publish. Inject `IServerMetricsService` (call `EvaluateCurrentAsync(serverId, now, ct)`),
`ICostAlertRecipientResolver`, `IHostingAuditWriter`, `IGameServerLifecycleService`. Per-server try/catch.

**Pending-stop claim** - copy `CostAlertDeliveryProcessor.TryClaimAsync`:
```csharp
var affected = await dbContext.CostAlertDeliveries
    .Where(DuePredicate(now)).Where(q => q.PrimaryKey == deliveryId)
    .ExecuteUpdateAsync(setters => setters.SetProperty(q => q.Status, ECostAlertDeliveryStatus.Sending), cancellationToken);
return affected == 1;
```
Apply as `Status Pending -> Executed WHERE PrimaryKey = @id AND Status = Pending`; only the winner enqueues the Stop.

**Provider branch for locking** (`AutomaticCostStopService.cs` line 200 and `GameServerLifecycleService.cs` 320-325):
```csharp
=> dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
serverQuery = dbContext.GameServers.FromSqlInterpolated(
    $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {gameServerId} FOR UPDATE""");
```

**Audit with state, single SaveChanges** (`AutomaticCostStopService.TriggerAsync`, lines 18-170):
```csharp
dbContext.LifecycleJobs.Add(job);
auditWriter.Add(dbContext, EHostingAuditEventKind.Recovery, "...", serverId: deployment.GameServerFk,
    deploymentId: deployment.PrimaryKey, lifecycleJobId: job.PrimaryKey, costOwnerId: deployment.CostOwnerFk,
    details: "...", costImpact: ECostImpact.StopsCost);
await dbContext.SaveChangesAsync(cancellationToken);
```
Use `AutomaticStopRequested` (`StopsCost`), `ScheduledStartRequested` (`StartsCost`), actor null.

---

### `GameServerLifecycleService.cs` (modify: shared enqueue core)

**Source:** lines 302-585. Public entry point keeps the principal checks:
```csharp
if (!principal.TryGetUserId(out var actorId)
    || !authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.Operate))
    throw new HostingForbiddenException("Server Operate permission is required.");
...
if (!await authorizationService.CanUseCostOwnerAsync(principal, server.CurrentCostOwnerFk, cancellationToken)...)
    throw new HostingForbiddenException("The assigned cost owner cannot be used by this user.");   // line 355-360
```
Move everything from the transaction start (line 317) through `CommitAsync` (line ~583) into a private core taking
`Guid? actorId`, `ELifecycleJobTrigger trigger`, `int? triggerLimit`, and an "authorized" flag that skips the
`CanUseCostOwnerAsync` call. Keep the Stop branch unchanged:
```csharp
else if (kind is ELifecycleJobKind.Stop)
{
    if (deployment.Status is not EDeploymentStatus.Running)
        throw new HostingConflictException("Stop is available only while the deployment is running.");
    if (hasPendingAction)
        throw new HostingConflictException("Another lifecycle action is already pending.");
    deployment.IsProvisioningBlocked = true;
    await CancelPendingWorkAsync(deployment.PrimaryKey, now, false, cancellationToken).ConfigureAwait(false);
}
```
and set `Trigger`/`TriggerLimit` in the `new LifecycleJob { ... RequestedByFk = actorId, ... }` initializer
(lines ~550-567). The evaluator must call the core inside its own transaction, so let the core accept an existing
transaction or have the evaluator's claim run inside the core's transaction (planner decides; one transaction is
required for exactly-once). Expose `EnqueueAutomatedActionAsync` on `IGameServerLifecycleService` (internal use).

---

### `GameServerProjectionService.cs` (modify)

- `operationalKinds` array lines 614-642: append the seven new kinds.
- `OperationStatus` lines 772-792: add `AutomaticStopCancelled` and `ScheduledRunSkipped` to the `"warning"` arm;
  `AutomaticStopWarned`, `AutomaticStopRequested`, `ScheduledStartRequested` to `"pending"` or default per UI-SPEC.
- `ProjectServerAsync` (line 822) is called from list (194), detail (214) and notifications (550): add an
  `includeAutomation` parameter, true only at line 214. Reuse `pendingStatuses` (lines 828-835) for the idle
  "active job" check.

---

### `FleetChangeInterceptor.cs` (modify `Collect`, lines 230-268)

```csharp
foreach (var entry in context.ChangeTracker.Entries<LifecycleJob>())
{
    if (!IsRelevant(entry))
        continue;
    (serverIds ??= []).Add(entry.Entity.GameServerFk);
}
```
Add sibling loops for `ServerAutomationSettings`, `ServerSchedule` (optional) and `AutomationPendingStop`. Not
`ServerAutomationState`. Test beside `tests/Api.Tests/GameServerHosting/FleetChangeInterceptorTests.cs`.

---

### Alert pipeline extensions (`CostAlert*`, `CostAlertMessageRenderer.cs`, resx)

Extend in place: `CostAlertDeliveryProcessor.DeliveryFacts` gains `Kind/StopsAt/StopReason/StopLimit` and branches in
`Render`; `CostAlertMessageRenderer` gets `RenderAutoStop` next to `Render` reusing `SanitizeTitle`, UTC time format
and `CostAlertOptions.ServerLinkPathTemplate`; `CostAlertDeliveryReasons` gains `"stop-resolved"`;
`CostAlertService.ListAsync` filters out AutoStop alerts whose pending row is not `Pending`. Tests extend
`CostAlertMessageRendererTests.cs`, `CostAlertServiceTests.cs`, `CostAlertPublisherTests.cs`.

---

### `sources/Api/Data/UatFleetScenarioSeeder.cs` (modify)

**Pattern** (lines 106-137): add flags to the `Scenario` record and a new entry in `Scenarios`.
```csharp
private sealed record Scenario(string Title, EDeploymentStatus? Status, bool PossibleCost,
    string? FaultCode = null, string? FaultMessage = null, bool AccruedCost = false, bool Metrics = false);

new(MetricsScenarioTitle, EDeploymentStatus.Running, true, Metrics: true),
```
Add e.g. `Automation: true` with a running deployment, primary worker (`AddPrimaryWorker`, line 338) and zero-player
metrics, plus `ServerAutomationSettings` with idle 1 minute; `Start-Uat.ps1` sets `Hosting__Automation__IdleMinutesMin=1`
and `WarningLead` 1-2 min. The seeder is idempotent by title; keep that.

---

### Backend tests

**Analog:** `tests/Api.Tests/GameServerHosting/CostAlertSettingsTests.cs` lines 1-60.
```csharp
public sealed class CostAlertSettingsTests
{
    [Fact]
    public async Task OwnerWithoutRow_ReadsEmptyDefaults_AndCreatesNoRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await CreateController(store, OwnerPrincipal(seed))
                .GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None)).Value);
```
Helpers: `HostingTestStore.CreateAsync(IInterceptor[]?)` (line 38), `ServerMetricsTestData.AddPrimaryWorkerAsync`
(line 92) and `AddSnapshotAsync` (line 128) for idle signals. Evaluator tests follow `CostAlertEvaluatorTests.cs`
(explicit `Instant now`, repeat pass = no new rows). Restart test: new store scope/second evaluator instance on the
same SQLite connection.

---

### `sources/ReactApp/src/features/hosting/automationModel.ts` (pure model)

**Analog:** `src/features/hosting/alertSettingsModel.ts` (275 lines).
```ts
import type { CostAlertSettings } from "@/features/hosting/types";
export type AlertAmountError = "Required" | "Number" | "Precision" | "Min" | "Max";
export type AlertLimits = Pick<CostAlertSettings, "maxThresholds" | "maxThresholdAmount">;
export interface AlertDraft { rows: readonly string[]; discordEnabled: boolean; emailEnabled: boolean; }
export interface AlertDraftValidation { errors: readonly (AlertRowError | null)[]; overLimit: boolean; valid: boolean; }
```
Mirror: `AutomationDraft`, `validateAutomationDraft(draft, limits)` with limits from the API, `isAutomationDraftDirty`.
No time/stop calculation on the client.

### `sources/ReactApp/src/features/hosting/AutomationPanel.tsx` (draft/save panel)

**Analog:** `src/features/hosting/CostAlertSettingsPanel.tsx` imports (lines 1-28):
```ts
import { useMutation, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { CheckboxField, TextField } from "@/components/FormFields";
import { ConfirmDialog, StatePanel, StatusBadge } from "@/components/primitives";
import "@/components/server/sections.css";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { CostAlertSettingsConflictError, hostingQueryKeys, hostingService } from "@/features/hosting/services";
```
Reuse the shared `FormFields`, `ConfirmDialog` (consent dialog), `useLeaveGuard`, conflict error class pattern for 409
(add `AutomationSettingsConflictError` in `services.ts`), and the "write saved DTO into query key" save path.
Treat 503 as a save error.

### `SettingsSection.tsx` (modify, lines 218-224)
```tsx
<SlotPlaceholder variant="panel" slot="automation" title={t("Server_Settings_AutomationTitle")}>
  {t("Server_Settings_AutomationUnavailable")}
</SlotPlaceholder>
```
Replace with `<AutomationPanel server={server} ... />`; keep the locale key until Phase 69 cleanup.

### `AutoStopNotice.tsx` (shell notice)
**Analog:** `src/components/server/CostAlertBanner.tsx` (imports lines 1-22; `useCostAlerts`, `useDismissCostAlerts`,
`formatInstant`, `StatePanel`, `cost-alerts.css`) and the smaller `PendingStartNotice.tsx` (56 lines) for structure.

### `useAutomationSettings.ts`, `useAutoStopNotice.ts`
**Analog:** `src/features/hosting/useCostAlerts.ts` (200 lines) - query keys from `hostingQueryKeys`, live update via
existing socket frames; no `setInterval`/`refetchInterval` (parity guard).

### Frontend tests
Mocks: copy `sources/ReactApp/tests/e2e/support/costMocks.ts` into `automationMocks.ts`; extend
`tests/hosting/parity-matrix.test.ts`, `tests/e2e/hosting-a11y.spec.ts`, `cost-alert-menu.test.tsx`.

## Shared Patterns

### Exceptions to HTTP mapping
**Source:** `CostAlertSettingsController.TryMapException` (lines 97-108). Apply to the automation controller.
Services throw `HostingForbiddenException`, `HostingValidationException`, `HostingConflictException`,
`HostingNotFoundException` (all in `sources/Api/Services/GameServerLifecycle/`).

### Time
`NodaSystemClock.Instance.GetCurrentInstant()` (alias in `sources/Api/GlobalUsings.cs:4`) only in the worker and
in user-driven saves; evaluator and pure functions take `Instant now`.

### DI
`[Scoped<Impl, IInterface>]` attribute from `X39.Util.DependencyInjection.Attributes` on every service; interface in
its own file.

### Logging
Structured templates, sanitize failures with `HostingFailureSanitizer.Sanitize(ex.Message)`; log ids only.

### Persist-intent + audit in one SaveChanges
`auditWriter.Add(dbContext, kind, summary, actorId, serverId, deploymentId, jobId, costOwnerId:, costImpact:)` before
the single `SaveChangesAsync` + `CommitAsync` (GameServerLifecycleService lines ~570-583).

### SQLite test caveats
No decimals in predicates (compare in memory), `Instant` is fine in SQL, no `LocalTime`, provider branch for `FOR UPDATE`.

### XML docs / enums / one type per file
AGENTS.md: every type and member documented, `E`-prefixed enums, one top-level type per file (decision records each in
their own file).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `sources/Api/Services/ServerAutomation/ScheduleCalculator.cs` | utility | transform | No zone-aware recurrence exists; use RESEARCH.md NodaTime `LenientResolver` example (`zone.ResolveLocal(local, Resolvers.LenientResolver)`) |
| `sources/ReactApp/tests/uat/automation.spec.ts` | test | seeded-stack | Analogs `tests/uat/cost-alerts.spec.ts`, `tests/uat/support/uat.ts` and `playwright.uat.config.ts` are UNTRACKED (git status `??`). Follow their style, but the planner should ensure they are committed first (or as part of this phase) |
| UAT worker emulator stop completion (`UatWorkerChannelEmulator.cs`) | service | event-driven | Emulator drains commands; whether to extend it is Open Question 3 in RESEARCH.md |

## Metadata

**Analog search scope:** `sources/Api/{HostedServices,Services/GameServerLifecycle,Services/ServerMetrics,Controllers,Data}`,
`sources/Database/{Hosting,DatabaseContext.cs}`, `sources/HostingContracts`, `sources/ReactApp/src/{features/hosting,components/server}`,
`sources/ReactApp/tests`, `tests/Api.Tests/GameServerHosting`
**Files scanned:** ~25 read/grepped
**Pattern extraction date:** 2026-11-22
