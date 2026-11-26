---
phase: 66-accrued-cost-cost-alerts
plan: 16
subsystem: api
tags: [accrued-cost, billing-unit, websocket, react, openapi, ui-spec, d-15, d-02]
status: complete
gap_closure: true

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-13 AccruedCostResult.BilledThrough and BillingUnit, 66-08 accrued strip and frame parser, 66-15 gap closure amendments section
provides:
  - "AccruedCostDto and GameServerAccruedCostMessage carry BillingUnitSeconds and BilledThrough, null with the amount (hidden viewers included)"
  - "Regenerated OpenAPI snapshot and generated TypeScript types with billingUnitSeconds and billedThrough"
  - "accruedPresentation().billing and a billing line in AccruedCost: 'Billed per started hour. The next hour starts at {{time}}.', 'Billed per started hour.', 'Billed per started minute.' (EN and DE)"
  - "parseAccruedCostFrame validates and maps both billing facts into the cached REST shape"
  - "UI-SPEC amendment 'Accrued row billing line (D-15)' and an e2e proof of the line and the in-place step to the next hour"
affects: [66-17, 66-18, phase-66-verification, uat]

plan_head_before: 293be5e2
plan_head_after: 2fc74c9740cdce80cc142033f9ffcb753ba2392e

actuals:
  tokens: 14300
  tasks: 3
  commits: 6

tech-stack:
  added: []
  patterns:
    - "Billing facts are control-plane values; the browser selects a sentence from a Map-like unit check and formats the instant, never derives it"
    - "Frame parser helpers isPositiveInteger and isInstant keep the untrusted-field checks readable"

key-files:
  created: []
  modified:
    - sources/Api/DTO/AccruedCostDto.cs
    - sources/Api/Services/GameServerLifecycle/CostAccrualService.cs
    - sources/Api/Services/GameServerLifecycle/CostAlertPublisher.cs
    - sources/WebSocketContracts/Hosting/GameServerAccruedCostMessage.cs
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts
    - sources/ReactApp/src/features/hosting/accruedModel.ts
    - sources/ReactApp/src/features/hosting/costFrames.ts
    - sources/ReactApp/src/components/server/AccruedCost.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - tests/Api.Tests/GameServerHosting/AccruedCostEndpointTests.cs
    - tests/Api.Tests/GameServerHosting/CostAlertPublisherTests.cs
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/hosting/accrued-model.test.ts
    - sources/ReactApp/tests/hosting/accrued-cost.test.tsx
    - sources/ReactApp/tests/hosting/cost-frames.test.ts
    - sources/ReactApp/tests/hosting/cost-strip.test.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx
    - sources/ReactApp/tests/e2e/support/fleetStream.ts
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - .planning/phases/66-accrued-cost-cost-alerts/66-UI-SPEC.md

key-decisions:
  - "The unit length is reported as whole seconds rounded down and only when an amount and a unit exist; the same helper logic sits in CostAccrualService (REST) and CostAlertPublisher (socket) so both carry identical facts"
  - "billedThrough is nulled with the amount in the service and the publisher, so a hidden viewer and an unavailable amount disclose neither fact (T-66-46)"
  - "A one-hour unit without a billedThrough value falls back to the plain 'Billed per started hour.' sentence instead of printing an empty time"
  - "The e2e billing locator matches unanchored regexes because toContainText on the row compares against the whole row text"

requirements-completed: [CST-03]

duration: 30min
completed: 2026-11-21
---

# Phase 66 Plan 16: Accrued row billing line Summary

**The accrued row of a Hetzner session now says "Billed per started hour. The next hour starts at 15:42." (German equivalent), driven entirely by a billing unit length and billed-through instant that the control plane puts on the REST accrued value and on every pushed accrued frame, so a full-hour amount after five minutes and an amount that stays put until the next hour read as correct.**

## Performance

- **Duration:** about 30 minutes (start 2026-11-21T19:45Z approx.)
- **Tasks:** 3 (task 1 is the tracer)
- **Commits:** 6 measured from the base `293be5e2` (`plan_head_before`) to `2fc74c97`
- **Files:** 23 modified, none created; nothing changed under `sources/WebApp`

## Accomplishments

- **Tracer (task 1):** `AccruedCostDto.BillingUnitSeconds` (`int?`) and `BilledThrough` (`Instant?`) are set from `AccruedCostResult.BillingUnit.Length` and `BilledThrough` in `CostAccrualService.GetForServerAsync` and are null whenever the amount is null, which includes the hidden-viewer branch. Endpoint tests cover a Hetzner session at minute 5 (3600 and first slice start plus one hour), a per-second test provider (1), a hidden viewer and an unavailable amount (both null). The OpenAPI snapshot and `api.ts` were regenerated through the export command and `contracts:generate` (only the two fields changed, the export equals the snapshot byte for byte). `accruedPresentation` gained `billing`; `AccruedCost` renders it as `p.accrued-cost__line` after the time line with the instant in a `<time dateTime>`. Three EN and DE keys were added and listed in `phase66RequiredKeys`. The tracer `<verify>` passed end to end before task 2 (auto mode: `⚡ Tracer verified end-to-end — expanding`).
- **Frames (task 2):** `GameServerAccruedCostMessage.BillingUnitSeconds` and `BilledThrough` (`DateTimeOffset?`, never NodaTime), set in `CostAlertPublisher.PublishAccrualsAsync`. A publisher test proves the facts, the null case for an unavailable amount, that frames still go only to the server-view and cost-owner-view intersection, and that both fields round trip through the real `UpdateStreamService` as a number and an ISO-8601 string with offset. `parseAccruedCostFrame` accepts a positive integer or null unit and a parseable instant or null, reads absent as null, drops a frame with a string unit, zero, negative or fractional unit, or an unparseable or numeric instant, and a newer frame replaces the billing line while the older frame is still rejected by `isNewerAccrued`.
- **UI-SPEC and e2e (task 3):** `### Accrued row billing line (D-15)` was added at the end of `## Gap Closure Amendments (2026-11-21)`: override of the Data table row ("plus the prorated in-progress slice"), the two fields, placement (between time line and notes, muted, not a live region), the unit table and the three EN/DE strings. The e2e journey asserts the billing line, then pushes a frame that steps the amount from 1.60 to 1.80 with a new billedThrough and asserts the in-place step with the REST read counter still 1. The layout test asserts the line in both languages at 320 and 390 px (no horizontal scroll, axe scans unchanged and green).

## Screenshot review (task 3)

Captured with `UTF_CAPTURE_SCREENS=1` into `sources/ReactApp/test-results/cost-alert-screens/` (git-ignored) and opened with the Read tool: `strip-en-1280.png`, `strip-en-390.png`, `strip-de-1280.png`, `strip-de-390.png`.

- 1280 px: the billing line sits under "As of 11:00 AM" in the narrow last column, same muted body style and left edge as the as-of line, wraps over three lines (four in German) without touching the next row and without crowding the bold amount.
- 390 px: the strip is a two column list; the billing line wraps over three lines inside the value column, aligned with the as-of line and the other values, no overflow.
- One cosmetic observation, not changed: in English the wrapped sentence can leave "AM." alone on the last line at 1280 px. It is a locale-formatted clock value, so no non-breaking-space rule was added.

## Task Commits

1. **Task 1 (tracer), API:** `0f41e134` feat(api): report the billing unit with the accrued cost
2. **Task 1, contract:** `51c9ee96` build(api): refresh the OpenAPI snapshot for the billing unit
3. **Task 1, strip:** `d11c7a59` feat(webapp): say how the accrued cost is billed
4. **Task 2:** `ff47c93a` feat(api),feat(webapp): push the billing unit with accrued cost frames
5. **Task 3, e2e:** `a402d185` test(webapp): prove the accrued billing line end to end
6. **Task 3, spec:** `2fc74c97` docs(planning): amend the accrued row for the billing unit

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CostAlert|FullyQualifiedName~AccruedCost|FullyQualifiedName~UpdateStreamServiceSendTests"`: 136 passed.
- OpenAPI export compared with `sources/ReactApp/openapi/utf-api.json`: identical; `contracts:check`, `locales:check` (3643 keys per language), `typecheck`, `lint` exit 0.
- `vitest run tests/hosting tests/locales`: 98 files, 1749 tests passed.
- `npm run test:e2e -- tests/e2e/hosting-cost-alerts.spec.ts`: 13 passed (12 capture tests skipped); with `UTF_CAPTURE_SCREENS=1`: 25 passed.
- Prettier check clean on every touched source and test file; `git status --porcelain -- sources/WebApp` is empty.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Frame parser change landed in the strip commit**
- **Found during:** Task 1
- **Issue:** `AccruedCost` in `types.ts` is `Required<Schema<"AccruedCostDto">>`, so the regenerated contract made `parseAccruedCostFrame` fail typecheck until it produced the two new fields.
- **Fix:** `costFrames.ts` and its tests (`cost-frames.test.ts`) were finished in the task 1 webapp commit instead of the task 2 commit, so no commit leaves the project failing typecheck. Task 2 keeps the API frame fields, the publisher and the e2e frame helper.
- **Files modified:** `sources/ReactApp/src/features/hosting/costFrames.ts`, `sources/ReactApp/tests/hosting/cost-frames.test.ts`
- **Commit:** `d11c7a59`

**2. [Rule 3 - Blocking] Extra fixture needed the new required fields**
- **Found during:** Task 1
- **Issue:** `tests/hosting/cost-confirm-dialog.test.tsx` (not listed in the plan) builds an `AccruedCost` value and failed typecheck; the plan's `fleetFixtures.ts` does not build accrued values, so it needed no change.
- **Fix:** added `billingUnitSeconds: null` and `billedThrough: null` to that fixture.
- **Commit:** `d11c7a59`

**3. [Rule 1 - Bug] Existing e2e journey would hit a strict-mode violation**
- **Found during:** Task 3
- **Issue:** `row.locator("time")` matched two elements once the billing line exists.
- **Fix:** the journey now addresses the as-of and billing `time` elements by position, and the copy regexes are unanchored because `toContainText` compares against the whole row text (first run failed on the anchored regex, fixed and re-run green).
- **Commit:** `a402d185`

**Total deviations:** 3 auto-fixed (two blocking, one bug), no scope change, no architectural decisions.

## Authentication Gates

None.

## Known Stubs

None. Every new field is wired from the control plane to the strip.

## Threat Flags

None. The two new fields are covered by T-66-46 (both null with the amount, same audience intersection for frames) and T-66-47 (the parser type-checks both and drops malformed frames); no new endpoint, auth path or trust boundary.

## Self-Check: PASSED

- Commits `0f41e134`, `51c9ee96`, `d11c7a59`, `ff47c93a`, `a402d185`, `2fc74c97` are ancestors of HEAD.
- `BilledThrough` appears in `AccruedCostDto.cs` and `GameServerAccruedCostMessage.cs`; `billedThrough` in `utf-api.json`, `accruedModel.ts` and `costFrames.ts`; three `Server_Workspace_Cost_AccruedBilling_` keys in each locale; `D-15` in the UI-SPEC; `per started hour` in the e2e spec.
