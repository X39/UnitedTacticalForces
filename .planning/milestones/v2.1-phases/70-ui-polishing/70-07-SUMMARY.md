---
phase: 70-ui-polishing
plan: 07
subsystem: ui
tags: [react, files, file-browser, toolbar, segmented-radio, dropzone, css-tokens, playwright]
status: complete

requires:
  - phase: 70-ui-polishing
    provides: "70-01 tokens (--utf-dropzone-min-height, --utf-hover) and action-control.css, 70-05 RadioGroupField layout segmented, 70-06 workspace-panel surface, 70-16 e2e Settings seeding"
provides:
  - "FileBrowser: one bordered card with a named toolbar, the explorer and the footer"
  - "FileUploadDropzone: the upload dialog drop zone"
  - "files.css: token-only file browser, toolbar, tree, table and drop zone styles"
  - "ServerFilesPanel on section.workspace-panel with a segmented context switch and one gold Upload"
affects: [70-14, 70-17, 70-18, 70-19, 70-20]

tech-stack:
  added: []
  patterns:
    - "Layout-only presentation component with slots; the data logic stays in the panel that owns it"
    - "Legacy hook classes (next-hosting-*) stay on the DOM next to new BEM classes; only the CSS moves"
    - "Drag outline drawn as a ::after border, because the style guard allows a literal 1px only on border properties"

key-files:
  created:
    - sources/ReactApp/src/components/server/FileBrowser.tsx
    - sources/ReactApp/src/components/server/FileUploadDropzone.tsx
    - sources/ReactApp/src/components/server/files.css
  modified:
    - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/hosting/files-panel-controls.test.tsx
    - sources/ReactApp/tests/hosting/files-panel-location.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
    - sources/ReactApp/tests/e2e/support/screens/adjacentScreens.ts

key-decisions:
  - "The tree pane's context title is a plain paragraph, not an h3: at the root the list heading is the same context label, and two h3 with one name break role queries and the screen reader outline"
  - "A change of the file context keeps focus on the context radios (the heading focus rule skips it once), so the Arrow keys can keep moving between the two contexts"
  - "The upload dialog moves focus to its first choose control from an effect in the panel, which runs after the dialog has been shown; the browser default would land on the focusable body region"
  - "The 16 MiB limit stays visible as a muted line under the drop zone (the old picker row carried it); it is not part of the drop zone's own title and line"

patterns-established:
  - "FileBrowser slots: breadcrumb, actions, refresh, search, treePane, listPane, footer, plus the drag handlers"

requirements-completed: [D-14]

duration: ~110 min
completed: 2026-11-26
commits: 3
plan_head_before: bfe88418bc1743f1ff0d814969dceecc4dec594f
plan_head_after: 84cb0aaab9e04eaf758d9e620f5743b280bdde49
actuals:
  tokens: 26000
  tasks: 3
  commits: 3
---

# Phase 70 Plan 07: Files panel file-browser card Summary

The Files panel is one workspace panel whose file-browser card holds the toolbar, the explorer and the footer, the list starts directly under a never-empty heading, the contexts are a segmented radio group, Upload is the one gold action, and the upload dialog opens on a drop zone.

## Accomplishments

- Task 1 (tracer): `FileBrowser.tsx` and `files.css`. The list pane is a top-aligned flex column (`align-self: start`), with no grid template, no auto top margin and no 32rem minimum on the explorer. The heading is the folder name or the context label (Persistent expected files or Live worker files) and is never empty. The permanent immediate-note paragraph is gone; the footer shows the item count and then "Changes to files take effect immediately." (`Server_Files_Footer_Immediate`). `Server_Files_ImmediateNote` and `Server_Files_FolderHeading` are deleted from both locales. About 330 lines of file rules left `hosting.css` for token-only rules in `files.css`; the legacy ratchet for `hosting.css` fell from 84 to 59 raw hex colours and from 18 to 13 off-grid pixel literals. Tracer gate: the vitest set and the Playwright spec (chromium and mobile-390) passed before expansion; at 1280 and 390 the first table row sits at most 24px below the heading, the explorer has no minimum height and the page does not scroll sideways (new spec, en and de).
- Task 2: the toolbar is `role="toolbar"` named `Server_Files_Toolbar_Label` ("File actions" / "Dateiaktionen"), two rows: the path with Upload (gold) and Create folder (ghost), then the search with an icon-only Refresh (`ArrowSync20Regular`, named by `Common_Refresh`). At 520px and below the two actions are equal columns. The context tablist is now `RadioGroupField layout="segmented" hideLegend` at the end of the panel head (wrapping below at 800px and below). The empty-folder Upload stays `primary`, so exactly one `button--gold` is in the view. The tab queries of both vitest files and the three `adjacentScreens.ts` selectors are migrated (Upload and Create folder by role and name, the context switch by its radios); S-08 gains the states "card with rows" (renamed from "persistent") and "empty folder".
- Task 3: `FileUploadDropzone.tsx` (dashed 1px `--utf-border-control`, `--utf-dropzone-min-height`, Document icon, "Drop files here", "Or choose what to upload.", the two choose buttons side by side and stacked at 520px and below). Dropping on the zone calls the existing `handleDrop`. The confirm is gold and uses `confirmUnavailableReason` ("Choose files or drop them into the explorer.") while the queue holds nothing uploadable; preflight and unapproved collisions still block through `confirmDisabled`. Focus starts on the first choose button. Each queued row has an icon-only remove control while queued and no batch runs; the collision lists recompute and folders only the removed file needed are no longer created.

## Verification

- `tests/hosting`, `tests/styles`, `tests/locales`: pass. A full `tests/hosting` run under parallel-worktree load timed out (5s) in `activity-section`, `automation-schedules`, `settings-leave-guard`, `workspace-page` (two cases) and `metrics-performance` (a 200 ms performance backstop); each file passes when re-run alone, as in earlier plans.
- `npm run lint`, `npm run typecheck`, `locales:check` (3908 keys each, 0 failures), `locales:unused` (0 failures), `prettier --check src tests --end-of-line auto`: clean.
- Playwright: `hosting-files-secrets.spec.ts` on chromium and mobile-390 (40 passed, 0 retries), S-08 on chromium, mobile-320 and mobile-390 (en and de, all audits including keyboard, reflow, text spacing and German walk) and O-06 on chromium and mobile-320: pass. `hosting-change.spec.ts` and `journeys/change-mods.spec.ts` (they open Files) pass on chromium.
- The plan's `--grep "S-08"` form runs as `--grep=S-08` through the lock helper (same match).
- Acceptance criteria: all 12 grep checks hold (hosting.css 0 for content, explorer, immediate and toolbar; `ImmediateNote` and `FolderHeading` 0 in en.json; `role="tablist"` 0; `getByRole("tab"` 0; `grid-template-rows` 0 in files.css; one `export function` each in FileBrowser and FileUploadDropzone; `Server_Files_Toolbar_Label` in de.json; `Server_Files_Upload_DropTitle` in en.json; `next-hosting-upload-pickers` 0 in the panel).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Duplicate h3 name at the root**
- **Found during:** Task 1
- **Issue:** the tree pane titled itself with an `h3` of the context label, which is now also the list heading at the root, so `getByRole("heading", { level: 3, name })` found two and the outline repeated one name.
- **Fix:** the tree pane title is a `p.file-browser__tree-title`; the preserved-paths `h3` stays.
- **Files modified:** `ServerFilesPanel.tsx`, `files.css`
- **Commit:** b40e9025

**2. [Rule 2 - Missing critical] Context switch must not steal focus from the radios**
- **Found during:** Task 2
- **Issue:** a context change is a router push, and the existing rule focuses the folder heading after every push once the listing loads, so an Arrow key press on the segmented control would have dropped focus out of the group.
- **Fix:** a ref set by the radios' `onChange` makes that one focus move skip. Links, the tree and Back/Forward behave as before.
- **Files modified:** `ServerFilesPanel.tsx`
- **Commit:** 0e6b36e7

**3. [Rule 1 - Bug] Drag outline broke the style guard**
- **Found during:** Task 1
- **Issue:** `outline: 1px dashed ...` is flagged because the guard allows a literal 1px only on border properties.
- **Fix:** the outline is a `::after` element with `border: 1px dashed var(--utf-border-control)`; the overlay itself carries no border.
- **Files modified:** `files.css`
- **Commit:** b40e9025

**4. [Rule 1 - Bug] Drop zone shrank under its content**
- **Found during:** Task 3 (S-08 German at 320px, sticky coverage: the limit line covered the folder button)
- **Issue:** the dialog body is a scrolling flex column and the zone's explicit `min-height` let it shrink below its stacked content.
- **Fix:** `flex: none` on `.file-dropzone`.
- **Files modified:** `files.css`
- **Commit:** 84cb0aaa

**5. [Rule 3 - Blocking] Locale test fallout**
- **Found during:** Task 1 and 3
- **Issue:** `tests/locales/server-keys.test.ts` lists every `Server_Files_` key and its count, and `identical-allowed.json` named the deleted `Server_Files_FolderHeading`.
- **Fix:** the key list and its count follow the changes (158, 159, then 162); the identical-allowed entry is removed.
- **Files modified:** `tests/locales/server-keys.test.ts`, `src/locales/identical-allowed.json`
- **Commits:** b40e9025, 0e6b36e7, 84cb0aaa

### Plan reading

- The plan names the buttons "Upload files"; the existing label is `React_ServerFiles_Upload` ("Upload", "Hochladen") and every test queries it, so the label is unchanged. The legend of the context group keeps its existing value "File context" (the plan prose says "File contexts").
- "First table row within 24px of the heading" is measured to the table's first row, the header row (heading, 8px gap, header row).
- `FileBrowserProps` gained `toolbarLabel` and `actions` is optional (a viewer without file rights has no actions); `FileUploadDropzoneProps` gained an optional `ref` so the panel can move focus after the dialog opens.
- `Server_Files_Upload_RemoveNamed` ("Remove {{name}} from the queue" / "{{name}} aus der Warteschlange entfernen") is a new key the UI-SPEC does not name; an icon-only control needs an accessible name. The compact queue row layout stays with 70-14; the badge and the remove control share a `file-dropzone__row-end` wrapper so the existing three-column row still lays out.
- The new locale keys sit next to the other `Server_Files_` keys, not in the earlier sorted block.
- Commits carry the `Co-Authored-By: Claude Sonnet 5.5` line from the session attribution instruction; the executor notes named Opus 5.5.

## Authentication Gates

None.

## Known Stubs

None.

## Deferred Issues

- Deferred follow-up: real-device look of the card, the segmented switch and the drop zone, a real drag from the desktop onto the zone (jsdom and Playwright dispatch synthetic drop events only), screen reader pass and the VER-03 baselines are milestone-end human items.
- XPF-70-07-1: `hosting.css` still holds the upload queue, summary and dialog width rules (raw hex such as #2b3748, #f29393 and `accent-color: #c4a95d` on the progress bar) and the shared `.next-hosting-table-scroll` rules other tables use; the queue moves in 70-14.
- XPF-70-07-2: `ConfirmDialog` with `bodyLabel` makes the body a focusable region, which the browser's default initial focus picks before any control inside it. Any other dialog that wants initial focus on its first control needs the same follow-up effect; a shared `initialFocus` option would be the cleaner fix for the owner of `primitives.tsx`.
- Known pre-existing e2e issues from the executor notes (`hosting-cost-alerts.spec.ts:552`, mobile axe `target-size` on `.section-navigation__link`) were not run here and are not touched.

## Threat Flags

None. T-70-11 is mitigated: file and folder names stay React text nodes in the heading, breadcrumb, table, queue and remove-control label; no HTML is rendered. T-70-12 is mitigated: the drop zone reuses `handleDrop`, so preflight, collision approval and the destination line apply unchanged, the dialog description still names the target folder, and the zone ignores drops while a batch runs or the preflight is pending.

## Self-Check: PASSED

- FOUND: `sources/ReactApp/src/components/server/FileBrowser.tsx`, `FileUploadDropzone.tsx`, `files.css`
- FOUND commits: b40e9025, 0e6b36e7, 84cb0aaa
