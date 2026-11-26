---
phase: 69-consistency-accessibility-verification
plan: 24
subsystem: testing
tags: [playwright, vitest, accessibility, wcag22, dialog, target-size, typescript-ast, react]

requires:
  - phase: 69-01
    provides: "ConfirmDialog shell in shared-primitives.css"
  - phase: 69-02
    provides: "Shared controls and primitives"
  - phase: 69-04
    provides: "Family ratchets (shared-workspace)"
  - phase: 69-06
    provides: "mockApi, a11y.ts audit helpers, discovery pass and the findings table"
provides:
  - "tests/e2e/audit/dialog-geometry.spec.ts: dialog shell geometry in chromium, mobile-390 and mobile-320, English and German"
  - "tests/a11y-static.test.ts: AST scans for positive tabindex, unnamed icon-only controls and click handlers on inert elements, each proven on bad samples"
  - "tests/e2e/audit/toast-target-size.spec.ts: rendered target size of the toast dismiss and the search clear control"
  - "CommandListToolbar on the shared SearchField (no raw control); toast dismiss and icon-only controls sized for touch"
  - "family.shared-workspace.json empty"
affects: [69-26, 69-27, 69-28, 69-31, 69-33]

actuals:
  tokens: 8550
  tasks: 3
  commits: 2
plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: 77d4c035a01089ffed12605214284c1c8b0a5ecd

tech-stack:
  added: []
  patterns:
    - "Geometry is measured in a real browser against the shared dialog shell, not in jsdom"
    - "Static source scans run on the TypeScript syntax tree with inline bad and good samples as non-vacuity proof"
    - "Component target sizes that jsdom cannot resolve are asserted twice: on the stylesheet source in Vitest and on the rendered box in Playwright"

key-files:
  created:
    - sources/ReactApp/tests/e2e/audit/dialog-geometry.spec.ts
    - sources/ReactApp/tests/a11y-static.test.ts
    - sources/ReactApp/tests/e2e/audit/toast-target-size.spec.ts
  modified:
    - sources/ReactApp/src/components/CommandListToolbar.tsx
    - sources/ReactApp/src/components/AppToasts.tsx
    - sources/ReactApp/src/components/shared-primitives.css
    - sources/ReactApp/tests/e2e/support/a11y.ts
    - sources/ReactApp/tests/e2e/audit/a11y-helpers.spec.ts
    - sources/ReactApp/tests/shared-controls.test.tsx
    - sources/ReactApp/tests/app-toasts.test.tsx
    - sources/ReactApp/tests/admin-authority-pages.test.tsx
    - sources/ReactApp/tests/ratchets/family.shared-workspace.json

key-decisions:
  - "The destructive confirmation in the geometry spec is the stop of a running server with a typed challenge (the fleet card only offers start and stop); it carries the danger variant and the challenge field row. The archive and delete dialogs live in the workspace settings section and need a full workspace mock, so they are left to the workspace slice plans."
  - "The stacked dialog layout is column-reverse (confirm first, per D-10), so the geometry helper orders the stacked actions by their visual top instead of DOM order; overlap and side-by-side actions still fail."
  - "A table row has the implicit role row, so the static click rule asks a tr for a key handler only; a decorative aria-hidden element and a wrapper whose whole handler is stopPropagation() are not clickable controls and are skipped."
  - "CommandListToolbar now renders SearchField, which brings role searchbox instead of textbox and an icon-only clear control."

patterns-established:
  - "Static rules: rulesOf(source) in a11y-static.test.ts gives one-line bad-sample proofs for each rule"

requirements-completed: [FND-06, CON-03]

coverage:
  - id: D1
    description: "ConfirmDialog content shares one left edge, actions are right-aligned above 520px and stacked full width at 44px at or below 520px with the confirm action first, initial focus is on dismiss, the dialog fits the viewport and the page never scrolls sideways"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/dialog-geometry.spec.ts (4 tests x 3 projects, 12 passed)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/audit/a11y-helpers.spec.ts#dialog geometry accepts a reversed stack and rejects side-by-side actions at a phone width"
        status: pass
    human_judgment: false
  - id: D2
    description: "No positive tabindex, no unnamed icon-only action control and no pointer-only click handler on an inert element anywhere in src"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/a11y-static.test.ts (12 passed, tree scan over more than 100 files)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The toast dismiss and the shared icon-only controls measure at least 24px, and 44px at or below 800px"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/toast-target-size.spec.ts (3 projects passed)"
        status: pass
      - kind: unit
        ref: "tests/app-toasts.test.tsx#gives the dismiss control a name and a target of at least 24px, 44px at or below 800px"
        status: pass
    human_judgment: false
  - id: D4
    description: "CommandListToolbar renders no raw control and the shared-workspace ratchet is empty"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/shared-controls.test.tsx#CommandListToolbar and tests/ratchets/ratchet.test.ts"
        status: pass
    human_judgment: false

duration: 85min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 24: Shared and server component verification Summary

**The shared dialog shell is measured in a real browser at 1280, 390 and 320 in two languages, static scans keep positive tabindex, unnamed icon-only controls and pointer-only clicks out of the source, the toast dismiss and icon-only controls meet 24px and 44px touch targets, and the shared-workspace ratchet is empty.**

## Performance

- **Duration:** about 85 min (start time not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (tracer, shared control fixes, server and workspace findings)
- **Files:** 3 created, 9 modified

## Accomplishments

- **Tracer (Task 1).** `dialog-geometry.spec.ts` opens the start cost confirmation and the typed-challenge stop confirmation from the fleet in chromium, mobile-390 and mobile-320, in English and German, and asserts `checkDialogGeometry`, initial focus on the dismiss control (T-69-30), the confirm-first stack at or below 520px and dismiss-before-confirm above it, a dialog inside the viewport whose last action can be scrolled into reach, and no sideways page scroll. 12 tests pass. The tracer exposed one real defect in the audit helper itself (see Deviations), not in the shell.
- **Static scans (Task 2).** `a11y-static.test.ts` scans every handwritten `.tsx` under `src` (over 100 files, over 2000 elements) on the TypeScript syntax tree and proves every rule on inline bad samples. The tree holds all three rules with no baseline.
- **Target size (Task 2).** `shared-primitives.css` sizes the toast dismiss at 2rem (24px floor) and the touch height at or below 800px, gives it a focus ring, and sets icon-only action buttons to the touch height at or below 800px. Because jsdom resolves neither media queries nor `var()`, the sizes are asserted on the stylesheet in Vitest and on the rendered box in a new Playwright spec in all three projects.
- **CommandListToolbar (Task 2).** The raw search input is replaced by the shared `SearchField`; the submit button and children are unchanged. The shared-workspace ratchet is empty (`files: {}`).
- **Discovery re-run (Task 3).** See below: no finding owned by 69-24.

## Task Commits

1. **Task 1: Tracer, dialog shell geometry proven in a real browser at three widths** - `87bff74a` (test)
2. **Task 2: Static accessibility scans and shared control fixes** - `77d4c035` (fix)
3. **Task 3: Server and workspace component findings** - no commit: the 69-06 findings table assigns no row to 69-24 and the discovery re-run finds none (details below); the ratchet was emptied with Task 2 because CommandListToolbar was its only entry.

Tracer gate: the automated verify was re-run end to end after the geometry fix (12 passed) before the expansion tasks began. The official command (`npm run test:e2e -- tests/e2e/audit/dialog-geometry.spec.ts`) was run through an uncommitted copy of `playwright.config.ts` that only moves the web servers to private ports (4273 and 4274), so a sibling executor's preview server on 4173 could not be reused by mistake; the projects, test directory and specs are identical. The copy was deleted before the final lint.

## Discovery re-run (Task 3)

`UTF_A11Y_DISCOVERY=1` on this branch: 114 page visits (11 hosting routes, 4 workspace tabs, 4 smoke routes, 2 cultures, 3 projects), 120 passed, 0 pages threw. Findings left in the data:

| Rule | Where | Owner |
|------|-------|-------|
| heading: no visible `h1` | smoke `/command/content/pages/home/edit` (6 runs) | 69-18 (already in the 69-06 table) |
| focus ring not gold-bright | `button.account-profile__trigger`, phone projects (41 page visits) | 69-08 (already in the 69-06 table) |

No axe, reflow, text-spacing or heading finding remains on any hosting route or workspace tab, and none is owned by 69-24. The 69-06 findings table contained no 69-24 row (its rows named 69-08, 69-10, 69-11, 69-12 and 69-18), so no server component or workspace section needed a fix and none was edited. The scope caveat of 69-06 still applies: only the fleet and the workspace carry fixtures, so populated states of other pages are first measured by the Wave 3 audits.

## Decisions Made

See the `key-decisions` frontmatter. The two with consequences for other plans: `CommandListToolbar` pages now expose a `searchbox` (page tests that queried `textbox` need the new role), and the stacked dialog order is confirm-first in the audit helper as in the shell.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Geometry helper rejected the designed stacked order**
- **Found during:** Task 1
- **Issue:** `collectDialogGeometry` required DOM order to equal visual order when stacked, but the shell stacks with `column-reverse` (confirm first, D-10), so every dialog failed at 390 and 320 with "is not stacked below the previous action".
- **Fix:** Order the stacked controls by their rendered top before the overlap check, with a comment. A new helper self-test proves a reversed stack passes and side-by-side actions at 400px fail.
- **Files modified:** `tests/e2e/support/a11y.ts` (owned by 69-06, not in this plan's list), `tests/e2e/audit/a11y-helpers.spec.ts`
- **Commit:** `87bff74a`

**2. [Rule 3 - Blocking] Search role change reached an existing page test**
- **Found during:** Task 2
- **Issue:** `SearchField` is a `type="search"` input (role `searchbox`); `admin-authority-pages.test.tsx` queried the roles and claims search as `textbox`.
- **Fix:** Query `searchbox` in the two assertions.
- **Files modified:** `tests/admin-authority-pages.test.tsx` (owned by 69-14, not in this plan's list)
- **Commit:** `77d4c035`

**3. [Rule 2 - Missing critical] Rendered target-size proof**
- **Found during:** Task 2
- **Issue:** The plan asks for computed-style minimums at 24px and 44px, which jsdom cannot compute (no media queries, no `var()`), so a Vitest test alone would prove nothing.
- **Fix:** Added `tests/e2e/audit/toast-target-size.spec.ts` (not in the plan's file list) that measures the rendered toast dismiss and search clear control in all three projects, next to the stylesheet assertion in `app-toasts.test.tsx`.
- **Commit:** `77d4c035`

**4. [Rule 2 - Missing critical] Decorative and stop-propagation click handlers**
- **Found during:** Task 2
- **Issue:** The literal click rule flagged 11 sites in other plans' files: 9 table rows that already have `tabIndex` and a key handler (a `tr` has the implicit role row), plus two non-controls (an `aria-hidden` scrim and a wrapper that only calls `stopPropagation()`). Fixing them would edit page files owned by their family plans.
- **Fix:** The rule asks a `tr` for a key handler only and skips the two non-control shapes; each exemption is proven by an inline sample. Every site now passes with no baseline.
- **Commit:** `77d4c035`

**Total deviations:** 4 auto-fixed. **Impact:** two edits in files outside `files_modified` (listed above) and one added spec; no behavior outside shared components changed.

### Notes (not deviations)

- The plan's destructive confirmation "archive or delete" is covered by the typed-challenge stop confirmation; see `key-decisions`.
- `src/generated/api.ts` was rewritten with different line endings by the preview build and restored with `git checkout`; it is in no commit.
- `node_modules` is a directory junction to the main checkout's install (untracked, no package or lockfile change).
- `npx tsc -b` and `npm run lint` are clean. The full Vitest run had three timeouts in `tests/hosting/workspace-page.test.tsx` under parallel load; the file passes alone (11 tests).

## Issues Encountered

- `tests/e2e/hosting-fleet.spec.ts` ("the Command Center nav and the fleet page read Servers", chromium) fails on the page title ("Server fleet" vs `/Servers/`); it does not involve any file this plan changed. Logged in `deferred-items-69-24.md` with three other observations (profile trigger focus ring and public page editor `h1`, both already owned; the column-reverse focus-order note).

## Known Stubs

None.

## Threat Flags

None. Presentation and test changes only (T-69-30 is mitigated by the initial-focus assertions at all three widths; T-69-SC: no package installed).

## Ratchet and file notes for the orchestrator

- No new stylesheet was added, so `tests/styles/legacy-ratchet.json` is untouched.
- `files_deleted:` none.
- Files edited outside this plan's `files_modified`: `tests/e2e/support/a11y.ts`, `tests/e2e/audit/a11y-helpers.spec.ts`, `tests/admin-authority-pages.test.tsx`; file added outside it: `tests/e2e/audit/toast-target-size.spec.ts`. `SearchField.tsx`, `ActionControl.tsx`, `Disclosure.tsx`, `FilterChipGroup.tsx`, `Breadcrumbs.tsx`, `SectionNavigation.tsx` and the server components needed no change.
- Pages that use `CommandListToolbar` (claims, roles, members, membership requests, terrains, field reports, member detail, role detail) now render the shared search field; the legacy `.search-field` rules in `styles.css` no longer apply to that input (`.verein-membership-requests-toolbar .search-field` is now unused). Sibling plans touching those pages should query `searchbox`.

## Self-Check: PASSED

- Created files exist: `dialog-geometry.spec.ts`, `a11y-static.test.ts`, `toast-target-size.spec.ts`, `deferred-items-69-24.md`.
- Commits `87bff74a` and `77d4c035` are ancestors of HEAD.
- Acceptance: `checkDialogGeometry` in `dialog-geometry.spec.ts` (5), raw controls in `CommandListToolbar.tsx` (0), `tabindex|tabIndex` in `a11y-static.test.ts` (23), `family.shared-workspace.json` has 0 files, the discovery re-run is recorded above with no 69-24 finding.
- Verification: `dialog-geometry.spec.ts` 12 passed; `a11y-audit.spec.ts` and `a11y-helpers.spec.ts` pass; `tests/hosting tests/styles tests/ratchets` and the shared control tests pass (124 files, 2329 tests); `npm run lint` and `npx tsc -b` clean.
