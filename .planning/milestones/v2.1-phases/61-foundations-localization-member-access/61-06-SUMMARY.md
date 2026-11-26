---
phase: 61-foundations-localization-member-access
plan: 06
subsystem: webapp-tooling
status: complete
tags: [npm, supply-chain, playwright, axe-core, accessibility]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-01 React app foundations and the Playwright test setup"
provides:
  - "@axe-core/playwright 4.13.0 as an exact-pinned devDependency of sources/ReactApp"
  - "axe-core 4.13.0 resolved in the lockfile (not the 4.14.1 latest tag)"
affects: [61-13]

actuals:
  tokens: 2500
  tasks: 2
  commits: 1
plan_head_before: 3e724d2cdc7465a9656d4213845ab90cb81f6391
plan_head_after: 03045771aa5468c59e2983c34b5894603e7d6d91

tech-stack:
  added:
    - "@axe-core/playwright 4.13.0 (dev)"
    - "axe-core 4.13.0 (dev, transitive)"
  patterns:
    - "Supply-chain gate: blocking human legitimacy approval, then exact pin with --ignore-scripts, then a lockfile scope assertion"

key-files:
  created: []
  modified:
    - sources/ReactApp/package.json
    - sources/ReactApp/package-lock.json

key-decisions:
  - "Pinned @axe-core/playwright exactly to 4.13.0; its ~4.13.0 range resolves axe-core to 4.13.0, the only stable 4.13.x, avoiding the two-day-old 4.14.1 tag"

requirements-completed: [FND-04, FND-05]

coverage:
  - id: D1
    description: "@axe-core/playwright is pinned exactly to 4.13.0 and the lockfile adds only it and axe-core 4.13.0"
    requirement: "FND-04"
    verification:
      - kind: command
        ref: "lockfile assertion script from 61-06-PLAN.md Task 2 (exit 0, added: node_modules/@axe-core/playwright,node_modules/axe-core, no changed or removed entries)"
        status: pass
      - kind: command
        ref: "grep -c '\"@axe-core/playwright\": \"4.13.0\"' sources/ReactApp/package.json (1)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The install ran with lifecycle scripts disabled after the human legitimacy checkpoint was approved"
    requirement: "FND-05"
    verification:
      - kind: command
        ref: "npm --prefix sources/ReactApp run typecheck"
        status: pass
    human_judgment: true
    rationale: "Ordering (approval before install) and --ignore-scripts are process facts recorded here, not asserted by a test"

duration: 2 min
completed: 2026-11-18
---

# Phase 61 Plan 06: Axe-core Playwright Dev Dependency Summary

**`@axe-core/playwright` 4.13.0 added as an exact-pinned dev dependency with `axe-core` resolved to 4.13.0 and a verified two-entry lockfile change, so Plan 61-13 can run real-Chromium WCAG scans.**

## Performance

- **Duration:** 2 min
- **Tasks:** 2 (Task 1 human checkpoint, Task 2 auto)
- **Files:** 2 modified

## Accomplishments

- Task 1 (blocking-human legitimacy checkpoint) was approved by the user in this run ("Approved") before any install ran. It was not auto-approved.
- Ran `npm install --save-dev --save-exact --ignore-scripts @axe-core/playwright@4.13.0` from `sources/ReactApp`: 2 packages added.
- Hand-reviewed the lockfile diff (`audit=false` in `.npmrc`): the only new entries are `node_modules/@axe-core/playwright` and `node_modules/axe-core`, both 4.13.0, both `dev: true`, license MPL-2.0, no install scripts. No existing entry changed version and none was removed. `@axe-core/playwright` declares `axe-core ~4.13.0` and the peer `playwright-core >= 1.0.0`, satisfied by the existing `@playwright/test` 1.62.1.

## Task Commits

1. Task 1: axe-core legitimacy checkpoint - approved by the user, no commit
2. Task 2: pinned dev dependency - `03045771` (build)

## Verification

- Lockfile assertion script: exit 0; `added: node_modules/@axe-core/playwright,node_modules/axe-core`; no changed or removed versions
- `grep -c '"@axe-core/playwright": "4.13.0"' sources/ReactApp/package.json`: 1 (HEAD was 0)
- `npm --prefix sources/ReactApp run typecheck`: exit 0
- T-61-SC: `--ignore-scripts` used and approval preceded install

## Deviations from Plan

None - plan executed exactly as written.

## Authentication Gates

None. The human-verify checkpoint (Task 1) was resolved by the user before this executor started.

## Known Stubs

None.

## Threat Flags

None. The only change is the two approved dev packages; they are never bundled into the shipped app.

## Notes

- The install printed an `EBADENGINE` warning (local Node 25.5.0 against the project's `>=24 <25`). `.npmrc` sets `engine-strict=false`; the warning is environmental, pre-existing, and unrelated to this plan.
- The assumption A4 (4.13.0 compatibility with Playwright 1.62.1 Chromium) is exercised by Plan 61-13; if it fails there, the recorded fallback (4.14.0 plus an `axe-core` override) needs a new checkpoint approval.

## Next Phase Readiness

Plan 61-13 can import `@axe-core/playwright`. No source or config files were created here.

## Self-Check: PASSED

- FOUND: sources/ReactApp/package.json, sources/ReactApp/package-lock.json (modified)
- FOUND commit: 03045771
