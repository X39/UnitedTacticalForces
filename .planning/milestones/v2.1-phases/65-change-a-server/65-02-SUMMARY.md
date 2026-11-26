---
phase: 65-change-a-server
plan: 02
subsystem: ui
tags: [react, hosting, draft-state, reducer, i18n, vitest]

requires:
  - phase: 64
    provides: workspaceModel configuration helpers, the Server_Create_* locale pattern and server-keys test
provides:
  - serverChangeDraft pure model (stage, normalize, count, plan, run, results, validation, review descriptors)
  - ServerChangeDraftProvider and useServerChangeDraft in-memory provider for the Settings section
  - configurationEntry test factory shared by Phase 65 tests
  - 145 Phase 65 copy keys in English and German plus the key, plural and button-label guards
affects: [65-07, 65-08, 65-09, 65-10, 65-11, 65-12, 65-13]

plan_head_before: bea396c2748f0080e79bfd14595339ee09ab951f
plan_head_after: fba30e9bc6815b420769c8d29372b475f0df3a93

actuals:
  tokens: 26000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Draft held as raw state and re-normalized against the latest saved data on every render (derived, never copied)"
    - "Apply runner with injected executors returning per-item results; failure reason mapped only from ApiError.code"
    - "Review descriptors are typed values; the dialog maps them to copy, so the model holds no translated text"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/serverChangeDraft.ts
    - sources/ReactApp/src/features/hosting/useServerChangeDraft.tsx
    - sources/ReactApp/tests/helpers/configurationFixtures.ts
    - sources/ReactApp/tests/hosting/server-change-draft.test.ts
    - sources/ReactApp/tests/hosting/use-server-change-draft.test.tsx
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/locales/server-keys.test.ts

key-decisions:
  - "Credential review rows carry a placeholder Before value (notSet); the dialog shows only After for the credentials group, because an existing credential is never described"
  - "buildApplyPlan leaves out a staged value whose entry is missing or unparseable, so callers run validateStagedSettings first; removeApplied clears all values and resets when the settings step applied"
  - "describeReview skips sensitive and redacted entries even if a non-normalized draft contains them"
  - "Mod pack and profile review rows have an empty name; the dialog renders their label from copy"

patterns-established:
  - "Phase key list plus mirrored button-key list in tests/locales/server-keys.test.ts (phase65RequiredKeys, phase65ButtonKeys)"
  - "Provider file useServerChangeDraft.tsx exports both the provider and the hook"

requirements-completed: [CHG-03]

coverage:
  - id: D1
    description: "One in-memory draft stages mod pack, profile, values, resets and credential changes with disjoint values and resets; staged values equal to saved data are not changes"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/server-change-draft.test.ts#normalizeDraft"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/use-server-change-draft.test.tsx#exposes the normalized draft and count; an equal value is not a change"
        status: pass
    human_judgment: false
  - id: D2
    description: "Apply plan order, dependency (settings not tried when the profile failed), independent steps still run, applied items leave the draft, failed and not-tried stay"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/server-change-draft.test.ts#apply runner"
        status: pass
    human_judgment: false
  - id: D3
    description: "Draft is memory only: remount starts empty, browser storage is never written, no storage or console usage in the model or provider"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/use-server-change-draft.test.tsx#starts empty after a remount and never writes to browser storage"
        status: pass
      - kind: other
        ref: "grep for localStorage|sessionStorage|indexedDB|document.cookie|console. in both source files returns 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "Failure reasons come from ApiError.code only; the error message is never read"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/server-change-draft.test.ts#failureReason"
        status: pass
      - kind: other
        ref: "grep -c '.message' serverChangeDraft.ts returns 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "Review descriptors never contain a staged secret value or a sensitive entry"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/server-change-draft.test.ts#omits empty groups and never exposes a staged secret value or a sensitive entry"
        status: pass
    human_judgment: false
  - id: D6
    description: "All 145 Phase 65 strings exist in English and German with equal interpolation names; plurals render; no button label resolves to a banned generic label"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/locales/server-keys.test.ts#Phase 65 locale vocabulary"
        status: pass
      - kind: other
        ref: "npm run locales:check: 3525 keys, 0 failures, 0 warnings"
        status: pass
    human_judgment: false
  - id: D7
    description: "Stale draft against changed saved data: normalization and fresh Before values are unit tested; a live two-session check is a Phase 69 journey"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/use-server-change-draft.test.tsx#re-normalizes against changed saved data without dispatching"
        status: pass
    human_judgment: false

duration: 13min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 02: Staged-change draft model, provider and copy Summary

**Framework-free staged-change draft (stage, normalize, plan, sequential apply with per-item results) plus an in-memory React provider and all 145 Phase 65 strings in English and German, so the UI plans compile against stable keys.**

## Performance

- **Duration:** 13 min (2026-11-20T19:28Z to 19:41Z)
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 9 (5 created, 4 modified)

## Accomplishments

- `serverChangeDraft.ts`: pure model with stage functions that never mutate, `normalizeDraft` against the latest saved data, `buildApplyPlan` (mod pack, profile, one settings update, credentials by ordinal key), `runApplyPlan` (sequential, never throws, settings Not tried when the profile failed, nothing rolled back), `failureReason` from `ApiError.code`, `removeApplied`, `summarizeResults`, typed validation (`configurationEntryIssue`, `validateStagedSettings`) and typed review descriptors (`describeReview`, `removeReviewItem`).
- `useServerChangeDraft.tsx`: `useReducer` over the pure functions; the effective draft and count are derived on every render with `useMemo(normalizeDraft(raw, saved))`; the hook throws a descriptive error outside `ServerChangeDraftProvider`.
- `configurationEntry` factory so plan 65-09 only updates one place when `tier` is added.
- 145 keys (63 Server_Change, 24 Server_Mods, 26 new Server_Settings, 21 Server_Credentials, 11 Server_Files) merged into the existing key order of both locale files, three identical-allowed entries, and a `phase65RequiredKeys` guard with plural, interpolation and button-label checks.

## Task Commits

1. **Task 1 (tracer): pure draft model, factory and tests** - `4ddfc387` (feat(webapp))
2. **Task 2: validation, review descriptors and provider** - `781ac7d6` (feat(webapp))
3. **Task 3: copy in English and German with guards** - `fba30e9b` (feat(webapp))

## Verification Results

- Tracer gate (Task 1): 19 tests green, typecheck and lint clean before expansion.
- `npm test` for the draft, provider and locale tests: green; full ReactApp suite 137 files, 1571 tests passed.
- `npm run locales:check`: 3525 English and 3525 German keys, 0 failures, 0 warnings.
- `typecheck` and `lint --max-warnings 0` exit 0; Prettier check passes on every file this plan touched.
- Acceptance greps: runApplyPlan 1, normalizeDraft 1, describeReview 1, ServerChangeDraftProvider 1, storage/console 0 in both source files, `.message` 0, locale counts 63 / 24 / 32 / 21 / 11, `phase65RequiredKeys` 7.
- `sources/Common/Ui/routes.json` unchanged; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`.

## Deviations from Plan

None - plan executed exactly as written. The TDD cycle was relaxed as in plan 65-01: implementation and tests were written together and run green rather than committed as separate RED and GREEN steps.

## Issues Encountered

`npm run format:check` over the whole project reports 40 files, all of them pre-existing test files outside this plan (for example `tests/hosting/create-*.test.ts`, `fleet-*.test.ts`); none is a file this plan touched, so they were left alone (out of scope). A repository-wide format pass is a separate cleanup.

## Known Stubs

None.

## Threat Flags

None. T-65-04 (no storage, URL, console or cache for the draft), T-65-05 (reasons from the API code only) and T-65-06 (credential descriptors are secretChanged or secretRotated only) are mitigated and covered by the tests and grep gates above.

## Next Phase Readiness

Plans 65-07, 65-08, 65-10, 65-11, 65-12 and 65-13 can build panels, the bar and the review dialog on `useServerChangeDraft`, `describeReview` and `runApplyPlan` without touching the locale files. Plan 65-09 swaps `GameConfigurationEntry` for the generated alias and adds `tier` in `configurationEntry`.

## Self-Check: PASSED

- Created and modified files verified on disk; commits `4ddfc387`, `781ac7d6`, `fba30e9b` verified as ancestors of HEAD.
