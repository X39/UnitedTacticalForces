---
phase: 69-consistency-accessibility-verification
plan: 19
subsystem: ui
tags: [react, admin, gallery, field-reports, confirm-dialog, state-views, tones, ratchet, leave-guard]

requires:
  - phase: 69-01
    provides: "PageStack, the shared dialog shell and the shared state panel styling"
  - phase: 69-03
    provides: "StateViews, reportTones with toneOf, the closed navigation labels and the dismiss and empty-state keys"
  - phase: 69-04
    provides: "family.admin-media-reports.json ratchet and its writer"
  - phase: 69-06
    provides: "audit baseline for the shared controls"
provides:
  - "Gallery, public image slots, gallery asset picker, field report list and field report editor on shared controls, shared state views and PageStack"
  - "GalleryAssetPicker as a ConfirmDialog shell variant (dialog--gallery-picker) with the outcome-named dismiss label"
  - "src/features/admin/mediaAdmin.css: the token-only layout for all five pages and the picker dialog"
  - "tests/admin-media-pages.test.tsx (35 tests) for header, tone, state, dialog and leave-guard behavior"
  - "An optional `accept` prop on FileChooserButton"
affects: [69-24, 69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: a57232aa51d2c05175270a48f039d70b394b0b75
commits: 3

actuals:
  tokens: 35400
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A picker with its own search and upload lives inside ConfirmDialog; its search field is not a form, and an Enter keydown wrapper keeps Enter from confirming the dialog"
    - "A new family stylesheet restates the layout of the legacy page classes with new class names, so the shared fields are not restyled by legacy descendant selectors"
    - "A multi-select filter uses the FilterChipGroup markup with aria-pressed ActionButtons when a single-select group does not fit"

key-files:
  created:
    - sources/ReactApp/src/features/admin/mediaAdmin.css
    - sources/ReactApp/tests/admin-media-pages.test.tsx
  modified:
    - sources/ReactApp/src/features/admin/MediaGalleryPage.tsx
    - sources/ReactApp/src/features/admin/SiteImagesPage.tsx
    - sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx
    - sources/ReactApp/src/features/admin/FieldReportsPage.tsx
    - sources/ReactApp/src/features/admin/FieldReportEditorPage.tsx
    - sources/ReactApp/src/components/FileChooserButton.tsx
    - sources/ReactApp/tests/admin-gallery-asset-picker.test.ts
    - sources/ReactApp/tests/ratchets/family.admin-media-reports.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/e2e/event-authoring.spec.ts

key-decisions:
  - "The gallery h1 is the navigation label (Shared_CommandCenter_Nav_Gallery) and the former duplicate kicker was dropped; the field report list does the same with Shared_CommandCenter_Nav_FieldReports."
  - "The picker uses the shared FileChooserButton, so no raw file input remains anywhere in the family. The plan allowed the picker's file input to stay on the ratchet; the family file is now empty instead."
  - "The field report editor has no Markdown helper inputs (its Markdown field is one textarea), so it converted fully and nothing editor-specific stays on the ratchet."
  - "The gallery and site images pages use the shared leave guard and a leave dialog (Keep editing / Leave without saving) instead of the native confirm prompt, like the editor."
  - "The picker apply action is the primary variant, the field report editor header Save is gold and its footer Save is primary, so each view has one gold forward action."

patterns-established:
  - "Picker dialog: ConfirmDialog with className dialog--<name>, doubled selector in the family stylesheet, confirmDisabled until a choice exists"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "The gallery h1 equals the Gallery navigation label in English and German, and every gallery state keeps one h1"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx#shows the navigation label as its one heading in English and German"
        status: pass
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx#renders one heading above the forbidden state and loads nothing"
        status: pass
    human_judgment: false
  - id: D2
    description: "The site images page keeps its dynamic title with the Public images kicker as the recorded exception"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx#keeps its dynamic title with the public images label as the recorded kicker exception"
        status: pass
    human_judgment: false
  - id: D3
    description: "The gallery asset picker is a ConfirmDialog shell variant with the outcome-named dismiss; Escape, Apply availability and Enter in search behave"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx (gallery asset picker, 6 tests)"
        status: pass
      - kind: unit
        ref: "tests/admin-gallery-asset-picker.test.ts (token stylesheet geometry rules)"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/event-authoring.spec.ts (picker bounds at 1440x900 and 1024x640, private port)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Field report badges take their tone from toneOf(reportTones) in the list and the editor"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx#takes the state badge tones from the report lexicon (list and editor)"
        status: pass
    human_judgment: false
  - id: D5
    description: "The five files contain no raw control and no button look-alike; the family ratchet is empty"
    requirement: CON-02
    verification:
      - kind: other
        ref: "tests/ratchets/ratchet.test.ts (family.admin-media-reports.json has no files) and npm run lint"
        status: pass
    human_judgment: false
  - id: D6
    description: "Editor leave guard, delete confirmation with focus on the dismiss action, load retry and the not-found state"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-media-pages.test.tsx (field report editor, 7 tests)"
        status: pass
    human_judgment: false
  - id: D7
    description: "The new layout looks right at real widths (card grids, picker split, editor form columns)"
    requirement: CON-03
    verification: []
    human_judgment: true
    rationale: "Only the picker bounds were measured in a real browser (event authoring e2e). The gallery, slot cards, report list and editor layouts were not looked at; 69-24 and the Wave 3 audits measure geometry."

duration: ~95min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 19: Media and Field Report Administration Summary

**Gallery, public image slots, the gallery asset picker, the field report list and the field report editor now use only shared controls, shared state views and the report tone lexicon, the picker is a ConfirmDialog shell variant that names its dismiss effect, every page keeps one h1 above all of its states, and the family ratchet is empty.**

## Performance

- **Duration:** about 95 min (start time was not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 2 created, 10 modified

## Accomplishments

- `MediaGalleryPage`: h1 is the Gallery label, `SearchField` plus a Search action, a pressed-toggle usage filter, `TextField` tag and upload fields, a shared `FileChooserButton` upload, a listbox of `ActionButton` tiles, `CopyButton` for the reference, `LoadingState`/`ErrorState`/`EmptyState`, and the shared leave guard for unsaved tag edits.
- `GalleryAssetPicker`: `ConfirmDialog` with `className="dialog--gallery-picker"`, kicker, title, description, `Don't choose an image` dismiss, `Apply` held unavailable until an image is selected, the upload error in the dialog error slot, and Enter in the search field running the search instead of confirming.
- `SiteImagesPage`: dynamic `{group} images` h1 with the `Public images` kicker, group navigation as `ActionLink`s with `aria-current`, shared buttons, state views and a leave dialog.
- `FieldReportsPage`: Field reports h1, tone from `toneOf(reportTones, state)`, summary tiles as pressed `ActionButton`s, `CommandListToolbar` with `FilterChipGroup` counts, an edit link named by its report, a gold New report action.
- `FieldReportEditorPage`: `TextField`, `TextAreaField` and `CheckboxField` with inline errors, shared image panel buttons, the leave guard with a "Keep editing" initial focus, a delete dialog with "Keep field report" as initial focus and the header Save shown only while the form exists.
- `family.admin-media-reports.json` went from 5 files (58 controls, 37 look-alikes) to none; `mediaAdmin.css` is new and holds zero hex, off-grid px and outline removals.

## Task Commits

1. **Task 1 (tracer): gallery and asset picker** - `8caf3001` (refactor)
2. **Task 2: site images page** - `e0ec5799` (refactor)
3. **Task 3: field report list and editor** - `a57232aa` (refactor)

Tracer gate: the tracer `<verify>` is automated only; it passed (`admin-media-pages`, picker test, ratchets, styles) before expansion.

## Verification

- `npm test -- tests/admin-field-reports.test.ts tests/admin-field-report-services.test.ts tests/admin-media-pages.test.tsx tests/admin-site-images.test.ts tests/admin-gallery-asset-picker.test.ts tests/ratchets tests/styles tests/locales`: 16 files, 317 passed, 1 skipped (the opt-in ratchet writer).
- `npx tsc -b`, `npm run lint` (max warnings 0) and `npm run locales:check` (3980 keys, 0 failures): clean.
- Playwright on private ports 4973 and 4974 (temporary config, deleted afterwards): `tests/e2e/event-authoring.spec.ts` passes, including the picker bounds assertions at 1440x900 and 1024x640.
- Acceptance greps: `Pages_Command_Gallery_PageTitle` in MediaGalleryPage 0, raw controls in all five files 0, `className="button` in the picker, site images and editor 0, `function stateTone` 0, `reportTones` in FieldReportsPage 2, `Public images` in the media test 2.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A missing field report never reached the not-found state**
- **Found during:** Task 3 (test for the not-found state)
- **Issue:** `loadCommandFieldReport` resolves `undefined` for a 404 and a TanStack query cannot hold `undefined`, so the `isSuccess && !data` branch was unreachable and the editor showed a load error. Same cause as the 69-16 event editor fix.
- **Fix:** The editor query function maps `undefined` to `null`.
- **Files modified:** `FieldReportEditorPage.tsx`
- **Commit:** `a57232aa`

**2. [Rule 3 - Blocking] The event authoring e2e depended on the old picker markup**
- **Found during:** Task 3 (search for readers of the old picker classes)
- **Issue:** `tests/e2e/event-authoring.spec.ts` located the picker by `dialog.gallery-picker-dialog`, its `footer` and a "Cancel" button, all of which the shell variant replaced.
- **Fix:** Selectors now use `dialog.dialog--gallery-picker`, `.gallery-picker__workspace`, `.dialog__actions` and the "Don't choose an image" dismiss. The spec passes in a real browser.
- **Files modified:** `tests/e2e/event-authoring.spec.ts` (outside `files_modified`)
- **Commit:** `a57232aa`

**3. [Rule 2 - Missing critical] Header Save with no form to save**
- **Found during:** Task 3
- **Issue:** In the loading, error and not-found states of the edit route the header still showed an enabled Save bound to a form that was not rendered.
- **Fix:** The header Save renders only while the form is shown.
- **Commit:** `a57232aa`

### Plan premises that did not hold

- **Delete confirmation on the site images page:** the page has no delete flow (the API and `siteImages.ts` have none), so the "Delete image / Keep image" behavior line has nothing to attach to. The page's only dialog is the leave dialog.
- **Editor Markdown helper inputs:** the editor has no Markdown helper inputs, so no editor-only ratchet entries remain (the family file is empty, not "editor-only").
- **Picker file input on the ratchet:** the picker uses `FileChooserButton`, so its file input also left the ratchet.

### Scope notes

- Files outside `files_modified`: `src/components/FileChooserButton.tsx` (optional `accept` prop so the image-type filter of the chooser is kept), `tests/styles/legacy-ratchet.json` (new stylesheet entry, see below) and `tests/e2e/event-authoring.spec.ts` (deviation 2).
- Gallery and site images use the shared leave guard instead of `useUnsavedChanges`; the plan required it only for the editor, and the native confirm prompt is what the hook documents as avoided.
- The old `Pages_Command_Gallery_CopyReferenceFailed` toast is gone: `CopyButton` shows its own failure text.

**Total deviations:** 3 auto-fixed (1 Rule 1, 1 Rule 2, 1 Rule 3) plus the premise and scope notes. **Impact:** additive; no interaction was removed except the toast text noted above.

## Orchestrator merge notes

- **New stylesheet needing a zero-ceiling entry:** `sources/ReactApp/src/features/admin/mediaAdmin.css` was added to `tests/styles/legacy-ratchet.json` with `hex: 0`, `offGridPx: 0`, `outlineRemoval: 0` (reason "Feature page stylesheet; converges with its page family in Phase 69 and may only shrink."). Siblings edit that file too; keep both entries on a merge conflict.
- **`family.admin-media-reports.json` is now `"files": {}`.**
- **files_deleted:** none.

## Cross-plan findings

| Id | File and line | Problem | Proposed fix (owner) |
|----|---------------|---------|----------------------|
| XPF-69-19-1 | `src/components/NotFoundPage.tsx` (whole component) | It renders a state panel with an h2 and no h1, so the invalid report id route (`/command/field-reports/not-a-guid`) has no h1 | Give `NotFoundPage` a `PageHeader`, or wrap it in a frame at the call site (69-24 or 69-33) |
| XPF-69-19-2 | `src/styles.css`: `.media-gallery-*`, `.site-image-*`, `.gallery-picker-dialog*`, `.field-report-admin-*`, `.field-report-editor-*`, `.field-report-image-panel*`, `.field-report-editor-preview*` | The pages no longer use these classes, so the rules are dead and `tests/admin-gallery-asset-picker.test.ts` no longer reads them | Delete the blocks (69-33 owns `styles.css`) |
| XPF-69-19-3 | `src/locales/en.json` and `de.json` | Keys without a reader now: `Pages_Command_Gallery_PageTitle`, `Pages_Command_Gallery_NoSelection`, `Pages_Command_Gallery_Empty`, `Pages_Command_Gallery_CopyReferenceFailed`, `Components_GalleryImagePicker_DialogCancel`, `Components_GalleryImagePicker_NoResults`, `Pages_Command_FieldReports_NoReports`, `Pages_Command_FieldReports_Edit_ButtonCancel` | Retire them with the unused-key check (69-33) |
| XPF-69-19-4 | `src/components/CommandListToolbar.tsx:36-43` | The shared toolbar still renders a raw `<input>` inside a `label.search-field` (not a `SearchField`), so every list page that uses it inherits a raw control outside the ratchet scan of its own file | Build the toolbar on `SearchField` with a submit handler (the owner of the shared-workspace family) |

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored, nothing installed). `npm run build:assets` regenerated `src/generated/api.ts` with a line-ending diff; it was restored and never committed.
- `tests/styles/button-wrap.test.ts` rejects a local `white-space: normal` override on a `.button` rule; the shared control already wraps, so the override was removed from the new stylesheet.
- One full-set run showed two failures that did not reproduce on the next two runs of the same set (the machine was shared with other executors).
- Keyboard focus return to the opener after the picker closes relies on the native `<dialog>`; jsdom cannot assert it, so the tests assert the `onCancel` call and the dismiss focus of the leave and delete dialogs.

## Known Stubs

None.

## Threat Flags

None. T-69-25 holds: upload validation (10 MB, accepted image types) stays in the page code and on the server, unchanged; the file chooser input is now behind the shared `FileChooserButton`.

## Next Phase Readiness

Media and field report administration is converged. 69-33 can delete the dead legacy rules and keys listed above. Real-browser geometry of the gallery, slot cards, report list and editor is left to the Wave 3 audits.

## Self-Check: PASSED

- Created files exist: `mediaAdmin.css`, `admin-media-pages.test.tsx`.
- Commits `8caf3001`, `e0ec5799`, `a57232aa` are ancestors of HEAD (`git rev-list --count 33900a1e..HEAD` is 3).
- Plan verification re-run: vitest set (317 passed), `npx tsc -b`, `npm run lint`, `npm run locales:check`, event authoring e2e on a private port.
