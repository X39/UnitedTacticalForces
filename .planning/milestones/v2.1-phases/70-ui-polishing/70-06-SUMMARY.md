---
phase: 70-ui-polishing
plan: 06
subsystem: ui
tags: [react, settings, disclosure, session-storage, accessibility, deep-link]
requires: [70-01, 70-02, 70-03]
provides:
  - "SettingsPanel: collapsible Settings panel on the panel Disclosure (h2 in the header, live summary, badge)"
  - "SettingsPanelStateProvider / useSettingsPanelState: open state backed by sessionStorage utf.settings.open"
  - "Credentials, Automation and Server record composed as panels; Expand all / Collapse all heading row"
  - "tests/helpers/settingsPanels.ts and a global Vitest seed of every panel open"
affects: [70-10, 70-11, 70-16]
tech-stack:
  added: []
  patterns:
    - "panel open state in a provider with a latest-snapshot ref so two same-tick opens both persist"
    - "staged-key signature effect (not render-time adjustment) because open() writes into a parent provider"
    - "hash effect keyed on location.key and location.hash: open, scroll on next frame, focus header button"
key-files:
  created:
    - sources/ReactApp/src/components/server/SettingsPanel.tsx
    - sources/ReactApp/src/components/server/useSettingsPanelState.tsx
    - sources/ReactApp/tests/helpers/settingsPanels.ts
    - sources/ReactApp/tests/settings-panel.test.tsx
    - sources/ReactApp/tests/hosting/settings-section.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/AutomationPanel.tsx
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/src/components/server/sections.css
    - sources/ReactApp/src/components/server/automation.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/setup.ts
    - sources/ReactApp/tests/helpers/automationPanelHarness.tsx
    - sources/ReactApp/tests/hosting/automation-panel.test.tsx
    - sources/ReactApp/tests/hosting/change-flow.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/e2e/hosting-automation.spec.ts
key-decisions:
  - "Only the anchor ids of open panels go to sessionStorage; non-array, non-JSON and non-string entries are dropped, a throwing storage means all collapsed in memory (T-70-08, T-70-10)"
  - "ServerSecretsPanel returns null before any SettingsPanel without canManageSecrets, so there is no heading, summary or request (T-70-09)"
  - "Collapsible means registered and not pinned; the Collapse all label follows whether such a panel is open, so a lone pinned panel leaves the button on Expand all"
requirements-completed: [D-09]
duration: 75min
completed: 2026-11-26
status: complete
commits: 2
plan_head_before: 95539d14bdea4265715a2f24ce6b7b8d8791582f
plan_head_after: f8221eb291cf5fc92e6f1555d3cfdc3c41b42d7b
actuals:
  tokens: 42000
  tasks: 2
  commits: 2
---

# Phase 70 Plan 06: Collapsible Settings panels with live summaries Summary

The Settings tab is now a heading row (h2 Settings plus a ghost compact Expand all / Collapse all button) over collapsible panels whose open state lives in sessionStorage; the Automation deep link opens only Automation, scrolls it below the sticky header and focuses its header button.

## Accomplishments

- `useSettingsPanelState.tsx`: provider and hook with `isOpen`, `open` (never closes), `toggle`, `expandAll`, `collapseAll` (skips pinned), `register`, `setPinned` and the derived `anyCollapsibleOpen`. The open ids are read lazily from `utf.settings.open` and written on every change; a corrupt value or throwing storage gives all collapsed with no error.
- `SettingsPanel.tsx`: `section#<id>.workspace-panel.settings-panel[data-settings-panel]` around `Disclosure variant="panel" headingLevel={2}`. It registers on mount, pins while `stagedKeys` is non-empty, opens on a newly seen staged key, and on every `location.key`/`location.hash` carrying `#<id>` opens, scrolls on the next frame (guarded for jsdom) and focuses the header button (C-08).
- Automation: `AutomationPanel` composes `SettingsPanel id="server-automation"`; summary is "Nothing automated" or "N automation(s) active" (idle + max runtime + every saved rule), badge "Unsaved" and pin while the draft is dirty; its section, h2, heading ref and deep-link effect are gone. The draft survives a collapse because content stays mounted.
- Credentials: `ServerSecretsPanel` composes `SettingsPanel id="server-credentials"` with "Credentials: N" once loaded (none while loading or failed), the staged count badge, staged keys, and the load error with retry inside the collapsed panel.
- Server record: `SettingsPanel id="server-record"` with "Delete this server permanently"; Delete and its ConfirmDialog unchanged inside.
- Invalid-field effect: finds the first `[aria-invalid="true"]`, opens its `[data-settings-panel]`, focuses it on the next frame.
- Locale keys (en and de, "du" form): `Server_Settings_Panels_ExpandAll/CollapseAll`, `Server_Settings_Summary_Credentials`, `Server_Settings_Summary_Automation_Off/_Active_one/_Active_other`, `Server_Settings_Summary_Record`, `Server_Settings_Panel_Unsaved`.
- Tests: global `beforeEach(seedSettingsPanels)` plus `afterEach(sessionStorage.clear)`; 16 new provider/panel cases in `settings-panel.test.tsx`; 13 section cases in `settings-section.test.tsx`; automation panel cases migrated (header-button focus, collapse keeps draft, summary and badge); one Playwright test of the collapsed default.

## Task Commits

1. Task 1 (tracer): `8d1789a9` feat(webapp): collapse Settings panels and open Automation by link
2. Task 2: `f8221eb2` feat(webapp): compose credentials and record panels with Expand all

Tracer gate: Vitest (`settings-panel`, `automation-panel`, `settings-leave-guard`: 51 passed) and the chromium test "opens only the collapsed Automation panel" (1 passed, re-run after Task 2) before expansion.

## Verification

- `vitest run` full suite: 3407 passed, 1 failed (`activity-section` deep-link test timed out at 5s under parallel load; passes alone, 36/36), unrelated to this plan.
- `tests/hosting` 2170 tests pass (same flake aside); `tests/styles` 5 files pass; `locales:check` 3905 keys 0 failures; `locales:unused` shows no warning for the new keys; `npm run lint` (max-warnings 0) and `npm run typecheck` pass.
- Playwright (chromium, serialized): the new test passes. Existing browser tests that open Settings without the hash or assert focus on the panel heading are expected to fail until 70-16 seeds and migrates them in Wave 3; they were not run or edited (prohibited by the plan).
- Acceptance greps: SettingsPanel export 1, `utf.settings.open` 1, SettingsPanel refs in AutomationPanel 4, `headingRef` 0, `seedSettingsPanels` 2, `Server_Settings_Panel_Unsaved` in de.json 1, e2e test name 1, `workspace-panel__head` in SettingsSection 0, `SettingsPanel` in ServerSecretsPanel 4, `Server_Settings_Panels_ExpandAll` in en.json 1, `data-settings-panel` in SettingsSection 1.

## Deviations from Plan

**1. [Rule 1 - Bug avoidance] Staged-key opening uses an effect, not the render-time pattern**
- **Found during:** Task 1
- **Issue:** The plan named the adjust-state-during-render signature pattern, but `open(id)` writes into the parent provider; calling it during a child render triggers React's "update a component while rendering another" error.
- **Fix:** A signature-keyed effect compares the previous staged key set and calls `open(id)` for a new key; the same semantics (member can close, a further new key reopens).
- **Files modified:** `SettingsPanel.tsx`
- **Commit:** `8d1789a9`

**2. [Rule 3 - Blocking/compat] No `scroll-margin-block-start` on `.settings-panel`**
- **Found during:** Task 1
- **Issue:** The plan asked for a scroll margin matching the html scroll padding. `html:has(.workspace-header)` already sets `scroll-padding-block-start` to the topbar plus 12rem reserve, which `scrollIntoView` honors; adding a margin of the same size would double the offset.
- **Fix:** Rely on the existing scroll padding; the browser test confirms the panel top rests at or below the sticky header bottom.
- **Files modified:** none (omission)

**3. [Rule 1 - Test fallout] Existing tests adapted to the new panel structure**
- **Found during:** Tasks 1 and 2
- **Issue:** `automation-panel` asserted no buttons without an automation block and a `tabindex` on the section and heading focus; `change-flow` mocks `ServerSecretsPanel` with a stand-in that relied on SettingsSection's inline wrapper and sync focus; `workspace-section-panels` read `#server-automation` from the slot.
- **Fix:** Asserted the single header button, header-button focus via `waitFor`, the anchor on the enclosing section; the `change-flow` stand-in now composes the real `SettingsPanel` with badge and staged keys; invalid-field focus is awaited on the next frame.
- **Files modified:** `automation-panel.test.tsx`, `change-flow.test.tsx`, `workspace-section-panels.test.tsx`, `automationPanelHarness.tsx` (provider wrap)
- **Commits:** `8d1789a9`, `f8221eb2`

**Total deviations:** 3 auto-fixed (1 bug avoidance, 1 blocking/compat, 1 test fallout). **Impact:** none on behavior or scope.

## Cross-plan findings

- XPF-70-06-1: `ModPackPanel` and `ServerConfigurationPanel` still render their own `section.workspace-panel` and h2, so Expand all / Collapse all and the sessionStorage state do not cover them and the global test seed ids `server-mods` and `server-configuration` are unused until 70-10 and 70-11 compose them on `SettingsPanel`. The invalid-field effect already opens any panel carrying `data-settings-panel`, so those plans need no extra wiring for it.
- XPF-70-06-2: `ServerSecretsPanel` and `AutomationPanel` now require `SettingsPanelStateProvider`; any other test or story that renders them directly must wrap them (the automation harnesses are updated).
- XPF-70-06-3: 70-16 must seed `utf.settings.open` in Playwright and migrate existing assertions that focus the panel heading or expect Settings content without the hash; the new test in `hosting-automation.spec.ts` must receive the collapsed-default option there.

## Known Stubs

None.

## Threat Flags

None. T-70-08 (ids only, non-string entries dropped), T-70-09 (null before SettingsPanel, no request, asserted in `settings-section.test.tsx`) and T-70-10 (corrupt value means all collapsed) are implemented and unit tested.

## Self-Check: PASSED

- Files exist: `SettingsPanel.tsx`, `useSettingsPanelState.tsx`, `settingsPanels.ts`, `settings-panel.test.tsx`, `settings-section.test.tsx`.
- Commits `8d1789a9` and `f8221eb2` are ancestors of HEAD.
