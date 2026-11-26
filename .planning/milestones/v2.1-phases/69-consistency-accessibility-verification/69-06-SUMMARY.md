---
phase: 69-consistency-accessibility-verification
plan: 06
subsystem: testing
tags: [playwright, axe-core, wcag22, keyboard, reflow, screen-registry, vitest, accessibility]

requires:
  - phase: 68-server-automation
    provides: "ScheduledStartConsentDialog and tests/e2e/support/automationMocks.ts (precondition of this plan)"
provides:
  - "tests/e2e/support/mockApi.ts: installApp, jsonHandler, ApiHandler, MockSession (one strict mock layer, unexpected and served request records)"
  - "tests/e2e/support/a11y.ts: scanAxe, walkKeyboard, checkReflow, checkTextSpacing, checkHeadings, checkReducedMotion, checkZoomReflow, checkDialogGeometry, checkStickyCoverage, plus collect* variants and collectFocusRings"
  - "Screen registry: registryTypes.ts (types, requiredRegistry manifest, sliceOf), three slice modules, serverScreens.ts"
  - "tests/e2e/audit/a11y-audit.spec.ts: registry-driven audit, one test per entry, culture and project, titled with its slice tag"
  - "tests/hosting/screen-registry.test.ts: Vitest completeness guard"
  - "tests/e2e/audit/a11y-discovery.spec.ts: opt-in discovery pass (UTF_A11Y_DISCOVERY=1)"
affects: [69-08, 69-09, 69-10, 69-11, 69-12, 69-24, 69-25, 69-26, 69-27, 69-28, 69-31, 69-33]

actuals:
  tokens: 25113
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Strict mock layer: handler chain, then record and abort; every audit asserts unexpected is empty"
    - "Registry entries group their states on one page load; slice modules hold entries plus a pending list"
    - "collect* helpers return findings, check* helpers throw them, so discovery and gating share one measurement"
    - "Known findings subtract by rule and target and are annotated on the test (axe and keyboard)"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/mockApi.ts
    - sources/ReactApp/tests/e2e/support/a11y.ts
    - sources/ReactApp/tests/e2e/support/serverScreens.ts
    - sources/ReactApp/tests/e2e/support/screens/registryTypes.ts
    - sources/ReactApp/tests/e2e/support/screens/fleetScreens.ts
    - sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts
    - sources/ReactApp/tests/e2e/support/screens/adjacentScreens.ts
    - sources/ReactApp/tests/e2e/audit/a11y-audit.spec.ts
    - sources/ReactApp/tests/e2e/audit/a11y-helpers.spec.ts
    - sources/ReactApp/tests/e2e/audit/a11y-discovery.spec.ts
    - sources/ReactApp/tests/hosting/screen-registry.test.ts
  modified: []

key-decisions:
  - "Registry ids not named by the plan: notices N-01 to N-11 and smoke entries M-01 to M-04 (mapping in the registryTypes.ts comments); Wave 3 plans must use them"
  - "Multi-route screens (S-11, S-13) carry alsoCovers so every hosting route pattern is covered by exactly one required screen"
  - "Keyboard findings can be recorded as known findings (rule focus-ring, target tag plus first two classes), the same mechanism as axe"
  - "A native modal hands focus to browser UI after its last control; the trap check fails only when focus lands on a page element outside the dialog"

patterns-established:
  - "Slice tags: audit tests end in @fleet, @workspace or @adjacent so a slice plan runs --grep @<slice>"
  - "Specs never import src at runtime; the discovery pass reads route and tab lists from the app source as text"

requirements-completed: [FND-06, VER-02, VER-03]

coverage:
  - id: D1
    description: "Strict shared mock layer: known paths answered, every other API request aborted and recorded, audits assert the record is empty"
    requirement: VER-02
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts (S-02, expect(session.unexpected).toEqual([]))"
        status: pass
    human_judgment: false
  - id: D2
    description: "Audit helpers (axe wcag22aa with non-vacuity and known findings, keyboard walk, reflow, text spacing, headings, reduced motion, zoom, dialog geometry, sticky coverage), each proven to fail on a broken page"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-helpers.spec.ts (9 tests, chromium)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Fleet screen S-02 audited from the registry in chromium, mobile-390 and mobile-320, in English and German"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "npm --prefix sources/ReactApp run test:e2e -- tests/e2e/audit/a11y-audit.spec.ts (chromium); three-project run with a temporary uncommitted config: 6 passed"
        status: pass
    human_judgment: false
  - id: D4
    description: "Registry completeness guard for hosting routes, workspace tabs and required overlay, notice and smoke ids"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts (10 tests; mutation check removing S-01 from fleetPending fails naming S-01)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Opt-in discovery pass and the findings table for Wave 2"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "UTF_A11Y_DISCOVERY=1 run, 120 passed in three projects (temporary config); playwright --list shows 40 tests per project"
        status: pass
    human_judgment: false

duration: ~90min
completed: 2026-11-25
status: complete
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: 9ef9e4faf3d164fa1f3b6288b9530b5215093a64
commits: 3
---

# Phase 69 Plan 06: Verification backbone Summary

**One strict API mock layer, nine audit helpers with a self-test, a required-id screen registry with a Vitest completeness guard, a registry-driven audit proven on the fleet screen in three projects and two languages, and an opt-in discovery pass whose findings table steers Wave 2.**

## Performance

- **Duration:** about 90 min (start time was not captured; estimated)
- **Completed:** 2026-11-25T11:23:24Z (last task commit)
- **Tasks:** 3 (tracer, guard, discovery)
- **Files created:** 11 (test-only; no source or existing spec file changed)

## Accomplishments

- `installApp` answers the signed-in user, authentication providers, site image settings and the quiet cost and notification reads, runs a caller handler chain first, and aborts and records everything else (`permissive` answers 404 and records in `served`). A handler that throws is recorded instead of hanging the request.
- `a11y.ts` runs axe at wcag2a, wcag2aa, wcag21a, wcag21aa and wcag22aa with a non-vacuity guard (passed rules, include selector matched) and known-finding subtraction by rule and target with a test annotation. The keyboard walk checks the skip link as first stop that moves focus to the landmark, DOM-order reach of every tabbable element of `main` or the open modal, no positive tabindex, no focus on hidden, inert, aria-hidden or covered elements, a focus ring of at least 2px in gold-bright with 3:1 contrast (an ancestor `:focus-within` ring counts), Shift+Tab reversal, modal focus trap and Escape returning focus. Reflow, text spacing, headings, reduced motion, 200 percent zoom, dialog geometry and sticky-bar coverage exist as `check*` (throwing) and `collect*` (non-throwing) pairs.
- The registry holds `requiredRegistry` (S-01 to S-13, O-01 to O-09, N-01 to N-11, M-01 to M-04), the S-02 populated entry (running, off, starting, attention and unconfirmed servers) and an explicit pending list per slice. `a11y-audit.spec.ts` runs each entry once per culture and project, titled `<id> (<culture>) @<slice>`.
- `screen-registry.test.ts` fails, naming the id, when a hosting route pattern, workspace tab or required id has no entry and no pending declaration, has both, is declared outside its slice, or is stray.
- Tracer verified end-to-end before expansion: S-02 passes in chromium, mobile-390 and mobile-320 in en and de.

## Task Commits

1. **Task 1: Tracer, fleet screen through the shared mock layer and the registry** - `1d11c6ea` (test)
2. **Task 2: Registry completeness guard** - `e55a989a` (test)
3. **Task 3: Opt-in discovery pass** - `9ef9e4fa` (test)

## Files Created/Modified

- `sources/ReactApp/tests/e2e/support/mockApi.ts` - strict mock layer
- `sources/ReactApp/tests/e2e/support/a11y.ts` - audit helpers
- `sources/ReactApp/tests/e2e/support/screens/registryTypes.ts` - types, required manifest, `sliceOf`
- `sources/ReactApp/tests/e2e/support/screens/fleetScreens.ts` - S-02 entry and fleet pending list
- `sources/ReactApp/tests/e2e/support/screens/workspaceScreens.ts`, `adjacentScreens.ts` - empty slices with pending lists
- `sources/ReactApp/tests/e2e/support/serverScreens.ts` - aggregate registry
- `sources/ReactApp/tests/e2e/audit/a11y-audit.spec.ts` - registry-driven audit
- `sources/ReactApp/tests/e2e/audit/a11y-helpers.spec.ts` - helper self-test (not listed in the plan)
- `sources/ReactApp/tests/e2e/audit/a11y-discovery.spec.ts` - opt-in discovery pass
- `sources/ReactApp/tests/hosting/screen-registry.test.ts` - completeness guard

## Decisions Made

- Notice ids N-01 to N-04 (fleet: live, deep-link action, create start, execution paused) and N-05 to N-11 (workspace: archived, pending start, cost alert banner, automatic shutdown, consent needed, job failure, job progress), and smoke ids M-01 to M-04 (members list, role detail, public page editor, home) are this plan's naming; the plan only listed the names.
- `alsoCovers` on S-11 and S-13 (additive field on `ScreenEntry` and `RequiredRegistryItem`) because one required id covers several hosting route patterns.
- The audit picks its checks by project name (`mobile-*` runs the phone set), so it depends on the project names that plan 69-05 defines.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] Helper self-test spec**
- **Found during:** Task 1
- **Issue:** Every audit relies on `a11y.ts`; without proof that each helper fails on a broken page, a silently permissive helper would make every audit green. Probing showed real behavior traps (smooth scrolling, mid-transition drawer, sequential focus start point, native modal focus hand-off).
- **Fix:** Added `tests/e2e/audit/a11y-helpers.spec.ts` (9 tests on hand-built pages, skipped in mobile projects because the pages carry no viewport meta tag).
- **Files modified:** `sources/ReactApp/tests/e2e/audit/a11y-helpers.spec.ts`
- **Commit:** `1d11c6ea`

**2. [Rule 3 - Blocking] Keyboard known findings and `alsoCovers`**
- **Found during:** Task 1 and the registry design
- **Issue:** The S-02 keyboard walk found a real defect in a file this plan may not edit (the translucent primary button focus ring in `commandCenter.css`, owned by 69-08); `KnownFinding` only applied to axe. Separately, one required id (S-11, S-13) covers several route patterns, which the single `routePattern` field could not express.
- **Fix:** `walkKeyboard` accepts `knownFindings` matched by rule (`focus-ring`) and element selector, with a test annotation; S-02 records `XPF-06-01`. Added optional `alsoCovers`.
- **Files modified:** `a11y.ts`, `registryTypes.ts`, `fleetScreens.ts`, `a11y-audit.spec.ts`
- **Commit:** `1d11c6ea`

**3. [Rule 1 - Bug] Measurement races in my own helpers**
- **Found during:** Task 1
- **Issue:** Focused elements were reported "outside the viewport" while smooth scrolling still ran; the zoom check saw the navigation drawer mid-transition; the `sr-only` label counted as clipped text.
- **Fix:** Wait for scroll to settle and for finite animations to end; ignore 1px visually hidden boxes in the text-spacing check.
- **Files modified:** `a11y.ts`
- **Commit:** `1d11c6ea`

**Total deviations:** 3 auto-fixed (1 missing critical, 1 blocking, 1 bug). **Impact:** additive test-support code only; no existing spec or source file touched.

### Verification environment notes (not deviations)

- The committed `playwright.config.ts` still has only the `chromium` project (the mobile projects arrive with plan 69-05, parallel wave). The official verify command therefore ran chromium only. The mobile-390 and mobile-320 behavior was verified with a temporary, uncommitted three-project config on its own port, removed afterwards.
- `node_modules` is not present in a fresh worktree; a directory junction to the main checkout's `node_modules` was used and removed afterwards. No package was installed.
- `npm run build:assets` regenerates `src/generated/api.ts` with different line endings on this machine; it was restored with `git checkout` and is not part of any commit.

## Discovery findings

Discovery run: `UTF_A11Y_DISCOVERY=1`, 19 pages (11 hosting routes, 4 workspace tabs, 4 smoke routes) x 2 cultures x 3 projects = 114 page visits plus 6 housekeeping tests, 120 passed. No page threw and no page error was raised. Raw data: `test-results/a11y-discovery-<project>.json` (git-ignored; rerun to regenerate).

Scope caveat: only the fleet list and the server workspace carry fixtures. Every other page was answered by the permissive fallback (404), so Tier B and C pages were audited in their error or empty state, and no row of populated tables, charts or forms was measured. The Wave 3 gating audits are the first to see those; the zero counts below for reflow, text spacing, contrast and target size are therefore weak evidence for the pages outside the fleet and workspace. The focus ring sample covers the first ten tab stops from the skip link; on desktop these are navigation links, so desktop rows describe the shell, and the phone projects reach page content sooner.

| Rule | Route | Target selector | Likely source file | Owning plan |
|------|-------|-----------------|--------------------|-------------|
| axe `aria-required-children` (serious) | `/command/servers/{id}?tab=files` (desktop layout, en and de) | `.next-hosting-file-tree` | `ServerFilesPanel.tsx:668` (`ul role="tree"` that holds only a state item) | 69-10 |
| axe `listitem` (serious) | `/command/servers/{id}?tab=files` (desktop layout, en and de) | `.next-hosting-tree-state` | `ServerFilesPanel.tsx:1207-1214` (state `li` outside a list) | 69-10 |
| heading: no visible `h1` | `/command/servers/cost-alerts` (all projects, en and de) | document | `CostAlertsPage.tsx` (state panel shown before `PageHeader`; research F12) | 69-12 |
| heading: no visible `h1` | `/command/configuration-profiles/{ProfileId}` (all projects, en and de) | document | `ConfigurationProfileWorkspacePage.tsx` | 69-11 |
| heading: no visible `h1` | `/command/hosting-finance/accounts/{CostOwnerId}` (all projects, en and de) | document | `HostingFinanceAccountPage.tsx` | 69-12 |
| heading: no visible `h1` | smoke `/command/content/pages/home/edit` (all projects, en and de) | document | `PublicPageEditorPage.tsx` | 69-18 |
| focus ring: translucent `rgba(229, 201, 111, 0.58)` outline, not gold-bright | 18 command pages (chromium), 294 stops | `a`, `a.active` (command navigation links) | `commandCenter.css` (`.command-shell` focus rules, lines 14-19) | 69-08 |
| focus ring: same translucent outline | `/command`, `/command/servers`, `/command/hosting-finance` (all projects) | `a.button.button--primary`, `button.button.button--primary` | `commandCenter.css:14-19`; S-02 records `XPF-06-01` until fixed | 69-08 |
| focus ring: same translucent outline | `/command` (all projects, 4 cards) | `a.command-work-area` | `commandCenter.css`, `CommandOverviewPage.tsx` | 69-08 |
| focus ring: outline `rgb(232, 220, 178)` not gold-bright (border and background change on focus) | every command page (phone projects) | `button.account-profile__trigger` | `ProfileMenu.css:22-26` | 69-08 |
| focus ring: 1px outline, below 2px | `/command/hosting-finance`, `/command/hosting-finance/settings` (phone projects) | `input` | `styles.css:3936-3940` (`.field input:focus`) reached by raw inputs in `HostingFinancePage.tsx` and `HostingFinanceSettingsPage.tsx` | 69-12 (the `styles.css` rule itself belongs to 69-01 or the closure plan) |
| axe `target-size`, `color-contrast`, reflow, text spacing | all 19 pages | none | none | no findings (see the scope caveat) |

The `XPF-06-01` known finding in `fleetScreens.ts` (rule `focus-ring`, target `button.button.button--primary`) must be removed by 69-08 together with its fix. The audit does not detect a stale known finding, so removing the entry is the only way the defect becomes guarded again.

## Issues Encountered

- None blocking. Playwright was shared with other parallel agents on the same machine; runs used a private port for the three-project verification.

## Next Phase Readiness

- Wave 2 fix plans can start from the findings table above.
- Wave 3 slice plans add entries to `fleetScreens.ts`, `workspaceScreens.ts` and `adjacentScreens.ts` and remove ids from the `*Pending` lists; plan 69-33 adds the rule that no pending id may remain.
- Plan 69-05 must define the project names `chromium`, `mobile-390` and `mobile-320`; the audit spec and the helper self-test rely on `mobile-*` and `isMobile`.

## Known Stubs

None. The empty `workspaceScreens` and `adjacentScreens` arrays are the declared empty slices, with every owed id listed in `workspacePending` and `adjacentPending` and enforced by the Vitest guard.

## Threat Flags

None. Test-only code; unmatched API requests are aborted and recorded (T-69-08), fixtures use synthetic ids and names only (T-69-09), no package was installed (T-69-SC).

## Self-Check: PASSED

- All 11 created files exist on disk.
- Commits `1d11c6ea`, `e55a989a` and `9ef9e4fa` are ancestors of HEAD.
- Plan verification: `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/audit/a11y-audit.spec.ts` (2 passed), `npm --prefix sources/ReactApp test -- tests/hosting/screen-registry.test.ts` (10 passed), `npm --prefix sources/ReactApp run lint` and `run typecheck` clean, `playwright test tests/e2e/audit/a11y-discovery.spec.ts --list` lists the discovery tests.
- Acceptance criteria: `wcag22aa` in `a11y.ts` (2), `"S-02"` in `fleetScreens.ts` (1), `unexpected` in `a11y-audit.spec.ts` (1), existing `hosting-a11y.spec.ts` tag list unchanged (1), `hostingRouteDefinitions` and `workspaceTabIds` in the guard, `UTF_A11Y_DISCOVERY` in the discovery spec (3), `## Discovery findings` present with an owner per row.
