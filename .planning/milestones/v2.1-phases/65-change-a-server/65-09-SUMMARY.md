---
phase: 65-change-a-server
plan: 09
subsystem: webapp
tags: [openapi, generated-types, pending-start, react, contracts]

requires:
  - phase: 65-01
    provides: pending-start fields on GameServerDto
  - phase: 65-05
    provides: mod pack option availability and the pack contents route
  - phase: 65-06
    provides: configuration tier and latestRevisionId in the API schema
provides:
  - Regenerated OpenAPI snapshot and generated TypeScript types carrying every Phase 65 API change
  - PendingStartNotice banner in the workspace notice slot, driven only by the API signal
  - Generated GameConfigurationEntry and GameConfiguration aliases (hand-written interfaces removed)
  - ServerModPackOption availability, ServerModPackContents, ServerChangeArea and ConfigurationProfileSummary.latestRevisionId types
  - hostingService.modPackContents and hostingQueryKeys.modPackContents with the SVC-modPackContents parity row
affects: [65-10, 65-11, 65-12, 65-13, 65-15]

plan_head_before: 73defbf816f757dcd4bc1c92ea69d7176976f0e8
plan_head_after: 31ab0e03a1099efe74a7132862598d6f897219cf

actuals:
  tokens: 14000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Configuration contract types alias the generated schema; only nested arrays that Required cannot reach get their own alias"
    - "Notice-slot banners are standalone StatePanel components that return null when their API signal is absent"

key-files:
  created:
    - sources/ReactApp/src/components/server/PendingStartNotice.tsx
    - sources/ReactApp/tests/hosting/pending-start-notice.test.tsx
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
  modified:
    - sources/ReactApp/openapi/utf-api.json
    - sources/ReactApp/src/generated/api.ts
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/tests/helpers/fleetFixtures.ts
    - sources/ReactApp/tests/helpers/configurationFixtures.ts
    - sources/ReactApp/tests/hosting/workspace-services.test.ts
    - sources/ReactApp/tests/hosting/workspace-model.test.ts
    - sources/ReactApp/tests/hosting/arma-placement.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/server-change-draft.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "Generated Kiota client under sources/WebApp/UtfApi is not regenerated: every API change of the phase is additive, the classic WebApp ignores unknown fields, and the legacy WebApp is untouched"
  - "The banner reads only hasChangesPendingStart, changesPendingStartAreas and isDeploymentActive; area labels resolve through a Map and unknown values are skipped"
  - "normalizeServer, modPackOptions and creationModPacks default the new fields so older payloads and existing mocks keep working; configuration payloads default entries and per-entry choices"

patterns-established:
  - "contract-shape.test.ts holds compile-time assignments that fail typecheck when the snapshot loses a Phase 65 field"

requirements-completed: [CHG-04, CHG-01, CHG-02]

coverage:
  - id: D1
    description: "OpenAPI snapshot equals the API export and generated types equal the generator output, including the Phase 65 additions"
    requirement: CHG-04
    verification:
      - kind: other
        ref: "dotnet run -- --export-openapi + cmp against sources/ReactApp/openapi/utf-api.json (identical)"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run contracts:check (no diff)"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/contract-shape.test.ts#generated contract shape"
        status: pass
    human_judgment: false
  - id: D2
    description: "The workspace shows the pending-start banner in every section only while the API signal is true, with running and not-running wording, the Waiting areas line, no dismiss control, and removes it when the signal clears"
    requirement: CHG-04
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/pending-start-notice.test.tsx#pending-start banner"
        status: pass
    human_judgment: false
  - id: D3
    description: "Configuration types come from the generated schema and hostingService.modPackContents loads one pack's contents with a parity row"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-services.test.ts#loads one pack's contents through the generated path and defaults null items to an empty list"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#has a row for hostingService.modPackContents"
        status: pass
    human_judgment: false
  - id: D4
    description: "Pending-start banner placement and wording read well in the shell notice slot on the live page"
    requirement: CHG-04
    verification: []
    human_judgment: true
    rationale: "Visual adequacy of the banner in the notice slot is not asserted by a test; the end-of-phase UAT covers it"

duration: 14min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 09: Contract refresh and pending-start banner Summary

**The OpenAPI snapshot and generated TypeScript types now carry every Phase 65 API change, proven end to end by a pending-start banner that follows the API signal; the configuration types are generated and the pack contents route has a typed client method.**

## Performance

- **Duration:** 14 min
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 17 touched (3 created, 14 modified)

## Accomplishments

- Snapshot exported from the built API and `contracts:generate` run; a semantic comparison against the previous snapshot shows only Phase 65 additions: one new path (the pack contents route), ten new schemas (`ConfigurationProfileSummaryDto`, `EGameConfigurationTier`, `EGameConfigurationValueKind`, `EGameServerChangeArea`, `GameConfigurationChoice`, `GameConfigurationDto`, `GameConfigurationEntryDto`, `GameServerModPackContentItemDto`, `GameServerModPackContentsDto`, `ValidationProblemDetails`), changed `GameServerDto` and `GameServerModPackOptionDto`, and the annotated configuration, profile and secret responses. No unrelated drift.
- `PendingStartNotice` renders a `StatePanel kind="info"` in the notice slot of every section, with the running or not-running sentence chosen by `isDeploymentActive`, the muted Waiting areas line (localized `Intl.ListFormat`, Map lookup), and no controls.
- `GameConfigurationEntry` and `GameConfiguration` alias the generated schema (hand-written interfaces deleted); `ServerModPackOption` keeps the availability fields optional; `ServerModPackContents`, `ServerChangeArea` and `latestRevisionId` added.
- `hostingService.modPackContents` and its query key, `SVC-modPackContents` parity row, and normalization defaults for the new server, option and configuration fields.

## Task Commits

1. **Task 1 (tracer): snapshot and generated types** - `3125ca3f` (build(api))
2. **Task 1 (tracer): pending-start banner** - `96fd762f` (feat(webapp))
3. **Task 2: generated configuration types and pack contents client** - `31ab0e03` (feat(webapp))

## Verification Results

- Tracer gate: export `cmp`, `contracts:check`, notice, workspace-page, single-owner and create-page tests, typecheck and lint all passed before expansion.
- Final plan check: API export identical to the snapshot; `contracts:check` clean; `typecheck` and `lint` exit 0; `vitest run tests/hosting tests/locales` 82 files, 1286 tests passed; `git status --porcelain -- sources/WebApp` empty; `git diff --exit-code -- sources/Common/Ui/routes.json` clean.
- Acceptance greps: `GameConfigurationEntryDto` in snapshot 2, `GameServerModPackContentsDto` 6, `changesPendingStartAreas` in `api.ts` 1, `PendingStartNotice` in the page 2, `export interface GameConfigurationEntry` 0, `GameConfigurationEntryDto` in `types.ts` 1, `modPackContents` in `services.ts` 2, `SVC-modPackContents` 1, `tier` in `configurationFixtures.ts` 1.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Typed literals also needed `isControlPlaneOnly` and `choices`**
- **Found during:** Task 2 (typecheck after the alias swap)
- **Issue:** `Required<>` makes `isControlPlaneOnly` required and the plan's alias makes `choices` required, so the configuration literals needed more than `tier`.
- **Fix:** Added `isControlPlaneOnly: false` and `choices: []` where the literals lacked them (`configurationFixtures.ts`, `workspace-model.test.ts`, `workspace-page.test.tsx`); `services.ts` also defaults each entry's `choices` to `[]` for degraded payloads.
- **Files modified:** tests/helpers/configurationFixtures.ts, tests/hosting/workspace-model.test.ts, tests/hosting/workspace-page.test.tsx, src/features/hosting/services.ts
- **Commit:** 31ab0e03

**2. [Rule 3 - Blocking] A test used a configuration kind the API does not have**
- **Found during:** Task 2 (typecheck)
- **Issue:** `server-change-draft.test.ts` built an entry with `kind: "Decimal"`, which the generated `EGameConfigurationValueKind` (`Number`) does not contain. File was not in the plan list.
- **Fix:** Use the API spelling `Number`; production code compares kinds case-insensitively and the asserted `decimal` issue code is unchanged.
- **Files modified:** tests/hosting/server-change-draft.test.ts
- **Commit:** 31ab0e03

**3. [Rule 2 - Missing critical] Banner styling file**
- **Found during:** Task 1
- **Issue:** The muted areas line needs a style rule; the plan lists no CSS file.
- **Fix:** Added `.pending-start-notice__areas` (token color only) to the existing `change.css`, which the style guard test still accepts.
- **Files modified:** src/components/server/change.css
- **Commit:** 96fd762f

**Total deviations:** 3 auto-fixed (2 blocking, 1 missing styling). **Impact:** test and style adjustments only; no contract or scope change.

## Issues Encountered

- The TDD ordering of the tracer was relaxed as in earlier plans: tests were written with the component and run green rather than committed as a separate RED step.
- `npm run format:check` reports 33 files with style issues (for example `tests/hosting/fleet-page.test.tsx`, `parity-matrix.test.ts`) that this plan did not touch; every file this plan touched passes `prettier --check`. This looks like a working-copy line-ending effect of the repository's CRLF setting and is out of scope.
- The Kiota client was not regenerated, by the plan's recorded decision (T-65-26 accepted).

## Known Stubs

None.

## Threat Flags

None. T-65-24 is mitigated (both generated files come from tooling, verified by `cmp` and `contracts:check`); T-65-25 is mitigated (Map of known labels, text rendering only).

## Next Phase Readiness

Plans 65-10 to 65-13 can use the generated `tier`, availability, `latestRevisionId` and `ServerModPackContents` types, and `hostingService.modPackContents`. Plan 65-15 sets the final status of the `SVC-modPackContents` parity row.

## Self-Check: PASSED

- Created files verified on disk; commits `3125ca3f`, `96fd762f` and `31ab0e03` verified as ancestors of HEAD.
