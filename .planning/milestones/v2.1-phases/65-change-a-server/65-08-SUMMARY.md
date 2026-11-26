---
phase: 65-change-a-server
plan: 08
subsystem: ui
tags: [react, react-router, files, url-state, leave-guard, a11y, vitest]

requires:
  - phase: 65-02
    provides: the Server_Files_* copy keys and Server_Change_Leave_Stay
  - phase: 65-03
    provides: useLeaveGuard and the leavesWorkspaceSection predicate
provides:
  - filesLocation.ts strict parse and build of ?path= and ?area=
  - URL-backed ServerFilesPanel (folder and context in the URL, router links, push history)
  - safe fallback with notice, folder heading focus, polite announcement and immediate-change note
  - Files leave guard on useLeaveGuard that never blocks folder navigation
affects: [65-09, 65-10, 65-11, 65-12, 65-13, 65-14, 65-15]

plan_head_before: 60748dc359f42eb08e98c4d882df4cae1e581ad8
plan_head_after: 548807b38f5e46b511f12efdea0f5177935540db

actuals:
  tokens: 8000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Untrusted URL values pass a strict pure parser before any request or href; links are built only from the normalized path"
    - "Folder-local state resets by adjusting state during render keyed on location.key (no setState in effects)"
    - "Fallback replace is detected per location key; the notice survives the replace because only non-REPLACE navigations clear it"
    - "Focus after a user-driven change is derived from useNavigationType (PUSH after mount) instead of a ref flag"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/filesLocation.ts
    - sources/ReactApp/tests/hosting/files-location.test.ts
    - sources/ReactApp/tests/hosting/files-panel-location.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
    - sources/ReactApp/src/features/hosting/hosting.css

key-decisions:
  - "Focus is driven by useNavigationType === PUSH plus a mount-key comparison rather than a ref flag set in navigateTo: it also covers link clicks, never moves focus on first load, Back, Forward or replace, and needs no ref access"
  - "The notice is cleared by the render-phase location-key adjustment for every non-REPLACE navigation, so the fallback replace keeps it while links, Back and Forward clear it"
  - "The root crumb keeps the existing Persistent or Live label; Server_Files_TopLevel is used for the folder heading and the announcement"

patterns-established:
  - "filesLocationTo(pathname, searchParams, { path, context }) is the single builder of Files router locations"

requirements-completed: [CHG-06, CHG-05]

coverage:
  - id: D1
    description: "The current folder lives in ?path= and the live context in ?area=live; reload and shared links show the same folder, and every folder change pushes one history entry restored by Back"
    requirement: CHG-06
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#reads the folder from ?path= and lists it first"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#pushes one history entry per folder link and restores the folder on Back"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#adds no history entry when the current folder is activated again"
        status: pass
    human_judgment: false
  - id: D2
    description: "Breadcrumb crumbs and folder names are router links with a real href, the tree keeps its keyboard buttons on the same navigation, context switches and preserved shortcuts navigate by push"
    requirement: CHG-06
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#navigates through the breadcrumb and keeps the tree on the same navigation"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#switches the context with a push and resets the folder"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#opens a preserved shortcut in the persistent context"
        status: pass
    human_judgment: false
  - id: D3
    description: "An invalid or missing ?path= falls back to the top level with a notice and a replace; Back is not trapped; a top-level or non-404 error keeps the error panel and never rewrites the URL"
    requirement: CHG-06
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-location.test.ts#parseFilesPath"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#does not trap Back after an invalid link was replaced"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#falls back to the top level once for a missing folder and keeps Back usable"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#keeps the error panel and the URL for a top-level not-found, with no loop"
        status: pass
    human_judgment: false
  - id: D4
    description: "After a user-driven folder change the folder heading takes focus and the polite region announces the folder and count; first load and Back or Forward only announce"
    requirement: CHG-06
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#moves focus to the folder heading after a link and announces the folder"
        status: pass
    human_judgment: false
  - id: D5
    description: "Queued uploads guard leaving the Files section with Stay here or Leave without uploading while folder navigation is never blocked; the immediate-change note is shown and file operations never enter the draft"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#guards leaving Files with a queued upload and never blocks folder navigation"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/files-panel-location.test.tsx#states that file changes are immediate"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual presentation of the breadcrumb links, folder heading and note (link look matches the replaced buttons)"
    requirement: CHG-06
    verification: []
    human_judgment: true
    rationale: "The link styling is CSS only; jsdom cannot judge that it matches the replaced buttons, and a measured visual check belongs to the Phase 65 e2e and Phase 69 accessibility pass"

duration: 15min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 08: Files folder in the URL Summary

**The Files section keeps its folder and context in `?path=` and `?area=live` behind a strict parser, folder links push history so reload, sharing and Back work, bad or missing folders fall back to the top level with a notice, and queued uploads guard leaving Files without blocking folder navigation.**

## Performance

- **Duration:** 15 min (2026-11-20T20:51Z to 21:06Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 5 (3 created, 2 modified)

## Accomplishments

- Tracer: `filesLocation.ts` (`parseFilesPath`, `readFilesLocation`, `filesLocationSearch`, `filesLocationTo`, `maxFilesPathLength`) rejects empty segments, `.` and `..`, backslashes, control characters and values over 1024 characters before any request; `ServerFilesPanel` derives `context` and `path` from `useSearchParams`, `navigateTo` pushes (no-op for the current location), breadcrumbs and table folder names are router `Link`s, and the folder-local state resets by render-phase adjustment on `location.key` (ancestors of the new path expand, a context change resets the tree).
- Fallback: a per-location-key notice (`invalid` or `notFound`) rendered as an info `StatePanel` with a dismiss button; one `replace: true` navigation drops `path` (keeping `area`); a root not-found or any other error keeps the existing error panel and never navigates, so no loop exists. The raw error text is suppressed while a not-found fallback runs.
- Focus and announcements: an `h3` (`tabIndex={-1}`) takes focus after a user-driven push once its listing is loaded; a mounted `aria-live="polite"` region always reads `Server_Files_FolderAnnounce` from the loaded listing.
- Guard: the old blanket `useUnsavedChanges` is replaced by `useLeaveGuard` with `leavesWorkspaceSection(..., "files")`, a `ConfirmDialog` with Stay here and Leave without uploading (discarding the queue and the unsaved preservation selection before proceeding), plus the immediate-change note above the toolbar.

## Task Commits

1. **Task 1 (tracer): keep the current files folder in the URL** - `58e1ec26` (feat(webapp))
2. **Task 2: fall back safely for missing files folders, guard uploads** - `548807b3` (feat(webapp))

## Verification Results

- Tracer gate (Task 1, automated-only): files-location, files-panel-location, workspace-page and style tests green (72), `typecheck` and ESLint on the touched files clean before expansion. Tracer verified end-to-end, then expanded.
- Plan-level: `vitest run tests/hosting tests/leave-guard.test.tsx tests/styles tests/locales` 83 files, 1315 tests passed; `locales:check` 3525 keys, 0 failures; `tsc -b` exit 0; full `eslint . --max-warnings 0` clean; Prettier clean on every touched file.
- Acceptance greps: `export function parseFilesPath` 1, `readFilesLocation` 2, `useState("")` 3, `useSearchParams` 2, `useUnsavedChanges` 0, `useLeaveGuard` 2, `Server_Files_PathFallback` 1, `Server_Files_ImmediateNote` 1, `replace: true` 1; `sources/Common/Ui/routes.json` unchanged; the two existing workspace-page file-panel cases pass unchanged.

## Deviations from Plan

### Auto-fixed Issues

None - the plan executed as written. Two implementation choices differ from the plan wording and are recorded here:

- **Focus trigger:** the plan describes a ref flag set inside `navigateTo`. The implementation derives it from `useNavigationType() === "PUSH"` with a mount-key comparison. It satisfies every behavior line (no focus on first load, Back, Forward or replace) and also covers link clicks, which do not pass through `navigateTo`, without reading or writing a ref during render.
- **Notice lifetime:** the notice is cleared by the render-phase location adjustment for any non-REPLACE navigation (instead of only inside `navigateTo`) so that link clicks and Back clear it too, while the fallback replace keeps it.

## Issues Encountered

- The jsdom test environment focuses the `autoFocus` input inside the closed "new folder" dialog on mount, so the first-load test asserts that the heading is not focused instead of asserting that the body is. The input is pre-existing markup that browsers do not focus while the dialog is closed.
- `npm run format:check` over the whole project lists 34 files from earlier plans (none touched here, for example `createServerModel.ts` and `ServerWorkspacePage.tsx`); logged as out of scope, the five files of this plan are clean.

## Known Stubs

None.

## Threat Flags

None. T-65-21 (strict parser before any request or href, covered by the `parseFilesPath` table test), T-65-22 (fallback only for non-root paths, one replace per bad key, root not-found never navigates, covered by the no-loop and Back tests) and T-65-23 (links are same-origin router locations built from the normalized path) are mitigated.

## Next Phase Readiness

Plans 65-09 onward are unaffected by the Files panel. `filesLocationTo` is available to any later plan that links into a Files folder, and `useLeaveGuard` is now used by the Files variant in addition to the Settings variant planned for 65-10.

## Self-Check: PASSED

- Created files verified on disk; commits `58e1ec26` and `548807b3` verified as ancestors of HEAD.
