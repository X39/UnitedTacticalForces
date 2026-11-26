---
phase: 69-consistency-accessibility-verification
plan: 27
subsystem: testing
tags: [playwright, axe-core, wcag22, keyboard, reflow, german-walk, screen-registry, workspace, automation]

requires:
  - phase: 69-06
    provides: "installApp strict mock layer, screen registry types, slice modules, registry-driven audit"
  - phase: 69-25
    provides: "German leak detector and the registry-driven German walk"
  - phase: 68-auto-stop-schedules
    provides: "automationMocks.ts (automationServer, automationBlock, mockAutomationRoutes) and the automation panel, consent dialog and shutdown notice"
provides:
  - "workspaceScreens.ts: 15 registry entries (S-04 to S-07, O-02 to O-05, N-05 to N-11) and an empty workspacePending list"
  - "workspaceMocks.ts: installWorkspace and installAutomationWorkspace, the strict mock of one workspace server with German-neutral fixtures"
  - "a11y.ts: date and time input segments count as one tab stop in the keyboard walk and the sticky coverage check"
affects: [69-31, 69-33]

actuals:
  tokens: 13700
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Entries that change the server between states replace the detail route and press Refresh server, so one page load shows read-only and unavailable variants"
    - "allowLongWalk() triples the test timeout from inside an entry setup with a dynamic Playwright import, so the Vitest guard can still load the slice module"
    - "Automation entries run on mockAutomationRoutes (Phase 68) and expose its handle through a WeakMap keyed by page, so a state can assert the saves list"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
  modified:
    - sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts
    - sources/ReactApp/tests/e2e/support/a11y.ts
    - sources/ReactApp/tests/e2e/audit/a11y-helpers.spec.ts
    - sources/ReactApp/src/components/server/ActivityEntry.tsx
    - sources/ReactApp/src/components/server/workspace-shell.css
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/components/server/ScheduledStartConsentDialog.tsx

key-decisions:
  - "S-06 and O-05 hold one state each: every state costs a full forward and backward keyboard walk over main, and two states took two thirds of the 30 s test timeout"
  - "S-07 audits the time zone select closed only: it is a native select, its open list is browser UI, and all options are already in the scanned DOM"
  - "O-05 audits the real delete confirmation, which has no typed challenge; the entry fails if an input in that dialog ever lacks a described-by helper"
  - "The shell scroll padding is a rem reserve (topbar plus 12rem) in workspace-shell.css, not a new token, because tokens.css is not owned by this plan"

patterns-established:
  - "Sticky header reserve: html:has(.workspace-header) { scroll-padding-block-start } keeps focus scrolling clear of the sticky stack"
  - "Machine output the worker wrote in English carries lang=en (OutputBox) like ApiText does for API prose"

requirements-completed: [FND-06, L10N-03, VER-03]

coverage:
  - id: D1
    description: "Workspace Overview (S-04) and Activity (S-05, collapsed and with the expanded failed entry) pass axe at wcag2a to 22aa, headings, zoom, reduced motion and keyboard walk on desktop, reflow, text spacing and sticky coverage at 390 and 320, in English and German"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts --grep @workspace (S-04, S-05; chromium, mobile-390, mobile-320; en, de)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Settings with a staged draft (S-06) never has its focus covered by the pending bar or the sticky header at 320; review (O-02), leave (O-03) and delete (O-05) dialogs pass the dialog geometry and every audit"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts --grep @workspace (S-06, O-02, O-03, O-05)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Automation panel (S-07: saved settings, read-only, unavailable), consent dialog with initial focus on Keep editing and no save before Allow (O-04), automatic shutdown notice above the cost alert banner (N-08) and consent-needed notice (N-09) pass every audit"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts and german-walk.spec.ts --grep @workspace (S-07, O-04, N-08, N-09)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Archived, pending start, cost alert banner, job failure and job progress notices audited inside their screens (N-05, N-06, N-07, N-10, N-11)"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit --grep @workspace: 135 passed in three projects"
        status: pass
    human_judgment: false
  - id: D5
    description: "German walk over every workspace entry finds no English leak"
    requirement: L10N-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/german-walk.spec.ts --grep @workspace (15 entries x 3 projects)"
        status: pass
    human_judgment: false
  - id: D6
    description: "The workspace slice declares no pending id and carries the baseline ids K-04 to K-07, K-12, K-13 and K-14 for plan 69-31"
    requirement: VER-03
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts (10 tests); grep workspacePending = [] in workspaceScreens.ts"
        status: pass
    human_judgment: false

duration: ~115min
completed: 2026-11-25
status: complete
plan_head_before: 95618ffdb528e15065ee9c68381efa4051f798fb
plan_head_after: 8ed1f4deafe8a7599d7b34ceaa7d2ac508d7b9e5
commits: 5
---

# Phase 69 Plan 27: Workspace slice audits Summary

**All 15 workspace registry entries (Overview, Activity, Settings, Automation panel, review, leave, consent and delete dialogs, seven notices) pass axe, keyboard, reflow, text spacing, sticky coverage and the German walk in three projects and two languages: 135 audit tests, with five real defects fixed.**

## Performance

- **Duration:** about 115 min
- **Tasks:** 3 (tracer, Settings and dialogs, Automation)
- **Commits:** 5 (three task commits and two fix or helper commits)
- **Files:** 1 created, 7 modified

## Accomplishments

- Registered S-04, S-05, S-06, S-07, O-02, O-03, O-04, O-05 and N-05 to N-11 in `workspaceScreens.ts` with the baseline ids K-04, K-05, K-06, K-07, K-12, K-13 and K-14 (K-14 on N-08). `workspacePending` is `[]`.
- Tracer verified end to end: S-04 and S-05 passed in chromium, mobile-390 and mobile-320 in en and de before any expansion.
- The review dialog keeps Apply reachable at 320; the leave dialog offers exactly stay, discard and review; the consent dialog opens with focus on Keep editing and sends no save before Allow (the entry fails if the mock saw one); the automatic shutdown notice precedes the cost alert banner in the document (the entry fails otherwise).
- Fixtures are German-neutral ("Alpha Ops", "Slots", "Join-Passwort"); API prose reaches the page through `ApiText`, which the German walk skips.

## Task Commits

1. **Task 1: Tracer, Overview and Activity** - `170a0985` (test)
2. **Task 2: Settings, review, leave and record dialogs, three notices** - `5ce4b005` (test)
3. **Task 3: Automation panel, consent dialog, shutdown and consent notices** - `8ed1f4de` (test)
4. Helper: date and time segments count as one stop - `4c56e540` (test)
5. Fix: consent dialog body reachable by keyboard - `008a7c4c` (fix)

**Plan metadata:** the SUMMARY commit follows this file.

## Files Created/Modified

- `sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts` - the 15 entries, their ready functions and the pending list
- `sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts` (new) - `installWorkspace`, `installAutomationWorkspace`, `reloadWorkspaceServer`, `automationHandleOf`, fixtures for configuration, credentials and mod packs
- `sources/ReactApp/tests/e2e/support/a11y.ts`, `tests/e2e/audit/a11y-helpers.spec.ts` - segmented-input rule and its self-test
- `sources/ReactApp/src/components/server/ActivityEntry.tsx` - worker output box declares `lang="en"`
- `sources/ReactApp/src/components/server/workspace-shell.css` - scroll padding for the sticky header, stacked actions of the Settings confirmations
- `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` - `dialog--record` class on the delete and discard confirmations
- `sources/ReactApp/src/components/server/ScheduledStartConsentDialog.tsx` - labelled, focusable body region

Not touched although listed in `files_modified`: `ServerWorkspacePage.tsx`, `WorkspaceShell.tsx`, `OverviewSection.tsx`, `ActivitySection.tsx`, `AutomationPanel.tsx`, `ServerConfigurationPanel.tsx`, `ServerSecretsPanel.tsx`, `ServerLogsPanel.tsx`, `HostingAuditFeed.tsx`, `WorkspaceHeader.tsx`, `ModPackPanel.tsx`, `PendingChangesBar.tsx`, `ReviewChangesDialog.tsx`, `PendingStartNotice.tsx`, `CostAlertBanner.tsx`, `JobFailurePanel.tsx`, `JobProgressStrip.tsx`, `AutomationStatusList.tsx`, `ScheduleRuleFields.tsx`, `AutoStopNotice.tsx`, `automation.css`: no audit found a defect in them. `WorkspaceHeader.tsx` is unchanged, so there is no merge overlap with 69-26's `<bdi>` change.

`files_deleted:` none. No new stylesheet, so no `legacy-ratchet.json` entry is needed.

## Decisions Made

See `key-decisions` above. The notable ones: one state per heavy entry to stay inside the 30 s test timeout; native select audited closed; the delete confirmation is audited as it exists (no typed challenge).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Worker diagnostic output shown as untranslated text on a German page**
- **Found during:** Task 1 (German walk of S-05, expanded failed entry)
- **Issue:** `OutputBox` renders the worker's English command output without a language, so a German page flagged it and a screen reader would read it with a German voice (WCAG 3.1.2).
- **Fix:** `lang="en"` on the `pre`.
- **Files modified:** `ActivityEntry.tsx`
- **Committed in:** `170a0985`

**2. [Rule 1 - Bug] Sticky workspace header covered focused elements at 320**
- **Found during:** Task 2 (S-06 sticky coverage, 13 findings in en and de)
- **Issue:** no scroll padding for the sticky topbar and header, so focus scrolling left a focused control under "Start server" (WCAG 2.4.11).
- **Fix:** `html:has(.workspace-header) { scroll-padding-block-start: calc(var(--command-header-height) + 12rem) }` in `workspace-shell.css`; the reserve covers the 190 px header budget plus the topbar.
- **Files modified:** `workspace-shell.css`
- **Committed in:** `5ce4b005`

**3. [Rule 1 - Bug] Settings confirmations did not stack at phone width (O-05)**
- **Found during:** Task 2 (dialog geometry, mobile-390 and mobile-320)
- **Issue:** the plain dialog shell sets `.dialog__actions > * { width: 100% }` at 520 px, but the legacy `.button { width: fit-content }` in `styles.css` comes later at equal specificity and wins.
- **Fix:** a `dialog--record` variant with a doubled selector in `workspace-shell.css`, used by the delete and the discard confirmations. This is the same workaround the review, leave and cost dialogs already carry.
- **Files modified:** `workspace-shell.css`, `SettingsSection.tsx`
- **Committed in:** `5ce4b005`

**4. [Rule 1 - Bug] Scrolling consent dialog body not reachable by keyboard at 320 (O-04)**
- **Found during:** Task 3 (axe `scrollable-region-focusable`, mobile-320)
- **Issue:** the body scrolls inside the dialog on the smallest phone but was not focusable.
- **Fix:** pass `bodyLabel` (existing key `Server_Automation_Consent_StartsLabel`) so `ConfirmDialog` renders it as a focusable, labelled region. No locale file changed.
- **Files modified:** `ScheduledStartConsentDialog.tsx`
- **Committed in:** `008a7c4c`

**5. [Rule 3 - Blocking] Keyboard walk could not audit a rule with a time input**
- **Found during:** Task 3 (S-07, N-09 in chromium)
- **Issue:** the walk presses Tab once per tabbable element, but a time input has one stop per native segment, so it ran out of presses, reported Add, Save, Discard and Delete as unreachable, reported a tab-order difference and a missing focus ring on later segments. This is a limit of the 69-06 helper, first exposed by a rule block.
- **Fix:** `FocusedStop.segmented`; the walk, the Shift+Tab reversal and the sticky coverage check treat the segments of one date or time input as one stop. Self-test added to `a11y-helpers.spec.ts`. This edits `a11y.ts` and its self-test, which are not in this plan's `files_modified`; no sibling plan of the wave edits them as far as the plans show, but the orchestrator should watch for a merge conflict there.
- **Committed in:** `4c56e540`

**6. [Rule 3 - Blocking] New support module and a longer timeout for long pages**
- **Issue:** the plan lists no created file, but one module of mocks for 15 entries would have made `workspaceScreens.ts` unreadable, and the keyboard walk of S-05, S-06 and S-07 takes 10 to 23 s alone, which fails under load at the 30 s default.
- **Fix:** `workspaceMocks.ts` holds the mocks; `allowLongWalk()` calls `test.slow()` (triple timeout) from the setup of S-05, S-06 and S-07. The Playwright import is dynamic so the Vitest guard still loads the module.

---

**Total deviations:** 6 auto-fixed (4 bugs, 2 blocking). **Impact:** four product accessibility defects fixed in the workspace files this slice owns or a file it uses directly; two test-support changes. No scope creep beyond the listed surfaces.

### Cross-plan findings (not fixed here)

- **XPF-69-27-01** - `sources/ReactApp/src/components/shared-primitives.css` (`.dialog__actions > *` at 520 px) loses to `.button { width: fit-content }` in `styles.css`; every plain `ConfirmDialog` without a doubled variant selector shows side-by-side actions on a phone. This plan only works around it for the Settings confirmations (`dialog--record`). The slices that audit plain confirmations (files dialogs O-06, finance O-07, configuration profile archive) will meet it; the root fix is one raised selector in `shared-primitives.css`, and `dialog--record` can then be deleted. The dialog geometry check has no known-finding mechanism, so it cannot be recorded in the registry.

## Issues Encountered

- **UI-SPEC O-05 versus the product:** the spec says the record delete or archive challenge field is described by its helper, but the delete confirmation has no typed challenge. The entry audits the dialog as built and fails if an input in it ever lacks a resolving `aria-describedby`. The challenge field with a helper is the terminate confirmation (O-01, fleet slice).
- **Time zone select "open":** a native `select`; Chromium renders the open list outside the page, so only the closed state is auditable. Recorded in a comment on the entry.
- **Load:** Vitest `tests/hosting/workspace-page.test.tsx` timed out three times under parallel load (5 s) and passed alone (11 tests). Playwright ran on private ports 4373 and 4374 through an uncommitted config that was deleted afterwards; `node_modules` was a junction to the main checkout and no package was installed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 69-31 can take the baseline ids K-04, K-05, K-06, K-07, K-12, K-13 and K-14 from `workspaceScreens.ts`; K-07 needs an element capture of `#server-automation` and K-14 of the notices region (the N-08 entry).
- Plan 69-33 finds `workspacePending` empty.

## Known Stubs

None. The fixtures are test data; no UI stub was added.

## Threat Flags

None. T-69-33 is mitigated: the O-04 entry asserts initial focus on Keep editing and that the mock recorded no automation save before Allow.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts`.
- Commits `170a0985`, `5ce4b005`, `4c56e540`, `008a7c4c` and `8ed1f4de` are ancestors of HEAD.
- Plan verification: `tests/e2e/audit --grep @workspace` 135 passed (chromium, mobile-390, mobile-320); `tests/hosting/screen-registry.test.ts` 10 passed; `tests/hosting`, `tests/styles` and `tests/audit` pass except load timeouts that pass alone; `npx tsc -b` exit 0; `npm run lint` clean (the only error was the temporary private Playwright config, deleted afterwards); `hosting-workspace`, `hosting-change`, `hosting-a11y` and `hosting-automation` specs pass in chromium.
- Acceptance criteria: `"S-05"` 1, `K-04` 1, `"O-03"` 1, `K-12` 1, `server-automation` 9, `workspacePending: readonly string[] = [];` 1.
