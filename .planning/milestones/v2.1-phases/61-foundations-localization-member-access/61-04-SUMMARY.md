---
phase: 61-foundations-localization-member-access
plan: 04
subsystem: webapp-hosting-parity
tags: [vitest, parity-matrix, fnd-07, planning-artifact, ci-guard]

requires:
  - phase: none
    provides: independent of plans 61-01 to 61-03
provides:
  - 61-PARITY-MATRIX.md, the tracked capability parity checklist (181 rows, all pending) consumed by phases 62 to 69
  - parity-matrix.test.ts, the CI completeness and vocabulary guard for the matrix
  - exported WorkspaceTab type and workspaceTabIds constant from ServerWorkspacePage.tsx
affects: [62, 63, 64, 65, 69, phase-61-verification]

actuals:
  tokens: 9700
  tasks: 2
  commits: 2
plan_head_before: d7d11dc0199a859191233116e62764bbf7682eb2
plan_head_after: 91334ea93f4b8111064b4f3db92043b27574668f

tech-stack:
  added: []
  patterns:
    - "Planning artifact under .planning is parsed by a Vitest guard through a repository-root-relative path and fails closed when absent"
    - "it.each over workspaceTabIds and Object.keys(hostingService) so a failure names the exact missing row id"

key-files:
  created:
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx

key-decisions:
  - "Phase column maps ACT-start and ACT-stop to 62 (roadmap names start/stop in Fleet and Quick Actions) and the other lifecycle actions to 63"
  - "Audit-feed capabilities are recorded as FACT-audit-* rows and logs-tab capabilities as JOB-logs-* rows so only the plan's ID families are used"
  - "SVC-changeProfileUserAccess and SVC-changeProfileRoleAccess have no screen caller today (service and tests only); the rows record that fact instead of inventing a screen"

patterns-established:
  - "Matrix row ids are deterministic (TAB-, SVC-, ACT-, FLT-, FACT-, CHG-, FIL-, JOB-, REC-, CRT-, ADJ-); later phases flip Status and fill New location and Evidence, never delete rows"

requirements-completed: [FND-07]

coverage:
  - id: D1
    description: "A single-table parity matrix lists every existing server capability (9 tabs, 51 hostingService methods, 7 actions, fleet, facts, change, files, jobs, record, create and adjacent rows), all pending"
    requirement: "FND-07"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#parity matrix"
        status: pass
    human_judgment: false
  - id: D2
    description: "The guard fails on a missing TAB-, SVC- or ACT- row, an unknown status, a relocated row without a new location, a removed row without reason and sign-off, and on a missing matrix file"
    requirement: "FND-07"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#has a row for hostingService.%s"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#is found at the repository-root-relative D-08 path"
        status: pass
    human_judgment: false
  - id: D3
    description: "Whether the CI source transfer carries .planning (residual risk A1) is unverified by this plan; the guard fails closed instead of passing vacuously"
    requirement: "FND-07"
    verification: []
    human_judgment: true
    rationale: "Only a real Mozaic react-tests run shows whether the source transfer includes .planning; a unit test cannot observe the CI transfer"

duration: 7min
completed: 2026-11-18
status: complete
---

# Phase 61 Plan 04: Server capability parity matrix Summary

**A tracked 181-row capability parity matrix at its D-08 path plus a Vitest guard that fails CI when a workspace tab, a `hostingService` method or an action id has no row, or when status, relocation or removal rules are broken.**

## Performance

- **Duration:** about 7 min (start 2026-11-18T17:32Z, end 2026-11-18T17:39Z)
- **Tasks:** 2 (Task 1 auto, Task 2 auto with tdd flag)
- **Files changed:** 3 (2 created, 1 modified)

## Accomplishments

- `61-PARITY-MATRIX.md`: English preface (purpose, status vocabulary, never-delete rule) and one table with the exact header `| ID | Capability | Today | Status | New location | Phase | Evidence |`. 181 rows: TAB 9, SVC 51 (every key of `hostingService`, each naming its calling screens found by searching the sources), ACT 7, plus FLT, FACT, CHG, FIL, JOB, REC, CRT and ADJ rows found by reading FleetPages, components, ServerWorkspacePage, the five panels, CreateServerPage, createServerModel, ProfileMenu, CommandOverviewPage, the configuration-profile pages and the finance pages. Items beyond the plan's seed list include the command-center attention list, deep links (`action` and `tab` query parameters), credential status, the game-specific configuration contribution and placement estimate, folder upload and collision handling, and the create-form aside and validation states.
- `parity-matrix.test.ts`: 74 cases (10 fixed cases plus `it.each` over 9 tab ids, 51 service methods and 7 action ids). Missing file throws a message naming the D-08 path and stating the file must be tracked and present in the CI source transfer, so every case fails rather than passing vacuously.
- `ServerWorkspacePage.tsx`: only added `export` to `WorkspaceTab` and `workspaceTabIds`.

## Task Commits

1. **Task 1: Write the parity matrix by reading the code** - `13358f28` (docs)
2. **Task 2: Completeness guard and the tab id export** - `91334ea9` (test)

**Plan metadata:** committed separately (docs commit carrying this SUMMARY, STATE.md and ROADMAP.md).

## Verification Results

- Task 1 automated script: 181 rows, 0 rows without exactly seven cells (exit 0); `TAB-` rows 9, `ACT-` rows 7, `SVC-` rows 51; header line matches exactly; every Status cell is `pending`.
- `npm --prefix sources/ReactApp test -- tests/hosting/parity-matrix.test.ts`: 74 passed.
- `npm --prefix sources/ReactApp run typecheck`: clean. ESLint (`--max-warnings 0`) and Prettier check clean on both touched source files.
- Negative proofs: removing the `SVC-revealSecret` row failed exactly one case with `missing parity matrix row SVC-revealSecret`; renaming the matrix away failed all 74 cases with the explicit D-08 message. Both were restored; `git ls-files --error-unmatch` confirms the matrix is tracked.
- `grep -c "export const workspaceTabIds"` equals 1.

## Decisions Made

See `key-decisions` in the frontmatter. The matrix intentionally records `ACT-start` and `ACT-stop` under Phase 62 (the roadmap names start/stop in Fleet and Quick Actions) rather than the plan's blanket "lifecycle actions 63".

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Matrix path resolution without `new URL(..., import.meta.url)`**
- **Found during:** Task 2
- **Issue:** The plan prescribed `fileURLToPath(new URL("../../../../.planning/...", import.meta.url))`. Vite treats that literal pattern as an asset import and the suite failed to load with `Denied ID .../AGENTS.md?url`.
- **Fix:** Resolve the same path with `resolve(dirname(fileURLToPath(import.meta.url)), "../../../..", relativePath)`. The target (four levels up to the repository root, D-08 path) is unchanged; a comment in the test records why.
- **Files modified:** `sources/ReactApp/tests/hosting/parity-matrix.test.ts`
- **Verification:** 74 tests pass; negative proofs above.
- **Commit:** `91334ea9`

**Total deviations:** 1 auto-fixed (1 blocking). **Impact:** none on behavior; same file, same path semantics.

## Deferred Issues

None.

## Known Stubs

None.

## Threat Flags

None. T-61-09 is mitigated by the per-tab, per-method and per-action row requirement plus the removal sign-off rule; T-61-10 is mitigated by the fail-closed missing-file error and by committing the matrix with this plan. Residual risk A1 (does the Mozaic source transfer carry `.planning/`) stays open and is flagged for human confirmation on the first CI run.

## Next Phase Readiness

Phases 62 to 69 can flip rows from `pending` and must record `relocated` targets and `removed` sign-offs. The guard runs under `npm test` and therefore under `npm run ci`.

## Self-Check: PASSED

- Matrix and test files exist on disk; commits `13358f28` and `91334ea9` are ancestors of HEAD; acceptance criteria and the plan-level verification re-ran green.
