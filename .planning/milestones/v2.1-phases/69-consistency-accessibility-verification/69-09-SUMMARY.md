---
phase: 69-consistency-accessibility-verification
plan: 09
subsystem: ui
tags: [react, hosting, state-views, i18n, accessibility, vitest]

requires:
  - phase: 69-consistency-accessibility-verification
    provides: "69-03 shared state views and key map, 69-04 ratchet families"
provides:
  - "Workspace, fleet, overview and create pages on LoadingState, EmptyState, ErrorState, ForbiddenState and AccessBoundary"
  - "One h1 above every state panel on the four core hosting pages"
  - "features/hosting/copy.ts and the three hosting-only state components deleted, guarded by an ownership test"
affects: [69-10, 69-11, 69-12, 69-13, 69-33]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: d90d16e10189788c1606b8d32f7512d46419509c

actuals:
  tokens: 14300
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Early-return states render the route PageHeader above the shared state panel; the workspace shell keeps WorkspaceHeader as its h1"
    - "A failed refetch with stale data keeps the workspace and shows the ErrorState through the shell notices slot"

key-files:
  created:
    - sources/ReactApp/tests/hosting/hosting-states.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/FleetPages.tsx
    - sources/ReactApp/src/features/hosting/CreateServerPage.tsx
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/tests/locales/ownership.test.ts
    - sources/ReactApp/tests/hosting/deep-link.test.tsx
    - sources/ReactApp/tests/hosting/workspace-header.test.tsx
    - sources/ReactApp/tests/hosting/workspace-shell.test.tsx
  deleted:
    - sources/ReactApp/src/features/hosting/copy.ts

key-decisions:
  - "Fleet and overview keep Server_Access_LoadError as the ErrorState body (generic title above it) so the copy is not repeated by the generic body; the old text still reaches the alert unchanged"
  - "The overview page drops the raw error message it used to print under the error title; no raw backend text is shown on any hosting page now"
  - "Workspace uses the generic error and forbidden keys; create keeps its own Hosting_Create_* forbidden, error and empty keys"

patterns-established:
  - "Ownership test pattern: file-must-not-exist plus a no-importer scan for a retired module"

requirements-completed: [CON-01, CON-02, L10N-03]

coverage:
  - id: D1
    description: "Workspace, fleet, overview and create pages use the shared state views with one h1 above every state"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/hosting/hosting-states.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "Overview copy entries and the create content label fallback come from locale keys in English and German"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/hosting/hosting-states.test.tsx#shows the former copy entries from locale keys in %s"
        status: pass
    human_judgment: false
  - id: D3
    description: "copy.ts and the hosting-only state components are gone and nothing imports them"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/locales/ownership.test.ts#has no hosting copy object"
        status: pass
    human_judgment: false

duration: 55min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 09: Core Hosting Pages on Shared States Summary

**The workspace, fleet, overview and create pages now render LoadingState, EmptyState, ErrorState, ForbiddenState and AccessBoundary under one route h1, read every former copy.ts string from locale keys, and the hosting copy object is deleted and guarded by an ownership test.**

## Accomplishments

- `ServerWorkspacePage` renders the route `PageHeader` ("Servers") above loading, error, forbidden and not-found, and lets `WorkspaceShell` supply its own `WorkspaceHeader` h1 once the server is known. The load error uses the generic "This couldn't be loaded" copy with a Try again button bound to `server.refetch()`. With stale data the error panel sits in the shell's `notices` slot, below the h1 and above the stale content.
- `FleetPages` (overview and fleet) replaced all `useHostingCopy` reads with the 69-03 key map (`Hosting_Overview_*`, `Hosting_Servers_*`, `Shared_CommandCenter_CurrentArea_ServerFleet`, `Hosting_ConfigurationProfiles_Title`, `HostingFinance_Title`) and all state panels with the shared views. The fleet's specific `Server_Access_LoadError`, `Server_Access_ForbiddenTitle` and `Server_Access_ForbiddenBody` pass through the override props.
- `CreateServerPage` uses the shared views with its own `Hosting_Create_*` keys and reads the terrain and scenario fallbacks from `Server_Overview_Terrain` and `Server_Overview_Scenario`.
- `components.tsx` exports only `ActionLabel` and `ServerActions`; the empty text is `Hosting_Detail_NoActions`. `copy.ts` is deleted; `tests/locales/ownership.test.ts` asserts the file is absent, that no src or tests file imports a hosting copy module or `useHostingCopy`, and that no `HostingAccessBoundary`, `HostingLoading` or `HostingError` remains in src.
- `tests/hosting/hosting-states.test.tsx` (new, 22 tests): the five workspace states, overview copy in English and German, overview and fleet error and forbidden copy, and the create page's states and German fallback label, each asserting exactly one h1 that is the first heading.

## Task Commits

1. **Task 1 (tracer): workspace on shared state views** - `24996047` (refactor)
2. **Task 2: fleet, overview and create copy from locale keys** - `0baa68ac` (refactor)
3. **Task 3: delete the hosting copy object** - `d90d16e1` (refactor)

Tracer gate: automated-only verify re-run end to end passed (60 hosting tests) - tracer verified, expansion continued.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests asserted the old workspace copy and heading shape**
- **Found during:** Task 1
- **Issue:** `deep-link.test.tsx` expected "Loading hosting data…" and the fleet's forbidden title on the workspace; `workspace-header.test.tsx` expected the fleet's "servers could not be loaded" error on the workspace and no h1 in the not-found state. The plan requires the generic keys on the workspace and an h1 above every state, so these assertions had to move. Neither file is in `files_modified` and neither is owned by another plan.
- **Fix:** Updated the minimal assertions (generic loading text "Loading...", forbidden title "You don't have access to this page", generic error title, exactly one h1 "Servers" in not-found).
- **Files modified:** `sources/ReactApp/tests/hosting/deep-link.test.tsx`, `sources/ReactApp/tests/hosting/workspace-header.test.tsx`
- **Commit:** `24996047`

**2. [Rule 3 - Blocking] workspace-shell test helper waited for any h1**
- **Found during:** Task 2 (full hosting run)
- **Issue:** `waitForWorkspace` awaited any level-1 heading. The route h1 now renders during loading, so the helper returned before the workspace existed and two Refresh server tests failed.
- **Fix:** The helper now waits until the h1 sits inside the `workspace-header` element.
- **Files modified:** `sources/ReactApp/tests/hosting/workspace-shell.test.tsx` (not in `files_modified`, not owned by another plan)
- **Commit:** `0baa68ac`

**3. [Plan premise differed from code] `family.hosting-core.json` has nothing to lower**
- The ratchet family file already holds `files: {}` (69-04 found no raw controls or literals in these files), so the ratchet writer has nothing to change and the file is unmodified. The literal-count criterion for `CreateServerPage` was already zero; the hard-coded scenario fallback was a neutral-token literal the scanner does not count, and it is now a locale key regardless.

**Total deviations:** 2 auto-fixed (both Rule 3), 1 premise note. **Impact:** none on deliverables; three test files outside the listed set changed by a few lines each.

## Issues Encountered

- Under the load of sibling executors, `workspace-page.test.tsx`, `configuration-profiles.test.tsx` and the ratchet test timed out at the 5 s default in some runs. All pass when rerun with less load (and with `--testTimeout=30000`); they are timing-sensitive, not affected by this plan's changes.
- `tests/e2e/hosting-fleet.spec.ts` "the Command Center nav and the fleet page read Servers" (390x844) fails with `toHaveTitle(/Servers/)` receiving "Server fleet". `CommandCenterLayout` sets the document title from the current-area key and runs its effect after the page's own `useDocumentTitle`, so the layout wins. This plan changes no title code; the layout is owned by 69-08. Not fixed here (out of scope). The other 32 tests in `hosting-fleet.spec.ts` and `hosting-workspace.spec.ts` pass.
- The e2e run regenerated `src/generated/api.ts` with line-ending differences; it was restored with `git checkout --` and not committed.
- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored, no install, no lockfile change).

## Known Stubs

None.

## Threat Flags

None. `AccessBoundary` receives the same `allowed` value the hosting boundary used (T-69-13); `authorization.test.ts` and `member-access.test.tsx` stay green. No package was installed (T-69-SC).

## Verification

- `npx vitest run tests/hosting tests/ratchets tests/locales`: 118 files, 2248 passed, 1 skipped (opt-in ratchet writer).
- `npm run lint` (max-warnings 0) and `npm run typecheck` (`tsc -b`): both exit 0.
- `npx playwright test tests/e2e/hosting-fleet.spec.ts tests/e2e/hosting-workspace.spec.ts --project=chromium`: 32 passed, 1 failed (the pre-existing title ordering above).
- Acceptance criteria: `ServerWorkspacePage.tsx` has 0 `Hosting*` state names and 1 `StateViews` import; `FleetPages.tsx` and `CreateServerPage.tsx` print 0 for the state names and `useHostingCopy` count is 0; `copy.ts` is absent; no `HostingAccessBoundary` remains in src; `ownership.test.ts` mentions `copy.ts` twice.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/hosting/hosting-states.test.tsx`; deleted file absent: `sources/ReactApp/src/features/hosting/copy.ts`.
- Task commits are ancestors of HEAD: 24996047, 0baa68ac, d90d16e1.
