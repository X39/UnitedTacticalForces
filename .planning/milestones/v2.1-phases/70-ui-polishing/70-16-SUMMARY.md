---
phase: 70-ui-polishing
plan: 16
subsystem: testing
tags: [playwright, e2e, settings-panels, session-storage, disclosure]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-05 drawn form controls (ring on the mark wrapper), 70-06 collapsible Settings panels and utf.settings.open"
provides:
  - "tests/e2e/support/settingsPanels.ts: allSettingsPanelIds, seedSettingsPanels"
  - "settingsPanels option on installApp, installWorkspace, installAutomationWorkspace and mockAutomationRoutes"
  - "Deep-link focus and K-07 arrival asserted on the Automation header button"
affects: [70-17, 70-18, 70-19, 70-20]

tech-stack:
  added: []
  patterns:
    - "Init script seeds sessionStorage only when the key is absent, anchor ids only"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/settingsPanels.ts
  modified:
    - sources/ReactApp/tests/e2e/support/mockApi.ts
    - sources/ReactApp/tests/e2e/support/automationMocks.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts
    - sources/ReactApp/tests/e2e/hosting-automation.spec.ts
    - sources/ReactApp/tests/e2e/hosting-a11y.spec.ts
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - sources/ReactApp/tests/e2e/hosting-placement.spec.ts
    - sources/ReactApp/tests/e2e/hosting-workspace.spec.ts

key-decisions:
  - "The seed is an init script that writes only when utf.settings.open is null, so a reload inside a test keeps the member's own open state"
  - "The collapsed-default test passes settingsPanels: [], which seeds an empty id array (an explicit collapsed state, not an absent key)"

requirements-completed: [D-09]

duration: 45min
completed: 2026-11-26
commits: 4
plan_head_before: 3938a0ae7b185625ebabea2cee8257b945baec38
plan_head_after: 1a31f16607d572a2ae7eaa25be819ef22ef0396c
actuals:
  tokens: 6000
  tasks: 3
  commits: 4
---

# Phase 70 Plan 16: Seed open Settings panels in the Playwright flows Summary

Every Playwright flow that reads Settings content now starts with all five panels open through an absent-only sessionStorage seed, the deep link is asserted on the Automation header button, and the collapsed default keeps its own test.

## Accomplishments

- Task 1 (tracer): `seedSettingsPanels(page, ids?)` and `allSettingsPanelIds` in `tests/e2e/support/settingsPanels.ts`. `mockAutomationRoutes` seeds right after its culture init script; `AutomationMockOptions` and `installAutomationWorkspace` forward `settingsPanels`. `hosting-automation.spec.ts`: the three focus assertions (owner journey en/de, Overview fact link, keyboard-order test) use `getByRole("button", { name: panelTitle })`; the collapsed-default test passes `{ settingsPanels: [] }`; the keyboard-walk ring test also asserts that the measured ring owner is the focused control or one of `.check-mark, .switch-control, .toggle-chip, .choice-card, .segmented__option, .form-field__check, .form-field__radio-row` (the 70-05 ancestor-outline walk was already in place and is kept). Tracer gate: the whole spec passes on chromium (20 passed, 12 screenshot-only skipped).
- Task 2: `installApp` seeds before the first navigation; `installWorkspace` forwards the option; `automationArrivalReady` waits for `#server-automation .disclosure__summary` (C-08); `unavailableReady` uses a descendant selector. Journeys (4) and S-06/S-07 on chromium and mobile-320 (8) pass.
- Task 3: the five specs that set their own culture import `seedSettingsPanels` and call it after the culture init script. The six hosting specs on chromium: 103 passed, 12 skipped.

## Verification

- `npm run lint` and `npm run typecheck` pass. `git diff --name-only 3938a0ae..HEAD` lists only `sources/ReactApp/tests/e2e/` paths.
- Full chromium plus mobile-390 and mobile-320 run: 834 passed, 10 failed, 4 flaky. After the fix below the O-05 failures (9 tests) pass. The remaining failure and the flakes are findings XPF-70-16-1 and XPF-70-16-2, neither caused by this plan.
- The plan's `--grep "S-06|S-07"` verify command cannot run through the lock helper on Windows (the shell treats `|` as a pipe), so it was run as `--grep=S-0[67]` with the same matches.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test fallout] O-05 delete-server audit clicked the panel header button**
- **Found during:** full-suite run after Task 3
- **Issue:** `openDeleteDialog` clicked `.workspace-panel--record button` first, which is now the Disclosure header button, so `dialog[open]` never appeared (O-05 a11y-audit and german-walk, all three viewports).
- **Fix:** the locator skips `.disclosure__summary`, so the first remaining button is Delete.
- **Files modified:** `sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts`
- **Commit:** 1a31f166

**2. [Plan reading] Ring-owner assertion**
- The plan asked to extend a `closest(...)` list; 70-05 had already replaced `closest` with an ancestor-outline walk. Instead of reverting it, the listed classes became an additional `ownerIsKnown` check on every stop. No assertion was weakened.

## Cross-plan findings

- XPF-70-16-1: `tests/e2e/hosting-cost-alerts.spec.ts:552` ("a pushed alert shows in the banner...") expects `role="status"` on the dismissed-cost-alert toast, but commit 1952665d moved announcements to persistent live regions (`AppToasts.tsx`), so the assertion is stale. It fails alone and is unrelated to Settings; not changed here (out of scope, no production defect).
- XPF-70-16-2: the mobile-390 and mobile-320 axe audits intermittently report `target-size` on `.section-navigation__link` (S-07, N-09 en, 8 of 32 runs with retries disabled; every run passes on retry). Timing-dependent under 12 workers; a production layout or transition question for a later plan, not caused by the panel seed.

## Authentication Gates

None.

## Known Stubs

None.

## Deferred Issues

- Deferred follow-up: XPF-70-16-1 and XPF-70-16-2 above.

## Threat Flags

None. T-70-23 is mitigated (collapsed-default test with `settingsPanels: []`, absent-only seed). T-70-24: test-only code under `tests/e2e`, ids only.

## Self-Check: PASSED

- FOUND: `sources/ReactApp/tests/e2e/support/settingsPanels.ts`
- FOUND commits: 54cb3530, e2c89708, c989dbbb, 1a31f166
