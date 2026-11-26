---
phase: 63-workspace-shell-activity
plan: 05
subsystem: ui
tags: [react, react-router, navigation, breadcrumbs, css, parity-matrix, vitest]

requires:
  - phase: 63-workspace-shell-activity
    provides: workspaceSections registry, sectionTo and Server_Workspace_Section_* keys (plan 63-01); --utf-indicator-width token (plan 63-04)
provides:
  - SectionNavigation, a generic link-based section navigation with aria-current
  - ServerWorkspaceView routed through five sections selected by ?tab= (overview, activity, settings, files, machine)
  - Breadcrumbs, a shared nav > ol breadcrumb trail
  - navigation.css, token-only wrapping section link and breadcrumb styles
  - parity rows TAB-activity, TAB-settings, TAB-machine
affects: [63-06, 63-07, 63-08, 63-09, 63-10, 63-11, 63-12]

requirements-completed: [WSP-02, WSP-03, WSP-06]

plan_head_before: b93864251296d62838bac58c1ea78994368df70b
plan_head_after: f0ce62b8e912d3563632447e8f62b392eb370396

actuals:
  tokens: 17000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Generic components in src/components import their stylesheet from src/components/server so the style guard covers them"
    - "Section links are plain router links that write only the tab parameter, so a stale ?action= never re-fires"

key-files:
  created:
    - sources/ReactApp/src/components/SectionNavigation.tsx
    - sources/ReactApp/src/components/Breadcrumbs.tsx
    - sources/ReactApp/src/components/server/navigation.css
    - sources/ReactApp/tests/breadcrumbs.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/src/components/commandCenter.css
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/hosting/workspace-header.test.tsx
    - sources/ReactApp/tests/e2e/hosting-placement.spec.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "Activity, Settings and Machine are stacked compositions of the existing panels inside a next-hosting-runtime-stack wrapper; plan 63-11 swaps them for dedicated section components"
  - "The legacy sticky tab strip is deleted outright, so the section links scroll with the page until plan 63-11 places the shell"
  - "The last breadcrumb item is plain text with aria-current=page even when it carries a target"

patterns-established:
  - "Retired or unknown ?tab= values fall through workspaceSectionFromSearch to Overview with no mapping table and no URL rewrite"

coverage:
  - id: D1
    description: "Five linked sections in order Overview, Activity, Settings, Files, Machine in a nav landmark; only the current link has aria-current=page; no tab role and no button inside the nav"
    requirement: "WSP-02"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#offers five section links in order as a nav landmark without tabs or buttons"
        status: pass
    human_judgment: false
  - id: D2
    description: "Retired ?tab=jobs renders Overview and the search stays ?tab=jobs; a section link writes only the tab parameter and Overview writes none"
    requirement: "WSP-02"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#opens Overview for a retired tab value and leaves the URL unchanged"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#moves between sections with links that write only the tab parameter"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every former tab capability stays reachable: Activity shows jobs, retry and skip, logs, protocol download and audit; Settings shows configuration and credentials only with canManageSecrets; Machine shows cloud and runtime panels"
    requirement: "WSP-02"
    verification:
      - kind: unit
        ref: "tests/hosting/workspace-page.test.tsx#requests the shared confirmation for Start and exposes jobs, audit, and diagnostic protocols"
        status: pass
      - kind: unit
        ref: "tests/hosting/workspace-header.test.tsx#shows the cloud panel state from the same derivation with the technical status as detail"
        status: pass
    human_judgment: false
  - id: D4
    description: "Parity matrix has TAB-activity, TAB-settings and TAB-machine rows with status kept in the same commit as the new ids"
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#has a row for workspace tab"
        status: pass
    human_judgment: false
  - id: D5
    description: "Breadcrumbs render nav > ol with links, a plain-text current item, hidden separators between items only and a full 256-character label"
    requirement: "WSP-06"
    verification:
      - kind: unit
        ref: "tests/breadcrumbs.test.tsx"
        status: pass
    human_judgment: false
  - id: D6
    description: "Section links wrap without a horizontal overflow rule, are 44px tall at 800px and below, mark the current section with a 2px gold bottom indicator, and the legacy tab rules are deleted; styles pass the token-only guard"
    requirement: "WSP-03"
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts"
        status: pass
      - kind: other
        ref: "grep of navigation.css, commandCenter.css and hosting.css against the task acceptance criteria"
        status: pass
    human_judgment: true
    rationale: "The wrapped layout at 320 and 390 px is asserted in a real browser by plan 63-12; this plan supplies the wrap rule only"

duration: 8min
completed: 2026-11-19
status: complete
---

# Phase 63 Plan 05: Five Linked Sections and Breadcrumbs Summary

**The server workspace now has five link-based sections (Overview, Activity, Settings, Files, Machine) selected by `?tab=` with retired ids opening Overview untouched, plus wrapping section link styles and a tested shared `Breadcrumbs` component.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-11-19T15:50:00Z
- **Completed:** 2026-11-19T15:58:00Z
- **Tasks:** 2
- **Files modified:** 12 (4 created, 8 modified)

## Accomplishments
- `SectionNavigation` replaces the nine raw tab buttons: a `nav` with a list of react-router links, `aria-current="page"` on the current one only, no tab role, no button.
- `ServerWorkspaceView` selects the section through `workspaceSectionFromSearch` and builds every link with `sectionTo`. Activity stacks the job list, the logs panel and the audit feed; Settings shows configuration and, only with `canManageSecrets`, credentials; Machine shows the cloud panel then the runtime panel; Files is unchanged. The Overview "show all" control is now an `ActionLink` to Activity.
- Three parity rows (`TAB-activity`, `TAB-settings`, `TAB-machine`, status `kept`) were added in the same commit as the new ids; the parity test now imports the registry from `workspaceSections`.
- `Breadcrumbs` and `navigation.css` give wrapping 44px-at-800px section links with a 2px gold current indicator, and breadcrumbs with chevron separators and `overflow-wrap: anywhere`. The legacy `next-hosting-tab*` rules in `commandCenter.css` and `hosting.css` are gone.

## Task Commits

1. **Task 1: Tracer - five deep-linkable sections replace the nine tabs** - `30084524` (feat)
2. **Task 2: Breadcrumbs and the wrapping section navigation styles** - `f0ce62b8` (feat)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified
- `sources/ReactApp/src/components/SectionNavigation.tsx` - generic link-based section navigation
- `sources/ReactApp/src/components/Breadcrumbs.tsx` - shared breadcrumb trail
- `sources/ReactApp/src/components/server/navigation.css` - token-only styles for both components
- `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx` - old tab registry, parser and buttons removed; five-section router
- `sources/ReactApp/src/features/hosting/hosting.css`, `sources/ReactApp/src/components/commandCenter.css` - legacy tab rules deleted
- `sources/ReactApp/tests/breadcrumbs.test.tsx` - five Breadcrumbs cases
- `sources/ReactApp/tests/hosting/workspace-page.test.tsx` - migrated to links and the new ids; three new cases (retired id, link navigation, nav landmark)
- `sources/ReactApp/tests/hosting/parity-matrix.test.ts`, `workspace-header.test.tsx`, `tests/e2e/hosting-placement.spec.ts` - new import and ids
- `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` - three new rows

## Decisions Made
- The three non-Overview compositions are stacked inside the existing `next-hosting-runtime-stack` wrapper so no new layout CSS was needed; plan 63-11 replaces them.
- The sticky strip was deleted as the plan specifies; the section links currently scroll away with the page.

## Deviations from Plan

None - plan executed exactly as written.

Two small notes that are not deviations. The Start test asserts the command diagnostic with `getAllByText`, because the stacked Activity section shows it both in the job's collapsed command details and in the logs panel. Task 2 was marked `tdd="true"`, but the components were written before their test, so there was no separate RED commit; the test and components landed together in the one task commit the plan prescribes.

## Issues Encountered

One test-only fix: the Vitest icon mock drops `className`, so the separator assertions select `fluent-icon` test ids instead of the class name. The three CRLF-only modified ReactApp files in the working tree were left unstaged as instructed.

## Verification

- `npm test` (full suite): 115 files, 1042 tests passed.
- `npm run typecheck` and `npm run lint` (`--max-warnings 0`) exit 0; Prettier clean on the new and changed files.
- Acceptance greps: `workspaceTabFromSearch` 0, `<button` 0, `tab=cloud` 0, `tab=configuration` 0, `next-hosting-tab` 0 in `commandCenter.css`, legacy tab selectors 0 in `hosting.css`, `overflow-x` 0 and `flex-wrap: wrap` 2 in `navigation.css`, `navigation.css` import 1 in `SectionNavigation.tsx`.
- `git diff --exit-code -- sources/Common/Ui/routes.json` exits 0.
- Tracer gate (auto mode): verify re-ran green, so expansion proceeded.

## Known Stubs

None.

## Threat Flags

None. T-63-02 is mitigated: `ServerSecretsPanel` renders only when `server.canManageSecrets`. T-63-34 is mitigated: labels render as React text children and link targets come from `sectionTo` with fixed section ids.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 63-06 can build header components on the section frame; plan 63-11 can place `Breadcrumbs` above the sticky header and swap the stacked compositions for dedicated section components.
- The wrapped layout at 320 and 390 px still needs browser proof in plan 63-12.

## Self-Check: PASSED

- Created files exist: `SectionNavigation.tsx`, `Breadcrumbs.tsx`, `navigation.css`, `breadcrumbs.test.tsx`.
- Commits `30084524` and `f0ce62b8` are ancestors of HEAD; `git rev-list --count` from the plan base measures 2.

---
*Phase: 63-workspace-shell-activity*
*Completed: 2026-11-19*
