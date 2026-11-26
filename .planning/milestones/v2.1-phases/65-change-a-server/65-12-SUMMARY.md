---
phase: 65-change-a-server
plan: 12
subsystem: ui
tags: [react, hosting, configuration, tiers, disclosure, staged-profile, accessibility, vitest, playwright]

requires:
  - phase: 65-07
    provides: the Settings draft provider, stageValue and stageReset, the pending bar and the review dialog
  - phase: 65-09
    provides: the generated GameConfigurationEntry tier (Common or Advanced) and latestRevisionId on profile options
  - phase: 65-10
    provides: reviewAttempt, configurationEntryIssue field errors and the invalid-field focus effect
provides:
  - ConfigurationEntryField, one generic entry control (game contribution, boolean, selection, string array, number, text) built from the shared form fields with Changed badge, Undo change and staged reset
  - Configuration panel split into Common settings and a collapsed Advanced settings disclosure that opens by itself for staged, required-empty and invalid entries
  - Configuration profile and revision mode staged through the draft (stageProfile and unstageProfile), applied before settings
  - Placement estimate that stays visible while changes are staged, with the saved-settings note
  - Load error with Try again, empty-game copy and a source scan that keeps raw controls, error text and game literals out of the panel
affects: [65-13, 65-14, 65-15]

plan_head_before: 72869d2aacac6475f398edce7b9574d2ea382f59
plan_head_after: c3238585d6a40bb8fad70f934ea1227aa383fa4f

actuals:
  tokens: 32000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Advanced auto-open is a trigger set (staged:key, required:key, invalid:key:attempt) kept as a signature string in state and compared during render; the disclosure opens only for a trigger id not seen before, so closing it sticks and Undo never reopens it"
    - "The saved profile and pinned revision come from the draft context (saved.profile); choosing them again calls unstageProfile, so a staged item exists only while it differs from the server"
    - "The source line and the description share the shared field hint, so API prose stays inside ApiText and the label, hint, control and error wiring come from FormFields"

key-files:
  created:
    - sources/ReactApp/src/components/server/ConfigurationEntryField.tsx
    - sources/ReactApp/src/components/server/configuration.css
    - sources/ReactApp/tests/hosting/configuration-panel.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx
    - sources/ReactApp/src/features/hosting/PlacementEstimate.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/arma-placement.test.tsx
    - sources/ReactApp/tests/hosting/change-flow.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/e2e/hosting-placement.spec.ts

key-decisions:
  - "A game with no Common entry gets one flat grouped list; the flat list shows the target caption too, because there is no Advanced part to carry it and the file name would otherwise be lost"
  - "Required entries are announced with aria-required instead of the shared field's visible asterisk, so existing label text and tests stay stable"
  - "The game contribution always receives allowUnset false on a server; only profile editing may leave a placement unset (the existing two-choices assertion is kept)"
  - "Reset to inherited and Undo change carry the entry name in their aria-label (Server_Settings_Reset_Name, Server_Change_Field_Undo_Name), so tests address them by name"

patterns-established:
  - "Entry field for any configuration setting: pass the staged value, the error text and the staging callbacks; it never reads draft state itself"

requirements-completed: [CHG-02, CHG-03]

coverage:
  - id: D1
    description: "Settings render Common entries under Common settings and every other entry inside a collapsed Advanced settings disclosure with a visible summary; a game without Common entries gets one flat list with no heading and no disclosure"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#shows Common settings first and hides the rest in a collapsed Advanced disclosure"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#renders one flat grouped list when no entry is Common"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#treats an entry without a tier as Advanced"
        status: pass
    human_judgment: false
  - id: D2
    description: "Advanced opens by itself for a staged change, a required empty entry or an invalid field after a review attempt (with focus on the field) and stays closed after the member closes it until a new trigger occurs"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#opens Advanced when a change is staged inside it and offers Undo change"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#stays closed after the member closes it until a new trigger occurs"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#opens Advanced on render when a required entry inside has no value"
        status: pass
    human_judgment: false
  - id: D3
    description: "Each entry renders through one generic field by kind (contribution, boolean, selection, string array, number, text) with source line, hint, warning, Changed badge, Undo change and a staged reset; sensitive or redacted entries are read-only and never staged"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#ConfigurationEntryField"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#stages a reset to the inherited value with its staged line and Undo change"
        status: pass
    human_judgment: false
  - id: D4
    description: "The configuration profile and its revision mode (including a newly chosen profile through latestRevisionId and the unavailable case with its reason) are staged, unstaged when the saved selection is chosen again, and applied before settings only after Apply"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#Configuration panel profile and states"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-page.test.tsx#stages typed sparse configuration overrides, applies them and a reset, and selects profile mode"
        status: pass
    human_judgment: false
  - id: D5
    description: "The placement estimate stays visible with the saved-settings note while changes are staged and is hidden while pending or unavailable; the placement e2e passes through the staged flow"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/arma-placement.test.tsx#keeps the estimate visible with the saved-settings note while changes are staged"
        status: pass
      - kind: e2e
        ref: "playwright:tests/e2e/hosting-placement.spec.ts#applies Arma placement through typed configuration and reloads its estimate"
        status: pass
    human_judgment: false
  - id: D6
    description: "Loading, load error with Try again (no raw error text), empty game with the profile choice, hand-edited file warning, incomplete warning, target caption once per target, read-only controls, and a source scan with no raw control, error text or game literal"
    requirement: CHG-02
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#Configuration panel source scan"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/configuration-panel.test.tsx#shows a load error without the raw error text and retries"
        status: pass
    human_judgment: false
  - id: D7
    description: "Visual rhythm of the Common and Advanced parts, the disclosure summary line and field wrapping at 320 and 390 px in English and German; jsdom cannot measure layout (the very long Advanced list is a plain stack, re-confirmed by the Phase 69 localization walk)"
    requirement: CHG-02
    verification: []
    human_judgment: true
    rationale: "The CSS is token-only and passes the style guard, but measured reflow and the long-list walk are the Phase 69 localization pass"

duration: 25min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 12: Common and Advanced settings with a staged profile Summary

**The Configuration panel now shows each game's Common settings first and everything else behind an Advanced disclosure that opens by itself when something inside needs attention, renders every entry through one generic field built from the shared form fields, stages the profile and revision mode like any other change, and keeps the saved placement estimate visible while changes are staged.**

## Performance

- **Duration:** 25 min (2026-11-20T21:52Z to 22:17Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 11 (3 created, 8 modified)

## Accomplishments

- Tracer: `ConfigurationEntryField` (contribution first, then boolean, selection, string array, number, text) with the source line and description in the shared hint, the warning, a Changed badge with `Undo change: {name}` and `Reset to inherited: {name}` staging. `ServerConfigurationPanel` splits entries by the API tier into an h3 Common settings list and a controlled `Disclosure` titled Advanced settings with the `1 more setting` or `1 more setting, 1 changed` summary; a game with no Common entry gets a flat grouped list. The disclosure opens during render for a newly appearing trigger (staged change, required empty entry, invalid value after a review attempt) and stays closed once the member closes it.
- Expansion: the profile choice is a `SelectField` that stages `{ profileId, revisionId }`; the revision `RadioGroupField` offers Follow the latest revision and Stay on revision N (the saved pinned or resolved revision for the saved profile, `latestRevisionId` for a newly chosen one, unavailable with a reason when there is none). The immediate profile mutation, the follow and pin buttons, every raw `select`, `textarea`, `input` and `button` and the `error.message` paragraph are gone. Warnings were restored (incomplete, hand-edited file replacing its group, target caption once per target inside Advanced, control-plane label), loading, load error with Try again and the empty-game copy were added, and `PlacementEstimate` takes `stagedNote` instead of replacing itself while staged.
- 33 new tests in `configuration-panel.test.tsx` cover every behavior line, including a live source scanner proven on inline bad samples.

## Task Commits

1. **Task 1 (tracer): group settings into common and advanced** - `3e683a96` (feat(webapp))
2. **Task 2: stage the configuration profile, keep saved estimate** - `c3238585` (feat(webapp))

## Verification Results

- Tracer gate (Task 1, automated only): configuration-panel, arma-placement, change-flow and tests/styles green (82), `locales:check`, `typecheck` and ESLint on touched files clean, then the broader `tests/hosting tests/styles tests/locales` run (1387 tests) before expanding. Tracer verified end-to-end, then expanded.
- Plan-level: `vitest run tests/hosting tests/styles tests/locales` 87 files, 1404 tests passed; `locales:check` 3526 keys, 0 failures; `typecheck` exit 0; full `npm run lint` exit 0; Prettier clean on every touched file; placement e2e (`tests/e2e/hosting-placement.spec.ts`) passes; `git diff --exit-code -- sources/Common/Ui/routes.json` exit 0.
- Acceptance greps: `export function ConfigurationEntryField` 1, `Server_Settings_Advanced_Title` 1, `"Common"` 2, `<textarea` 0, game literals in the field 0, `stageProfile` 6, `selectConfigurationProfile` 0, `<select` 0, `<button` 0, `error.message` 0, `Server_Settings_EstimateSaved` 1.
- Repo-wide `prettier --check` still lists 26 files under `tests/hosting` and `src/components/server` that this plan did not touch (for example `fleet-page.test.tsx`); none of the files changed here is among them.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing change-flow tests encoded the old raw-control markup**
- **Found during:** Task 1
- **Issue:** The shared fields announce a field error as `role="alert"` and link the hint and the error through `aria-describedby`, and the reset control now carries the entry name. Four plan 65-10 tests in `change-flow.test.tsx` (not in this plan's file list) failed: they expected a single alert on the page, an `aria-describedby` equal to the error id, no alert after blur and the bare name `Reset to inherited`.
- **Fix:** The tests now read the bar alert inside the Pending changes region, accept the error id inside `aria-describedby`, and address the reset by `Reset to inherited: Hostname`. Every assertion about behavior (blocked review, focus on the field, error after blur, range wording) is unchanged. `workspace-page.test.tsx` and `arma-placement.test.tsx` got the same reset-name update.
- **Files modified:** `tests/hosting/change-flow.test.tsx`, `tests/hosting/workspace-page.test.tsx`, `tests/hosting/arma-placement.test.tsx`
- **Commit:** 3e683a96

**2. [Rule 2 - Missing critical] No copy for an unavailable pinned revision**
- **Found during:** Task 2
- **Issue:** The plan requires Stay on revision to be unavailable "with its reason", but no key from plan 65-02 words the reason, and an unexplained disabled radio would hide the cause.
- **Fix:** Added `Server_Settings_Revision_Unavailable` to `en.json` ("This profile has no revision to stay on yet.") and `de.json`; the radio group renders it through the shared `reason` slot, linked to the disabled radio.
- **Files modified:** `src/locales/en.json`, `src/locales/de.json`
- **Commit:** c3238585

### Plan wording that did not match the model

- The contribution receives `allowUnset={false}` instead of "allowUnset": the kept arma assertion says a server placement always offers exactly two choices, even for an optional entry. A stub-contribution test pins the received props.
- Required entries use `aria-required` rather than the shared field's visible " *" marker: the marker would change every label text (`Maximum players *`) that plan 65-07 and 65-10 tests address by label.
- The source line shares the shared field's hint with the description, which puts it under the label and above the control and keeps API prose inside `ApiText`, instead of a separate element before the control.
- Sensitive or redacted entries show the existing stored-value copy; no key exists for an extra "see Credentials" sentence, and such entries never arrive from the API anyway (RESEARCH Pitfall 2), so the pointer was not invented.
- The flat list (no Common entry) also shows the target caption; the spec limits captions to Advanced, but a flat list has no Advanced part and the file name would otherwise never be shown. The arma control-plane test now declares its entry as Advanced to exercise that path.
- The panel head badge, the estimate note and the review item use the profile as one staged change, so a staged profile also shows the saved-settings note.
- The Task 2 commit subject in the plan exceeds 80 characters; it was shortened to `feat(webapp): stage the configuration profile, keep saved estimate` to follow AGENTS.md.

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing critical) plus 7 wording notes. **Impact:** none on scope; every behavior line of both tasks is covered by a test.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None. T-65-33 (sensitive or redacted entries render disabled with the stored-value text, never call `onChange`, and `normalizeDraft` drops them), T-65-34 (all API strings render as React text inside `ApiText`, no `dangerouslySetInnerHTML`, choice values compared as strings) and T-65-35 (the client only proposes `latestRevisionId`; the review and apply path is unchanged) are mitigated and covered by the tests above.

## Next Phase Readiness

Plans 65-13 to 65-15 can rely on the panel being fully staged: profile, values and resets all flow through the shared draft, review and apply. `workspace-section-panels.test.tsx` was not touched (plan 65-13 owns it) and still passes with the new panel markup.

## Self-Check: PASSED

- Created and modified files verified on disk; commits `3e683a96` and `c3238585` verified as ancestors of HEAD.
