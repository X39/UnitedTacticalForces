---
phase: 69-consistency-accessibility-verification
plan: 21
subsystem: ui
tags: [react, verein, kassenbuch, settings, tone-lexicon, leave-guard, ratchet]

requires:
  - phase: 69-03
    provides: "statusTones lexicon, StateViews and the dismiss and empty-state keys"
  - phase: 69-04
    provides: "family ratchet admin-verein-settings and the UTF_RATCHET_UPDATE writer"
provides:
  - "SettingsPage badge tones from toneOf(settingTones) through settingsFeatureState; the page-local tone function is gone"
  - "Seven Verein and settings pages on PageHeader, PageStack, shared states, shared fields and ActionButton/ActionLink with zero raw controls"
  - "Verein document and Kassenbuch editors on useLeaveGuard with Keep editing and Leave without saving"
  - "family.admin-verein-settings.json at zero"
affects: [69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: e5ac286a4063ce4cdecfac952c19931f74c12ad5
commits: 3

actuals:
  tokens: 60000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Editor state frame: one PageHeader above forbidden, loading, error and not-found views"
    - "Leave guard with a ref-gated shouldBlock so the post-save navigation passes before the form reset re-renders"
    - "Feature stylesheet replaces legacy styles.css control rules by renaming the grid container class"

key-files:
  created:
    - sources/ReactApp/src/features/admin/vereinSettings.css
    - sources/ReactApp/src/features/admin/vereinDocuments.css
  modified:
    - sources/ReactApp/src/features/admin/SettingsPage.tsx
    - sources/ReactApp/src/features/admin/VereinSettingsPage.tsx
    - sources/ReactApp/src/features/admin/VereinDocumentsPage.tsx
    - sources/ReactApp/src/features/admin/VereinDocumentDetailPage.tsx
    - sources/ReactApp/src/features/admin/VereinDocumentEditorPage.tsx
    - sources/ReactApp/src/features/admin/VereinKassenbuchPage.tsx
    - sources/ReactApp/src/features/admin/VereinKassenbuchEditorPage.tsx
    - sources/ReactApp/src/features/admin/vereinKassenbuch.css
    - sources/ReactApp/tests/admin-settings.test.ts
    - sources/ReactApp/tests/admin-verein-settings.test.tsx
    - sources/ReactApp/tests/admin-verein-documents.test.tsx
    - sources/ReactApp/tests/admin-kassenbuch.test.tsx
    - sources/ReactApp/tests/e2e/verein-kassenbuch.spec.ts
    - sources/ReactApp/tests/ratchets/family.admin-verein-settings.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json

key-decisions:
  - "The feature helper is settingsFeatureState (returns the settingTones key) plus settingsFeatureStatusKey; the label ternary that was duplicated twice now has one source."
  - "Document published and draft badges use reportTones (published success, draft neutral); draft-only was warning before, the lexicon lists Draft as neutral."
  - "Kassenbuch amount badges use financeTones income and expense, which reproduce the old success and danger colors."
  - "Summary tiles of the documents list stay filter buttons (ActionButton around Metric, as EventAdminPage does) so no behavior is lost."

patterns-established:
  - "Raw-control grid containers get a new class in the feature stylesheet; the legacy class is left in styles.css for 69-33 to retire"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Settings feature badges take their tone from the settingTones lexicon; the exported page-local tone function is gone"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-settings.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "All seven pages render one PageHeader h1 above forbidden and not-found states, with no raw control or look-alike"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-verein-settings.test.tsx, tests/admin-verein-documents.test.tsx, tests/admin-kassenbuch.test.tsx (one-h1 tests); tests/ratchets (family at zero)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Document and ledger editors leave through the shared guard with Keep editing and Leave without saving; delete dismiss reads Keep ledger entry with initial focus on it"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-verein-documents.test.tsx and tests/admin-kassenbuch.test.tsx (leave dialog tests, delete focus test)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Empty document and ledger lists show EmptyState with a title and a next step"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-verein-documents.test.tsx, tests/admin-kassenbuch.test.tsx"
        status: pass
    human_judgment: false
  - id: D5
    description: "Ledger payloads, amounts and Intl formatting are unchanged"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-kassenbuch.test.tsx, tests/admin-kassenbuch-services.test.ts"
        status: pass
      - kind: e2e
        ref: "playwright (private ports, chromium): tests/e2e/verein-kassenbuch.spec.ts 2 passed; public-shell.spec.ts 27 passed, 1 failed outside this plan (XPF-69-21-2)"
        status: pass
    human_judgment: false

duration: ~50min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 21: Verein administration and operational settings Summary

**Settings badges now take their tone from the lexicon, and all seven Verein and settings pages share one header, shared states, shared fields and the shared leave guard, with the family ratchet at zero.**

## Performance

- **Duration:** about 50 min (start time was not captured; estimated)
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files created:** 2 stylesheets; modified: 15 (7 pages, 1 stylesheet, 5 tests, e2e spec, 2 ratchet files)

## Accomplishments

- `SettingsPage.tsx`: `settingsFeatureTone` replaced by `settingsFeatureState` (`off`, `configured`, `missing-configuration`) and `settingsFeatureStatusKey`; both badges use `toneOf(settingTones, state)`. Forbidden state keeps the h1 above it; loading, error (real refetch) and the two empty states use `StateViews`.
- `VereinSettingsPage.tsx`: TextField and TextAreaField with react-hook-form `register`, ActionLink and ActionButton, gold save action, shared states; the long h1 "Verein contribution settings" is kept as the recorded exception.
- Documents list, detail and editor: SearchField plus FilterChipGroup, ActionLink actions, EmptyState with title and next step, detail and editor state frames, SelectField, CheckboxField, TextField and TextAreaField. The editor's Cancel link became "Leave without saving" and leaving while dirty opens the shared dialog with "Keep editing".
- Kassenbuch list and editor: SearchField, TextField and SelectField filters and fields, `financeTones` amount badges, EmptyState, the delete dialog dismiss is "Keep ledger entry" with initial focus on it, and the editor leaves through `useLeaveGuard`. Payloads, amounts and Intl currency formatting are untouched.
- `family.admin-verein-settings.json` is empty (7 files, 42 controls, 34 look-alikes removed).

## Task Commits

1. **Task 1 (tracer): settings badge tones from the lexicon** - `c1fda385` (refactor)
2. **Task 2: Verein document pages** - `8a026c59` (refactor)
3. **Task 3: Kassenbuch pages and the family at zero** - `e5ac286a` (refactor)

Commits were created with hooks on and signing left to the repository default; no `--no-verify`.

## Files Created or Modified

See the frontmatter `key-files`. Two new stylesheets, `vereinSettings.css` and `vereinDocuments.css`, hold the token-only form grids that replace the legacy `styles.css` control rules. `vereinKassenbuch.css` was rewritten token-only (label, input and message rules removed, shared field selectors added).

**Shared file note for the orchestrator:** `tests/styles/legacy-ratchet.json` gained two zero-ceiling entries (`features/admin/vereinSettings.css` and `features/admin/vereinDocuments.css`). Siblings edit that file too; resolve the merge by keeping all entries. `files_deleted:` none.

## Decisions Made

- Document `draft only` badges are now neutral (lexicon: Draft is neutral) where they were warning; published stays success.
- The Reset and Leave controls stay ghost, the forward action of each view (create, save, add entry, edit) is gold.
- `VereinSettingsPage` keeps `useUnsavedChanges` (the plan scopes the leave guard to the two editors).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] Stale legacy CSS would have restyled the shared fields**
- **Found during:** Task 1 and Task 2
- **Issue:** `styles.css` styles `label`, `input`, `textarea`, `select` and `small` inside `.verein-settings-admin-form__fields` and `.verein-document-editor-form`; with TextField those rules would uppercase the labels and override the control chrome. `styles.css` is not editable by this plan.
- **Fix:** Renamed the two grid containers (`verein-settings-fields`, `verein-document-fields`) and moved their layout to two new token-only stylesheets, registered with zero ceilings.
- **Files modified:** `vereinSettings.css`, `vereinDocuments.css`, `legacy-ratchet.json`
- **Commits:** `c1fda385`, `8a026c59`

**2. [Rule 1 - Bug] Leave guard would have blocked the post-save navigation**
- **Found during:** Task 2
- **Issue:** `form.reset` re-renders asynchronously, so `navigate` right after a save still saw a dirty form and the guard would have shown the leave dialog after saving a document.
- **Fix:** A `saved` ref set before `navigate` is read by `shouldBlock`, so exactly that navigation passes. The Kassenbuch editor already had an `allowNavigation` state that runs the navigation from an effect after the reset, so it needed no ref.
- **Commit:** `8a026c59`

**Total deviations:** 2 auto-fixed (1 missing critical, 1 bug). **Impact:** no scope change; two extra stylesheets.

## Cross-plan findings

| Id | File | Line | Problem | Proposed fix |
|----|------|------|---------|--------------|
| XPF-69-21-1 | `sources/ReactApp/src/styles.css` | 1363-1460, 877-1017, 4282 | Dead rules remain for `.verein-settings-admin-form__fields` (label, input, textarea, small, is-wide), `.verein-document-editor-form` (same set), `.verein-settings-admin-page`, `.command-settings-page` and the `.verein-document-detail` padding rules; no element carries these classes any more except the unchanged workspace and shell grids | 69-33 deletes the dead selectors and keeps `.verein-settings-admin-workspace`, `.verein-settings-admin-form footer`, `.verein-settings-admin-preview`, `.verein-document-editor-shell`, `.verein-document-detail-shell`, `.verein-document-facts`, `.verein-revision-*` |
| XPF-69-21-2 | `sources/ReactApp/tests/e2e/public-shell.spec.ts` | 479 | `filters the event administration ledger on canonical and legacy routes` looks for the h1 "Event Administration" but `EventAdminPage.tsx` (converted by 69-16) now renders the title `Shared_CommandCenter_Nav_Events`; the spec was not updated. Neither the page nor the spec is touched by this plan, so the failure comes from the branch base, not from this plan's changes | Owner of `EventAdminPage` or 69-33 updates the locator to the current h1 (or restores the long page title) |

## Authentication Gates

None.

## Issues Encountered

- The worktree has no `node_modules`; a directory junction to the main checkout's install was used (git-ignored). Nothing was installed.
- The plan's e2e verify uses the fixed ports 4173 and 4174; to avoid hitting a sibling's server a temporary uncommitted config on ports 4373 and 4374 was used and deleted afterwards. `src/generated/api.ts` was rewritten by `build:assets` with line-ending changes and restored with `git checkout --`.
- Under load the ratchet test timed out once at the 5 s default (it takes about 3 s alone); it passes alone.
- The new `vereinKassenbuch.css` has no more hard-coded colors; its ceiling in `legacy-ratchet.json` was left at its previous value (offGridPx 2) because counts may only fall.

## Verification

- `vitest` over `admin-settings`, `admin-verein-settings`, `admin-verein-documents`, `admin-kassenbuch`, `admin-kassenbuch-services`, `tests/ratchets`, `tests/locales`, `tests/styles`: 16 files, 299 passed, 1 skipped (the opt-in ratchet writer).
- `npm run lint` and `npx tsc -b` (whole project): exit 0, no output.
- Playwright (chromium, private ports): `verein-kassenbuch.spec.ts` 2 passed; `public-shell.spec.ts` 27 passed, 1 failed (XPF-69-21-2, outside this plan).
- Acceptance criteria: `settingsFeatureTone` export count 0; raw controls in `VereinSettingsPage` 0; `Common_Cancel` in the two editors 0; `className="button` in `VereinDocumentsPage` 0; family file has 0 files.

## Known Stubs

None.

## Threat Flags

None. No network, auth or trust-boundary surface was added; T-69-27 is mitigated by the unchanged payload assertions in `admin-kassenbuch.test.tsx` and `admin-kassenbuch-services.test.ts`.

## Next Phase Readiness

69-33 can retire the dead `styles.css` selectors named in XPF-69-21-1 and resolve XPF-69-21-2.

## Self-Check: PASSED

- Created files exist: `vereinSettings.css`, `vereinDocuments.css`.
- Task commits `c1fda385`, `8a026c59`, `e5ac286a` are ancestors of HEAD.
