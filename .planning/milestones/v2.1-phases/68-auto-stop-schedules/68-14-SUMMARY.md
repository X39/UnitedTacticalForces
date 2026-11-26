---
phase: 68-auto-stop-schedules
plan: 14
subsystem: ui
tags: [react, settings, automation, idle-shutdown, max-runtime, leave-guard]

requires:
  - phase: 68-auto-stop-schedules
    provides: automationModel (68-04), useSaveAutomationSettings and GameServer.automation types (68-12), automation locale keys (68-04)
provides:
  - AutomationPanel in the Settings section (idle shutdown, maximum runtime, own draft and Save row)
  - AutomationStatusList three-row saved-state summary
  - One shared leave guard for staged changes and unsaved automation settings
affects: [68-17 schedules sub-section, 68-15 shutdown notice, 68-16 overview fact, 68-18 source scan]

actuals:
  tokens: 48000
  tasks: 3
  commits: 4

plan_head_before: 8dd946cfff91d8396d4207a325384ad57bf436c1
plan_head_after: 2ee3567a228ae4eeb533197c1a002a16342df009

tech-stack:
  added: []
  patterns:
    - "Panel owns its draft; host owns one leave guard fed by onDirtyChange and registerDiscard"
    - "Translated sentence with [[marker]] placeholders split into time elements"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/AutomationPanel.tsx
    - sources/ReactApp/src/components/server/AutomationStatusList.tsx
    - sources/ReactApp/src/components/server/automation.css
    - sources/ReactApp/tests/hosting/automation-panel.test.tsx
    - sources/ReactApp/tests/hosting/automation-status-list.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/hosting/settings-leave-guard.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx

key-decisions:
  - "Manage right is canManageAutomation only (research A-3); archived servers are read-only regardless"
  - "Panel validates and focuses only the two number fields; rule and zone fields belong to plan 68-17"
  - "A 403 invalidates the exact server query so the panel turns read-only after the refetch"

requirements-completed: [AUT-01, AUT-02]

coverage:
  - id: D1
    description: "Owner sets idle shutdown minutes and maximum runtime hours and saves them from the Settings Automation panel"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-panel.test.tsx#saves idle shutdown with the loaded version, then shows the saved status and a toast"
        status: pass
    human_judgment: false
  - id: D2
    description: "Viewers without the manage right and archived servers see read-only controls with one notice and no Save row"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-panel.test.tsx#shows every control disabled and one read-only notice without a save row"
        status: pass
    human_judgment: false
  - id: D3
    description: "Always-rendered three-row status list of the saved state"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-status-list.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "One leave dialog with three bodies behind a single blocker"
    verification:
      - kind: unit
        ref: "tests/hosting/settings-leave-guard.test.tsx#with unsaved automation settings"
        status: pass
    human_judgment: false

duration: 50min
completed: 2026-11-22
status: complete
---

# Phase 68 Plan 14: Automation panel Summary

**Settings now hosts a working Automation panel: owners save idle shutdown minutes and a maximum runtime from their own draft and primary Save row, everyone else sees the values read-only, and one shared leave guard covers staged changes plus unsaved automation settings.**

## Accomplishments

- `AutomationPanel` (`id="server-automation"`, `data-slot="automation"`) replaces the placeholder between Credentials and Server record. It shows the lead line from `warningLeadMinutes`, the optional cost alerts link, the read-only or archived notice, the status list, and the Idle shutdown and Maximum runtime sub-sections with `TextField` and `CheckboxField` only.
- Save sends `toAutomationUpdate(draft, saved, false, idleAvailable)` with the loaded version. Success replaces the draft with the returned settings, shows the saved status and toast and keeps focus on Save. 409 keeps the draft and offers Reload settings, 403 shows the forbidden status and refetches the server, anything else shows the save error. While saving every control is `aria-disabled` and inputs are `readOnly`.
- A missing `automation` block renders only the title and the unavailable state. A game without a players signal disables the idle checkbox and field and shows the structural reason through `aria-describedby`.
- `AutomationStatusList` renders three rows always, from the saved block only, with API instants in `<time dateTime>`, the saved zone for the next action, and the fixed unknown sentence for unknown idle states. No timers, no live region.
- `SettingsSection` keeps a single `useLeaveGuard` with `dirty = count > 0 || automationDirty`; the dialog chooses the staged, automation-only or combined body, offers Review only while changes are staged, and Discard and leave discards each dirty draft.
- Three layout tokens added to `tokens.css`; `automation.css` is token-only and passes the server style guard.

## Task Commits

1. Task 1 (tracer): `17998dee` feat(webapp): add the automation panel
2. Task 2: `238e85d1` feat(webapp): add the automation status list
3. Task 3: `9312f52c` feat(webapp): share the settings leave guard with automation, `2ee3567a` test(webapp): cover the automation panel

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated the obsolete placeholder test**
- **Found during:** Task 1
- **Issue:** `tests/hosting/workspace-section-panels.test.tsx` asserted the "Not available yet" placeholder text in the automation slot, which this plan removes. The file is not in the plan's `files_modified`.
- **Fix:** Replaced that test with one asserting the panel mounts in the slot (`id="server-automation"`, unavailable state without an automation block) and one asserting the order Credentials, Automation, Server record. Removed the now unused `anyDigit` constant.
- **Files modified:** sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
- **Commit:** 17998dee

**2. [Plan wiring] Dirty plumbing mounted in Task 1**
- The `automationDirty` state, discard registration and `dirty: count > 0 || automationDirty` were added with the panel mount in Task 1 (an unused state variable would fail lint). Task 3 then added the dialog bodies and the conditional Review action.

## Known Stubs

None. `Server_Settings_AutomationUnavailable` stays in the locale files unused, as the plan directs.

## Threat Flags

None. The panel sends only the existing settings PUT; the draft lives in React state and is never written to storage, the URL, router state or a log.

## Self-Check: PASSED

All five created files exist, commits 17998dee, 238e85d1, 9312f52c and 2ee3567a are on the branch, no file under `sources/Api` or `sources/WebApp` changed, and typecheck, lint, locales:check and the affected vitest files pass.
