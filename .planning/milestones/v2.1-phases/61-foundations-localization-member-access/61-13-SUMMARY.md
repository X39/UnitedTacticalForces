---
phase: 61-foundations-localization-member-access
plan: 13
subsystem: webapp-verification
status: complete
tags: [react, vitest, playwright, axe, accessibility, parity-matrix, localization]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-03 member-access API proof, 61-04 parity matrix and guard, 61-06 @axe-core/playwright 4.13.0, 61-09 to 61-12 derived state on every surface, shared confirmation and deep-link notice"
provides:
  - "server-state-surfaces.test.tsx: identical state and cost text on the fleet card, workspace header and profile-menu notification row in English and German, raw statusLabel never rendered, a may-still-cost server never labelled off"
  - "server-state-single-owner.test.ts: repository scan that fails with file and line on any raw deployment status or statusLabel access outside serverState.ts and services.ts, with a live-scanner proof"
  - "hosting-a11y.spec.ts: axe at WCAG 2.1 AA in real Chromium for the start, stop, terminate (challenge) and request-error dialog states and the deep-link notice, English and German, plus dismiss focus and 320 by 568 fit"
  - "public-shell.spec.ts journeys aligned with the new labels and derived profile-menu state"
  - "Parity matrix: 18 rows marked kept with Phase 61 evidence, none deleted, none removed"
  - "Locale audit: three keys orphaned by the notification rework removed"
affects: [phase-61-verification, 62-fleet-and-quick-actions, 69-consistency-accessibility-verification]

actuals:
  tokens: 7500
  tasks: 3
  commits: 5
plan_head_before: 1309ae2c9100ac81c940d17e9f6a8b816dfcfa76
plan_head_after: 5ab46a85949e3d7a504a0876be9abe641eed3c47

tech-stack:
  added: []
  patterns:
    - "Repository guard test: scan src with node:fs, allow-list the owner files, prove the scanner on an inline bad source"
    - "Axe e2e: strict API mock set that records and aborts unmocked requests, then asserts none happened, plus a non-vacuous check that axe evaluated rules"
    - "Cross-surface consistency: read .server-state-badge and [data-cost] from each surface and compare to one expected pair"

key-files:
  created:
    - sources/ReactApp/tests/hosting/server-state-surfaces.test.tsx
    - sources/ReactApp/tests/hosting/server-state-single-owner.test.ts
    - sources/ReactApp/tests/e2e/hosting-a11y.spec.ts
  modified:
    - sources/ReactApp/tests/e2e/public-shell.spec.ts
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The a11y spec records unmocked API requests and aborts them, then asserts the list is empty, instead of throwing inside the route handler (a thrown handler leaves the request hanging); the strictness of the plan is kept and failures stay visible"
  - "scan() asserts axe reported at least one passed rule so an include selector that matched nothing cannot pass vacuously"
  - "Hosting_Notification_ActionError, Hosting_Notification_ActionPending and Hosting_Notification_PossibleCost were removed from en and de: no source or test references them after plan 61-10"
  - "FLT-card and FLT-action-deep-link were marked kept in addition to the rows the plan listed, because Phase 61 changed both (derived badge on the card, the ?action= confirmation or notice)"

requirements-completed: [FND-02, FND-03, FND-04, FND-05, FND-07, ACC-01, ACC-03]

coverage:
  - id: D1
    description: "The card, workspace header and notification row show identical state and cost text in English and German; the raw statusLabel never appears and a may-still-cost server is never labelled off"
    requirement: "FND-02"
    verification:
      - kind: unit
        ref: "tests/hosting/server-state-surfaces.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "No source outside serverState.ts and services.ts reads a raw deployment status or a notification statusLabel; the scanner is proven live"
    requirement: "FND-03"
    verification:
      - kind: unit
        ref: "tests/hosting/server-state-single-owner.test.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Zero axe violations at WCAG 2.1 AA in real Chromium for the start, stop, terminate and request-error dialog states and the deep-link notice, in English and German; dismiss button focused on open; dialog fits 320 by 568"
    requirement: "FND-05"
    verification:
      - kind: e2e
        ref: "tests/e2e/hosting-a11y.spec.ts (12 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Existing public-shell hosting journeys pass with the new labels, a derived profile-menu state and API-realistic fixtures"
    requirement: "FND-04"
    verification:
      - kind: e2e
        ref: "tests/e2e/public-shell.spec.ts"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-placement.spec.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Parity matrix rows for the capabilities Phase 61 changed are kept with evidence; no row deleted or removed; completeness test passes"
    requirement: "FND-07"
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Phase gate: React ci steps, the full Vitest suite (719 tests), build:assets and the GameServerHosting dotnet filter (531 tests) pass"
    requirement: "ACC-01"
    verification:
      - kind: other
        ref: "npm --prefix sources/ReactApp run ci (all steps; see Issues Encountered), dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~GameServerHosting"
        status: pass
    human_judgment: false

duration: 27 min
completed: 2026-11-18
---

# Phase 61 Plan 13: Cross-Surface Proof, Axe Gate and Parity Matrix Summary

**Cross-surface state consistency, a repository guard that keeps status decisions in serverState.ts, a 12-test real-Chromium axe gate (WCAG 2.1 AA, English and German) for the shared confirmation and deep-link notice, realistic e2e journeys, and 18 parity-matrix rows recorded as kept.**

## Performance

- **Duration:** 27 min
- **Tasks:** 3
- **Files:** 3 created, 4 modified

## Accomplishments

- **Surfaces test.** Card, workspace header and profile-menu notification row are rendered for a running-with-cost, a stopped-with-cost and a no-deployment server; all three surfaces read exactly "Running / Costing now", "Stop not confirmed / May still cost" and "Off / Not costing". German matches ("Läuft / Kosten laufen"). The raw notification statusLabel is never in the row, and a possibleCost server is never Off, Aus, Stopped or Terminated.
- **Single-owner guard.** The scan found zero real violations (no source change needed). It runs against every .ts and .tsx under src (generated code skipped), fails with file and line, and an inline bad-source fixture proves it is live.
- **Axe gate.** `@axe-core/playwright` 4.13.0 works with Playwright 1.62.1, so the 4.14.0 plus axe-core override fallback (which would need a new approval) was not needed. All 12 tests (6 scenarios in two languages) pass first run; the spec also proves axe evaluated rules, so a mismatched selector cannot pass vacuously.
- **Journeys.** public-shell uses "Ohne Speichern herunterfahren" and "Shut down without saving", and the profile-menu journey now serves the fleet and asserts derived "Running" and "Costing now" instead of the raw statusLabel (the fixture field is kept because the API still sends it). The fixtures contain no Start action marked dangerous (D-13), and terminate keeps its typed challenge.
- **Matrix.** Seven ACT rows, FLT-card, FLT-card-cost-stop, FLT-empty-state, FLT-overview-dashboard-attention, FLT-command-center-attention, FLT-profile-menu-notifications, FLT-profile-menu-cost-stop, FLT-action-deep-link, FACT-status-stage, FACT-cost-owner and REC-archive-server are `kept` with `Phase 61:` evidence. Every other row stays `pending`; `removed` count is 0.

## Task Commits

1. **Task 1:** `b7275bd5` test(webapp): guard cross-surface server state consistency
2. **Task 2a:** `115de2e6` test(webapp): scan the cost confirmation and notice with axe in Chromium
3. **Task 2b:** `ea962a94` test(webapp): align hosting e2e journeys with the shared confirmation
4. **Locale audit (carry-over from 61-10):** `754133f2` chore(webapp): remove locale keys orphaned by the notification rework
5. **Task 3:** `5ab46a85` docs(webapp): record Phase 61 parity matrix evidence

## Verification

- `npm --prefix sources/ReactApp test -- tests/hosting/server-state-surfaces.test.tsx tests/hosting/server-state-single-owner.test.ts`: 10 passed. Typecheck, lint (`--max-warnings 0`) and Prettier clean on the new files.
- `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-a11y.spec.ts tests/e2e/public-shell.spec.ts tests/e2e/hosting-placement.spec.ts`: 41 passed (run again after the locale removal).
- `npm --prefix sources/ReactApp run ci` steps: locales:check (3105 keys each, 0 failures, 0 warnings), contracts:check, format:check, lint, typecheck, build:assets all pass. The full Vitest suite passed 719 of 719 with `--maxWorkers=2`.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 531 passed, 0 failed.
- Acceptance greps: `| kept |` count 18 (at least 10), `| removed |` count 0, the matrix is tracked in git, AxeBuilder and wcag21aa present in the a11y spec.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Pre-existing format:check failure of arma-placement.test.tsx**
- **Found during:** Task 3 (phase gate)
- **Issue:** The working copy was CRLF while git holds LF (already in deferred-items.md), so `format:check` failed and blocked `npm run ci`.
- **Fix:** `prettier --write` on the working copy only; the file has no content diff in git (index is LF, now `i/lf w/lf`). No commit includes it.
- **Files modified:** sources/ReactApp/tests/hosting/arma-placement.test.tsx (line endings in the working tree only)

**2. [Rule 2 - Missing critical] Strict unmocked-request handling in the a11y spec**
- **Issue:** Throwing inside a Playwright route handler leaves the request hanging rather than failing visibly.
- **Fix:** record the unmocked request, abort it and assert the recorded list is empty in every scenario.
- **Files modified:** sources/ReactApp/tests/e2e/hosting-a11y.spec.ts

**3. [Rule 2 - Missing critical] Non-vacuous axe evidence**
- **Fix:** `scan()` also asserts axe reported at least one passed rule.
- **Files modified:** sources/ReactApp/tests/e2e/hosting-a11y.spec.ts

**4. [Carry-over audit] Locale keys orphaned by plan 61-10**
- **Fix:** removed `Hosting_Notification_ActionError`, `Hosting_Notification_ActionPending` and, found by the same audit, `Hosting_Notification_PossibleCost` (no references in src or tests; not in the identical-allowed list). Commit `754133f2`.

**5. [Judgment] Two extra matrix rows marked kept:** FLT-card and FLT-action-deep-link, because Phase 61 demonstrably changed both.

**Total deviations:** 5 (1 blocking fix, 2 added safeguards, 1 audit cleanup, 1 matrix scope judgment). **Impact:** none on the plan outcome.

## Issues Encountered

- Under full parallel Vitest load three unrelated or timing-sensitive tests timed out once (event-slot-planner, workspace-page file mutation test, wiki page). All three pass in isolation and the whole suite passed 719 of 719 on a rerun with two workers. This is the known flakiness listed in the carry-overs; the 5000 ms default test timeout is the cause.
- REC-archive-server evidence is thin: the dismiss key is proven to exist in both locales (`tests/locales/server-keys.test.ts`), but no behaviour test exercises the archive dialog's dismiss label. Worth a test in a later phase.
- Residual risk A1 (parity matrix test needs `.planning/` in the CI source transfer): `.mozaic/react-browser.Containerfile` copies only `sources/ReactApp`, and `.dockerignore` excludes `.planning`. That job runs Playwright only, so it does not need the matrix. The `react-tests` job runs `npm test` in the Mozaic workspace checkout; whether `.planning/` is part of that source transfer could not be verified locally. By design the matrix test throws when the file is missing, so a missing transfer fails the job rather than passing vacuously.
- Requirement scope note: FND-04 names "create and start"; the phase CONTEXT defers the create-and-start confirmation to Phase 64. FND-04 is marked complete because the plan declared it for this phase and start, stop and cost-stop are all proven through the shared dialog.

## Known Stubs

None.

## Threat Flags

None. The plan added tests and documentation only and no new network, auth or file surface.

## Next Phase Readiness

Phase 61 has all 13 plans summarized and is ready for phase verification. Phase 62 can consume the shared state, dialog and matrix. Phase 69 still owns the German 320 and 390 visual baselines and promotion of the identical-value locale warning to a failure.

## Self-Check: PASSED

- Created files exist: server-state-surfaces.test.tsx, server-state-single-owner.test.ts, hosting-a11y.spec.ts, this SUMMARY.
- Commits `b7275bd5`, `115de2e6`, `ea962a94`, `754133f2`, `5ab46a85` are ancestors of HEAD.
