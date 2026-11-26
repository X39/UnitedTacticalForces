---
phase: 69-consistency-accessibility-verification
plan: 30
subsystem: testing
tags: [playwright, e2e, journeys, german-locale, automation, focus-management, react]

requires:
  - phase: 69-05
    provides: "Playwright projects chromium, mobile-390 and mobile-320; journeys folder runs in chromium and mobile-390"
  - phase: 69-06
    provides: "installApp strict mock layer (jsonHandler, ApiHandler, MockSession) and the unexpected-request record"
  - phase: 69-25
    provides: "assertNoGermanLeak"
  - phase: 68-server-automation
    provides: "automationMocks.ts (automationBlock, AutomationSaveBody) and the Automation panel"
provides:
  - "tests/e2e/support/journeyMocks.ts: installJourney, a stateful API and update-stream mock (create, start, stop, mod pack change, automation save) with pushRunning, pushStopped and pushChanged"
  - "Four end-to-end journey specs (J-1 create then start, J-2 change mods, J-3 German locale, J-4 automation), each run in chromium and mobile-390"
  - "Fix: focus no longer falls to the page body after a confirmed server action"
affects: [69-31, 69-32, 69-33, 69-34]

actuals:
  tokens: 15600
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "One stateful journey mock composed from installApp handlers: reads answer from mutable state, writes change that state the way the control plane does, push helpers move a server on and send the fleet-changed frame"
    - "Keyboard-only leave guard walk: focus the section link, Enter, then Tab stops to Stay, Discard and Review"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/journeyMocks.ts
    - sources/ReactApp/tests/e2e/journeys/create-then-start.spec.ts
    - sources/ReactApp/tests/e2e/journeys/change-mods.spec.ts
    - sources/ReactApp/tests/e2e/journeys/german-locale.spec.ts
    - sources/ReactApp/tests/e2e/journeys/automation.spec.ts
  modified:
    - sources/ReactApp/src/components/server/CostConfirmationProvider.tsx
    - sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx

key-decisions:
  - "J-4 uses installJourney with automationBlock and AutomationSaveBody from automationMocks.ts, not mockAutomationRoutes: both layers register one catch-all route that aborts what it does not know, so they cannot be composed; the journey mock implements the automation save itself and computes the saved next action"
  - "installJourney and JourneyState carry additive members beyond the plan's interface (alerts, requests, pushStopped, pushChanged, servers option) because J-1 needs the stop half, J-2 needs the recorded writes and J-4 needs the alert list"
  - "The focus assertion after a confirmed action accepts the header action or the workspace heading: a confirmed action turns its control into the pending In progress button, which React mounts anew, so the documented fallback target is the heading"

patterns-established:
  - "Journeys never import src at runtime; types only, and every journey ends with expect(session.unexpected).toEqual([])"

requirements-completed: [VER-02, L10N-03]

coverage:
  - id: D1
    description: "J-1: from the fleet create a server with name and preset, Start it now, shared cost confirmation facts, progress strip names the stage, Running with the cost chip after a pushed frame, Stop through the confirmation, focus kept, toast text, no raw stage text, back to Off after the pushed stop"
    requirement: VER-02
    verification:
      - kind: e2e
        ref: "tests/e2e/journeys/create-then-start.spec.ts (chromium and mobile-390, 3 repeats each)"
        status: pass
    human_judgment: false
  - id: D2
    description: "J-2: pick another mod pack, staged count 1, review diff with added and removed mods, apply, pending-start banner, leave guard stay, review and discard by keyboard"
    requirement: VER-02
    verification:
      - kind: e2e
        ref: "tests/e2e/journeys/change-mods.spec.ts (chromium and mobile-390, 3 repeats each)"
        status: pass
    human_judgment: false
  - id: D3
    description: "J-3: J-1 and the first half of J-2 in German with assertNoGermanLeak after every step, German action and dismiss labels, html lang de, German currency format and German toast close label"
    requirement: L10N-03
    verification:
      - kind: e2e
        ref: "tests/e2e/journeys/german-locale.spec.ts (chromium and mobile-390, 3 repeats each)"
        status: pass
    human_judgment: false
  - id: D4
    description: "J-4: idle shutdown 30 minutes, Friday 18:00 start rule in Europe/Berlin, consent dialog facts, status list shows the saved next action and not the draft, a pushed pending stop shows the notice above the cost alert banner and exactly one toast, then clears with the cancelled toast, focus unchanged"
    requirement: VER-02
    verification:
      - kind: e2e
        ref: "tests/e2e/journeys/automation.spec.ts (chromium and mobile-390, 3 repeats each)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Defect fix: focus is placed again after the refreshed server replaces the control that asked for a confirmed action"
    requirement: VER-02
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirmation-provider.test.tsx#places focus again when the refreshed server replaces the control that asked (red without the fix, green with it)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/journeys/create-then-start.spec.ts (mobile-390 failed 3 of 3 before the fix, passes after)"
        status: pass
    human_judgment: false

duration: ~75min
completed: 2026-11-25
status: complete
plan_head_before: ddbb4944079e1b33dda28b5f98a1676ad9894ff1
plan_head_after: 74f25bbd58348a148eb6963518307810ea0de798
commits: 4
---

# Phase 69 Plan 30: End-to-end journeys Summary

**A stateful journey mock and four journeys (create then start, change mods, German locale, automation) that pass in chromium and the 390 phone width against the strict mock layer, and that exposed one real defect: focus fell to the page body after a confirmed server action on a phone, now fixed in the shared confirmation provider.**

## Performance

- **Duration:** about 75 min (start time was not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (tracer, journeys J-2 and J-3, journey J-4), plus one defect fix commit
- **Files:** 5 created, 2 modified

## Accomplishments

- `installJourney` composes `installApp` with handlers over one mutable state (fleet, jobs, alerts). A create adds an off server, a confirmed start or stop queues a job and moves the deployment, a mod pack change marks the Mods area as waiting for the next start, and an automation save stores the rules and calculates the next scheduled action. `pushRunning`, `pushStopped` and `pushChanged` move a server on and send the fleet-changed frame. Any request no handler names is aborted and recorded.
- J-1 walks the whole lifecycle: create with name and the Operations preset, "Start it now", the shared start confirmation (cost, owner and how-to-stop facts, dismiss focused), confirm, the progress strip naming "Preparing the cloud machine" (never the raw `provider-creating` token), Running with the "Costing now" chip after a pushed frame, Stop through the confirmation with its toast and the "Releasing the cloud machine" step, and Off with "Not costing" after the pushed stop. The create and both action requests are asserted in order.
- J-2 stages a pack change (count 1), reviews the diff (Added Mod C, Removed Mod A), applies it (toast and the pending-start banner naming the Mod pack), then stages another change and drives the leave guard by keyboard only: Enter on a section link, Stay by Enter, Review by two Tab stops, Discard by one.
- J-3 replays J-1 and the first half of J-2 with `UtfCulture=de`, `html lang="de"` and `assertNoGermanLeak` after every step; it checks the German action and dismiss labels ("Server starten", "Server nicht starten", "Server weiterlaufen lassen"), the German toast close label, and the cost in German format (`0,05 € pro Stunde, solange er läuft`). No German leak was found anywhere in the flow.
- J-4 saves idle shutdown (30 minutes) and a Friday 18:00 Start rule in Europe/Berlin through the consent dialog (owner, hourly cost, "1 schedule starts the server"), shows that the status list reads "No schedules." for the draft and the saved next action after the save, then pushes a pending stop: the shell notice sits above the cost alert banner (document order and top edge), exactly one warning toast appears (a redelivered frame adds none), and the cancelled toast follows the clear, with the focused idle checkbox never losing focus.

## Task Commits

1. **Tracer task 1: J-1 create then start** - `966acd72` (test), preceded by the defect fix `8e50eec1` (fix)
2. **Task 2: J-2 change mods and J-3 German locale** - `b9f74464` (test)
3. **Task 3: J-4 automation** - `74f25bbd` (test)

**Plan metadata:** the SUMMARY commit that follows (docs).

## Files Created/Modified

- `sources/ReactApp/tests/e2e/support/journeyMocks.ts` - stateful journey mock
- `sources/ReactApp/tests/e2e/journeys/create-then-start.spec.ts` - J-1
- `sources/ReactApp/tests/e2e/journeys/change-mods.spec.ts` - J-2
- `sources/ReactApp/tests/e2e/journeys/german-locale.spec.ts` - J-3
- `sources/ReactApp/tests/e2e/journeys/automation.spec.ts` - J-4
- `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx` - focus placed again after the refreshed server renders (fix)
- `sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx` - regression test for that fix

## Decisions Made

See `key-decisions` in the frontmatter. In short: J-4 runs on `installJourney` plus the value helpers of `automationMocks.ts` because the two route layers cannot be composed; the journey interface gained additive members; the focus assertion accepts the header action or the workspace heading.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Focus fell to the page body after a confirmed server action on the phone width**
- **Found during:** Task 1 (J-1, the Stop confirmation, `mobile-390`; failed 3 of 3 runs)
- **Issue:** The shared confirmation returns focus to the control that asked in a `requestAnimationFrame`. A confirmed action turns that control into the pending "In progress" button, which `ActionButton` wraps in a new `span`, so React mounts a new button when the refreshed server arrives and the control that had just received focus disappears. Focus then falls to the body: a keyboard user loses their place. On desktop another effect happened to land on the heading by timing; on the phone nothing did. A focus event log confirmed the sequence.
- **Fix:** `CostConfirmationProvider` collects the invalidation promises of a confirmed action and, once they settle and the refreshed tree has rendered (two animation frames), places focus again by the same rule as before (supplied target, invoker, workspace heading) but only if focus is on the body, so a move the member made meanwhile is never undone.
- **Files modified:** `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx`, `sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx`
- **Verification:** new Vitest case fails without the fix (focus on body) and passes with it; the existing 21 provider tests and the create-start flow tests pass (48 tests); J-1 passes in both projects, 3 repeats each.
- **Committed in:** `8e50eec1`

**Total deviations:** 1 auto-fixed (1 bug, in a `components/server` file this plan owns). **Impact:** the journey did what it exists for; the defect was real and user visible.

### Verification environment notes (not deviations)

- The plan's verify commands use the default Playwright config (ports 4173 and 4174 with `reuseExistingServer`). Because sibling executors share the machine, all runs used a temporary private-port copy of the config (ports 46731 and 46732, `reuseExistingServer: false`, chromium and mobile-390 only, pre-built `dist`), removed before the final commit. The test files and the specs under test are the committed ones.
- `node_modules` is a directory junction to the main checkout's install (no package installed). `npm run build:assets` rewrote `src/generated/api.ts` with line endings only; it was restored with `git checkout` each time and never committed.
- Prettier was run only on the files this plan touched.

## Issues Encountered

- A lint run over the whole project reports one parsing error for the temporary Playwright config (not part of the tsconfig project); the file was deleted before the final lint, which is clean (`npx tsc -b` exit 0, `npx eslint . --max-warnings 0` exit 0).
- The whole existing chromium suite next to the new journeys (private-port config, 4 workers): 452 passed, 77 skipped by their own conditions, 5 failed. Re-run alone, the five still fail and none touches the cost confirmation provider or a journey file: `hosting-fleet.spec.ts:558` (known, 69-05), `public-shell.spec.ts:406` (known, 69-21 and 69-23), `public-shell.spec.ts:809` and `:1367` (gallery heading and picker dialog) and `hosting-files-secrets.spec.ts:794` at 320x568 (2 px button drift). They are logged in `deferred-items-69-30.md` and were not fixed here.

## Known Stubs

None. The mod pack contents, cost figures and server names are synthetic fixtures; no value flows to the UI from an unwired source.

## Threat Flags

None. Test-only code plus one focus-management change; the journeys never reach a real backend (T-69-36): every journey asserts the unexpected request list is empty, and no package was installed (T-69-SC).

## Next Phase Readiness

- Plans 69-31 and 69-32 can reuse `installJourney` and its push helpers.
- Five existing specs fail in the whole-suite run for reasons outside this plan (`deferred-items-69-30.md`); 69-33 and 69-34 should pick them up.

## Self-Check: PASSED

- All five created files exist; commits `8e50eec1`, `966acd72`, `b9f74464` and `74f25bbd` are ancestors of HEAD.
- Acceptance criteria: `pushRunning` in the J-1 spec (1), `export async function installJourney` (1), `assertNoGermanLeak` in the J-3 spec (3), `Server starten` (1), `automationMocks` in the J-4 spec (1).
- `npx playwright test tests/e2e/journeys` on the private-port config: 24 passed (4 specs x chromium and mobile-390 x 3 repeats). `npx tsc -b` exit 0, `npx eslint . --max-warnings 0` exit 0, Vitest provider and create-start tests 48 passed.
- The default-port `npm run test:e2e` was deliberately not used (sibling executors share ports 4173 and 4174); the private-port config differs only in ports, `reuseExistingServer`, the pre-built `dist` and the omitted `mobile-320` project.
