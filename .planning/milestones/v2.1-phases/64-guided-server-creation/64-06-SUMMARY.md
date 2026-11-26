---
phase: 64-guided-server-creation
plan: 06
subsystem: ui
tags: [react, react-hook-form, hosting, create-server, presets, size, storage-fit]
status: complete
requires:
  - phase: 64-02
    provides: projectCreatePresets, presetSelection, customPresetValue and the create fixtures
  - phase: 64-03
    provides: SelectField with option groups
  - phase: 64-01
    provides: Server_Create_* copy in English and German
provides:
  - PresetField: grouped preset select with applied, customized, loading and failure states
  - SizeField: size select with price or price not available, selected-size facts and the IncludedDisk fit line
  - create-fields.css token-only field styles
affects: [64-07, 64-08]
commits: 2
plan_head_before: a1f05352
plan_head_after: 558a0bbf
requirements-completed: [CRT-01, CRT-05]
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
key-files:
  created:
    - sources/ReactApp/src/components/server/PresetField.tsx
    - sources/ReactApp/src/components/server/SizeField.tsx
    - sources/ReactApp/src/components/server/create-fields.css
    - sources/ReactApp/tests/hosting/create-fields.test.tsx
  modified: []
key-decisions:
  - "A size whose hourly price is null, not finite or not positive is shown as price not available, never as 0"
  - "The fit line is looked up by case-insensitive runtimeProfileIdentifier only; unknown fit and no match both show the unknown line"
  - "The custom option is added to the preset select only while the value is custom, so a controlled select never holds a value without an option"
coverage:
  - id: D1
    description: "PresetField groups defaults, profiles and mod packs, resolves choices only through presetSelection, handles Customized, applied line, loading, failure and omission"
    requirement: "CRT-05"
    verification:
      - kind: unit
        ref: "tests/hosting/create-fields.test.tsx#PresetField"
        status: pass
    human_judgment: false
  - id: D2
    description: "SizeField shows price or price not available, selected-size facts, and the fit line only for IncludedDisk"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/create-fields.test.tsx#SizeField"
        status: pass
    human_judgment: false
---

# Phase 64 Plan 06: Preset and Size Fields Summary

The preset shortcut and the size decision of the create form as tested components: a preset choice yields exactly the two form values it stands for and states that it keeps the server off, and sizes show honest prices with a fit line only where it applies.

## Accomplishments

- **Task 1 (tracer)**: `PresetField` with one native select (Game defaults, Configuration profiles group, Mod packs group), a disabled Customized option and line when the fields match no preset, the applied line controlled by `showAppliedLine`, loading panel with a disabled select, and a warning panel with Try again on failure. Renders nothing with only the default preset. Commit `69403849`.
- **Task 2**: `SizeField` registered on `runtimeProfileIdentifier` (member change reported through the register `onChange`), option labels with vCPU, memory and `formatHourlyRate` price or price not available, a facts list with the carried terms, and the fit line (note, warning panel or unknown note) shown only for `IncludedDisk` with a loaded demand. Commit `558a0bbf`.

## Verification

- `tests/hosting/create-fields.test.tsx tests/styles tests/locales`: 7 files, 93 tests passed (16 new tests in create-fields).
- `typecheck` and `lint --max-warnings 0` exit 0.
- `prettier --check --end-of-line auto` passes for all four touched files (repo-wide `format:check` fails in this CRLF checkout for pre-existing files, as noted in earlier plans).
- `sources/Common/Ui/routes.json` has no diff. `CreateServerPage.tsx` was not touched. All acceptance greps match their expected counts.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] node_modules missing in the fresh worktree**
- Ran `npm ci` from the lockfile (no package changes). The branch also started on a non-descendant HEAD and was reset to the expected base `a1f05352` per the worktree branch check.

**2. [Rule 1 - Lint] Test harness used `form.watch`**
- The React Compiler lint rule rejected `watch()`; the harness uses `useWatch` instead.

## Known Stubs

None.

## Threat Flags

None. T-64-12 mitigated: the preset value resolves only through `presetSelection` (an `Array.find` over the passed presets) and titles render as plain option strings.

## Self-Check: PASSED

- FOUND: PresetField.tsx, SizeField.tsx, create-fields.css, create-fields.test.tsx
- FOUND commits: 69403849, 558a0bbf
