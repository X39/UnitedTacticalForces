---
phase: 66-accrued-cost-cost-alerts
reviewed: 2026-11-22T00:00:00Z
depth: standard
files_reviewed: 64
files_reviewed_list:
  - sources/Api/Controllers/CostAlertsController.cs
  - sources/Api/DTO/AccruedCostDto.cs
  - sources/Api/DTO/Payloads/CostAlertDismissAllPayload.cs
  - sources/Api/Data/UatFleetScenarioSeeder.cs
  - sources/Api/Services/GameServerLifecycle/AccruedCostResult.cs
  - sources/Api/Services/GameServerLifecycle/AccruedCostSlice.cs
  - sources/Api/Services/GameServerLifecycle/AccruedCostUnavailableReasons.cs
  - sources/Api/Services/GameServerLifecycle/CostAccrualCalculator.cs
  - sources/Api/Services/GameServerLifecycle/CostAccrualService.cs
  - sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs
  - sources/Api/Services/GameServerLifecycle/CostAlertService.cs
  - sources/Api/Services/GameServerLifecycle/ICostAlertService.cs
  - sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs
  - sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs
  - sources/Api/Services/HostingRuntime/RuntimeBillingUnit.cs
  - sources/Api/Services/HostingRuntime/RuntimeProviderCapabilities.cs
  - sources/ReactApp/openapi/utf-api.json
  - sources/ReactApp/src/components/server/AccruedCost.tsx
  - sources/ReactApp/src/components/server/CostAlertBanner.tsx
  - sources/ReactApp/src/components/server/CostAlertMenuItem.tsx
  - sources/ReactApp/src/components/server/cost-alerts.css
  - sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx
  - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
  - sources/ReactApp/src/features/hosting/accruedModel.ts
  - sources/ReactApp/src/features/hosting/alertSettingsModel.ts
  - sources/ReactApp/src/features/hosting/cost-alert-settings.css
  - sources/ReactApp/src/features/hosting/costFrames.ts
  - sources/ReactApp/src/features/hosting/services.ts
  - sources/ReactApp/src/features/hosting/useCostAlerts.ts
  - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
  - sources/ReactApp/src/generated/api.ts
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/styles/tokens.css
  - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
  - sources/ReactApp/tests/e2e/support/fleetStream.ts
  - sources/ReactApp/tests/hosting/accrued-cost.test.tsx
  - sources/ReactApp/tests/hosting/accrued-model.test.ts
  - sources/ReactApp/tests/hosting/alert-settings-model.test.ts
  - sources/ReactApp/tests/hosting/contract-shape.test.ts
  - sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx
  - sources/ReactApp/tests/hosting/cost-alert-menu.test.tsx
  - sources/ReactApp/tests/hosting/cost-alert-persistence.test.tsx
  - sources/ReactApp/tests/hosting/cost-alert-services.test.ts
  - sources/ReactApp/tests/hosting/cost-alert-settings.test.tsx
  - sources/ReactApp/tests/hosting/cost-alerts-page.test.tsx
  - sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx
  - sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx
  - sources/ReactApp/tests/hosting/cost-frames.test.ts
  - sources/ReactApp/tests/hosting/cost-strip.test.tsx
  - sources/ReactApp/tests/hosting/finance-pages.test.tsx
  - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
  - sources/ReactApp/tests/hosting/fleet-page.test.tsx
  - sources/ReactApp/tests/hosting/live-badge.test.tsx
  - sources/ReactApp/tests/locales/server-keys.test.ts
  - sources/ReactApp/tests/mocks/react-icons.tsx
  - sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs
  - tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs
  - tests/Api.Tests/GameServerHosting/CostAccrualCalculatorTests.cs
  - tests/Api.Tests/GameServerHosting/CostAlertEvaluatorTests.cs
  - tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs
  - tests/Api.Tests/GameServerHosting/CostAlertServiceTests.cs
  - tests/Api.Tests/GameServerHosting/HetznerRuntimeProviderTests.cs
  - tests/Api.Tests/GameServerHosting/HostingTestData.cs
  - tests/Api.Tests/GameServerHosting/RuntimeBillingUnitTests.cs
  - tests/Api.Tests/GameServerHosting/TestRuntimeProvider.cs
  - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs
findings:
  critical: 0
  warning: 3
  info: 6
  total: 9
  necessity: 0
status: issues_found
---

# Phase 66: Code Review Report (incremental, gap-closure plans 66-13..66-18)

**Reviewed:** 2026-11-22
**Depth:** standard
**Files Reviewed:** 64 (the generated `api.ts` and `utf-api.json` were checked only for contract agreement with the C# DTOs; CSS, locale JSON and the large test files were skimmed, with the logic-bearing tests read in detail where they pin new behaviour)
**Status:** issues_found

## Summary

This run is incremental: it covers the started-hour billing unit (66-13), the id-scoped dismiss-all (66-14/15), alert persistence until dismissal, the settings and banner/menu redesign, the billing line (66-16) and the dismissal toasts (66-17/18), plus the clock pinning in commit ea759b42.

The billing-unit work is sound. I traced `RuntimeBillingUnit.BilledThrough` and `CostAccrualCalculator.Calculate` by hand for the UAT seed: at the seeding instant the billed-through instant is anchor + 2 h, and the proration gives 1.3 * 120/130 = 1.20 EUR. Five minutes later the third hour starts, billed-through is anchor + 3 h, and the latest slice's rate prices the 50 minutes beyond its end, giving 1.30 + 0.50 = 1.80 EUR. This matches the seeder constants and the seeder test. BigInteger arithmetic avoids overflow. The `hidden` mask correctly nulls `BillingUnitSeconds` and `BilledThrough` together with the amount in both the REST DTO and the frames. The id-scoped dismissal is authorized by cost-owner visibility, ignores foreign and unknown ids without a signal, and is idempotent. I found no security defect.

No blocker was found. Three warnings concern the dismiss-all path and alert loading. The Info items are duplication, documentation and test-robustness issues.

Necessity assessment (necessity-review.md not loaded for this incremental pass): no necessity findings.

## Prior review

The previous report is `66-REVIEW.md` from 2026-11-21 (commit 58b7af7c, the diff base of this run). Status of each prior finding:

**Resolved**
- IN-05 (private-use character literal in `CostAlertBanner`): the banner was rewritten in 66-17 and `nameToken` no longer exists anywhere in `src`.

**Superseded:** none.

**Still present.** The code of every file below is unchanged since 58b7af7c (`git diff 58b7af7c HEAD` is empty for them), except where noted. These files are outside this run's scope and were not re-read; the status comes from the empty diff.
- WR-01 (stale `now` in `CostAlertDeliveryProcessor`)
- WR-02 (unbounded WebSocket send in `UpdateStreamService`)
- WR-03 (`CostAlertEvaluator` candidate filter and `IsCosting` ignore `PossibleCost` after resolution)
- WR-04 (swallowed `dispatch-error` in `CostAlertDeliveryProcessor`)
- WR-05 (undocumented members). New examples are listed in IN-06 below.
- WR-06 (every `DbUpdateException` reported as a revision conflict in `CostAlertSettingsService`)
- IN-01, IN-02, IN-03, IN-08, IN-09 (all in unchanged files)
- IN-04: `AccruedCostDto.Currency` still defaults to `"EUR"` and `CostAccrualService` still never sets it (`sources/Api/DTO/AccruedCostDto.cs:25`)
- IN-06: two `formatAlertAmount` functions. `CostAlertSettingsPanel.tsx:15` still imports the editor variant from `alertSettingsModel`, while `CostAlertBanner.tsx:16` imports the money variant from `costAlertFormat`.
- IN-07: `CostAlertSettingsPanel.tsx:171` still calls `invalidateQueries({ queryKey: hostingQueryKeys.fleet })` without `exact: true`.

These carried-over items are not re-counted in the frontmatter totals, which cover only findings new in this run.

Known and already triaged, not re-reported: `CostAlertBanner` hides the cost-stop button once the session ended (`CostAlertBanner.tsx:145-147`).

## Warnings

### WR-01: A concurrent partial dismissal turns Dismiss all into a 500, although the end state is reachable

**File:** `sources/Api/Services/GameServerLifecycle/CostAlertService.cs:186-197`
**Issue:** `DismissAlertsAsync` saves one dismissal per alert in a single `SaveChangesAsync`. If a concurrent request has already stored a dismissal for some, but not all, of the ids (the single-alert `DismissAsync`, a second tab, or the profile menu row racing the banner's Dismiss all), the unique index rejects the whole batch. The catch block then calls `AllDismissedAsync`, which returns `false` because the other ids were never stored, and the original `DbUpdateException` is rethrown. The controller does not map it, so the client gets a 500. The alerts the request was meant to dismiss stay undismissed and the UI shows "dismiss failed". Before the id-scoped batch existed, a single-row race was fully covered by the all-or-nothing check. The new batch widens the window to partial overlap. The banner's `busy` guard only covers one hook instance, not the menu rows or other tabs.
**Fix:** After a unique violation, re-read the alerts that are still undismissed and retry the save once for only those.
```csharp
catch (DbUpdateException)
{
    foreach (var dismissal in added)
        dbContext.Entry(dismissal).State = EntityState.Detached;
    var stillOpen = await UndismissedAsync(idArray, userId, cancellationToken);
    if (stillOpen.Length == 0) return;            // concurrent request finished the job
    if (!retried) { retried = true; /* rebuild rows for stillOpen and save again */ }
    else throw;
}
```
Publish only the ids this request actually stored.

### WR-02: The client can send more than 50 ids, after which Dismiss all fails with 400 on every attempt

**File:** `sources/ReactApp/src/features/hosting/useCostAlerts.ts:150-165`, `sources/ReactApp/src/features/hosting/costAlertFormat.ts:63-70`, `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:234-239`
**Issue:** The API accepts 1 to 50 ids (`CostAlertService.cs:109`, `CostAlertDismissAllPayload`). The REST list returns at most 50 alerts across all servers, so the cache starts at or below the cap. Pushed `CostAlertRaisedMessage` frames are inserted with `insertAlert`, which has no cap. Once a viewer's cache holds more than 50 alerts for one server, `dismissAll(serverId)` sends every cached id of that server, the API answers 400, and the banner shows "dismiss failed" again on every click. Alerts accrue per deployment and per threshold (up to 10 thresholds per session), so six undismissed sessions of one server reach 60 alerts. The same code path would also send an empty list, and fail the same way, if the cache held no alerts of the server when the click is handled.
**Fix:** Send at most 50 ids per request and loop until the server's cached alerts are gone, or slice to the newest 50 and let the toast count reflect what was dismissed.
```ts
const ids = dismissed.map((a) => a.id);
for (let i = 0; i < ids.length; i += 50) {
  await hostingService.dismissServerCostAlerts(serverId, ids.slice(i, i + 50));
}
```
Also skip the request when `ids.length === 0`.

### WR-03: A failed first load of the alerts is not retried when the socket opens

**File:** `sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:276-280`, `sources/ReactApp/src/features/hosting/useCostAlerts.ts:46-53`
**Issue:** `useCostAlerts` uses `retry: false` and `staleTime: Infinity`, and the documented recovery for a missed load or frame is the refetch in `onOpen`. `onOpen` only invalidates when `alertsState.dataUpdatedAt > 0`. A query whose first fetch failed (for example the API restarting while the page loads, which the file's own comments name) has no data and `dataUpdatedAt === 0`, so neither the first open nor a reconnect refetches it. The surfaces render nothing while `loaded` is false, so the viewer sees no banner and no menu rows. Recovery then depends on an observer remounting or on the manual `reconnectNow`. For a cost-control notification, a transient failure at page load silently hides the alerts that the viewer has not dismissed.
**Fix:** Refetch on open whenever the query exists and has no data or is in error, not only when it has data.
```ts
const alertsState = queryClient.getQueryState(hostingQueryKeys.costAlerts);
if (alertsState && (alertsState.dataUpdatedAt === 0 || alertsState.dataUpdatedAt < Date.now())) {
  invalidateCostAlerts(queryClient);
}
```

## Info

### IN-01: Success side effects run inside the `try`, so a failing toast reports a failed dismissal

**File:** `sources/ReactApp/src/features/hosting/useCostAlerts.ts:116-130`
**Issue:** `markCostAlertsDismissed`, `setQueryData`, `confirmation()` and `showToast` all run in the same `try` as `send()`. If `confirmation()` or `showToast` throws (a missing translation callback, a toast provider fault), the `catch` sets `failed` to `true` and returns `false`, even though the API already stored the dismissal and the cache was already filtered. The banner then shows "dismiss failed" and skips its focus handling for an alert that is gone.
**Fix:** Keep only `await send()` in the `try`; do the cache update and the toast after it, with the toast in its own guarded call.

### IN-02: `BillingUnitSecondsOf` is duplicated, and the DTO documentation omits the new reason

**File:** `sources/Api/Services/GameServerLifecycle/CostAccrualService.cs:174-177`, `sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs:101-104`, `sources/Api/DTO/AccruedCostDto.cs:40-47`
**Issue:** The same conversion (`result.Amount is null || BillingUnit is null ? null : (int) Math.Min(int.MaxValue, Math.Floor(Length.TotalSeconds))`) exists in two classes, so the REST and push paths can drift. A unit shorter than one second yields `0`, which `costFrames.isPositiveInteger` rejects, so the whole frame would be dropped. Separately, the `UnavailableReason` value documentation lists `rate`, `not-yet-charged` and `hidden` but not the new `billing-unit` code.
**Fix:** Expose the value once, for example `AccruedCostResult.BillingUnitSeconds`, and use it in both places. Document `billing-unit` in `AccruedCostDto.UnavailableReason`.

### IN-03: The unavailable reason `billing-unit` is silent end to end

**File:** `sources/Api/Services/GameServerLifecycle/CostAccrualService.cs:187-191`, `sources/ReactApp/src/features/hosting/accruedModel.ts:89-91`
**Issue:** When a deployment's slices carry no, several or an unregistered provider identifier, the amount becomes unavailable with reason `billing-unit`. Nothing logs it, the evaluator silently never raises alerts for that deployment, and the UI shows the bare "Not available" sentence. With a single registered provider this cannot happen today, but it is a configuration fault that would suppress cost alerts without any trace, in a cost-control feature.
**Fix:** Log one warning per deployment id and reason code when the unit cannot be resolved, so the fault is visible to operators.

### IN-04: The clock pin fixes the date but not the time zone

**File:** `sources/ReactApp/tests/hosting/accrued-cost.test.tsx:152-160`, `sources/ReactApp/tests/hosting/cost-alert-banner.test.tsx:163-171`, `sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx:100-108`
**Issue:** `formatInstant` compares local calendar days. The tests pin "now" to `2026-11-21T14:30:00Z` and use fixtures between 14:05Z and 15:42Z, which share a local day only for zones whose offset keeps 14:30Z and the fixtures on the same date. In zones from UTC+9 eastward, 15:42Z falls on 2026-11-22 locally, so the date is added and the time-only expectations fail. `vitest.config.ts` sets no `TZ`. The three test files also add `import { beforeEach } from "vitest"` although `globals: true` is on, which is unneeded and sits out of import order.
**Fix:** Set `process.env.TZ = "UTC"` in the Vitest setup (or `test.env.TZ`), and drop the redundant import.

### IN-05: The billing anchor is the first charged slice, but the UI states the next-hour instant as fact

**File:** `sources/Api/Services/GameServerLifecycle/CostAccrualCalculator.cs:88-89`, `sources/ReactApp/src/features/hosting/accruedModel.ts:117-120`
**Issue:** Hetzner starts its billed hour when the server is created. The calculator starts counting at the earliest charged slice start, which is when the control plane reserved the first slice. If the resource is created after that instant, the real hour boundaries lag the calculated ones. The UI sentence "The next hour starts at {{time}}" is then slightly early. This rests on judgment about the provisioning order, so it is Info only.
**Fix:** If a server-creation instant is stored on the deployment resource, anchor on it. Otherwise soften the sentence ("about").

### IN-06: New or changed handwritten members lack XML documentation

**File:** `sources/Api/Data/UatFleetScenarioSeeder.cs:115`, `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs:47-49`, `sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs:82-85`
**Issue:** AGENTS.md requires meaningful XML documentation on every C# member. `UatFleetScenarioSeeder.Scenarios` (the array extended in this phase) and `HetznerRuntimeProvider.ProviderIdentifier` and `Capabilities` (modified here) have none. The new property assignments in `CostAlertPublisher` also break the aligned `=` layout of the initializer.
**Fix:** Add `<summary>` blocks to those members and align the initializer.

---

_Reviewed: 2026-11-22_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
