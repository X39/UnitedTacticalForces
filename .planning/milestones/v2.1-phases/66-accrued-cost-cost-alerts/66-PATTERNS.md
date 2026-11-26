# Phase 66: Accrued Cost & Cost Alerts - Pattern Map

**Mapped:** 2026-11-21
**Files analyzed:** 27 (new/modified groups)
**Analogs found:** 25 / 27 (all analog paths verified git-tracked)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `sources/Database/Hosting/CostAlertSetting.cs`, `CostAlertThreshold.cs` | model | CRUD | `sources/Database/Hosting/HostingFinancePolicy.cs` / `HostingCostLedgerEntry.cs` | role-match |
| `sources/Database/Hosting/CostAlert.cs`, `CostAlertDismissal.cs` | model | CRUD (unique-once) | `HostingCostLedgerEntry.cs` (unique composite `[Index]`) | role-match |
| `sources/Database/Hosting/CostAlertDelivery.cs` (+ `ECostAlertChannel`, `ECostAlertDeliveryStatus`) | model (outbox) | event-driven / batch | `sources/Database/Hosting/LifecycleJob.cs` (lease, attempt, next-attempt) | exact |
| `sources/Database/Authority/User.cs` (+`Locale`) and `DatabaseContext.cs` | model/config | CRUD | existing `User.EMail` (line 42), ledger `modelBuilder` block (DatabaseContext 636+) | exact |
| EF migration (scaffolded) | migration | batch | AGENTS.md command, never hand-written | n/a |
| `sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs`, `CostAlertRaisedMessage.cs`, `CostAlertDismissedMessage.cs` | contract | pub-sub | `GameServerFleetChangedMessage.cs` (+ `EventChangedMessage.cs` for `DateTimeOffset`) | exact |
| `Api/Services/GameServerLifecycle/CostAccrualCalculator.cs` | utility | transform | `HostingCostSliceService.CalculateAmount` (lines 380-385) | role-match |
| `Api/HostedServices/CostAlertWorker.cs` | hosted worker | batch/polling loop | `HostingCostReconciliationWorker.cs` | exact |
| `Api/HostedServices/CostAlertDispatcher.cs` | hosted worker | event-driven outbox | `HostingCostReconciliationWorker.cs` + `LifecycleJobQueue.cs` lease | role-match |
| `Api/Services/GameServerLifecycle/CostAlertEvaluator.cs`, `CostAlertService.cs`, `CostAlertSettingsService.cs`, `CostAlertOptions.cs` | service/options | CRUD + tx | `HostingCostSliceService.cs`, `HostingFinanceOptions.cs`, `HostingConflictException` | role-match |
| `Api/Services/GameServerLifecycle/CostAlertAudienceSource.cs` (+ interface) | service | request-response | `FleetAudienceSource.cs` / `IFleetAudienceSource.cs` | exact |
| `HostingAuthorizationService.cs` + `IHostingAuthorizationService.cs` (+`GetCostOwnerViewAudienceAsync`) | service | request-response | `CanViewCostOwnerAsync` (lines 77-93) | exact |
| `Api/Services/GameServerLifecycle/CostAlertPublisher` (value/alert frames) | service | pub-sub | `FleetChangeNotifier.DeliverBatchAsync/SendAsync` (194-253) | exact |
| `Api/Services/Notifications/IEmailSender.cs`, `SmtpEmailSender.cs`, `EmailOptions.cs` | service | request-response | `HostingFinanceOptions.cs` (options+Validate) | partial |
| `Api/Services/Notifications/IDiscordDirectMessageSender.cs` + `DiscordBot` DM method | service | request-response | `DiscordBot.WithDiscordAsync` (line 220) | role-match |
| `Api/Services/Notifications/CostAlertMessageRenderer.cs` + `Language*.resx` keys | utility | transform | `DiscordBot.cs` lines 187-203 (`Language.ResourceManager.GetString(..., cultureInfo)`) | exact |
| `Api/Controllers/CostAlertsController.cs`, accrued-cost + alert-settings actions on `GameServersController.cs`/`CostOwnersController` | controller | request-response | `GameServersController.cs` (409 mapping 458-460) | exact |
| `Api/DTO/AccruedCostDto.cs`, `CostAlertDto.cs`, `CostAlertSettingsDto.cs` + payloads | DTO | request-response | `Api/DTO/Payloads/*`, `Api/DTO/Plain*Dto.cs` | role-match |
| `HostingControlPlaneServiceCollectionExtensions.cs` (register 2 hosted services) | config | n/a | same file lines 22-28 | exact |
| `FleetLiveUpdatesProvider.tsx` (frame registry) | provider | pub-sub | itself, lines 55-95 | exact |
| `CostStrip.tsx` (accrued row) | component | request-response | itself, lines 55-62 | exact |
| `components/server/AccruedCost.tsx`, `CostAlertBanner.tsx`, `CostAlertMenuItem.tsx` | component | pub-sub | `CostStrip.tsx`, `FleetLiveNotice.tsx`, `CostConfirmationProvider.tsx` | role-match |
| `features/hosting/accruedModel.ts`, `alertSettingsModel.ts` | utility | transform | `features/hosting/model.ts` (`formatMoney`), `costConfirmationModel.ts` | role-match |
| `features/hosting/useAccruedLive.ts`, `useCostAlerts.ts` | hook | pub-sub | `useWorkspaceLiveUpdates.ts`, `useFleetQuery.ts` | role-match |
| `features/hosting/CostAlertSettingsPanel.tsx`, `CostAlertsPage.tsx` | component/page | CRUD | `HostingFinanceSettingsPage.tsx` | role-match |
| `components/ProfileMenu.tsx` (alert rows) | component | request-response | itself (notifications 124-350; poll at 23, 98) | exact |
| Tests (`tests/Api.Tests/GameServerHosting/CostAlert*Tests.cs`, `CostAccrualCalculatorTests.cs`) | test | n/a | `FleetAudienceSourceTests.cs`, `FleetChangeNotifierTests.cs`, `HostingTestStore/Seed/Data.cs` | exact |
| `sources/AppHost/Program.cs` (optional Mailpit under `Uat:Enabled`) | config | n/a | lines 12-25 | role-match |

## Pattern Assignments

### `CostAlertWorker.cs` and `CostAlertDispatcher.cs` (hosted worker, polling loop)

**Analog:** `sources/Api/HostedServices/HostingCostReconciliationWorker.cs` (lines 9-49). Copy the whole shape: primary-ctor with `IServiceScopeFactory`, `IOptions<T>`, `ILogger<T>`; validate options once; per iteration open a scope, resolve the scoped service, swallow-and-log non-cancellation failures with `HostingFailureSanitizer.Sanitize`, then `Task.Delay` with cancellation break.

```csharp
internal sealed class HostingCostReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<HostingFinanceOptions> options,
    ILogger<HostingCostReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IHostingCostReconciler>()
                    .ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("... failed and will retry: {Failure}", HostingFailureSanitizer.Sanitize(ex.Message));
            }
            try { await Task.Delay(_options.ReconciliationInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
```

**Registration:** add both inside the `!state.IsPaused` block of `HostingControlPlaneServiceCollectionExtensions.cs` lines 22-28 (`services.AddHostedService<...>()`), matching assumption A9.

---

### `CostAlertDelivery.cs` (outbox entity)

**Analog:** `sources/Database/Hosting/LifecycleJob.cs` lines 59-71 for lease and retry columns:

```csharp
public int Attempt { get; set; }
public int MaxAttempts { get; set; } = 3;
[MaxLength(256)] public string? LeaseOwner { get; set; }
public Instant? LeaseExpires { get; set; }
public Instant? TimeStampNextAttempt { get; set; }
public Instant TimeStampCreated { get; set; }
```
Use `Instant` for DB columns, `[Index(...)]` attributes on the class (see `LifecycleJob.cs` lines 13-14 and `HostingCostLedgerEntry.cs` lines 13-14: `[Index(nameof(DeploymentFk), nameof(SliceStartsAt), IsUnique = true)]`). Use the same for `CostAlert` unique `(DeploymentFk, ThresholdAmount)` and delivery unique `(CostAlertFk, Channel, RecipientUserFk)`. Entity conventions (`HostingCostLedgerEntry.cs`): `[Key] Guid PrimaryKey`, `IPrimaryKey<Guid>, ITimeStampCreated`, `[ForeignKey(nameof(XFk))] [DeleteBehavior(DeleteBehavior.Restrict)]` nav plus `Guid XFk`, `[Column(TypeName = "numeric(18,4)")]` for money, `[MaxLength(3)] string Currency = "EUR"`. Enums named `E...` and in `sources/Database/Hosting` or Hosting contracts (see `EHostingLedgerEntryKind`). Register the `DbSet` and `modelBuilder.Entity<...>` block next to `HostingCostLedgerEntry` (`DatabaseContext.cs` line 83 and 636+, `HasOne/WithMany/HasForeignKey/OnDelete(Restrict)`). Do not use Postgres arrays (SQLite tests).

---

### `CostAccrualCalculator.cs` (pure static transform)

**Analog:** `HostingCostSliceService.cs` `CalculateAmount` (lines 380-385) and in-memory summing in `GetOtherReservationTotalAsync` (277-292); decimal rounded to 4. Use the formula in RESEARCH "Accrued cost calculation" and the "Slice-prorated accrual" code example verbatim (prorate every slice; freeze `asOf` at `TimeStampResolved` when `!PossibleCost`). Fetch slice rows then sum in memory (SQLite cannot aggregate decimals).

---

### `WebSocketContracts/Hosting/*Message.cs` (wire messages)

**Analog:** `sources/WebSocketContracts/Hosting/GameServerFleetChangedMessage.cs`.

```csharp
namespace X39.UnitedTacticalForces.WebSockets.Contracts.Hosting;
/// <summary>...</summary>
public sealed class GameServerFleetChangedMessage
{
    /// <summary>...</summary>
    /// <value>...</value>
    public required Guid ServerId { get; init; }
}
```
Sealed class, `required ... { get; init; }`, XML doc with `<value>`, file per type. Use `DateTimeOffset` (not `Instant`) for as-of and timestamps (`EventChangedMessage.cs`, UpdateStreamService has no NodaTime converter). Update the stale remark that the stream "never discloses costs" for the new messages.

---

### `CostAlertAudienceSource` and `GetCostOwnerViewAudienceAsync`

**Analog:** `FleetAudienceSource.cs` (singleton bridge that opens one scope per call, avoids a DI cycle):

```csharp
[Singleton<FleetAudienceSource, IFleetAudienceSource>]
internal sealed class FleetAudienceSource(IServiceScopeFactory scopeFactory) : IFleetAudienceSource
{
    public async Task<...> GetViewersAsync(IReadOnlyCollection<Guid> gameServerIds, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IHostingAuthorizationService>();
        return await authorizationService.GetServerViewAudienceAsync(gameServerIds, ct).ConfigureAwait(false);
    }
}
```
**Rule to mirror** (`HostingAuthorizationService.cs` 78-93): hosting admin OR `Claims.Finance.All/ViewAll` empty claim OR `Claims.ResourceBased.CostOwner.View` claim with owner id OR `CostOwners.UserFk == userId`. The audience method must produce the same set and be intersected with the server view audience; add a drift test next to the existing audience tests.

---

### Frame publisher (value and alert frames)

**Analog:** `FleetChangeNotifier.cs` lines 185-253: resolve audience once per batch, fail closed with a single `LogWarning` on audience read failure, swallow `OperationCanceledException` when stopping, then per recipient send via `_updateStreamService.SendUpdateAsync(FleetChangeStreamPaths.ForUser(userId), new XMessage{...})` wrapped in try/catch with `LogWarning` (never throw). Publish after the evaluation transaction commits.

---

### `CostAlertEvaluator` (crossing transaction), settings service (409)

**Analogs:** `HostingCostSliceService.cs` (persist intent in a transaction before side effects, guard raw SQL with its `IsPostgreSql` check; ledger unique index). Use RESEARCH Pattern 1 example (catch the unique violation as idempotent). Conflict: throw `HostingConflictException` (mapped to 409 at `GameServersController.cs` 458-460); validation: `HostingValidationException`; not found/forbidden: `HostingNotFoundException`/`HostingForbiddenException` (return 404 for unauthorized alert ids). Options class: copy `HostingFinanceOptions` (`const string SectionName`, defaults, `Validate()` throwing `InvalidOperationException`), e.g. `"Hosting:CostAlerts"` and new `"Email:Smtp"` (never log values).

---

### `DiscordBot` DM method and `IDiscordDirectMessageSender`

**Analog:** `sources/Api/HostedServices/DiscordBot.cs` line 220-225:

```csharp
public async Task WithDiscordAsync(Func<DiscordSocketClient, Task> action)
{
    if (_discordSocketClient.ConnectionState is ConnectionState.Connected)
        await action(_discordSocketClient)...
```
This silently no-ops when disconnected. Add a sibling `TrySendDirectMessageAsync(...)` returning an `EDirectMessageOutcome` (RESEARCH Pattern 2 example) using `ConnectionState`, never `WithDiscordAsync`, and wrap in an interface so the dispatcher test can fake it. `DiscordBot` is registered as a hosted service, so inject the sender via an adapter rather than `IHostedService`.

---

### `CostAlertMessageRenderer.cs` (localized text)

**Analog:** `DiscordBot.cs` lines 187-203:

```csharp
Language.ResourceManager.GetString(nameof(Language.DiscordCommand_TeamSpeak_IpAddress), cultureInfo)
```
Add `CostAlert_*` keys to `Language.resx` and `Language.de.resx`, do not edit `Language.Designer.cs` (generated); use string constants in one handwritten class for key names (RESEARCH Pitfall 7). Format amounts with the user's `CultureInfo` from `User.Locale`.

---

### Controllers and DTOs

**Analog:** `sources/Api/Controllers/GameServersController.cs` (authorization via `IHostingAuthorizationService`, `ProducesResponseType`, domain exception to HTTP mapping at 458-460, `ILogger<T>`). DTO naming: `*Dto`, `*Payload` (create/update), return DTOs never EF entities. Regenerate OpenAPI snapshot and `src/generated/api.ts` (`npm run contracts:check`).

---

### React: `FleetLiveUpdatesProvider.tsx` (frame registry)

**Analog:** same file lines 57-95. It owns the single per-user socket, dispatches by `frame.type`, has a coalescer and a `subscribe` listener registry. Extend `onMessage` with typed handlers for the three new frame types that call `queryClient.setQueryData(...)`; leave the id-only fleet branch intact; guard accrued writes by `asOf`; on fleet signal also invalidate `accrued(serverId)`. Type string is `typeof(T).FullName` of the C# message.

### React: `CostStrip.tsx` (accrued row)

**Analog:** same file lines 55-62, replace the `SlotPlaceholder` with `<AccruedCost serverId=... />`. Row layout stays `<div className="cost-strip__row"><dt>..</dt><dd>..</dd></div>`; use `formatMoney` from `@/features/hosting/model`; text via `t("Server_Workspace_Cost_*")` and add keys to `en.json` and `de.json` (locales parity gate). No `setInterval`/`refetchInterval` in the new files (note: `ProfileMenu.tsx` lines 23 and 98 keep the existing poll for non-cost notifications; alerts rows come from the cache written by frames).

### React: banner action

Use `useCostConfirmation().request(...)` (`components/server/useCostConfirmation.ts`, `CostConfirmationProvider.tsx`, `costConfirmationModel.ts`) as the "Shut down and stop billing" link target.

---

### Tests

**Analog:** `tests/Api.Tests/GameServerHosting/FleetAudienceSourceTests.cs` (xUnit `Fact`, XML docs on class and test, `BuildProvider` with recording fakes) and `FleetChangeNotifierTests.cs`; DB-backed tests use `HostingTestStore.CreateAsync()` (SQLite in-memory + `EnsureCreatedAsync`), `HostingTestData.cs`/`HostingTestSeed.cs` (extend, do not fork). Namespace `Api.Tests.GameServerHosting`. React tests under `sources/ReactApp/tests/hosting/` per RESEARCH Wave 0.

## Shared Patterns

### Singleton-from-scoped bridge (avoid DI cycles)
**Source:** `FleetAudienceSource.cs`. **Apply to:** any singleton publisher/audience reader that needs scoped services. Register with `[Singleton<Impl, IFace>]` attribute (`X39.Util.DependencyInjection.Attributes`); scoped services use `[Scoped<...>]`.

### Failure logging/sanitizing
**Source:** `HostingFailureSanitizer.Sanitize(ex.Message)`, structured `{Placeholders}`. **Apply to:** worker, dispatcher, adapters. Never log SMTP creds, tokens, or recipient addresses.

### Persist intent before side effect, best-effort publish after commit
**Source:** `HostingCostSliceService.cs`, `LifecycleJobQueue.cs` (lease). **Apply to:** evaluator (crossing plus deliveries in one tx), dispatcher (claim `Sending` + lease and save before sending).

### Fail-closed audience
**Source:** `FleetChangeNotifier.cs` 199-218. **Apply to:** value and alert frame publisher.

### XML docs / one-type-per-file / `E` enums
**Source:** AGENTS.md. **Apply to:** all handwritten C#. EF migration only via `dotnet ef migrations add` (command in AGENTS.md); update global `dotnet-ef` to 10.0.9 first.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `SmtpEmailSender.cs` / `IEmailSender.cs` | service | request-response | No email/SMTP infrastructure exists; use RESEARCH MailKit example (human-verify checkpoint for the new package) plus `HostingFinanceOptions` for options shape |
| Optimistic-concurrency `Revision` on `CostAlertSetting` | model | CRUD | No concurrency-token pattern in repo; use RESEARCH Pattern 5 with `HostingConflictException` |

## Metadata

**Analog search scope:** `sources/Api/HostedServices`, `sources/Api/Services/GameServerLifecycle`, `sources/Database/Hosting`, `sources/WebSocketContracts/Hosting`, `sources/ReactApp/src/{components/server,features/hosting}`, `tests/Api.Tests/GameServerHosting`
**Files scanned:** about 20 read or grepped
**Pattern extraction date:** 2026-11-21
