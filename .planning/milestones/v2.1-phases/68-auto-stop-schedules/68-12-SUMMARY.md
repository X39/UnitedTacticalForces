---
phase: 68-auto-stop-schedules
plan: 12
subsystem: webapp
tags: [openapi, contract, react, data-layer, automation, cost-alerts, degrade]

requires:
  - phase: 68-auto-stop-schedules
    provides: automation read model and projector (68-06), AutoStop alert kind and frame (68-07), PUT automation endpoint (68-08), job trigger (68-09), automation model types (68-04)
provides:
  - Refreshed OpenAPI snapshot and regenerated React contract carrying the automation block, canManageAutomation, the PUT automation endpoint, LifecycleJobDto Trigger and TriggerLimit, and the four new CostAlertDto fields
  - Optional client members (automation, canManageAutomation, trigger, triggerLimit, kind, stopsAt, stopReason, stopLimit) that degrade when an older API omits them
  - hostingService.saveAutomationSettings with typed conflict, forbidden and validation errors, and the useSaveAutomationSettings mutation
  - Fixtures serverAutomation, autoStopAlert and workspaceAutomaticJob
affects: [68-14, 68-15, 68-16, 68-17, 68-18]

status: complete
commits: 4
plan_head_before: 5485b357415ce1e5806dc2d768963a3bb3c5014b
plan_head_after: 4a0f33c372a6495c3b4e4e3508230a788a1064f5
actuals:
  tokens: 10800
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Members a newer API adds are optional on the client alias (Omit plus optional re-add) so an older payload degrades to null or absent instead of invented data"
    - "A save mutation writes the returned block into the exact server cache entry and invalidates that exact key"
    - "Compile-time assignability functions (generated type to model input type) in the contract-shape test catch drift"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/useAutomationSettings.ts
    - sources/ReactApp/tests/hosting/automation-services.test.ts
  modified:
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/costFrames.ts
    - sources/ReactApp/src/api/client.ts
    - sources/ReactApp/tests/helpers/fleetFixtures.ts
    - sources/ReactApp/tests/helpers/costAlertFixtures.ts
    - sources/ReactApp/tests/helpers/workspaceFixtures.ts
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/hosting/cost-frames.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The new members are optional on GameServer, LifecycleJob and CostAlert even though the generated schema types them as required, because the UI-SPEC degrade rule says an older API omits them"
  - "The ServerAutomation types re-declare the nested optional members as nullable (Omit plus Required) so they are assignable to the model's SavedAutomationInput and AutomationLimitsInput"
  - "ApiError carries the field list of a hosting 400 answer ({ error, fields }) as a new optional constructor argument, so the typed validation error can name the fields"
  - "The mutation does not create a server cache entry that was never loaded; it only replaces automation in an existing one"
  - "A raised frame without a kind maps exactly as before (no new keys); a frame with a kind carries kind, stopsAt, stopReason and stopLimit (null when absent)"

requirements-completed: []

coverage:
  - id: D1
    description: "The exported OpenAPI snapshot is byte-identical to the API export and the generated contract carries the automation block, endpoint, job trigger and alert kind"
    requirement: "AUT-01"
    verification:
      - kind: other
        ref: "cmp of a fresh API export against sources/ReactApp/openapi/utf-api.json; npm run contracts:check"
        status: pass
  - id: D2
    description: "The data layer maps automation, job trigger and alert kind and degrades when an older API omits them; raised AutoStop frames carry kind, stop instant, reason and limit"
    requirement: "AUT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts, tests/hosting/cost-frames.test.ts"
        status: pass
  - id: D3
    description: "Saving automation sends one PUT, returns the block into the exact server cache entry, and tells conflict, forbidden, invalid and error apart"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-services.test.ts"
        status: pass
---

# Phase 68 Plan 12: Automation Contract and Data Layer Summary

**The React app now has a CI-identical OpenAPI contract for server automation, maps the new server, job and alert members with graceful degradation, and saves automation settings through one typed mutation that updates the exact server cache entry.**

## Accomplishments

- Built the API, exported the snapshot with the proven recipe, copied it to `sources/ReactApp/openapi/utf-api.json` and ran `contracts:generate`. The snapshot now holds `ServerAutomationDto`, `ServerAutomationUpdate`, the `PUT game-servers/{gameServerId}/automation` path, `LifecycleJobDto.trigger`/`triggerLimit` and the `CostAlertDto` kind and stop members (this closes the regeneration notes deferred by 68-06, 68-07 and 68-08).
- `types.ts`: `ServerAutomation` with its settings, limits, status, pending stop and schedule aliases; `GameServer`, `LifecycleJob` and `CostAlert` gain the optional members.
- `services.ts`: `normalizeServer` keeps `automation` and `canManageAutomation` (null when absent); `normalizeJob` keeps `trigger` and `triggerLimit` untouched; `AutomationSettingsConflictError`, `AutomationSettingsForbiddenError`, `AutomationSettingsValidationError` (with `fields`) and `hostingService.saveAutomationSettings` (PUT with the full update).
- `costFrames.ts`: `parseCostAlertRaisedFrame` validates an optional kind, an ISO stop instant, a string reason and a finite limit; a malformed member drops the frame (T-68-31).
- `useAutomationSettings.ts`: `useSaveAutomationSettings` (cache write plus exact invalidation, no timers) and `automationSaveFailure`.
- Parity matrix row `SVC-saveAutomationSettings` keeps the every-service-has-a-row guard green.
- Tests: assignability of the generated settings, limits and update types to `SavedAutomationInput`, `AutomationLimitsInput` and the contract update; mapping and degrade cases through `hostingService`; AutoStop frame cases; save outcomes (200, 409, 403, 400 with and without fields, 503, 500) and the hook's cache behavior.

## Task Commits

1. Task 1 (tracer): `dcaaa718` build(webapp): refresh the API contract for server automation; `29c26dcc` feat(webapp): map automation, job trigger and alert kind data
2. Task 2: `5dd569df` feat(webapp): save automation settings; `4a0f33c3` test(webapp): cover the automation data layer

## Verification

- Tracer gate (re-run before Task 2): `cmp` of a fresh API export against the committed snapshot passed, `contracts:check` clean, contract-shape and cost-frames tests (34) passed, `typecheck` clean. Auto mode did not apply; the gate ran on the automated verify.
- After Task 2: `automation-services` and `parity-matrix` tests passed (103), `typecheck` and `lint` (`--max-warnings 0`) clean, `locales:check` clean, `contracts:check` clean after the final commit.
- Full React suite: 171 files, 2408 tests passed.
- `git diff 5485b357 -- sources/WebApp` is empty: the legacy Blazor client is untouched because every API change is additive.
- Acceptance greps: `ServerAutomationDto` in the snapshot, `/automation` in `api.ts`, `canManageAutomation` in `services.ts`, one `AutomationSettingsConflictError` class, zero `refetchInterval`/`setInterval` in the hook, 7 test cases in `automation-services.test.ts`.
- Plan 68-11 was running in parallel; the API built cleanly on first attempt and no retry was needed.

## Deviations from Plan

**1. [Rule 3 - Blocking] `ApiError` could not carry the 400 field list**
- **Found during:** Task 2
- **Issue:** `ApiClient` reads only the ProblemDetails `errors` object, but the automation endpoint answers `{ error, fields }`, so `AutomationSettingsValidationError.fields` could not be filled.
- **Fix:** `sources/ReactApp/src/api/client.ts` reads an optional string array `fields` into a new optional last constructor argument of `ApiError`. Existing callers are unaffected.
- **Commit:** `5dd569df`

**2. [Minor] Tests for the mapping live partly in `contract-shape.test.ts`, with an extra fixture builder**
- `serverAutomation`, `autoStopAlert` and `workspaceAutomaticJob` were added as fixture builders beyond the plan's option list, to give plans 68-14 to 68-18 ready blocks.

**3. [Minor] The existing alert shape in `contract-shape.test.ts` gained the four required generated members**
- The generated `CostAlertDto` types the new members as required, so the Phase 66 `alertShape` literal needed them.

## Known Stubs

None.

## Threat Flags

None. T-68-31 is covered by the frame validation cases; T-68-32 by the byte-identical snapshot check, `contracts:check` and the compile-time assignability functions.

## Notes

- `npm run format:check` reports 502 files repo-wide in this Windows worktree (line-ending checkout); it is not part of this plan and files touched here were formatted with Prettier.
- The Co-Authored-By trailer follows the attribution line of the session reminder (Claude Sonnet 5.5) rather than the Opus line in the dispatch prompt.

## Self-Check: PASSED

- Created files exist: `useAutomationSettings.ts`, `automation-services.test.ts`.
- Commits `dcaaa718`, `29c26dcc`, `5dd569df`, `4a0f33c3` are ancestors of HEAD; `git rev-list --count` from the base is 4, matching `commits: 4`.
