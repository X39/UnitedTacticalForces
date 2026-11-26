---
phase: 68-auto-stop-schedules
plan: 20
subsystem: ui
tags: [react, css, button, i18n, playwright, vitest, gap-closure]
gap_closure: true
gap_ids: [G-68-1]

requires:
  - phase: 68-auto-stop-schedules
    provides: the workspace header cost-stop button and the cost confirmation dialog that UAT test 1 measured
provides:
  - "Shared .button rule whose label wraps like the surrounding text (white-space: inherit, overflow-wrap: break-word)"
  - "German 320 px Playwright check for the header cost-stop button and the confirmation button"
  - "Vitest guard for the shared wrap contract and against any new page-local .button wrap override"
affects: [phase-70-ui-polishing, any consumer of ActionButton, ActionLink and ActionAnchor]

actuals:
  tokens: 3000
  tasks: 2
  commits: 2
plan_head_before: 8f99917f558e0305a76df0b615a5b7e3e65a1e9a
plan_head_after: abbe2a42dd7ff70ad86aa811ad75c92103617798

tech-stack:
  added: []
  patterns:
    - "A shared control inherits white-space; a container that must keep buttons on one line says so itself"

key-files:
  created:
    - sources/ReactApp/tests/styles/button-wrap.test.ts
  modified:
    - sources/ReactApp/src/styles.css
    - sources/ReactApp/src/components/server/cost-alerts.css
    - sources/ReactApp/src/components/server/fleet-card.css
    - sources/ReactApp/src/components/server/fleet-toolbar.css
    - sources/ReactApp/src/features/hosting/cost-alert-settings.css
    - sources/ReactApp/tests/e2e/hosting-workspace.spec.ts

key-decisions:
  - "Base rule uses white-space: inherit, not normal, so single-line containers (command topbar, hosting/finance/member table cells) keep their buttons on one line without a new rule"
  - "overflow-wrap: break-word, not anywhere, so min-content width and flex/grid sizing of buttons do not change"
  - "ActionControl.tsx gets no new prop; no consumer needs an opt-in or opt-out"

requirements-completed: [AUT-01, AUT-02, AUT-03]

coverage:
  - id: D1
    description: "The German cost-stop label wraps inside the workspace header button and the cost confirmation button at 320 px"
    requirement: "AUT-01"
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-workspace.spec.ts#the German cost-stop label wraps inside the header button and the confirmation button"
        status: pass
    human_judgment: false
  - id: D2
    description: "The shared .button rule wraps its label and no stylesheet re-adds a page-local wrap override"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/styles/button-wrap.test.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Existing 320/390 px layout gates (fleet, cost alerts, public shell, workspace, automation) stay green after the six local overrides were removed"
    requirement: "AUT-03"
    verification:
      - kind: e2e
        ref: "npm --prefix sources/ReactApp run test:e2e -- hosting-fleet hosting-cost-alerts public-shell hosting-workspace hosting-automation (94 passed, 36 skipped)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The wrapped German label looks acceptable on the real seeded stack"
    verification: []
    human_judgment: true
    rationale: "Deferred follow-up: post-gap-closure seeded-stack re-checks run once at the milestone end (user preference)"

duration: 14min
completed: 2026-11-25
status: complete
---

# Phase 68 Plan 20: Shared button wrap (G-68-1) Summary

**The shared `.button` label now wraps (`white-space: inherit`, `overflow-wrap: break-word`), so the German "Herunterfahren und Abrechnung beenden" stays inside the workspace header and confirmation buttons at 320 px, and the six page-local wrap workarounds are gone.**

## Performance

- **Duration:** 14 min
- **Tasks:** 2 of 2
- **Files modified:** 7 (1 created)

## Accomplishments

- Tracer: wrote the German 320x568 Playwright test first, saw it fail on the unchanged stylesheet (RED), changed the shared rule, saw it pass (GREEN).
- Removed the public hero override (Task 1) and the cost alert banner, fleet card, filter chip, fleet live notice and cost alert settings overrides (Task 2); the fleet card, chip and notice rules keep their other declarations.
- Added `button-wrap.test.ts`: it parses the top-level `.button` rule (comments stripped, brace-depth aware), requires `white-space: inherit` exactly once and `overflow-wrap: break-word`, and scans every stylesheet under `src` for a `.button` rule that forces `white-space: normal`. Each check has an in-memory negative sample so it cannot pass vacuously.

## RED and GREEN (Task 1)

| Run | Header button | Result |
|-----|---------------|--------|
| RED (unchanged CSS) | scrollWidth 266 against clientWidth 250 | failed: `Expected <= 251, Received 266` |
| GREEN (shared rule changed) | label inside the box, edges inside the 320 px viewport | passed; whole `hosting-workspace.spec.ts` 13 passed |

The confirmation button was not reached in RED (the header assertion fails first); in GREEN its scrollWidth is within clientWidth + 1 and inside the viewport.

## Audit of white-space rules around `.button` (Task 2)

Every `white-space` declaration under `sources/ReactApp/src` was listed with its selector. Rows that can contain a `.button`:

| Rule (file) | Value | Buttons should | Observed after the change |
|-------------|-------|----------------|---------------------------|
| `.button` (styles.css) | inherit | wrap like the context | wraps; proven by the 320 px German e2e |
| `.command-topbar__actions .button` (styles.css) | nowrap | stay on one line | unchanged explicit rule, still one line |
| `.next-hosting-table-scroll th, td` (hosting.css) | nowrap | stay on one line | row buttons inherit nowrap, one line |
| `.finance-table th`, `.finance-table .number` (finance.css) | nowrap | stay on one line | inherited where a button sits in a cell |
| `.em-table-scroll td:last-child, th:last-child` (eventsMember.css) | nowrap | stay on one line | row action buttons inherit nowrap |
| `.next-hosting-ledger-context` (hosting.css) | normal !important | wrap | cell is not a `.button`; unchanged |
| `.sr-only` (styles.css) | nowrap | n/a (icon-only button text) | unchanged; `.button--icon-only` keeps its fixed width |
| span/strong/small/summary/nav rules (commandCenter, ProfileMenu, wiki, modPacks, events, calendar, pickers, gallery) | nowrap | n/a | target text elements, not `.button`; unaffected |
| `pre`/`pre-wrap` rules (activity, credentials, hosting, verein) | pre / pre-wrap | n/a | code and prose blocks; unaffected |
| `normal` on badges, kicker, summary, notification title (server.css, fleet-card.css, create-options.css, cost-alerts.css, cost-alert-settings.css, hosting.css) | normal | n/a | not `.button`; unaffected |

No container that should wrap turned out to force one line, so no override was needed. No ancestor of the workspace header or the cost dialog sets a no-wrap value.

## Verification

- `vitest run tests/styles`: 82 passed (button-wrap 5 + 4 + per-stylesheet cases, server-style-guard, tokens-contrast).
- Full `npm test`: 2600 passed (178 files) on the final run.
- `npm run lint`: clean.
- Playwright gates (fleet, cost alerts, public shell, workspace, automation): 94 passed, 36 skipped (skips are pre-existing in those specs, not added here).
- Acceptance greps of both tasks: all at the expected value (nowrap 0, inherit 1, break-word 1, hero 0, topbar rule 1, banner 0, settings 0, fleet-card rule kept 1, chip 0, notice 0, stale comments 0, `label wraps inside` 1); `git diff` against the base shows no change to `ActionControl.tsx` or `sources/WebApp`.

Deferred follow-up: re-check the German header button at 320 px on the real seeded stack at the milestone end (user preference for post-gap-closure seeded-stack re-checks).

## Deviations from Plan

### Auto-fixed Issues

None to the code. Environment notes:

1. **[Rule 3 - Blocker] Worktree had no `node_modules`.** Ran `npm ci` from the committed `package-lock.json` (no package added or changed; Node 25 against `engines >=24 <25` only printed a warning).
2. **`npm run format:check` reports 524 files.** Pre-existing and environmental: this Windows checkout carries CRLF line endings, so even untouched files such as `ActionControl.tsx` fail. All seven files this plan touched pass `prettier --check` after `prettier --write` (git normalizes line endings on commit). Not fixed (out of scope).
3. **Two flakes under parallel load, not reproducible.** One full-suite Playwright run failed `hosting-automation.spec.ts` "German notice title..." (title y = -1.375 against tolerance -1) and one Vitest run failed 3 tests in a file-browser test file. Both passed on re-run (the automation test 3 of 3 in isolation, the full gate 94/94, the full Vitest run 2600/2600); neither touches the changed rules.
4. `src/generated/api.ts` is rewritten by the build (`generate` step) with line-ending noise only; it was never staged and was restored with `git checkout -- <file>`.

**Total deviations:** 0 code deviations, 4 environment notes. **Impact:** none on scope or outcome.

## Known Stubs

None.

## Threat Flags

None. T-68-45 (clipped cost-stop label) is mitigated by the 320 px German e2e test and the Vitest guard.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/styles/button-wrap.test.ts
- FOUND commits 1f07e904 (fix) and abbe2a42 (refactor) on `worktree-agent-a85586d88a25ea6e6`
