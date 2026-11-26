---
phase: 63-workspace-shell-activity
plan: 10
subsystem: ui
tags: [react, workspace-sections, cost-stop, slots, vitest, localization]

requires:
  - phase: 63-workspace-shell-activity
    provides: costStopAction and the workspace-machine source (63-04), useNow (63-03), SlotPlaceholder (63-06), buildActivityEntries, filterEntries and the compact ActivityEntry (63-08), the Server_Machine_*, Server_Settings_* and Server_Overview_* keys (63-01)
provides:
  - MachineSection, the plain-words cost notice with the shared cost-stop above the cloud machine and the runtime settings
  - CloudMachinePanel, every fact of the old cloud tab with localized per-hour amounts
  - SettingsSection, configuration, credentials only for members who may manage secrets, the automation slot and the server record with Delete server
  - OverviewSection, server details without cost, the metrics slot, the five newest merged activity entries and More actions
  - sections.css, the token-only workspace-panel styles
affects: [63-11, 63-12, 67, 68]

requirements-completed: [WSP-02, CST-02]

plan_head_before: d4b55e29f3b13722af74fb6c2df806e594a63c77
plan_head_after: 945021304a9c304c77a5e8ea238c14f7a19f0357

actuals:
  tokens: 12000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A section owns its mutation (Settings owns the delete useMutation and navigation) so the page shell no longer carries section state"
    - "Reserved slots are titled panels with one muted line and a data-slot hook; tests assert the slot text contains no digit"
    - "Overview recent activity reuses the activity-timeline list classes and the compact ActivityEntry, so it can sit beside the Activity section without duplicate anchors"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/sections/MachineSection.tsx
    - sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx
    - sources/ReactApp/src/components/server/sections.css
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx

key-decisions:
  - "A failed deletion closes the confirm dialog so the role=alert message below the Delete server button is reachable; the dialog also shows busy while the request runs"
  - "The cost notice button is omitted when the server projects no cost-stop action, and shows the disabled fallback (with its mapped reason) when only an unavailable stop exists"
  - "The Delete server button uses disabled while the deletion is pending and unavailableReason for the can-not-delete rule, never a title tooltip"
  - "The record panel top margin is calc(xl - lg) because the section grid already adds the lg panel gap, giving exactly the xl gap of the spec"

patterns-established:
  - "Section components take the server plus only the data they need (jobs, operations) and read JSON keys only; none reads deployment.status or the legacy copy hook"

coverage:
  - id: D1
    description: "Machine section: warning notice with the plain may-still-cost sentence only while cost is live or maybe, the shared cost-stop button (request source workspace-machine), disabled shutdown without saving with its mapped reason, no notice for an off server"
    requirement: CST-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#MachineSection"
        status: pass
    human_judgment: false
  - id: D2
    description: "Cloud machine panel keeps the old cloud tab facts, writes hourly amounts as 'amount per hour' and shows no /h suffix; the runtime external storage note uses the stop-billing wording"
    requirement: CST-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#MachineSection shows the technical status, the localized hourly amounts and never a /h suffix"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#MachineSection states the external storage deletion in the stop-billing vocabulary"
        status: pass
    human_judgment: false
  - id: D3
    description: "Settings section: configuration panel, credentials heading and secrets panel only with canManageSecrets (no heading and no request otherwise), automation slot, server record with the unchanged delete rule, confirm dialog, redirect to the fleet and a role=alert failure message; record hidden for archived servers and viewers without canModify"
    requirement: WSP-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#SettingsSection"
        status: pass
    human_judgment: false
  - id: D4
    description: "Overview section: server details without cost owner or cost, terrain and scenario only when set, metrics slot without any digit, five newest compact merged entries with Show all activity, empty text, More actions without collect-logs and the header primary"
    requirement: WSP-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-section-panels.test.tsx#OverviewSection"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual quality of the new panels (spacing, two-column details above 520px, details and metrics side by side above 800px) at real viewport widths"
    verification: []
    human_judgment: true
    rationale: "Layout and spacing are asserted only by the token style guard; nothing renders the sections at real widths until plan 63-11 wires them into the page"

duration: 267min
completed: 2026-11-19
status: complete
---

# Phase 63 Plan 10: Overview, Settings and Machine Sections Summary

**Overview, Settings and Machine section components with the plain-words cost notice and shared shutdown, gated credentials, delete flow, honest metrics and automation slots, and the five newest merged activity entries**

## Performance

- **Duration:** 267 min wall clock. The implementation itself took well under an hour; roughly four hours were spent blocked on a GPG signing timeout at the first commit (see Issues Encountered).
- **Started:** 2026-11-19T16:57:08Z
- **Completed:** 2026-11-19T21:24:00Z
- **Tasks:** 3
- **Files modified:** 7 (6 created, 1 modified)

## Accomplishments

- The Machine section shows, only while the derived cost is live or maybe, "This server may still be costing money..." with the one shared cost-stop (source `workspace-machine`), then the cloud machine panel and the runtime settings. Hourly amounts now read "amount per hour" and the external storage note uses the "shut down and stop billing" wording.
- The Settings section keeps the configuration, shows the credentials panel only for members who may manage secrets (no heading, no request otherwise), reserves the automation area and owns the Delete server flow with its unchanged rules.
- The Overview section lists the server details without repeating cost, reserves the metrics area, shows the five newest merged entries as compact entries with a link to Activity, and lists the remaining actions.
- Plan 63-11 can route `?tab=` to the four components; nothing in `ServerWorkspacePage.tsx` was edited.

## Task Commits

1. **Task 1: Machine section tracer** - `eb24a35e` (feat)
2. **Task 2: Settings section** - `c1550f26` (feat)
3. **Task 3: Overview section** - `94502130` (feat)

**Plan metadata:** committed with this SUMMARY (docs: complete plan)

The tracer's `<verify>` was automated only (tests, style guard, single-owner guard, typecheck, lint); it passed end to end before Tasks 2 and 3 expanded.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/sections/MachineSection.tsx` - cost notice, cloud machine, runtime settings
- `sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx` - cloud facts moved from the old cloud tab
- `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` - configuration, gated credentials, automation slot, server record and deletion
- `sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx` - details, metrics slot, recent activity, more actions
- `sources/ReactApp/src/components/server/sections.css` - token-only panel, facts and overview layout styles
- `sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx` - external storage note key swap only
- `sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx` - 21 tests for the three sections

## Decisions Made

See `key-decisions` above. No new dependencies and no new localization keys were needed; every string comes from the Phase 63 keys of plan 63-01 or the existing keys the plan names.

## Deviations from Plan

None - plan executed exactly as written.

Two small judgment calls inside the plan's latitude: a failed delete closes the confirm dialog so the alert is reachable, and the Overview test asserts that the compact articles carry no `id` or `tabindex` (the compact sentence id still starts with `activity-`, by design, so a prefix check on ids would be wrong).

## Issues Encountered

- `git commit` failed twice with a GPG signing timeout (pinentry waiting for a passphrase). Signing was not bypassed; the work was staged and the plan paused with a human-action checkpoint. After the coordinator confirmed signing worked, Task 1 was committed unchanged and Tasks 2 and 3 ran normally. All three task commits show a good signature.
- One test in `tests/hosting/activity-section.test.tsx` (not part of this plan) timed out once in a full `tests/hosting tests/styles` run and passes alone; logged in `deferred-items.md`.

Out-of-scope items logged to `deferred-items.md`: the remaining hard-coded `/h` in the runtime profile option text, the now-unused locale key `React_ServerWorkspace_ExternalStorageDeleted`, and the flaky timeout above.

## User Setup Required

None - no external service configuration required.

## Known Stubs

The two `SlotPlaceholder` areas are intentional and honest, not data stubs: `data-slot="metrics"` (Overview, filled by Phase 67) and `data-slot="automation"` (Settings, filled by Phase 68). Each shows one "Not available yet" line and never a number.

## Threat Flags

None. Credentials gating (T-63-24), the confirm dialog and unchanged delete rule (T-63-25) and the shared cost-stop confirmation (T-63-27) are implemented as planned; the administrative SSH command (T-63-26) is unchanged behaviour.

## Next Phase Readiness

- Plan 63-11 can import `OverviewSection`, `SettingsSection` and `MachineSection`, route `?tab=`, and remove the old `CloudPanel`, delete mutation and overview markup from `ServerWorkspacePage.tsx`.
- `SettingsSection` takes `{ server, jobs }`, `OverviewSection` takes `{ server, jobs, operations }`, `MachineSection` takes `{ server }`; all need `CostConfirmationProvider` and a router above them.

## Self-Check: PASSED

- Created files exist: MachineSection.tsx, CloudMachinePanel.tsx, SettingsSection.tsx, OverviewSection.tsx, sections.css and workspace-section-panels.test.tsx.
- Commits `eb24a35e`, `c1550f26` and `94502130` are ancestors of HEAD and carry good signatures.
- Acceptance greps pass: `"workspace-machine"` 1, `Server_Machine_ResourceHourly` 3, `Hosting_Detail_PossibleCost` 0, old external storage key 0, `useHostingCopy` 0 in the new files, `slot="automation"` 1, `slot="metrics"` 1, `Server_Overview_Terrain` 1, `Server_Settings_DeleteError` 1.
- Plan verification: `tests/hosting tests/styles` green apart from the one isolated timeout above, typecheck and lint exit 0, Prettier clean on the touched files.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*
