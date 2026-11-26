---
phase: 69-consistency-accessibility-verification
plan: 10
subsystem: ui
tags: [react, hosting, files, accessibility, confirm-dialog, action-controls, ratchet]

requires:
  - phase: 69-03
    provides: "Named row-action keys, outcome-specific dismiss keys and Files empty-state keys, plus EmptyState and LoadingState"
  - phase: 69-04
    provides: "Raw-control ESLint rule, family ratchet files and the writer"
  - phase: 69-06
    provides: "Discovery findings for the Files tab (aria-required-children and listitem on the folder tree)"
provides:
  - "Inline compact row actions (Open, Download, Delete) with file-named accessible names and no popup menu"
  - "New folder, upload and delete dialogs on ConfirmDialog with outcome-specific dismiss labels"
  - "FileChooserButton: a shared button that opens the browser file chooser through a hidden input"
  - "ServerFilesPanel with zero raw controls, zero look-alikes and no outline removal in its styles"
affects: [69-24, 69-33, 69-34]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 5e8d3cbb1c4f07d37dc2cad33c13352fbe8bd981

actuals:
  tokens: 9000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Row actions are inline compact ActionButton and ActionAnchor controls named after the row; no menu role, no disclosure in a table row"
    - "A busy ConfirmDialog disables dismissal and Escape; an in-flight cancel control lives in the dialog body"
    - "Tree states render outside the tree role and nested states use li role none"

key-files:
  created:
    - sources/ReactApp/src/components/FileChooserButton.tsx
    - sources/ReactApp/tests/hosting/files-panel-controls.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/ratchets/family.hosting-files.json

key-decisions:
  - "The hidden file inputs moved into a new shared FileChooserButton, recorded on the hosting-files ratchet as a browser file chooser wrapper (the UI-SPEC allowance), so ServerFilesPanel itself reaches zero"
  - "While an upload runs the dialog is busy (dismiss and Escape disabled) and the abort control sits in the dialog body next to the progress"
  - "The files search clear label reuses Server_Fleet_SearchClear because the locale files are off limits for this plan"

patterns-established:
  - "Files dialogs: kicker, title, destination description, fields as children, error slot, outcome-named dismiss"

requirements-completed: [CON-02, CON-03, FND-06, L10N-03]

coverage:
  - id: D1
    description: "Files rows show inline Open, Download and Delete with file-named accessible names; no menu, menuitem or disclosure"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx#shows the actions of a row inline with the file name in their accessible names"
        status: pass
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx#has no popup menu, menu item or disclosure in the panel"
        status: pass
    human_judgment: false
  - id: D2
    description: "Delete asks first, focuses the dismiss control, never deletes before confirmation and returns focus to the invoker on Escape; members without file rights get no Delete"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx#asks before deleting, focuses the dismiss control and returns focus to Delete on Escape"
        status: pass
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx#offers no Delete action to a member without file rights and keeps Download"
        status: pass
    human_judgment: false
  - id: D3
    description: "New folder and upload dialogs on the shared shell with named dismiss labels, field-wired errors, keyboard-reachable collision approval, focus return and disabled dismissal while pending"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx (describe ServerFilesPanel dialogs on the shared shell, 5 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Search is the shared SearchField, preservation fields are labelled shared fields, empty states carry title and next step, tree states sit outside the tree role"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx (describe ServerFilesPanel shared controls, 6 tests)"
        status: pass
      - kind: e2e
        ref: "npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-files-secrets.spec.ts --project=chromium (34 passed, axe scan of .next-hosting-files-panel included)"
        status: pass
    human_judgment: false
  - id: D5
    description: "The focused search input shows the focus ring token"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/files-panel-controls.test.tsx#keeps a visible focus ring on the search and removes no outline in the files styles (rule-text assertion; jsdom loads no stylesheet)"
        status: pass
    human_judgment: true
    rationale: "Computed focus-ring style is not measurable in jsdom; the registry keyboard walk for the Files tab arrives with the Wave 3 workspace slice, so a human or that audit confirms the ring visually"
  - id: D6
    description: "Hosting-files ratchet lowered: the ServerFilesPanel entry is gone"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts (family.hosting-files.json holds only the FileChooserButton entry)"
        status: pass
    human_judgment: false

duration: ~45min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 10: Files panel convergence Summary

**The server Files panel now uses inline named row actions instead of a popup menu, runs all three dialogs on the shared ConfirmDialog shell with outcome-named dismiss labels, and contains no raw control, button look-alike or outline removal; the browser file chooser sits behind a new shared FileChooserButton.**

## Performance

- **Duration:** about 45 min (start time was not captured; estimated from the first and last commit)
- **Tasks:** 3 (1 tracer, 2 auto, all TDD)
- **Files:** 2 created, 4 modified

## Accomplishments

- Rows: directories offer `Open {name}` and `Delete {name}`, files offer `Download {name}` (existing URL and `download` attribute) and `Delete {name}`. Visible text ("Open", "Download", "Delete") is contained in each accessible name. The cell wraps (`.next-hosting-file-actions`, 11rem minimum column).
- Delete: `ConfirmDialog` with dismiss "Keep file", initial focus on dismiss, busy while the request runs, focus back on the invoking Delete button on dismiss and on the folder heading after a successful delete. No request is made before confirmation.
- New folder: `ConfirmDialog` with a `TextField` whose error is the field error, request errors in the dialog alert slot, create blocked until the name is valid, dismiss "Don't create folder".
- Upload: `ConfirmDialog` carrying the file choosers, queue, progress and `CheckboxField` approvals; the confirm is blocked (aria-disabled) until approvals are given; "Retry failed (n)" is the extra action; dismiss "Don't upload files"; busy while uploading.
- Search: `SearchField` (clear control, Escape clears). The `.next-hosting-file-search` rules including the outline removal are gone.
- Preservation form: `TextField` and two `SelectField`s; Remove is a compact danger `ActionButton` with a visible reason for game-seeded entries instead of a title on a disabled button.
- Discovery findings fixed: the folder tree no longer holds only a state item (states render as a paragraph outside `role="tree"`; nested states are `li role="none"` with a span), clearing both axe findings recorded for `/command/servers/{id}?tab=files` in 69-06.
- Empty states use `EmptyState` with the 69-03 title and body keys and an Upload action when allowed; loading uses `LoadingState`.

## Task Commits

1. **Task 1 (tracer): inline row actions from click to the delete confirmation** - `34ea59c9` (refactor)
2. **Task 2: upload, new folder and delete dialogs on ConfirmDialog** - `9c12c84d` (refactor)
3. **Task 3: remaining controls, search focus ring, ratchet** - `5e8d3cbb` (refactor)

Commits were created with the repository hooks enabled. Tracer gate: the automated-only verify (files-panel-controls and workspace-page tests) passed, plus lint and typecheck, before expansion.

## Decisions Made

- `FileChooserButton` (new, `src/components/`) wraps the hidden `<input type="file">` and an `ActionButton`. It is the one remaining raw control from this plan and is listed in `family.hosting-files.json` with the reason "browser file chooser wrapper (UI-SPEC allowance)". The UI-SPEC already lists file choosers as the allowed raw input; the alternative of editing the primitive list was outside this plan's files.
- Upload dismissal while running: `ConfirmDialog` disables its extra action while `busy`, and busy is what disables dismiss and Escape, so the abort control ("Cancel uploads") is an `ActionButton` inside the dialog body beside the progress bar.
- Search clear label reuses `Server_Fleet_SearchClear` ("Clear search"); the locale files are not editable here and no Files-specific key exists.
- The delete and upload invokers get explicit focus return (and the heading after a successful delete) so the guarantee does not depend on native dialog focus restoration, which jsdom lacks.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] New shared FileChooserButton outside the listed files**
- **Found during:** Task 2
- **Issue:** The upload dialog's two hidden file inputs are raw `<input>` elements; the acceptance criterion needs zero raw controls in ServerFilesPanel, and no shared file-chooser control existed.
- **Fix:** Added `src/components/FileChooserButton.tsx` and one ratchet entry with a reason. `src/components/FormFields.tsx` was deliberately not edited because plan 69-26 (Wave 3) also modifies it.
- **Files modified:** `src/components/FileChooserButton.tsx`, `tests/ratchets/family.hosting-files.json`
- **Commit:** `9c12c84d`

**2. [Rule 2 - Missing critical] Delete dialog dismiss label and focus in Task 1**
- **Found during:** Task 1
- **Issue:** The Task 1 behavior requires initial focus on a dismiss control that names keeping the file, which is the Task 2 key swap.
- **Fix:** Applied `React_ServerFiles_DeleteFile_Dismiss`, `initialFocus="dismiss"` and `busy` to the delete dialog in Task 1; Task 2 covered the other two dialogs.
- **Commit:** `34ea59c9`

**3. [Rule 1 - Bug] Duplicate create-folder error behind the modal**
- **Found during:** Task 2
- **Issue:** The create-folder request error appeared both in the dialog and in the panel-level inline alert.
- **Fix:** The panel-level error no longer includes the directory mutation; the dialog owns it.
- **Commit:** `9c12c84d`

**4. [Rule 1 - Bug] Folder tree axe findings**
- **Found during:** Task 3 (69-06 discovery table rows owned by 69-10)
- **Fix:** States render outside `role="tree"`; nested states use `li role="none"`.
- **Commit:** `5e8d3cbb`

**5. [Plan premise differed from code] Open and Download are not both on every row**
- The plan's behavior line lists Open, Download and Delete for "a file row"; the existing menu showed Open for directories and Download for files, and that condition and the capability gate for Delete were kept unchanged (T-69-15).

**Total deviations:** 4 auto-fixed (1 blocking, 1 missing critical, 2 bugs) plus 1 premise note. **Impact:** none on the plan's outcomes.

### Test changes recorded

- `workspace-page.test.tsx`: Delete is now clicked by its inline name; the live-context check asserts no `Delete ...` button; the upload dialog dismiss uses "Don't upload files"; the empty-search text is "No files match"; the required path field is queried by role and name because the decorative asterisk is part of the label text content.
- `hosting-files-secrets.spec.ts` was not touched; no locator broke (34 passed).

## Authentication Gates

None.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was used and no package was installed.
- `npm run test:e2e` regenerated `src/generated/api.ts` with a line-ending diff; it was restored with `git checkout --` and never committed.
- One full-file run of `workspace-page.test.tsx` timed out once under heavy machine load (a parallel lint and typecheck); it passes alone at about 2.8 s for the slowest test.
- jsdom raises no `cancel` event for Escape, so the dialog tests dispatch it directly (`pressEscape`).

## Notes for later plans

- 69-33 can retire these now unused keys: `React_ServerFiles_ActionsFor`, `Hosting_Detail_FilesEmpty`, `React_ServerFiles_NoSearchResults`.
- 69-34: `tests/styles/legacy-ratchet.json` still lists `features/hosting/hosting.css` with `outlineRemoval: 1` and the earlier hex and off-grid counts; the file now measures 0 outline removals and fewer hex values (the JSON is not part of this plan, and the test only requires counts at or below the ceiling).
- The Files-tab keyboard walk and focus-ring measurement need the Wave 3 workspace registry entry; until then the ring is asserted from rule text only.

## Verification

- `npm --prefix sources/ReactApp test -- tests/hosting tests/ratchets tests/styles/legacy-ratchet.test.ts tests/locales`: 119 files passed, 2266 passed, 1 skipped (opt-in writer).
- `npm --prefix sources/ReactApp run lint` (`--max-warnings 0`): exit 0.
- `npm --prefix sources/ReactApp run typecheck`-equivalent `tsc -b`: exit 0.
- `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-files-secrets.spec.ts --project=chromium`: 34 passed.
- Acceptance criteria: `role="menu` count 0; `React_ServerFiles_DeleteNamed` count 1; `Common_Cancel` count 0; `<dialog` count 0; raw control elements in ServerFilesPanel 0; `className="button` 0; `outline: none|0` in hosting.css 0.

## Known Stubs

None.

## Threat Flags

None. T-69-14 is covered by the confirm-first test (no `deleteFile` call before confirmation) and T-69-15 by the member-without-rights test; no package was installed (T-69-SC).

## Self-Check: PASSED

- Created files exist: `FileChooserButton.tsx`, `files-panel-controls.test.tsx`.
- Task commits are ancestors of HEAD: `34ea59c9`, `9c12c84d`, `5e8d3cbb`.
