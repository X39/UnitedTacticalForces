---
phase: 69-consistency-accessibility-verification
plan: 18
subsystem: ui
tags: [react, shared-controls, leave-guard, state-views, page-header, ratchet, content-administration]

requires:
  - phase: 69-01
    provides: "Ratchet scanner, shared control rules and PageStack"
  - phase: 69-03
    provides: "State views, dismiss keys and leave labels"
  - phase: 69-04
    provides: "Family ratchet files and the UTF_RATCHET_UPDATE writer"
  - phase: 69-06
    provides: "Discovery finding: no visible h1 on /command/content/pages/home/edit"
provides:
  - "Public page editor that leaves through the shared leave dialog (Keep editing, Leave without saving) and renders its header above forbidden, loading and error states"
  - "Content hub, public pages list and detail on PageStack, PageHeader, state views and action controls"
  - "GDPR content editor on shared text fields and the shared action button"
  - "family.admin-content ratchet reduced to zero files"
affects: [69-28, 69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: bd63bc275555204cc2b616a5a502d5f66d2768ef

actuals:
  tokens: 14000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Editor leave: useLeaveGuard plus ConfirmDialog with Server_Alerts_Leave_Title, React_UnsavedChanges, Server_Change_KeepEditing and the editor leave key (same wiring as EventCreatePage)"
    - "State shell: a routed page's early returns render the same PageHeader above ForbiddenState, LoadingState or ErrorState"
    - "A missing resource is carried as null through TanStack Query and mapped back to undefined at the page"

key-files:
  created:
    - sources/ReactApp/tests/admin-content-pages.test.tsx
  modified:
    - sources/ReactApp/src/features/admin/PublicPageEditorPage.tsx
    - sources/ReactApp/src/features/admin/ContentHubPage.tsx
    - sources/ReactApp/src/features/admin/PublicPagesPage.tsx
    - sources/ReactApp/src/features/admin/PublicPageDetailPage.tsx
    - sources/ReactApp/src/features/admin/GdprContentEditor.tsx
    - sources/ReactApp/tests/admin-public-page-editor-page.test.tsx
    - sources/ReactApp/tests/ratchets/family.admin-content.json

key-decisions:
  - "Converted the editor and GDPR field grids to TextField, TextAreaField and CheckboxField instead of keeping them raw; the family ratchet therefore reaches zero files (the plan allowed at most two)."
  - "The hub drops its kicker because it repeated the h1 (the nav label and Pages_Command_Content_HubTitle are the same text)."
  - "The hub cards use the primary variant, not gold, because two gold actions on one view break the one-gold rule."
  - "Row and revision actions carry an aria-label that names the page or revision, following the existing row aria-label pattern."

patterns-established:
  - "Leave without saving is both the editor's leave link label and the leave dialog's confirm label; Keep editing is the dismiss"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Public page editor leave guard shows Keep editing and Leave without saving in English and German, keeps the draft on Keep editing and navigates on Leave without saving"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-public-page-editor-page.test.tsx#guards leaving with unsaved changes through the shared leave dialog"
        status: pass
      - kind: unit
        ref: "tests/admin-public-page-editor-page.test.tsx#renders the leave dialog in German"
        status: pass
    human_judgment: false
  - id: D2
    description: "Hub, list, detail and editor render one PageHeader h1 above forbidden, loading, error and not-found states; retry refetches"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-content-pages.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Content pages contain no raw control and no button look-alike; the content ratchet family is empty"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Visual layout of the converted editor form fields inside the legacy editor grid and sticky revision panel"
    verification: []
    human_judgment: true
    rationale: "The legacy styles.css grid and label selectors were written for raw labels; no browser run was made in this plan, so the look of the shared fields in that grid is unverified until the 69-28 editor smoke audit."

duration: 19min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 18: Content Administration Convergence Summary

**The public page editor now leaves through the shared Keep editing and Leave without saving dialog with shared fields and action controls, the content hub, list, detail and editor render their header above every state, and the content control ratchet is empty.**

## Performance

- **Duration:** about 19 min (2026-11-25T12:42:34Z to 13:01:32Z)
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files modified:** 7 (plus 1 created)

## Accomplishments

- `PublicPageEditorPage`: `useUnsavedChanges` (browser confirm prompt) replaced by `useLeaveGuard` and a `ConfirmDialog`; both `Common_Cancel` links are now the editor leave link "Leave without saving"; metadata fields, comment, publish checkbox and every button or link are shared controls; a state shell renders the header above forbidden, loading and error.
- `ContentHubPage`, `PublicPagesPage`, `PublicPageDetailPage`: `PageStack` body, `ForbiddenState`, `LoadingState`, `ErrorState` (retry calls `refetch`), `EmptyState` with a title and next step, `ActionLink` and `ActionButton`; list metrics and rows render only once data exists, so a failed first load no longer shows an empty-list panel under the error.
- `GdprContentEditor`: `TemplateField` renders `TextField` or `TextAreaField` (the accessible name still carries the card context); the preview toggle is an `ActionButton` with `aria-expanded`.
- `family.admin-content.json` has no files left.

## Task Commits

1. **Task 1 (tracer): public page editor with named dismiss labels and the leave guard** - `f101cf47` (refactor)
2. **Task 2: content hub, public pages list and detail** - `88f5ca33` (refactor)
3. **Task 3: GDPR content editor and the empty content ratchet** - `bd63bc27` (refactor)

Tracer gate: the tracer `<verify>` (editor page test, editor unit test, ratchets) passed before expansion.

## Files Created/Modified

- `sources/ReactApp/src/features/admin/PublicPageEditorPage.tsx` - leave guard, shared fields and controls, state shell
- `sources/ReactApp/src/features/admin/ContentHubPage.tsx`, `PublicPagesPage.tsx`, `PublicPageDetailPage.tsx` - shared header, states and controls
- `sources/ReactApp/src/features/admin/GdprContentEditor.tsx` - shared fields and button
- `sources/ReactApp/tests/admin-content-pages.test.tsx` - new: header, state, retry, publication and GDPR editor tests
- `sources/ReactApp/tests/admin-public-page-editor-page.test.tsx` - leave dialog tests in English and German, router handle
- `sources/ReactApp/tests/ratchets/family.admin-content.json` - lowered to an empty file list

`files_deleted:` none. No new stylesheet was added, so `tests/styles/legacy-ratchet.json` is untouched.

## Decisions Made

See `key-decisions` above. The leave dialog reuses `Server_Alerts_Leave_Title` and `React_UnsavedChanges` for title and body, exactly as `EventCreatePage` does, so editor leave dialogs read the same across the app.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A missing public page produced a query error instead of the not-found state**
- **Found during:** Task 2 (not-found test)
- **Issue:** `loadAdminPublicPage` returns `undefined` for a 404; TanStack Query treats `undefined` query data as an error ("data is undefined"). The detail page therefore never reached its "Page not found" panel and the editor could never open for a new page; this is also the source of the 69-06 "no visible h1" finding on `/command/content/pages/home/edit`.
- **Fix:** both consumers' `queryFn` return `?? null`; the page maps `null` back to `undefined`. `publicPages.ts` is unchanged (not in this plan's files).
- **Files modified:** `PublicPageDetailPage.tsx`, `PublicPageEditorPage.tsx`
- **Verification:** `admin-content-pages.test.tsx` not-found and new-page editor tests
- **Committed in:** `88f5ca33`

**2. [Rule 1 - Bug] List showed the empty state under a failed first load**
- **Found during:** Task 2
- **Issue:** `isPending` is false after an error, so the old list branch rendered "no pages" beneath the error panel.
- **Fix:** list, metrics and empty state render only when `query.data` exists.
- **Committed in:** `88f5ca33`

**3. [Plan premise differed from code] Editor and GDPR inputs converted instead of kept raw**
- **Found during:** Tasks 1 and 3
- **Issue:** the plan expected field-grid and file-chooser inputs to stay on the ratchet. The editor has no file chooser, and `register()` spreads work with the shared fields.
- **Fix:** converted them; the ratchet family is empty (acceptance asks for at most two entries).

**4. [Plan premise differed from code] No publish or delete confirmation exists on these pages**
- **Found during:** Tasks 1 and 2
- **Issue:** the plan's truths name a publish dialog ("Don't publish page"), "Delete page" and "Keep page" with the name in `bdi`. Publishing is the editor's "Publish this revision immediately" checkbox and the detail page's per-revision Publish or Unpublish button; both act immediately, and there is no page delete anywhere. The keys "Don't publish page", "Publish page", "Delete page" and "Keep page" do not exist, and the plan forbids editing the locale files.
- **Handling:** no dialog was invented. Threat T-69-24 is covered as far as the code allows: the detail test asserts no publication request is sent until the named revision button ("Publish: Revision 3") is pressed. Recorded as cross-plan finding X69-18-01 below.

### Cross-plan findings

- **X69-18-01:** a publish confirmation for public page revisions (and its keys: confirm "Publish page", dismiss "Don't publish page", the unpublish counterpart) is a product decision plus new locale keys; route to the locale owner (69-33 or a follow-up) if the owner wants it.
- **X69-18-02:** the Markdown preview renders the page's own `# heading` as a second `h1` inside the editor and detail pages. Markdown rendering is a closed non-goal, so it was left; the tests assert the page header h1 is the single `.page-header h1` and the first h1 in the document.
- **X69-18-03:** `GalleryAssetPicker` (opened from the editor) still has a hidden generic cancel button; it belongs to the plan that owns that file.
- **X69-18-04:** `styles.css` keeps now-unused selectors (`.public-page-editor-check`, `.gdpr-editor-field` and its input rules, `.public-page-editor-fields > label`); 69-33 can retire them. `Pages_Command_Content_Pages_NoRows` and `Common_Cancel` readers in this plan are gone.

**Total deviations:** 2 auto-fixed bugs, 2 plan-premise differences. **Impact:** no scope creep beyond the pages in `files_modified`; the null fix is required for the plan's not-found and h1 truths.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored); nothing was installed and the lockfile is unchanged.
- The tracer's German-key and "no Cancel" assertions use role queries because the closed gallery picker dialog in the DOM still contains a generic cancel (X69-18-03).

## Known Stubs

None.

## Threat Flags

None. No new network surface; T-69-24 handling is described under deviation 4.

## Verification

- `npx vitest run` on the plan's six test files plus `tests/ratchets`, `tests/locales`, `tests/styles`, `tests/routes.test.ts`, `tests/app`: 21 files, 353 passed, 1 skipped (the opt-in ratchet writer).
- `npm run lint` (eslint, max warnings 0) and `npm run typecheck` (`tsc -b`): both exit 0.
- Acceptance: `Common_Cancel` count in the editor 0; `className="button` count 0 in the editor, `PublicPagesPage`, `GdprContentEditor`; raw control count 0 in `PublicPageDetailPage`; `family.admin-content.json` has 0 files.

## Next Phase Readiness

69-28 (editor smoke audit) should look at the converted editor form in a browser (coverage item D4). No blockers.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/admin-content-pages.test.tsx`.
- Task commits are ancestors of HEAD: f101cf47, 88f5ca33, bd63bc27.
