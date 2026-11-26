---
phase: 70-ui-polishing
plan: 11
subsystem: webapp
tags: [react, configuration, settings-panel, search, filter, i18n]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-05 segmented RadioGroupField, 70-06 SettingsPanel and panel state, 70-10 state slot and the D-11 bounding-box test"
provides:
  - "Configuration composed as the collapsible Settings panel server-configuration (D-09)"
  - "Pure filterEntries, countEntries, isFilterActive and isRequiredEmpty (D-10)"
  - "ConfigurationFilter toolbar: search, All / Changed / Needs a value chips, visible and polite result count"
  - "Segmented revision mode (D-03)"
affects: [70-16]

key-files:
  created:
    - sources/ReactApp/src/components/server/configurationFilterRules.ts
    - sources/ReactApp/src/components/server/ConfigurationFilter.tsx
    - sources/ReactApp/tests/hosting/configuration-filter.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx
    - sources/ReactApp/src/components/server/configuration.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/configuration-panel.test.tsx
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts

key-decisions:
  - "The pure module is named configurationFilterRules.ts, not configurationFilter.ts: next to ConfigurationFilter.tsx the two names differ only in case, and on a case-insensitive file system the import of the component resolved to the .ts file (TS1149)"
  - "Searching never reads any entry value, sensitive or not; it matches display name, group and description only, so a redacted value cannot be found or inferred (T-70-17)"
  - "Covered by a staged profile change is implemented as: a profile change is staged and the entry's value is inherited (source other than Server)"
  - "An open Configuration panel renders its full list without pagination or virtualization (largest catalog 82 entries, about six Common ones open by default); search and the chips are the findability tool"
  - "Visible count is aria-hidden and a separate sr-only polite region settles 300ms after the last change, starting equal to the visible text so mounting announces nothing"

requirements-completed: [D-03, D-09, D-10]

duration: 95min
completed: 2026-11-26
commits: 4
plan_head_before: e6eece4e142655c27046c8515a4d17ed9555f3c4
plan_head_after: 12001117ccb9aa45dffcb684213338bb52a06aae
actuals:
  tokens: 30000
  tasks: 3
  commits: 4
---

# Phase 70 Plan 11: Configuration as a searchable Settings panel Summary

Configuration is now a collapsible Settings panel (summary "Settings: N", badge, staged keys, retry inside the panel) with a segmented revision choice, and from eight entries on it gets a search field and the chips All, Changed and Needs a value backed by a pure, unit-tested filter that never looks at any value.

## Accomplishments

- Task 1 (tracer, `07eddba0`): `ServerConfigurationPanel` composes `SettingsPanel id="server-configuration"`. The summary appears only once the configuration loaded (absent while loading or failed); the loading and error states (with the real retry) live in the collapsed body. `stagedKeys` is `value:<key>`, `reset:<key>` and `profile`, so staging a value, a reset or a profile change opens the panel and the badge shows the count. The revision `RadioGroupField` is `layout="segmented"`. The inert `.configuration-panel .workspace-panel__head` rule (70-10 stopgap) is deleted from `configuration.css`. Tracer gate: Vitest (configuration-panel, settings-section) and Playwright hosting-placement and hosting-change on chromium passed before expansion.
- Task 2 (`f29ff647`): `configurationFilterRules.ts` holds `ConfigurationFilterMode`, `isRequiredEmpty` (moved unchanged), `isFilterActive`, `filterEntries` and `countEntries`. Normalization is trim, `toLocaleLowerCase`, NFD and stripping of combining marks. 21 pure unit cases cover case, blanks, diacritics ("gelande" finds "Gelände"), description only when supplied, never matching a sensitive or redacted value, the Changed rules including the staged-profile coverage, Needs a value judged on the staged value, and counts equal to the list lengths.
- Task 3 (`1ddd7656`): `ConfigurationFilter` (SearchField, FilterChipGroup labelled "Filter settings", visible muted count plus the 300ms polite region). The panel holds `query` and `mode`, renders the toolbar only for `entries.length >= 8` (after the profile choice, before the list), shows one flat grouped list through `renderEntries(filtered, true)` while filtering, the `EmptyState` ("No settings match" with a Clear search action) for zero results, and restores Common first and the Advanced disclosure when neither query nor chip is active. A review attempt that finds an invalid staged entry hidden by the filter clears query and chip (state adjusted during render, keyed on `reviewAttempt`) so the section can focus the first invalid field. Eleven toolbar locale keys in en and de plus `Server_Settings_Summary_Settings`.
- Extra (`12001117`): `hosting-change.spec.ts` D-11 test now waits for the "Calculating storage demand" line to disappear before measuring.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Pure module renamed to `configurationFilterRules.ts`**
- **Found during:** Task 3 (typecheck after adding `ConfigurationFilter.tsx`)
- **Issue:** `configurationFilter.ts` and `ConfigurationFilter.tsx` in one directory differ only by case. On a case-insensitive file system the import `@/components/server/ConfigurationFilter` resolved to the `.ts` file; `tsc -b` failed with TS1149.
- **Fix:** `git mv` to `configurationFilterRules.ts` and updated three imports. The plan's acceptance grep on `configurationFilter.ts` is satisfied against the renamed file (`export function filterEntries` count 1).
- **Files modified:** `configurationFilterRules.ts`, `ServerConfigurationPanel.tsx`, `ConfigurationFilter.tsx`, `configuration-filter.test.ts`
- **Commit:** `1ddd7656`

**2. [Rule 3 - Blocking] D-11 layout shift from the SettingsPanel header badge**
- **Found during:** Task 1 e2e gate
- **Issue:** The change badge made the new disclosure header grow by 2.19px, moving every field (the D-11 bounding-box test failed in all four variants).
- **Fix:** `[data-settings-panel="server-configuration"] .disclosure__title { min-height: var(--utf-control-height-compact) }` in `configuration.css` reserves the badge height; the rule replaces the deleted stopgap in spirit.
- **Files modified:** `configuration.css`
- **Commit:** `07eddba0`

**3. [Rule 1 - Test flake] D-11 test measured before the placement estimate settled**
- **Found during:** Task 1 and Task 3 e2e runs (the first, cold `en 390x844` attempt received 31.56px, the retry passed)
- **Issue:** The placement estimate loads independently; measuring while it still showed its loading line recorded that late change as a staging shift.
- **Fix:** The test waits for the loading line (en or de) to be absent before the first measurement. After the fix, the three specs (42 tests, chromium and mobile-390) pass with `--retries=0`.
- **Files modified:** `tests/e2e/hosting-change.spec.ts`
- **Commit:** `12001117`

**4. [Plan addition recorded as requested] `Server_Settings_Filter_GroupLabel`**
- The UI-SPEC does not list this key; `FilterChipGroup` needs an accessible group name, so it was added ("Filter settings" / "Einstellungen filtern").

## Verification

- `vitest run tests/hosting`: 116 files, 2262 tests passed (`--maxWorkers=2 --testTimeout=30000`); `tests/styles` 5 files pass; `locales:check` 3920 keys 0 failures; `locales:unused` has no warning for any `Server_Settings_*` key; eslint (max-warnings 0), `tsc -b` and prettier (`--end-of-line auto`) clean on touched files.
- Playwright (serialized helper): hosting-placement, hosting-change, hosting-a11y on chromium and mobile-390, 42 passed with `--retries=0`. Broader run (hosting-workspace, hosting-files-secrets, hosting-automation, hosting-create, journeys): 99 passed, 12 skipped, 2 failed, both the same pre-existing test (see below).
- The D-11 bounding-box test passes in 24 of 24 repeated runs and after the estimate fix.
- Acceptance greps: `workspace-panel__head` in the panel 0, `server-configuration` 2, `layout="segmented"` 1, dead CSS rule 0, `filterEntries` export 1, `function isRequiredEmpty` in the panel 0, `ConfigurationFilter` export 1, `Server_Settings_Filter_Label` in en 1, `_Result_other` in de 1, `filterEntries` in the panel 2.

## Deferred Issues (pre-existing, not caused by this plan)

- `tests/e2e/journeys/change-mods.spec.ts:119` ("guards the staged leave by keyboard", chromium and mobile-390) expects the leave dialog buttons in the order Stay here, Discard and leave, Review changes, but the dialog now renders Discard and leave first (`confirmPlacement="start"` from the leave-dialog commits `9046cc83` and `668ce38c`). No file of this plan touches it.

## Cross-plan findings

- XPF-70-11-1: `ServerSecretsPanel` (and any other `SettingsPanel` that shows a badge) has the same header-growth risk the Configuration panel had: the shared `.disclosure__badge` can be taller than the title row. This plan reserved the height only for `server-configuration`; a shared reservation on `.disclosure--panel .disclosure__title` in `form-fields.css` would cover every panel.

## Known Stubs

None.

## Threat Flags

None. T-70-17 is mitigated and tested (`filterEntries` never reads a value; "hunter2" is not found for a redacted entry, and no ordinary value is searched either). T-70-18: results render through the existing `renderEntries` and `ApiText` text nodes; the filter returns entries, not HTML.

## Self-Check: PASSED

- Files exist: `configurationFilterRules.ts`, `ConfigurationFilter.tsx`, `configuration-filter.test.ts`, `ServerConfigurationPanel.tsx`.
- Commits `07eddba0`, `f29ff647`, `1ddd7656`, `12001117` are ancestors of HEAD.
