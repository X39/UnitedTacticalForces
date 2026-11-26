---
phase: 69-consistency-accessibility-verification
plan: 29
subsystem: testing
tags: [playwright, route-walk, route-certification, headings, navigation-labels, react-app]

requires:
  - phase: 69
    provides: navLabelContract (69-03), mobile Playwright projects (69-05), the strict mockApi layer, certified redesigned pages (69-14 to 69-23)
provides:
  - Browser route walk over all 76 certified manifest patterns (chromium and mobile-390) with minimal per-route fixtures
  - Classic hand-off assertion for the three uncertified routes on the configured origin only
  - Not-found pass over the 17 detail routes, h1 equals nav label check for the 19 command center destinations in en and de, German drawer check at 320
  - Fixes for four real defects the walk found
affects: [69 verification, 69-31 visual baselines, 69-34]

requirements-completed: [VER-01, CON-01, CON-03]

actuals:
  tokens: 14000
  tasks: 3
  commits: 6
plan_head_before: 95618ffdb528e15065ee9c68381efa4051f798fb
plan_head_after: 38697ec1ca6b5b280b2f9c68649234f60a0f9eb0

tech-stack:
  added: []
  patterns:
    - "Route walk driven by the shared routes.json: fixtures keyed by route id, sample ids guid and long, a completeness test that fails when a certified id has no fixture"
    - "knownRouteFindings map keyed by routeId:state subtracts recorded extra h1 elements and annotates the test"

key-files:
  created:
    - sources/ReactApp/tests/e2e/audit/route-walk.spec.ts
    - sources/ReactApp/tests/e2e/support/routeFixtures.ts
  modified:
    - sources/ReactApp/src/api/services.ts
    - sources/ReactApp/src/features/admin/ModPackWorkspacePage.tsx
    - sources/ReactApp/src/features/admin/ClaimsPage.tsx

key-decisions:
  - "The h1 equals label rule is asserted for the 19 command center destinations (the destinations of the contract's three recorded exceptions). Community navigation targets are PublicHero marketing titles (E-01) and only get the aria-current and one-h1 checks."
  - "The spec reads routes.json with node:fs instead of importing src/app/routes.ts, because that module reads import.meta.env, which Playwright does not provide; the local const is named routeManifest."
  - "Non-vacuity is proven by the test plan, not by shared in-process state: every route test asserts it visited all of its patterns, and a static test asserts the planned patterns equal the manifest's certified patterns, that every certified id has a fixture and that every command route declares its navigation item. This keeps the route tests fully parallel."
  - "The sample public page slug is operations-brief (Markdown editor); the GDPR slug opens a different editor and is walked through fixture extraPaths."

patterns-established:
  - "Fixtures return the loaded state, and the walk fails on any visible error or forbidden panel and on any request no handler answered"

coverage:
  - id: D1
    description: "Every certified manifest pattern (76) loads with no pageerror, is not the 404 page or the certification fallback, has exactly one h1 that is the first heading and marks its navigation item aria-current, in chromium and mobile-390"
    requirement: VER-01
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/route-walk.spec.ts route walk: certified routes (chromium 56 passed, mobile-390 56 passed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The three uncertified routes (field report, GDPR, Verein ledger) redirect to the classic app on the configured origin; the walk proves it visited every certified pattern"
    requirement: VER-01
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/route-walk.spec.ts route walk: uncertified routes; every certified route has a fixture and the walk plans every certified pattern"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every command center destination has an h1 equal to its label in en and de (except Overview, Contribution settings, Public images) and a current nav item; detail routes show exactly one h1 above their not-found state"
    requirement: CON-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/route-walk.spec.ts route walk: navigation labels and route walk: not-found states"
        status: pass
    human_judgment: false
  - id: D4
    description: "German drawer labels (Konfigurationsprofile, Beitragseinstellungen and all others) stay whole at 320 with no ellipsis and no horizontal page scroll"
    requirement: CON-01
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/route-walk.spec.ts route walk: German drawer labels (mobile-320)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Very long admin tables on the ratchet list scroll inside their region and stay usable"
    verification: []
    human_judgment: true
    rationale: "Backstop truth of the plan: covered by reflow metrics of other plans, not by pixels in this walk"

duration: 150min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 29: Route walk Summary

**A parallel Playwright route walk opens all 76 certified patterns with minimal fixtures and asserts one leading h1, the current navigation item and the classic hand-off, plus an h1 equals label check for the 19 command destinations in en and de, a not-found pass over the detail routes and a 320px German drawer check; it found and fixed four real defects.**

## Performance

- **Duration:** about 150 min
- **Completed:** 2026-11-25
- **Tasks:** 3 (6 commits including 3 defect fixes)
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments

- `routeFixtures.ts`: one fixture per certified route id (56), built from the API shapes of the generated contract and the existing fleet, create and cost-alert fixture builders. Detail fixtures carry an `h1` pattern so a page that falls back to its not-found state fails.
- `route-walk.spec.ts`: 119 tests in chromium, 119 in mobile-390 and 1 in mobile-320 (the drawer test). The walk runs against the strict mock (any request no handler named fails the route) and fails on any visible error or forbidden panel.
- The walk caught four real defects (below) and records one cross-plan finding.

## Task Commits

1. **Task 1: Tracer, walk every certified route** - `0e736584` (test)
2. **Task 2: h1 equals nav label, aria-current, not-found pass** - `1e0f43b9` (test)
3. **Task 3: German drawer labels at 320** - `38697ec1` (test)

Defect fixes committed before the test that needs them: `bc0abf75` (fix, Verein reads), `6a619789` (fix, mod pack), `2c654e59` (fix, Claims heading).

**Plan metadata:** the SUMMARY commit that follows (docs).

## Files Created/Modified

- `sources/ReactApp/tests/e2e/audit/route-walk.spec.ts` - the walk: certified, uncertified, not-found, navigation label and drawer passes
- `sources/ReactApp/tests/e2e/support/routeFixtures.ts` - minimal per-route API fixtures and the sample id constants
- `sources/ReactApp/src/api/services.ts` - `vereinService.statutes`, `minute` and `myMembershipRequest` return null for the expected none answer
- `sources/ReactApp/src/features/admin/ModPackWorkspacePage.tsx` - empty effective mod list for a mod pack without a revision
- `sources/ReactApp/src/features/admin/ClaimsPage.tsx` - h1 uses the navigation label key

`files_deleted:` none.

## Decisions Made

See `key-decisions` in the frontmatter. The label equality scope (command center only) and the file-based manifest read are the two the verifier should know about. Public images, Overview and Contribution settings keep their recorded exceptions; the test adds a `label-exception` annotation for them.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Verein reads returned undefined to TanStack Query**
- **Found during:** Task 1 (join and verein walk showed "Your membership request could not be loaded")
- **Issue:** `vereinService.myMembershipRequest`, `statutes` and `minute` return `undefined` for an expected 204 or 404. TanStack Query v5 treats an undefined query result as a failed load, so a signed-in applicant without a request saw the error panel on `/join` and `/verein`, and a missing Satzung or minutes document showed the error panel instead of the intended empty state. Unit tests mocked `null`, which hid it.
- **Fix:** the three reads return `?? null`.
- **Files modified:** `sources/ReactApp/src/api/services.ts` (outside the plan's file list; no sibling plan owns it)
- **Verification:** walk for `join`, `verein`, `verein-statutes`, `verein-minute`; `tests/public-form-pages.test.tsx` and the other related Vitest files (258 tests) pass
- **Committed in:** `bc0abf75`

**2. [Rule 1 - Bug] Mod pack without revisions showed an endless spinner**
- **Found during:** Task 1 (mod-pack walk never left the loading state)
- **Issue:** the effective mod list queries are disabled without a revision, and the panel rendered `LoadingState` for the absent result.
- **Fix:** without a revision the panel shows the empty state.
- **Files modified:** `sources/ReactApp/src/features/admin/ModPackWorkspacePage.tsx`
- **Committed in:** `6a619789`

**3. [Rule 1 - Bug] German Claims h1 did not equal its navigation label**
- **Found during:** Task 2 (`/command/claims` in de read "Claim-Berechtigungen", label "Claims")
- **Fix:** both `PageHeader` titles use `Shared_CommandCenter_Nav_Claims`, so the h1 equals the label by construction. The old key `Pages_Command_Claims_HeaderTitle` is now unused and remains in both locale files (a later cleanup).
- **Files modified:** `sources/ReactApp/src/features/admin/ClaimsPage.tsx`
- **Committed in:** `2c654e59`

**4. [Rule 3 - Blocking] node_modules junction and a private Playwright config**
- A junction to the main checkout's `node_modules` was created (no install, lockfile untouched). Because sibling executors share ports 4173 and 4174 and `reuseExistingServer` would have served a stale foreign build, the walk was run with an uncommitted private config on ports 4573 and 4574 that otherwise equals `playwright.config.ts` (preview of a fresh `vite build`). The config was deleted before the final lint. The plan's literal command (`npm run test:e2e -- ... --project=chromium`) uses the standard ports and was not run for this reason.

**Total deviations:** 4 (3 bugs fixed, 1 blocking environment workaround). **Impact on plan:** the defect fixes are small, needed for a green walk and outside no sibling plan's files.

## Cross-plan findings

- **XPF-69-29-1** (recorded in `knownRouteFindings`, annotated in the report): the public page editor of an unknown slug starts from the default body `# New public page`, and the Markdown preview renders it as a second h1 in the not-found pass. Markdown preview headings are a closed non-goal (69-18, 69-23); `PublicPageEditorPage.tsx` is owned by 69-28. With a loaded page whose body starts below the page level heading the editor has one h1.
- **Known from Wave 2, not fixed here:** `NotFoundPage` (`src/components/NotFoundPage.tsx`) renders no h1 (XPF-69-17-3, XPF-69-19-1). No certified route renders it, so the walk does not visit it; the file is outside this plan's scope. Wiki and Markdown content that starts with `# Heading` renders a second h1 (closed non-goal); the wiki and field-report fixtures use bodies that start at level 2. `tests/e2e/public-shell.spec.ts:479` still expects the h1 "Event Administration" while `EventAdminPage.tsx` was retitled to "Events" by 69-16; the new walk asserts the retitled value.
- No hosting defect was found; hosting routes were walked with minimal fixtures (fleet list, workspace with live stream, create, cost alerts, profile list, create and detail, finance overview, account and settings).

## Issues Encountered

- A walk fixture can only show the loaded state of a route when it knows the API shape. Fixtures keep `session.unexpected` empty (strict mode), so a page that later adds a request fails the walk until its fixture answers it. This is intended.
- `tsc -b` and `eslint` were slow under the parallel CPU load but finished with exit code 0.

## Self-Check: PASSED

- `routes.test.ts`, `hosting/routes.test.ts`, `tests/app`, `public-form-pages`, `admin-authority-pages`, `admin-asset-pages`, `admin-mod-packs`, `tests/locales`, `api-client` Vitest files: 258 passed.
- Final spec in chromium (119), mobile-390 (119) and mobile-320 (1): 239 passed, 0 failed, 0 flaky.
- `npx tsc -b` exit 0; `npm run lint` exit 0 (`--max-warnings 0`).
- Acceptance criteria: `routeManifest` 5, `verein-ledger` 1, `navLabelContract` 4, `aria-current` 5, `Beitragseinstellungen` 1 (all at least 1).
- No `src/generated/api.ts` change, no STATE.md or ROADMAP.md edit, no new stylesheet and no `legacy-ratchet.json` entry.
