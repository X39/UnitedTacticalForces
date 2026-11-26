---
phase: 63-workspace-shell-activity
plan: 01
subsystem: ui
tags: [react, i18n, localization, routing, vitest, hosting]

requires:
  - phase: 61-foundations-localization-member-access
    provides: flat en/de JSON locales, locales:check gate, Server_Action_* vocabulary, ownerFor placeholder pattern
  - phase: 62-fleet-quick-actions
    provides: Server_Fleet_Live_Reconnect and the fleet vocabulary reused by the live badge
provides:
  - closed five-section workspace registry with whitelist ?tab= parser and section/activity-entry link builders
  - every Phase 63 string in English and German (161 new keys plus the section keys)
  - shared job-kind label map and actor display rule
  - cost-stop vocabulary guard over every new Phase 63 key
affects: [63-02, 63-03, 63-04, 63-05, 63-06, 63-07, 63-08, 63-09, 63-10, 63-11, 63-12]

actuals:
  tokens: 12964
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Untrusted URL and API strings resolve only through find or Map lookups, never object indexing"
    - "Phase copy contract enforced by a prefix-scanning vocabulary test that proves its own pattern is live"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/workspaceSections.ts
    - sources/ReactApp/src/features/hosting/activityLabels.ts
    - sources/ReactApp/tests/hosting/workspace-sections.test.ts
    - sources/ReactApp/tests/hosting/activity-labels.test.ts
    - sources/ReactApp/tests/hosting/workspace-vocabulary.test.ts
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/locales/server-keys.test.ts

key-decisions:
  - "Section ids are the lowercase English names overview, activity, settings, files, machine; Overview writes no tab parameter"
  - "workspaceSectionFromSearch matches with find over workspaceTabIds, so __proto__ and constructor resolve to overview"
  - "sectionTo and activityEntryTo encode the server id as one path segment and return plain location objects"
  - "actorFor treats a blank name like the hidden-member placeholder so an empty actor is never rendered"

patterns-established:
  - "Link builders in workspaceSections.ts are the only place that writes ?tab= and activity-* fragments"
  - "Job-kind labels and actor display live in activityLabels.ts for progress, failure and activity"

requirements-completed: [WSP-02, CST-02]

coverage:
  - id: D1
    description: "A ?tab= value resolves through the closed section registry; retired, unknown, cased and prototype values open Overview"
    requirement: WSP-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-sections.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "Section labels render in English and German through the app i18n instance, and the section navigation label reads Server sections / Serverbereiche"
    requirement: WSP-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-sections.test.ts#resolves a tab value to its localized label through the app i18n instance"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every Phase 63 UI-SPEC string exists in both languages with identical placeholders and plural forms"
    verification:
      - kind: unit
        ref: "tests/locales/server-keys.test.ts#Phase 63 locale vocabulary"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
  - id: D4
    description: "The cost-stop phrase is Shut down and stop billing and a live guard rejects banned synonyms in every new key"
    requirement: CST-02
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-vocabulary.test.ts"
        status: pass
    human_judgment: false
  - id: D5
    description: "Job-kind label map and actor display rule defined once, safe against prototype-key input"
    verification:
      - kind: unit
        ref: "tests/hosting/activity-labels.test.ts"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-11-19
status: complete
commits: 2
plan_head_before: eab7b5fab3d87f620af18abdedea04db2557c81a
plan_head_after: 0fab36e4c5db4fca3d76860ed30a808f694e1406
---

# Phase 63 Plan 01: Workspace Section Registry and Phase Copy Summary

**Closed five-section `?tab=` registry with a whitelist parser and link builders, all Phase 63 copy in English and German, shared job-kind and actor label rules, and a live cost-stop vocabulary guard.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-11-19T15:16:03Z
- **Completed:** 2026-11-19T15:24:24Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- `workspaceSections.ts` resolves any `?tab=` value to one of five sections; retired ids (`jobs`, `cloud`, `runtime`, `logs`, `configuration`, `credentials`, `audit`), wrong case, empty values and prototype keys open Overview with no mapping table. `sectionSearch` writes only `tab`, so a stale `?action=` deep link cannot re-fire after a section switch.
- Tracer proven end to end first: a tab value renders its label in English and German through the app's i18n instance ("Machine"/"Maschine", unknown value "Overview"/"Übersicht").
- 161 locale keys added to `en.json` and `de.json` (75 `Server_Activity_*`, 20 `Server_Progress_*`, 19 `Server_Failure_*`, `Server_Live_*`, `Server_Overview_*`, `Server_Settings_*`, `Server_Machine_*`, remaining `Server_Workspace_*`, `Shared_Copy_*`), purely additive; three identical loanword values are allow-listed.
- `activityLabels.ts` defines `ActorDisplay`, `hiddenActorName`, `actorFor`, `actorLabelKey` and `jobKindLabelKey` once for the later progress, failure and activity components.
- `workspace-vocabulary.test.ts` scans every Phase 63 key prefix in both languages for banned cost-stop synonyms, proves the pattern live on in-memory samples, and checks key-set and placeholder parity.

## Task Commits

1. **Task 1: Tracer, section registry resolves a ?tab= value to a localized label** - `6bd0a3a8` (feat)
2. **Task 2: Remaining Phase 63 strings, shared label rules, vocabulary guard** - `0fab36e4` (feat)

**Plan metadata:** recorded in the docs commit that carries this summary.

## Files Created/Modified

- `sources/ReactApp/src/features/hosting/workspaceSections.ts` - section registry, whitelist parser, link and anchor builders
- `sources/ReactApp/src/features/hosting/activityLabels.ts` - job-kind label keys and actor display rule
- `sources/ReactApp/src/locales/en.json`, `de.json` - every Phase 63 string, changed `Hosting_Detail_WorkspaceNavigation`
- `sources/ReactApp/src/locales/identical-allowed.json` - three loanword entries
- `sources/ReactApp/tests/hosting/workspace-sections.test.ts`, `activity-labels.test.ts`, `workspace-vocabulary.test.ts` - Wave 0 tests
- `sources/ReactApp/tests/locales/server-keys.test.ts` - `phase63RequiredKeys` completeness and plural checks

## Decisions Made

- Section ids are lowercase English names; Overview writes no tab parameter (RESEARCH A1).
- The parser uses `find` over the whitelist and never indexes by the raw value (T-63-01); `jobKindLabelKey` and `actorLabelKey` use `Map` lookups (T-63-03).
- `sectionTo` and `activityEntryTo` percent-encode the server id as a single path segment; for GUID ids this is a no-op.
- A blank actor name is treated as hidden, so an empty actor is never rendered.

## Deviations from Plan

None - plan executed exactly as written.

The activity-labels test was written together with its module instead of as a separate failing step; the tracer task followed a visible RED run.

## Issues Encountered

`npm run format:check` reports two pre-existing files that this plan does not touch (`tests/hosting/arma-placement.test.tsx`, `tests/hosting/fleet-fixtures.test.ts`). Every file this plan touched passes Prettier. Left alone per the scope boundary and logged in `deferred-items.md`.

## Known Stubs

None.

## Threat Flags

None. The plan introduces no endpoint, auth path or schema change.

## Next Phase Readiness

Ready for 63-02 and the wave-2 component plans: every key they reference exists in both languages, and plan 63-05 can swap the page's tab registry for `workspaceSections.ts` (it still exports `workspaceTabIds` for the parity test). `ServerWorkspacePage.tsx`, the parity matrix and `routes.json` were not touched.

## Self-Check: PASSED

- Created files verified on disk (`workspaceSections.ts`, `activityLabels.ts`, three new tests).
- Commits `6bd0a3a8` and `0fab36e4` are ancestors of HEAD.
- Verification run: `tests/hosting` and `tests/locales` 52 files, 676 tests green; `locales:check` 3303 keys, 0 failures, 0 warnings; `typecheck` and `lint` exit 0; `routes.json` unchanged; neither commit lists `ServerWorkspacePage.tsx`.
