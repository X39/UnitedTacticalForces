---
phase: 66-accrued-cost-cost-alerts
plan: 11
subsystem: ui
tags: [react, i18n, cost-alerts, accessibility, finance, locale]
status: complete

requires:
  - phase: 66-accrued-cost-cost-alerts
    provides: plan 66-08 strings, GameServer.costAlertThresholds and canManageCostAlerts, identityService.recordLocale; plan 66-10 CostAlertSettingsPanel, CostAlertsPage and alertSettingsModel
provides:
  - "CostStrip Cost alerts row (None set, first three amounts via Intl.ListFormat unit style, and N more, Set an alert or Manage alerts link for managers)"
  - "Channels panel: always-on In the app row, Discord and Email rows with one availability and audience hint sentence each, Open account settings link, language line"
  - "channelHintKey: pure Map lookup (channel x availability x audience) with a fixed fallback"
  - "Cost alerts h2 section on the finance account page using the shared panel at heading level 3"
  - "Fleet header ghost link to /command/servers/cost-alerts"
  - "Fire-and-forget locale recording on a signed-in language switch in both layouts"
affects: [66-12, uat]

plan_head_before: 1fbc3ff6051d27d1961d23e29711890f19fa1008
plan_head_after: 93f3606f4510d7c6f4eeefa1a749d5a95c83e768

actuals:
  tokens: 9500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Hint text chosen by a typed Map lookup keyed channel:availability:audience, with a per-channel fallback to the plain nothing-can-be-sent sentence"
    - "Locale recording is void identityService.recordLocale(language).catch(() => undefined) guarded by auth.user"

key-files:
  created:
    - sources/ReactApp/tests/locale-recording.test.tsx
  modified:
    - sources/ReactApp/src/components/server/CostStrip.tsx
    - sources/ReactApp/src/components/server/workspace-shell.css
    - sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx
    - sources/ReactApp/src/features/hosting/alertSettingsModel.ts
    - sources/ReactApp/src/features/hosting/cost-alert-settings.css
    - sources/ReactApp/src/features/hosting/HostingFinanceAccountPage.tsx
    - sources/ReactApp/src/features/hosting/FleetPages.tsx
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/tests/hosting/cost-strip.test.tsx
    - sources/ReactApp/tests/hosting/cost-alert-settings.test.tsx
    - sources/ReactApp/tests/hosting/finance-pages.test.tsx
    - sources/ReactApp/tests/hosting/fleet-page.test.tsx

key-decisions:
  - "A personal owner whose messageLanguage is null falls back to the active UI language in the language line, because saving records that language"
  - "The finance section is a local FinanceCostAlerts component with its own loading and error panels, so a failing alert request never replaces the account"
  - "The locale test spies on identityService.recordLocale (msw is not a dependency); the request shape is covered by the plan 66-08 service tests"

requirements-completed: [CST-04, CST-05, CST-06]

coverage:
  - id: D1
    description: "The strip renders a Cost alerts row only for a summary array, with None set, up to three amounts, and N more, and a manage link only when canManageCostAlerts, in English and German"
    requirement: "CST-04"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-strip.test.tsx#CostStrip cost alerts row"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every availability and audience pair shows its hint in English and German; In the app is checked and disabled; undeliverable channels stay selectable and dirty the form; the language line names the recorded language or the per-person rule"
    requirement: "CST-05"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-alert-settings.test.tsx#Cost alert channels panel"
        status: pass
    human_judgment: false
  - id: D3
    description: "Finance viewers see the owner's alerts between the metrics and the protocol, read-only when they cannot manage, with a section error that keeps the account, and no request without view permission"
    requirement: "CST-06"
    verification:
      - kind: unit
        ref: "tests/hosting/finance-pages.test.tsx"
        status: pass
    human_judgment: false
  - id: D4
    description: "The fleet header links to Cost alerts; a signed-in language switch records the language once in both layouts, a signed-out switch records nothing, and a failing request shows nothing and raises no unhandled rejection"
    requirement: "CST-06"
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-page.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/locale-recording.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "German wrapping of the strip row, hints and language line at 320px, the 44px rows at 800px and the account settings link on its own line are asserted by CSS review and unit text only, not in a browser"
    requirement: "CST-05"
    verification: []
    human_judgment: true

duration: about 55m
completed: 2026-11-21
---

# Phase 66 Plan 11: Cost Alert Channels, Strip Row and Locale Recording Summary

**The workspace cost strip now summarizes the owner's alert amounts and links to manage them, the channels panel says plainly what Discord and email can deliver and in which language, finance staff and the fleet page reach the same settings, and every signed-in language switch is recorded.**

## Performance

- **Duration:** about 55 min
- **Completed:** 2026-11-21
- **Tasks:** 3 (1 tracer, 2 auto/tdd)
- **Files:** 1 created, 13 modified

## Accomplishments

- Tracer: sixth `Cost alerts` strip row only when `costAlertThresholds` is an array; `None set`, the first three amounts in the locale money format joined by `Intl.ListFormat` unit style, `and N more` beyond three, and a ghost compact `Set an alert` or `Manage alerts` link to `/command/servers/cost-alerts?owner={costOwnerId}` for managers.
- Channels panel: description, a checked and disabled In the app row with its reason linked by `aria-describedby`, Discord and Email rows whose hint comes from `channelHintKey`, the Open account settings link on its own line for a not-linked personal owner, and a language line from `Intl.DisplayNames` (personal) or the per-person sentence (group).
- Finance account page: an h2 `Cost alerts` section between the metrics and the protocol with its own loading and error panels, the shared panel at heading level 3, shown only when `permissions.view`.
- Fleet header ghost link and fire-and-forget locale recording in both layouts.
- About 54 new tests (strip 11, channels about 30 including both languages for all 12 availability and audience pairs, finance 4, fleet 2, locale recording 7).

## Task Commits

1. **Task 1: Tracer, strip Cost alerts row** - `869e8aa4` (feat, webapp)
2. **Task 2: Channels panel, hints, language line** - `e429687f` (feat, webapp)
3. **Task 3: Finance section, fleet link, locale recording** - `93f3606f` (feat, webapp)

## Verification

- `vitest run tests/hosting tests/locale-recording.test.tsx tests/styles tests/locales tests/app tests/form-fields.test.tsx`: 103 files, 1776 tests passed.
- `typecheck`, `lint --max-warnings 0` and `locales:check` (3635 keys each) clean; `prettier --check` is clean on every file this plan changed (the pre-existing format drift in other files is untouched).
- `git status --porcelain -- sources/WebApp` is empty. Acceptance greps: `Server_Workspace_Cost_Alerts` 6, `cost-alerts?owner=` 1, `Intl.DisplayNames` 1, `Server_Alerts_Channel_InAppHint` 1, `Server_Alerts_Discord_OpenSettings` 1, `recordLocale` 1 in each layout, `cost-alerts` 1 in `FleetPages.tsx`, `CostAlertSettingsPanel` 6 in the finance page.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Hint key helper had to be typed**
- **Found during:** Task 2 typecheck
- **Issue:** `t()` only accepts the generated `TranslationKey` union, so a helper returning `string` failed to compile.
- **Fix:** Added the `AlertChannelHintKey` union to `alertSettingsModel.ts` (the helper lives there so it can be unit tested without rendering). The plan listed only the panel; the model file is the natural owner.
- **Commit:** `e429687f`

**2. [Rule 3 - Blocking] Strip styling and tests needed a router and one rule**
- **Issue:** `ActionLink` needs a router, so the strip test helper now wraps in `MemoryRouter`; a `.cost-strip__alerts` rule in `workspace-shell.css` (not in the plan's file list) sets the summary line's margin and tabular figures.
- **Commit:** `869e8aa4`

**3. [Rule 3 - Process] Locale tests spy on `identityService.recordLocale`, not msw**
- msw is not a dependency (same finding as plan 66-10). The request itself is covered by the plan 66-08 service tests.

**4. [Rule 3 - Blocking] localStorage in the test runtime**
- **Issue:** `setCulture` writes to `localStorage`, and the runtime's experimental global shadows jsdom's with a non-working one, so the language select threw in `tests/locale-recording.test.tsx`.
- **Fix:** The test stubs an in-memory `localStorage` for its own file only.
- **Commit:** `93f3606f`

**5. [Rule 3 - Process] Signed-out case only applies to the community layout**
- The command center layout renders an access panel without a signed-in user and has no language select, so the signed-out assertion covers the community layout only.

**6. [Process] Tests were written together with the implementation, not RED first**
- Task 2 and 3 tests passed on their first run after the implementation; they still assert every plan behavior line.

**Total deviations:** 6 (no bug fixes, no scope change).

## Known Stubs

None.

## Threat Flags

None. T-66-36 (finance section rendered only for `permissions.view`, API authorizes independently) and T-66-37 (locale recorded only for a signed-in user, to the caller's own record) are implemented as planned and tested; T-66-SC accepted.

## Self-Check: PASSED

- Created file `sources/ReactApp/tests/locale-recording.test.tsx` exists; commits `869e8aa4`, `e429687f`, `93f3606f` are ancestors of HEAD (`git rev-list --count` from the ledger base is 3).
