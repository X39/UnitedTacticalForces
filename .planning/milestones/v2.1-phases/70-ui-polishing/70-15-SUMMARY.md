---
phase: 70-ui-polishing
plan: 15
subsystem: ui
tags: [react, playwright, accessibility, screen-registry, settings-panels, css]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-06 SettingsPanel and the sessionStorage open state; 70-08 automation controls; 70-11 Configuration panel and filter; 70-12 credentials; 70-13 compact review dialog; 70-14 Files card; 70-16 panel seeding for the e2e suite"
provides:
  - "Registry entry S-14 with seven Settings states (collapsed default, staged badge, deep link, auto-open, invalid auto-open, filter with results, filter with none)"
  - "O-02 states for 10 and 40 staged changes, O-03 state for an automation-only leave, key screens K-16 and K-17 (17 key screens)"
  - "WorkspaceMockOptions.configurationEntryCount for generated typed integer entries"
  - "Three real defects found by the new states and fixed: invalid-field focus in a collapsed panel, pending bar alert overflow at 320px, review dialog geometry probe"
  - "Every XPF-70-* finding of Plans 70-01 to 70-16 resolved or decided"
affects: [milestone-end visual baselines, UAT]

tech-stack:
  added: []
  patterns:
    - "Settings states are audited at a fixed scroll offset (settleScroll) that cuts no control with a sticky edge (clearStickyEdges), after the smooth scroll rested (waitForScrollRest)"
    - "Large review and filter states use generated mock entries addressed by control id, never by translated label"

key-files:
  created: []
  modified:
    - sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
    - sources/ReactApp/tests/e2e/support/screens/registryTypes.ts
    - sources/ReactApp/tests/e2e/support/keyScreens.ts
    - sources/ReactApp/tests/e2e/support/a11y.ts
    - sources/ReactApp/tests/e2e/mobile/key-screens.spec.ts
    - sources/ReactApp/tests/e2e/mobile/visual-stability.spec.ts
    - sources/ReactApp/tests/e2e/hosting-cost-alerts.spec.ts
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/actionVocabulary.ts
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/src/components/server/files.css
    - sources/ReactApp/src/components/server/configuration.css
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/tests/hosting/action-vocabulary.test.ts
    - sources/ReactApp/tests/hosting/actions.test.tsx

key-decisions:
  - "K-16 and K-17 are state-level baselines (`baseline` on the registry state), so `stateNameOf` needs no entry; the count and a comment in keyScreens.ts record them"
  - "ConfirmDialog gets no shared initial-focus option (XPF-70-07-2): the one dialog that needs the first control focused keeps its panel-local effect"
  - "The More actions grid has its own variant (danger or primary, never gold) instead of the shared confirm variant"

patterns-established:
  - "An audit state that depends on scroll position must rest and clear sticky edges before axe runs, otherwise target-size reports partially covered controls"

requirements-completed: [D-09, D-18]

duration: ~160 min
completed: 2026-11-26
commits: 10
plan_head_before: 14645e06d2e691ffe310eecd426e111acf238771
plan_head_after: d376c77c81d5c1ca760363beace4200e6811f50a
actuals:
  tokens: 12000
  tasks: 3
  commits: 10

coverage:
  - id: D1
    description: "S-14 Settings panel states (all collapsed, staged badge, deep link, auto-open, invalid auto-open, filter with results and none) pass axe, reflow, spacing, German and headings on chromium, mobile-390 and mobile-320"
    requirement: "D-18"
    verification:
      - kind: automated_ui
        ref: "playwright: tests/e2e/audit/a11y-audit.spec.ts and german-walk.spec.ts --grep=S-14 (36 passed with 4 repeats, no retries)"
        status: pass
    human_judgment: false
  - id: D2
    description: "O-02 review dialog with 10 and 40 staged changes and O-03 leave dialog with automation changes only pass the audit on all three projects"
    requirement: "D-18"
    verification:
      - kind: automated_ui
        ref: "playwright: tests/e2e/audit --grep=O-0[23] (36 passed with 2 repeats, no retries)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Key screens K-16 and K-17 are registered so the milestone baseline run captures 17 key screens"
    requirement: "D-18"
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts; resolveKeyScreens in tests/e2e/support/keyScreens.ts throws unless 17 are found"
        status: pass
    human_judgment: false
  - id: D4
    description: "The phase gate is green: ci steps and the e2e audit and hosting specs on chromium, mobile-390 and mobile-320"
    requirement: "D-18"
    verification:
      - kind: e2e
        ref: "playwright: tests/e2e/audit plus four hosting specs on three projects, 776 passed, 0 failed, 0 flaky, retries 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "The polished Settings, Review, Leave and Files UI looks finished"
    verification: []
    human_judgment: true
    rationale: "Visual judgment of the phase goal; deferred to the milestone-end visual review by the standing policy"
---

# Phase 70 Plan 15: Phase 70 states in the screen registry and the phase gate Summary

The audited registry now reaches every Phase 70 Settings, review and leave state (S-14, O-02 at 10 and 40 changes, O-03 automation-only) with 17 key screens registered, three real defects the new states exposed are fixed, every XPF-70-* finding is closed or decided, and the phase gate is green (776 e2e tests, 0 retries).

## Performance

- **Duration:** ~160 min
- **Completed:** 2026-11-26
- **Tasks:** 3 (Task 1 tracer, Task 2 auto, Task 3 auto)
- **Files modified:** 19
- **Commits:** 10 (measured from the ledger)

## Accomplishments

- **Task 1 (tracer, 07e2a017):** `configurationEntryCount` appends generated Common integer entries (keys `generated.cfg@paramNN`, names `Parameter NN`, one group `Block N` per eight, culture-neutral so the German walk has nothing to flag). S-14 opens Settings with `settingsPanels: []` and visits seven states in order: all five panels collapsed with summaries; Slots staged and the Configuration panel collapsed with its badge (K-16); the Automation deep link by hash change with the draft kept; a second value staged into the collapsed panel which opens it again; an invalid Slots value with the panel collapsed and Review changes pressed, which opens the panel and focuses the field; the filter with results; the filter with none. Keyboard is off with the written justification (S-06 walks the fully open page). Tracer gate: the registry unit test and the S-14 audit passed before Task 2.
- **Task 2 (76e00a01):** O-02 gains "10 staged changes" and "40 staged changes" (the three base changes plus generated fields, asserting the exact row count and the typed-secret absence), O-03 gains "leave with automation changes only" (discard the staged Slots change, switch idle shutdown on, leave by the section navigation, expect exactly Stay and Discard). K-17 is the first O-03 state, `keyScreenCount = 17`. O-02 uses `allowLongWalk()` (24 s per test on chromium, within the tripled timeout).
- **Task 3 (edf57fdf and companions):** orphan sweep and XPF closure (table below), phase gate run.

## Task Commits

1. `abd16bb3` fix(webapp): focus an invalid field inside a collapsed Settings panel
2. `c349e022` fix(webapp): keep the pending changes bar alert inside narrow screens
3. `07e2a017` test(webapp): audit the collapsible Settings panel states (Task 1)
4. `70af89cd` test(webapp): ignore review row pairs in the dialog facts probe
5. `76e00a01` test(webapp): audit large review drafts and the automation-only leave (Task 2)
6. `3d37c277` fix(webapp): keep gold off the More actions grid of the Overview
7. `edf57fdf` chore(webapp): remove orphaned panel head styles and close findings (Task 3)
8. `2bda3b51` test(webapp): fix a stale toast check and two timing sensitive checks
9. `c7844f1f` test(webapp): expect a neutral variant for the More actions grid
10. `d376c77c` test(webapp): document the state level key screens K-16 and K-17

## XPF-70-* resolutions

| Id | Resolution |
|----|------------|
| XPF-70-03-1 | Fixed: the container `border-top` of `.next-hosting-audit-evidence` is removed (edf57fdf), so only the bordered disclosure header draws it |
| XPF-70-03-2 | Fixed: `.next-hosting-protocol-list .disclosure` leaves the shared padding, border and fill rule (edf57fdf) |
| XPF-70-03-3 | Checked in 70-03, unaffected; no action |
| XPF-70-04-1 | Fixed (real defect): `gridVariantFor` gives the More actions grid danger for cost stops and neutral primary otherwise, never gold (3d37c277); the `confirmVariantFor` fallback still styles confirm dialogs, header and banner. `actions.test.tsx` now asserts primary (c7844f1f) |
| XPF-70-04-2 | Confirmed acceptable, no change: the empty-state link and the gold header link are mutually exclusive, so the view never shows two calls to action |
| XPF-70-06-1 | Resolved earlier: 70-10 and 70-11 composed the Mods and Configuration panels on `SettingsPanel`; S-14 audits all five panels collapsed and open |
| XPF-70-06-2 | Resolved earlier: the harnesses wrap the provider; S-14 renders through the real section |
| XPF-70-06-3 | Resolved by 70-16 (seeding) and used here: S-14 passes `settingsPanels: []` for the collapsed default |
| XPF-70-07-1 | Confirmed: hosting.css holds no upload queue, summary or dialog width rule (`grep` for upload, queue and dialog rules finds none) |
| XPF-70-07-2 and XPF-70-14-1 (the `initialFocus` question) | Decided, no shared option: ConfirmDialog stays as is. The focusable `bodyLabel` region is the right first stop for the review, consent and cost dialogs (they are read first, and review and leave already pass `initialFocus="dismiss"`), and the only dialog that wants its first control, the Files upload dialog, keeps its working local effect. A shared option would have no second user |
| XPF-70-10-1 | Confirmed deleted: `grep` for `.configuration-panel .workspace-panel__head` and a Configuration-scoped min-height finds none |
| XPF-70-11-1 | Fixed: the badge height reservation moved from `[data-settings-panel="server-configuration"]` in configuration.css to `.disclosure--panel ... .disclosure__title` in form-fields.css, covering every panel (edf57fdf) |
| XPF-70-14-2 (named XPF-70-14-1 in the orchestrator list: the one-time block credentials flake at 390 de) | Fixed: the focus move scrolls smoothly, and the test measured the block before the scroll rested; it now waits with the exported `settleScroll` (2bda3b51) |
| XPF-70-16-1 | Fixed: the stale `role="status"` assertion now asserts the dismissed toast sits in the persistent polite live region and has no role of its own (2bda3b51) |
| XPF-70-16-2 | Root-caused and fixed in the registry: not a layout defect (the link is 44px at 800px and below). The Automation deep link scrolls smoothly and axe measured the section navigation half behind the sticky header while it moved. `automationReady` now waits for the scroll to rest (2bda3b51). S-07 and N-09 on mobile-390 and mobile-320 passed 32 of 32 each at 12 workers with retries off (8 of 32 failed before) |

## Defects found by the new states and fixed (Rule 1)

1. **Invalid field in a collapsed panel never took focus** (`SettingsSection.tsx`, abd16bb3). The review attempt opens the panel through a state update that can commit after the next frame, so the single-frame `focus()` hit a hidden field. The focus now waits up to ten frames until the field is no longer inside a `[hidden]` region. The spec in S-14 asserts the focus.
2. **Pending bar alert overflowed a 320px viewport** (`change.css`, c349e022). Below 520px the bar is a column that still wrapped, so the alert's full-width basis started a second column outside the viewport (document scrollWidth 564). The column no longer wraps.
3. **Review dialog failed the registry geometry probe** (`a11y.ts`, 70af89cd). The probe took the first `dl`, now the after cell of a compact review row; 70-13 knew this and skipped the probe in its own spec, but the registry audit calls it. The probe now ignores `.change-item__pair`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan premise: the panel head rule was not orphaned**
- **Found during:** Task 3
- **Issue:** `.workspace-panel .workspace-panel__head` (and its h2 rule) in change.css is still used by the Files panel (`ServerFilesPanel.tsx`, class `workspace-panel__head next-hosting-files-heading`), so deleting it would have lost the Files head layout. The second acceptance criterion (`grep -rc "workspace-panel__head" src --include=*.tsx` prints 0) therefore prints 1.
- **Fix:** moved the two rules into files.css under `.next-hosting-files-panel`, the only consumer, so change.css (Settings flow) no longer holds them (first criterion prints 0).
- **Files modified:** change.css, files.css
- **Committed in:** edf57fdf

**2. [Rule 3 - Blocking] Smooth scroll and sticky edges made axe target-size flaky and deterministic at 320 and 390**
- **Found during:** Task 1 (S-14 on mobile)
- **Issue:** the page scrolls smoothly, and a sticky bar (the pending bar, or the workspace header) covered part of a panel header or field at the offset the state happened to end at; axe reported it as a target under 24px.
- **Fix:** `waitForScrollRest`, `settleScroll` and `clearStickyEdges` in `workspaceScreens.ts` put every S-14 state at a rested offset that cuts no control; they run only in the registry module, never in the app.
- **Verification:** S-14 on chromium, mobile-390 and mobile-320 with 4 repeats at 8 workers: 36 passed, no retries.

**3. [Rule 3 - Blocking] A hidden control cannot be typed into for the auto-open state**
- **Found during:** Task 1
- **Issue:** "a staged draft that auto-opened its panel" needs a value to reach a collapsed panel, but a hidden field cannot be focused.
- **Fix:** the state writes the value through the native input setter plus an `input` event (`stageWhileCollapsed`), which is how a value from another source reaches the draft; it exercises the documented rule that the panel the member closed opens again on the next staged key.

**4. [Plan wording] K-16 registration**
- The plan says to register K-16 in `stateNameOf`; that map only serves entry-level `baseline` ids. K-16 and K-17 use state-level `baseline`, which `resolveKeyScreens` already supports; the acceptance `grep '"K-16"' keyScreens.ts` is met by the documenting comment (d376c77c).

---

**Total deviations:** 4 (1 bug in plan premise, 2 blocking, 1 wording); three further product and test-support defects fixed (listed above).
**Impact on plan:** the three fixes are correctness fixes the new audited states exposed; no scope beyond the plan's "fix in the file that owns it" rule.

## Verification

Phase gate, with `npm run ci` run as its individual steps (the `format:check` step fails on every file here because core.autocrlf checks out CRLF while Prettier expects LF, which is environmental; `prettier --check . --end-of-line auto` replaces it):

- `locales:check`: 3921 keys en and de, 0 failures. `locales:unused`: 0 failures (46 pre-existing warnings). `contracts:check`: clean (the regenerated `src/generated/api.ts` is line-ending only and was restored, never committed).
- `prettier --check . --end-of-line auto`: clean. `lint` (`--max-warnings 0`): clean. `typecheck` (`tsc -b`): clean. `build:assets`: builds.
- `vitest run --maxWorkers=4`: 215 files, 3540 passed, 2 skipped, with one failure (`actions.test.tsx` still asserted gold on the More actions grid, the deliberate XPF-70-04-1 change); fixed and re-run green (6 passed), `tests/styles` and `tests/ratchets` 160 passed. The legacy ratchet writer found no ceiling change (the deleted rules held no raw literal), so `legacy-ratchet.json` is unchanged.
- Playwright through the lock helper, retries off: `tests/e2e/audit`, `hosting-a11y`, `hosting-change`, `hosting-automation`, `hosting-files-secrets` and `hosting-cost-alerts` on chromium, mobile-390 and mobile-320: **776 passed, 0 failed, 0 flaky** (5.1 min). `hosting-cost-alerts.spec.ts` was added to the plan's list because XPF-70-16-1 changed it.
- Visual baselines were not generated (CONTEXT Deferred; `scripts/Update-VisualBaselines.ps1` runs at milestone end).

## Cross-plan findings

None open. Every XPF id is in the table above.

## Known Stubs

None.

## Threat Flags

None. No network endpoint, auth path or schema changed; T-70-22 (collapsed-state regressions hidden by seeded open panels) is mitigated by S-14 auditing the collapsed default and the auto-open rules next to S-06's open state.

## Deferred follow-up

Deferred follow-up: milestone-end visual review of the polished Settings, Review, Leave and Files UI (the end-of-phase `<human-check>`; real-environment and visual-baseline checks are deferred by the standing policy). Start the seeded stack from the repository root with:

```
pwsh ./scripts/Start-Uat.ps1 -Fresh
```

It opens the fleet page of the React app; stop everything with Ctrl+C. Then open "UAT 11 Idle shutdown", go to the Settings tab and look at:

- the collapsed panels and their summary lines; press Expand all and open Automation (switches, weekday chips, radio cards);
- change a configuration value and confirm nothing on the page moves (the badge appears in the header without growing it);
- open Review changes (compact rows), then leave through another tab and look at the leave dialog (one row with a gold Review, Stay on a backdrop click);
- the Files tab and the upload dialog (the list sits under its heading inside one card);
- narrow the browser to about 390px once.

Expected: checkboxes, radios, switches, chips, selects and buttons look designed and consistent, Automation is one tap from the top of Settings, the review dialog is compact, and no view shows more than one gold main action (the Overview More actions grid is now neutral).

## Self-Check: PASSED

- Registry entries exist: `S-14` in `workspaceScreens.ts` and `registryTypes.ts`, `K-17` in `workspaceScreens.ts`, `configurationEntryCount` in `workspaceMocks.ts`, `keyScreenCount = 17` in `keyScreens.ts`.
- Commits `abd16bb3`, `c349e022`, `07e2a017`, `70af89cd`, `76e00a01`, `3d37c277`, `edf57fdf`, `2bda3b51`, `c7844f1f` and `d376c77c` are ancestors of HEAD.
- The gate re-ran green after the last code change.

---
*Phase: 70-ui-polishing*
*Completed: 2026-11-26*
