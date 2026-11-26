---
phase: 61-foundations-localization-member-access
plan: 05
subsystem: webapp-localization-hosting
status: complete
tags: [react, i18next, localization, vitest, hosting, cost-confirmation]

requires:
  - phase: 61-foundations-localization-member-access
    provides: "61-01 React-owned locale JSON, locale gate and the state model in serverState.ts"
provides:
  - "74 Server_* locale keys (action, confirm, toast, reason, deep-link, access, technical status) in English and German"
  - "actionVocabulary.ts: prototype-safe action id to copy lookups, cost classification, confirm variant, dismiss, pending, confirmation-copy and toast keys"
  - "useFleetQuery, the single fleet query hook (same key, function and 10 s poll)"
  - "isDeploymentActive in the state model and formatHourlyRate in the hosting model"
affects: [61-07, 61-08, 61-09, 61-10, 61-11, 61-12]

actuals:
  tokens: 21000
  tasks: 3
  commits: 3
plan_head_before: 75ace4952371ecde38307de2b539d0abca4ccd7e
plan_head_after: 2e3ee5e5e1603b7dbb95e695087c96c91fb6155a

tech-stack:
  added: []
  patterns:
    - "Action copy lives in a literal as-const table checked against TranslationKey, exposed as a ReadonlyMap so URL and API text never index a plain object"
    - "Unrecognised or missing costImpact resolves to unknown, never none"
    - "Server_ keys are guarded by a literal inventory test plus banned-label, cost-stop-phrase, name-pattern and placeholder-parity tests"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/actionVocabulary.ts
    - sources/ReactApp/src/features/hosting/useFleetQuery.ts
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/action-vocabulary.test.ts
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/features/hosting/serverState.ts
    - sources/ReactApp/src/features/hosting/model.ts
    - sources/ReactApp/tests/hosting/server-state.test.ts
    - sources/ReactApp/tests/hosting/model.test.ts

key-decisions:
  - "isDeploymentActive keeps the command-center semantics verbatim (no deployment or Terminated/TerminatedDueToFault is inactive) so replacing the page-local helper later is behavior-neutral"
  - "formatHourlyRate is a separate function and formatMoney stays untouched, so existing two-digit money output does not change"
  - "FleetPages.tsx and CommandOverviewPage.tsx were deliberately not edited; their migration to the shared helpers belongs to later plans"

patterns-established:
  - "Later plans reference Server_* keys and these helpers only; they do not edit the locale files"

requirements-completed: [FND-02, FND-03, ACC-03, L10N-01]

coverage:
  - id: D1
    description: "All remaining Server_* keys exist in English and German with real values and pass the locale gate"
    requirement: "L10N-01"
    verification:
      - kind: unit
        ref: "tests/locales/server-keys.test.ts"
        status: pass
      - kind: command
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
  - id: D2
    description: "No action label, dismiss, pending or retry value is a generic button label; the cost-stop phrase is identical in the stop label and the start how-to-stop sentence in both languages"
    requirement: "ACC-03"
    verification:
      - kind: unit
        ref: "tests/locales/server-keys.test.ts#has no generic button label among the action keys"
        status: pass
    human_judgment: false
  - id: D3
    description: "Action id lookups are prototype-safe and costImpact classification falls to unknown, never none"
    requirement: "FND-03"
    verification:
      - kind: unit
        ref: "tests/hosting/action-vocabulary.test.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "isDeploymentActive, useFleetQuery and formatHourlyRate exist; sub-cent hourly rates stay visible in English and German"
    requirement: "FND-02"
    verification:
      - kind: unit
        ref: "tests/hosting/server-state.test.ts#isDeploymentActive"
        status: pass
      - kind: unit
        ref: "tests/hosting/model.test.ts#hourly rate and money formatting"
        status: pass
    human_judgment: false
  - id: D5
    description: "German copy reads naturally and the planner-authored strings fit the register of the UI-SPEC"
    requirement: "L10N-01"
    human_judgment: true
    rationale: "Wording quality is not asserted by any test; a German-speaking reviewer should read the planner-authored strings"

duration: 6 min
completed: 2026-11-18
---

# Phase 61 Plan 05: Server Vocabulary and Shared Helpers Summary

**74 Server_* locale keys in English and German, a prototype-safe typed action vocabulary with unknown-by-default cost classification, a shared fleet query hook, `isDeploymentActive` and a sub-cent-safe hourly rate formatter.**

## Performance

- **Duration:** 6 min
- **Tasks:** 3 (all `tdd="true"`, tests written together with the implementation because `workflow.tdd_mode` is off)
- **Files:** 4 created, 6 modified

## Accomplishments

- Added every `Server_Action_*`, `Server_Confirm_*`, `Server_Toast_*`, `Server_Reason_*`, `Server_DeepLink_*`, `Server_Access_*` and `Server_State_TechnicalStatus` key in both languages. UI-SPEC copy is verbatim; planner-authored strings follow the plan. Locale gate: 3108 keys per language, 0 failures, 0 warnings.
- `server-keys.test.ts` pins the literal key inventory, bans generic button labels (English and German) on every action label, dismiss, pending and retry key, asserts the cost-stop phrase appears in the start how-to-stop sentence, validates key naming, and checks placeholder parity.
- `actionVocabulary.ts` exposes `lookupActionVocabulary`, `actionCostCategory`, `isCostBearingAction`, `confirmVariantFor`, `dismissKeyFor`, `pendingKeyFor`, `confirmationCopyKeysFor`, `toastKeyFor` and the `ActionCostCategory` type. Prototype names (`constructor`, `__proto__`, `toString`, `hasOwnProperty`) resolve to nothing (T-61-11); unrecognised or missing `costImpact` is `unknown` (T-61-12).
- `useFleetQuery(enabled)`, `isDeploymentActive(server)` and `formatHourlyRate(value, currency)` added; no consumer was changed.

## Task Commits

1. Task 1: Server locale vocabulary - `1dfe198c` (feat)
2. Task 2: typed action vocabulary - `45008312` (feat)
3. Task 3: fleet query, active deployment and rate helpers - `2e3ee5e5` (feat)

## Verification

- `npm --prefix sources/ReactApp test -- tests/locales tests/hosting`: 26 files, 314 tests passed
- `locales:check`: 0 failures, 0 warnings; `typecheck` and `lint` (max-warnings 0): clean
- All acceptance-criteria greps pass (stop label EN and DE, `Server_Access_Retry` DE, one export each for `useFleetQuery`, `formatHourlyRate` and `isDeploymentActive`)
- `format:check` reports one pre-existing file (see Deferred Issues); every file touched by this plan is formatted

## Deviations from Plan

None - plan executed exactly as written.

## Deferred Issues

- `sources/ReactApp/tests/hosting/arma-placement.test.tsx` fails `prettier --check` before this plan (last touched by `2965563a`, not by this plan). Out of scope; the plan-level `format:check` therefore reports that single file. Recorded for the phase gate.

## Known Stubs

None.

## Threat Flags

None. The plan introduces no network endpoint, auth path or trust-boundary change.

## Next Phase Readiness

Plans 61-07 to 61-12 can reference all keys and helpers without touching the locale files. Plan 61-12 owns replacing the local `useFleet` in `FleetPages.tsx` with `useFleetQuery` and the command overview's local `deploymentIsActive` with `isDeploymentActive`.

## Self-Check: PASSED

- FOUND: actionVocabulary.ts, useFleetQuery.ts, server-keys.test.ts, action-vocabulary.test.ts
- FOUND commits: 1dfe198c, 45008312, 2e3ee5e5
