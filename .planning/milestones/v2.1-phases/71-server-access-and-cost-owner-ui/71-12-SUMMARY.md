---
phase: 71-server-access-and-cost-owner-ui
plan: 12
subsystem: testing
tags: [playwright, e2e, screen-registry, axe, reflow, german-walk, key-screens, phase-gate, search-picker, layout-shift]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-11 stateful access mock, accessState and canManageAccess options of installWorkspace"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-09 mutable server record, cost owner options and transfer handler of workspaceMocks.ts"
provides:
  - "Registry entries S-15 (13 Access panel states), S-16 (6 Cost owner states), O-10 (3 transfer dialog states) and O-11 (3 access dialog states), all audited on chromium, mobile-390 and mobile-320 in English and German"
  - "Key screens K-18 (S-15, one row open, element capture of the panel) and K-19 (O-10 idle); keyScreenCount is 19"
  - "WorkspaceControls and workspaceControlsOf(page): patchServer, setTransfer (including a held outcome), releaseTransfers and the access state, so one page load walks through several states"
  - "AccessMockState.heldSearch and failingSearch: a candidate search that is never answered (loading) or refused (error)"
  - "ScreenState.dialogStaysOpen: a state whose dialog Escape cannot close (the busy transfer dialog) is not reported by the keyboard walk"
  - "Four product fixes the new states exposed: SearchPicker listbox children, idle cost owner picker, Transfer cell layout shift, German leak check for claim ids"
affects: [milestone-uat, visual-baselines]

actuals:
  tokens: 13500
  tasks: 3
  commits: 6
plan_head_before: c8d7dabb9d508403d3aefa8d84921c41adf0cc7d
plan_head_after: c5c1dc214f3e65575cb3fe9d5f776d2b6abcf82c

tech-stack:
  added: []
  patterns:
    - "A registry state reaches the next state through WorkspaceControls (change the mocked record, then Refresh server or a visibilitychange) instead of a second page load"
    - "A state that must wait for a translated text waits for the text to change from the previous reading (WeakMap per page) or for a structural element"
    - "A listbox owns only options; loading is marked with aria-busy on the listbox, the empty line is aria-hidden (the status region announces it), the error box sits beside the listbox"

key-files:
  created: []
  modified:
    - sources/ReactApp/tests/e2e/support/screens/registryTypes.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
    - sources/ReactApp/tests/e2e/support/keyScreens.ts
    - sources/ReactApp/tests/e2e/audit/a11y-audit.spec.ts
    - sources/ReactApp/tests/e2e/mobile/key-screens.spec.ts
    - sources/ReactApp/tests/e2e/mobile/visual-stability.spec.ts
    - sources/ReactApp/tests/e2e/hosting-cost-owner.spec.ts
    - sources/ReactApp/tests/e2e/support/germanLeak.ts
    - sources/ReactApp/tests/audit/german-leak.test.ts
    - sources/ReactApp/src/components/SearchPicker.tsx
    - sources/ReactApp/src/components/search-picker.css
    - sources/ReactApp/src/components/server/cost-owner.css
    - sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx

key-decisions:
  - "Both panels run the keyboard audit off (justified in the entries): hosting-access.spec.ts and hosting-cost-owner.spec.ts walk them with the keyboard, and more walked states exceed the test timeout"
  - "O-10 keeps the keyboard walk on and uses a new per-state flag dialogStaysOpen for the busy dialog, instead of switching the whole entry off, so the idle and the error dialog are still walked"
  - "S-15 forces canManageAccess true, because the last state empties the list and the signed-in member with it; without the force the panel would unmount through the lost-access flow (XPF-71-10-3)"
  - "The filter-with-none state first gives the member without View the View permission and refetches, because the filter never hides a subject that cannot see the server (D-16); the empty result is otherwise unreachable"

patterns-established:
  - "installWorkspace + workspaceControlsOf(page) for registry states that change the mock after the page loaded"

requirements-completed: [D-18, D-19, D-20]

coverage:
  - id: R1
    description: "S-15, S-16, O-10 and O-11 are in the required registry and the slice, with every state of the UI-SPEC; the completeness guard passes and no UI-SPEC id (S-09, S-10, O-04, O-05) was reused"
    requirement: "D-19, research Critical Finding 1"
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts (12 passed)"
        status: pass
    human_judgment: false
  - id: R2
    description: "Every new state passes axe WCAG 2.0 to 2.2 AA, reflow, text spacing, the German walk and the heading outline on chromium, mobile-390 and mobile-320"
    requirement: "D-19"
    verification:
      - kind: e2e
        ref: "tests/e2e/audit --grep=S-1[56] and --grep=O-1[01], three projects: 18 and 18 passed"
        status: pass
      - kind: e2e
        ref: "phase gate run: 857 passed, 3 failed (German leak, fixed and rerun green), 1 flaky (route walk load flake, rerun alone 239 passed)"
        status: pass
    human_judgment: false
  - id: R3
    description: "Key screens K-18 and K-19 are registered, keyScreenCount is 19, and both captures run to the point of comparing with a baseline that milestone end creates"
    requirement: "UI-SPEC Verification Contract"
    verification:
      - kind: e2e
        ref: "tests/e2e/mobile/visual-stability.spec.ts#resolves the nineteen key screens K-01 to K-19"
        status: pass
      - kind: e2e
        ref: "tests/e2e/mobile/key-screens.spec.ts --grep=K-1[89] with UTF_VISUAL_BASELINES=1 --update-snapshots=none: 8 tests reach the capture and fail only on the missing baseline image"
        status: pass
    human_judgment: false
  - id: R4
    description: "The phase gate is green: dotnet test of the solution, the ReactApp ci steps one by one and the e2e audit and hosting specs on three projects; no dependency, ConfigurationProfileAccessSection.tsx or sources/WebApp change since 5fbb2d99"
    requirement: "D-18, D-19, D-20"
    verification:
      - kind: unit
        ref: "dotnet test X39.UnitedTacticalForces.slnx: 93 + 364 + 2 + 1792 passed, 0 failed"
        status: pass
      - kind: unit
        ref: "locales:check, locales:unused, contracts:check, prettier --end-of-line auto, lint, typecheck, vitest run --maxWorkers=4 (221 files, 3783 passed, 2 skipped), build:assets"
        status: pass
      - kind: other
        ref: "git diff --stat 5fbb2d99 -- package.json package-lock.json ConfigurationProfileAccessSection.tsx sources/WebApp prints nothing"
        status: pass
    human_judgment: false
  - id: R5
    description: "Spoken output of the combobox and switches, the visual look of both panels, the seeded-stack pass against the real API and the K-18, K-19 and cost strip baselines"
    requirement: "UI-SPEC Verification Contract (backstop)"
    verification:
      - kind: manual
        ref: "Deferred follow-up (milestone end), see below"
        status: deferred
    human_judgment: true

# Metrics
duration: 70min
completed: 2026-11-26
---

# Phase 71 Plan 12: Phase gate and registry closure Summary

**Every Phase 71 state is in the audited registry under the free ids S-15, S-16, O-10 and O-11, 19 key screens are registered, and the full gate is green; the new states exposed four real defects (a listbox that owned non-option children, an idle picker that reported itself busy, a 46 px layout shift in the Transfer cell, a German leak false positive on claim ids) that were fixed in the owning files.**

## Accomplishments

- **S-15 (Task 1, tracer):** thirteen states on one page load: collapsed with summary, open, one row open with all eight switches (K-18), the Full control subject (seven switches included and disabled), the no-View warning row, filter with results, filter with none, picker with results, loading, empty, error, with a chosen token, and the open empty list. The mock got `heldSearch` and `failingSearch` so loading and error are real states, and `WorkspaceControls` so a state can change the server, the access state and the transfer outcome. The tracer verify (S-15 on chromium and mobile-320, plus mobile-390) passed before Task 2.
- **S-16, O-10, O-11 (Task 2):** S-16 walks viewer without `canModify`, modifier, picker open with the empty query (four owners, one frozen and disabled), frozen current owner, transfer unavailable with a machine present and with jobs pending (each waits for the reason text to change). O-10 walks idle (K-19), busy (the PUT is parked, `dialogStaysOpen`) and the 409 error (the parked request is released with a 409). O-11 walks Remove all access, Remove all access on the own row and the self-removal confirmation.
- **Key screens:** K-18 is an element capture of `#server-access`, K-19 the viewport with the idle transfer dialog; `keyScreenCount` is 19 and the comment above `stateNameOf` names both as state-level baselines.
- **Gate (Task 3):** see Verification.

## Task Commits

1. **Product fix found by S-15 picker states** - `d8d51f38` (fix: SearchPicker keeps non-option lines out of the listbox)
2. **Task 1 (tracer): S-15 states, K-18, mock capabilities** - `d39c1f5b` (test)
3. **Product fix found by the key screen settle check** - `5d3f0243` (fix: cost owner picker idle until focused)
4. **Product fix for XPF-71-11-2** - `687129ab` (fix: Transfer cell keeps room for its reason, with a no-shift test)
5. **Task 2: S-16, O-10, O-11, K-19, count 19, dialogStaysOpen** - `aa7bdd12` (test)
6. **Task 3 gate fix: German leak check** - `c5c1dc21` (test)

Task 3 itself changed no file beyond that fix; the gate is a run, recorded below.

## Verification

- `dotnet test X39.UnitedTacticalForces.slnx`: X39.BI.Config.Parsing.Tests 93, CloudWorker.Tests 364, Ui.Tests 2, Api.Tests 1792 passed, 0 failed.
- ReactApp ci steps one by one: `locales:check` (4035 keys each, 0 failures), `locales:unused` (46 pre-existing warnings, none from Phase 71), `contracts:check` (no content diff in `src/generated/api.ts`, only the known line-ending noise, restored), `npx prettier --check . --end-of-line auto` clean, `lint` (max-warnings 0) clean, `typecheck` clean, `npx vitest run --maxWorkers=4` 221 files, 3783 passed, 2 skipped (pre-existing), `build:assets` built. **Substitution:** `npm run ci` was not run, because `format:check` fails on every file in a CRLF checkout (environmental); prettier with `--end-of-line auto` replaced it.
- e2e gate (audit folder plus `hosting-access`, `hosting-cost-owner`, `hosting-a11y`, `hosting-change`, `hosting-automation`, `hosting-files-secrets`, `hosting-workspace`, `hosting-cost-alerts` on chromium, mobile-390, mobile-320): first run 857 passed, 333 skipped (root specs do not run on the mobile projects), 3 failed and 1 flaky. The 3 failures were the German leak check of M-02 on the role page (fixed in `c5c1dc21`, then the whole German walk ran 141 passed); the flaky one was `route-walk /command/verein/settings (en)` under 12-worker load, and the whole route walk then ran alone with 2 workers: 239 passed.
- Guards: `git diff --stat 5fbb2d99 -- package.json package-lock.json ConfigurationProfileAccessSection.tsx sources/WebApp` prints nothing. No dependency was installed; the node_modules junction to the main checkout was removed before returning.
- Style guard `npx vitest run tests/styles`: 156 passed after each CSS change.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] SearchPicker listbox owned non-option children (axe aria-required-children, critical)**
- **Found during:** Task 1 (picker empty and picker error states)
- **Issue:** The empty line and the error alert (with its Retry button) sat inside `ul[role=listbox]`, which may only own options. Both picker flavors (Access grant form, Cost owner panel) are Phase 71 code, so no earlier screen was affected.
- **Fix:** The listbox is `aria-busy` while a search runs (its spinner line stays), the empty line is `aria-hidden` (the polite status region already announces "no match"), and the error line moved to a box next to the listbox with the same visual classes; an empty listbox is `display: none`. The error box keeps `mousedown` default prevention so Retry does not blur the input.
- **Files modified:** `SearchPicker.tsx`, `search-picker.css`
- **Commit:** `d8d51f38`

**2. [Rule 1 - Bug] Cost owner picker reported aria-busy before it was ever used**
- **Found during:** Task 2 (key screen capture of K-18 failed the settle check for a busy element)
- **Issue:** Until the first focus nothing is requested, but `owners.data === undefined` mapped to the `loading` status, which put `aria-busy="true"` on an idle combobox.
- **Fix:** The picker is `idle` until it was engaged by focus.
- **Files modified:** `ServerCostOwnerPanel.tsx`
- **Commit:** `5d3f0243`

**3. [Rule 1 - Bug] Transfer cell lost its reason line and moved the panels below (XPF-71-11-2)**
- **Found during:** Task 2 (new no-shift test, written because 71-11 flagged the Give access button)
- **Issue:** Choosing an owner removes "Choose the new cost owner first." under the Transfer button; the cell shrank and the Record panel below moved up 25 px on desktop (English) and 46 px on the phone widths and for the German text, which wraps in the narrow column.
- **Fix:** `.cost-owner-transfer__action` keeps the room of the button plus two reason lines at every width (desktop, touch and stacked variants), the same technique as `.access-grant__action`.
- **Files modified:** `cost-owner.css`, `hosting-cost-owner.spec.ts` (four no-shift tests: en, de, and de at 320 and 390)
- **Commit:** `687129ab`

**4. [Rule 3 - Blocking] German leak check flagged the claim id sv:operate on the role page**
- **Found during:** Task 3 (full gate, German walk of M-02)
- **Issue:** Phase 71 added the English value "Operate" (`Server_Access_Permission_Operate`), which now matches the raw claim id `sv:operate` the role page prints from API data.
- **Fix:** In `containsPhrase`, a phrase glued to a colon-joined prefix is the tail of an identifier, not prose. Unit tests pin both sides.
- **Files modified:** `germanLeak.ts`, `german-leak.test.ts`
- **Commit:** `c5c1dc21`

**5. [Rule 3 - Blocking] Registry and capture plumbing the plan did not list**
- `ScreenState.dialogStaysOpen` (registry types) and one line in `a11y-audit.spec.ts` pass `closeWithEscape: false` for it: a busy dialog ignores Escape on purpose, and the keyboard walk otherwise reports it. Chosen over turning the whole O-10 keyboard audit off.
- `keyScreens.ts` gets a K-18 element capture of `#server-access` (the open row is below the fold on a phone), and `visual-stability.spec.ts` / `key-screens.spec.ts` are updated for the new count (the resolution test pins K-01 to K-N).
- The self-removal switch click in O-11 is forced, because on the phone widths the visible switch track lies over the input and Playwright refuses the click; a pointer hits the track and toggles through the label.

### Plan wording interpreted

- **"Filter with none"** needed the no-View member made a normal member first (see key-decisions), so S-15 has one extra step in that state.
- **O-11 has three states** (remove all access, remove all access on the own row, self-removal), because the own-row variant of Remove all has its own copy and dialog text in addition to the permission self-removal the plan names.
- **The plan's ledger file** for commit counting could not be written inside the shared git directory of this isolated worktree; `commits` and `plan_head_before` were measured from the expected base commit.
- **The commit trailer** names Claude Sonnet 5.5 (the session attribution reminder), not the Opus 5.5 line of the environment notes, as plans 71-08 to 71-11 did.
- **S-16 and the page top:** the S-16 states settle the scroll at the page top, not at the panel. The page is short, and scrolling the panel to the top leaves the Expand all button half under the sticky header, which axe reported as a small target in English.

## Cross-plan findings resolved (XPF)

- **XPF-71-11-1:** done. S-15 and O-11 pass an `accessState`; S-16 and O-10 pass none, and their server record has no `canManageAccess`, so the Access panel never mounts there and `session.unexpected` stays empty.
- **XPF-71-11-2:** found and fixed (deviation 3), with a no-shift assertion in four tests.
- **XPF-71-11-3:** applied. Subject rows are opened with `getByRole("button", { name, exact: true })`; no `.access-row` text match addresses a subject (the class is used only to count rows and to find the open row).
- **XPF-71-07-2:** carried, not needed. The baseline `quietCostOwnerOptions` of `installApp` answers every router; all four new entries end with an empty `session.unexpected`.
- **XPF-71-10-2:** carried, not exercised in the registry (focus targets are asserted in `hosting-access.spec.ts`). **XPF-71-10-3:** applied, S-15 forces `canManageAccess` true so emptying the list does not trigger the lost-access flow; O-11 never confirms a revoke. **XPF-71-10-4:** applied, the filter state waits for `.access-filter__result`, which is mounted whenever the filter shows (S-15 has nine subjects).
- **XPF-71-09-2:** done, option locators are scoped to the panel listbox. **XPF-71-09-6:** checked, the registry entries live in the audit folder and run on chromium, mobile-390 and mobile-320 (18 tests per verify command); the root hosting specs run on chromium only, as before.
- **XPF-71-08-5:** did not occur; `metrics-performance.test.tsx` passed in the full vitest run.
- **XPF-71-01-2:** carried to the milestone: the older actions of `GameServersController` still lack XML docs (pre-existing); not touched here as instructed.
- **XPF-71-04-4:** decided. The `h2` of `EmptyState` inside the panel body (the states "filter with none" and "open empty") passes the heading audit (exactly one `h1`, first) on chromium, so no heading-level prop was added; the UI-SPEC wording `h3` stays a design note for the milestone UI review.
- **XPF-71-02-1 and XPF-71-05-2:** accepted. The 42 px clear control of the chosen token passes axe target size in the "picker with a chosen token" state on all three projects.
- **XPF-71-05-6:** carried. The 3fr/1fr grid split between 521 and 800 px is not a viewport of the audit (1280, 390, 320); it is part of the visual look under Deferred follow-up.
- **XPF-71-07-5:** applied, `src/generated/api.ts` was restored after every build and never committed.
- **Registry ids** S-15, S-16, O-10, O-11, K-18, K-19 and `keyScreenCount` 19 are as locked.

## Deferred follow-up

These are milestone-end human items. The standing policy defers real-environment, real-device, screen-reader and visual-baseline checks, so none stopped this plan. The orchestrator should carry each into UAT as a skipped item with the reason "Deferred follow-up: ...".

- **Deferred follow-up: visual look of both panels on the seeded stack.** From the repository root run `pwsh ./scripts/Start-Uat.ps1 -Fresh` (it opens the fleet page of the React app; stop everything with Ctrl+C). Open a UAT server you own, go to Settings and open Access: give a member access, open their row, switch Operate on and off, remove all access. Open Cost owner on a stopped server: choose another owner, transfer, check the cost strip. Narrow the browser to about 390 px once, and also look once between 521 and 800 px (XPF-71-05-6, the grid split of the grant form and the Transfer row), and switch the language to German. Expected: both panels look like the other Settings panels, every change applies at once and is announced, the transfer dialog says exactly what happens, nothing moves when a candidate or an owner is chosen, and the German layout fits without sideways scrolling.
- **Deferred follow-up: spoken output in NVDA and VoiceOver, English and German.** The member and role combobox (typing, loading, no match, error, choosing), the permission switches with their Granted and Not granted words, and the Cost owner combobox. Roles and states are asserted; the spoken quality is not. The empty candidate line is now `aria-hidden` and announced through the status region, so check that "no match" is spoken once.
- **Deferred follow-up: a seeded-stack pass of grant, revoke and transfer against the real API.** Grant View to a member, revoke it again, give a role access and remove all access, and transfer the cost owner of a stopped server, using the same start command. The automated flows run against the mocked API only.
- **Deferred follow-up: visual baselines.** `scripts/Update-VisualBaselines.ps1` in the pinned container creates the images for K-18 and K-19 (mobile-320 and mobile-390, en and de) and re-captures the cost strip key screens; review them by eye. The capture of both new screens was run to the point of comparison in this plan; only the committed image is missing.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Test-only registry code plus four small fixes in existing UI files; no new endpoint, auth path or schema. T-71-28 (unaudited state) is mitigated: `requiredRegistry` and `screen-registry.test.ts` fail when an id lacks an entry, and every new state runs the axe, reflow, spacing, German and heading audits. T-71-SC: no package was installed and the gate guards `package.json` and `package-lock.json`.

## Self-Check: PASSED

- FOUND: `sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts` (S-15, S-16, O-10, O-11, K-18, K-19)
- FOUND: `sources/ReactApp/tests/e2e/support/screens/registryTypes.ts` (four required items)
- FOUND: `sources/ReactApp/tests/e2e/support/keyScreens.ts` (`keyScreenCount = 19`)
- FOUND commits `d8d51f38`, `d39c1f5b`, `5d3f0243`, `687129ab`, `aa7bdd12`, `c5c1dc21`
