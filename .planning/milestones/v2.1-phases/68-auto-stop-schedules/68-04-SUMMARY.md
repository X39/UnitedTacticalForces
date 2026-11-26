---
phase: 68-auto-stop-schedules
plan: 04
subsystem: ui
tags: [react, i18n, locales, vitest, automation, schedules]

requires:
  - phase: 68-auto-stop-schedules
    provides: approved 68-UI-SPEC copy contract (no code dependency, wave 1)
provides:
  - All Phase 68 strings in English and German (135 keys, 116 under Server_Automation_)
  - Vocabulary and button-label guards for the Phase 68 prefixes
  - Pure automationModel.ts (draft, validation, dirty detection, update body, consent trigger, closed maps)
affects: [68-12, 68-13, 68-14, 68-15, 68-16, 68-17, 68-18, 68-19]

actuals:
  tokens: 38000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Structural input types so the model lands before the generated contract is bound"
    - "Closed key maps (Map lookup) with a fixed fallback for unknown API values"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/automationModel.ts
    - sources/ReactApp/tests/hosting/automation-model.test.ts
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/workspace-vocabulary.test.ts

key-decisions:
  - "toAutomationUpdate takes an optional fourth parameter idleAvailable (default true) so an unavailable idle shutdown is never sent as enabled, without changing the planned three-argument call shape"
  - "A new Start rule needs consent only once it holds a day or a time, consistent with dirty detection; a blank rule is blocked by validation first"
  - "Time zone ids without a slash form their own one-zone region group, following the 'part before the first slash' rule literally"
  - "formatWallTime returns an empty text for an unparsable instant or zone instead of throwing or showing a wrong time"

patterns-established:
  - "Phase 68 locale block: explicit key list, plural bases, button keys and a banned-label set in server-keys.test.ts"
  - "Phase 68 prefix scan (Server_Automation_, Server_Activity_Trigger_) in workspace-vocabulary.test.ts"

requirements-completed: [AUT-01, AUT-02, AUT-03, AUT-04]

coverage:
  - id: D1
    description: "Every Phase 68 string exists in English and German with equal placeholders, plural pairs and no cost-stop vocabulary"
    requirement: "AUT-01"
    verification:
      - kind: unit
        ref: "tests/locales/server-keys.test.ts#Phase 68 locale vocabulary"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-vocabulary.test.ts#Phase 68 cost-stop vocabulary guard"
        status: pass
    human_judgment: false
  - id: D2
    description: "Pure automation model: validation, dirty detection, update body, consent trigger, closed presentation maps, zone options"
    requirement: "AUT-02"
    verification:
      - kind: unit
        ref: "tests/hosting/automation-model.test.ts"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-22
status: complete
commits: 4
plan_head_before: a3d6871108e7d2338fa89fddef179e6898e5344d
plan_head_after: 0dec69d9d0986c5fb8f8f43de658670b022eb35a
---

# Phase 68 Plan 04: Automation strings and pure model Summary

**All Phase 68 copy in EN and DE with vocabulary and button-label guards, plus a framework-free automation model (draft, validation, consent trigger, closed maps) with 50 unit tests.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 2 of 2
- **Files modified:** 7 (2 created)

## Accomplishments

- Added 135 keys to `en.json` and `de.json` exactly as the UI-SPEC Copywriting Contract specifies: 116 `Server_Automation_*`, 5 `Server_Activity_Trigger_*`, seven `Server_Activity_Audit_*` kinds and the `Server_Overview_Automation*` row. Carried keys were reused, not re-added. `Server_Automation_Status_Next_Value` is allow-listed ("placeholders only"). `npm run locales:check` reports 0 failures and 0 warnings.
- Added the "Phase 68 locale vocabulary" block (key list, once-each, unlisted-key guard, placeholder parity, plural forms, plural rendering, seven audit kinds, app terms, nine button keys against banned generic labels in both languages) and a Phase 68 cost-stop scan of the two new prefixes.
- Built `automationModel.ts` with TSDoc on every export: `draftFromSaved`, `parseWholeNumber`, `validateAutomationDraft`, `isAutomationDraftDirty`, `toAutomationUpdate`, `needsStartConsent`, `sortRulesForDisplay`, `idleStatusKey`, `lastRunKey`, `ruleSummaryKey`, `weekdayLabels`, `timeZoneOptions`, `formatWallTime`. No timers, no instant arithmetic, no generated-contract import.

## Task Commits

1. **Task 1 (tracer): strings and guards** - `fa17b01b` feat(webapp): add the automation and schedule strings; `3f5f9eb6` test(webapp): guard the automation vocabulary
2. **Task 2: pure model** - `817634ac` feat(webapp): add the pure automation model; `0dec69d9` test(webapp): cover the automation model

The tracer verification (`locales:check` plus the two vocabulary suites) passed before the model was written.

## Decisions Made

See `key-decisions` above. None changes the visual contract.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Optional `idleAvailable` parameter on `toAutomationUpdate`**
- **Found during:** Task 2
- **Issue:** The UI-SPEC requires that an unavailable idle shutdown is "not sent as enabled", but the planned signature `toAutomationUpdate(draft, saved, startConsent)` cannot know availability.
- **Fix:** Added a fourth optional parameter `idleAvailable = true`; the three-argument call keeps working.
- **Files modified:** `sources/ReactApp/src/features/hosting/automationModel.ts`
- **Commit:** `817634ac`

Otherwise the plan was executed as written.

## Issues Encountered

- `prettier --check` flags the working-tree locale files because this checkout uses CRLF; the LF form of all three locale files passes. This is the existing state of the checkout, not a content issue.
- The full `vitest run` also tries to load the Playwright files under `tests/uat/` and fails those four files (pre-existing, unrelated to this plan). Two timing-sensitive component tests timed out under the load of a concurrent run and pass in isolation.

## Known Stubs

None.

## Threat Flags

None. T-68-07 is mitigated: unknown idle states and outcome codes map to fixed fallback keys (`Unknown`, `Other`), covered by tests.

## Next Phase Readiness

Plans that build the panel, notice, Overview fact and Activity entries can import the model and use the keys without touching the locale files. Plan 68-12 should assert the structural model types against the generated contract.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/hosting/automationModel.ts
- FOUND: sources/ReactApp/tests/hosting/automation-model.test.ts
- FOUND commits: fa17b01b, 3f5f9eb6, 817634ac, 0dec69d9
- Verified: locales:check 0 failures, vitest for the three suites pass, typecheck and lint clean
