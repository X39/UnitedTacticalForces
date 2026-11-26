---
phase: 70-ui-polishing
plan: 04
subsystem: ui
tags: [react, button-variants, gold-cta, command-center, vitest, playwright]

requires:
  - phase: 70-ui-polishing
    provides: "70-01 turns variant primary into the neutral fill (this plan reclassifies the main actions beforehand)"
provides:
  - "Gold main call-to-action on 15 former primary sites (fleet, overview, retry, confirm defaults, finance, public pages, roles, terrain wizard, membership approve)"
  - "confirmVariantFor and FinanceDialog default to gold; danger confirmations unchanged"
affects: [70-01, 70-07, 70-14]

plan_head_before: b803c3e9fba5523e4c80c759050f6b8f460d90d0
plan_head_after: 26c9a1ab71e9871acf259949141d1157b90ab179

actuals:
  tokens: 9000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "One gold main action per view; repeated, sibling and secondary actions keep variant primary (neutral fill)"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/FleetPages.tsx
    - sources/ReactApp/src/pages/CommandOverviewPage.tsx
    - sources/ReactApp/src/components/server/JobFailurePanel.tsx
    - sources/ReactApp/src/features/hosting/actionVocabulary.ts
    - sources/ReactApp/src/features/hosting/FinanceDialog.tsx
    - sources/ReactApp/src/features/hosting/HostingFinanceAccountPage.tsx
    - sources/ReactApp/src/features/hosting/HostingFinancePage.tsx
    - sources/ReactApp/src/features/hosting/HostingFinanceSettingsPage.tsx
    - sources/ReactApp/src/features/admin/PublicPageDetailPage.tsx
    - sources/ReactApp/src/features/admin/PublicPageEditorPage.tsx
    - sources/ReactApp/src/features/admin/PublicPagesPage.tsx
    - sources/ReactApp/src/features/admin/RoleDetailPage.tsx
    - sources/ReactApp/src/features/admin/RolesPage.tsx
    - sources/ReactApp/src/features/admin/TerrainCreatePage.tsx
    - sources/ReactApp/src/features/admin/VereinMembershipRequestsPage.tsx

key-decisions:
  - "Followed the classification table exactly; no site was left primary for a visible-gold conflict beyond those the table already lists"
  - "Kept confirmVariantFor's gold fallback as the plan mandates, accepting that it also styles the trigger buttons in WorkspaceHeader, CostAlertBanner and the overview More actions grid (see XPF-70-04-1)"

patterns-established:
  - "Reclassification is a pure variant-prop change: no CSS, no locale, no new component"

requirements-completed: [D-08]

coverage:
  - id: D1
    description: "Create server in the fleet header (link and unavailable button) and the overview header is gold; the empty fleet shows exactly one gold action"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-page.test.tsx"
        status: pass
      - kind: e2e
        ref: "tests/e2e/hosting-fleet.spec.ts --project=chromium (20 passed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Retry, confirmVariantFor default, FinanceDialog default, ledger dialog non-danger confirm and finance header/settings actions are gold; danger branches stay danger"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/hosting/action-vocabulary.test.ts, tests/hosting/actions.test.tsx, tests/hosting/job-failure.test.tsx, tests/hosting/finance-pages.test.tsx, tests/hosting/cost-confirm-dialog.test.tsx"
        status: pass
    human_judgment: false
  - id: D3
    description: "Admin main actions (public pages, role save and create, terrain Next, membership approve) are gold and the former-primary count under src is 26"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/admin- and tests/command-overview (31 files, 276 tests)"
        status: pass
      - kind: other
        ref: "grep -ro 'variant=\"primary\"' sources/ReactApp/src --include=*.tsx | wc -l prints 26; npm run lint and npm run typecheck exit 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "The resulting visual hierarchy (one gold per view, neutral primary elsewhere) reads correctly on the real command center"
    verification: []
    human_judgment: true
    rationale: "Visual hierarchy needs the deferred visual-baseline and real-device review (VER-03, milestone end by standing policy); the 70-01 neutral primary style is not in this branch"

duration: 16min
completed: 2026-11-26
---

# Phase 70 Plan 04: Gold Main Action Reclassification Summary

**The single main call-to-action of 15 former `variant="primary"` sites is now gold (Create server, Retry, confirm defaults, finance and admin main actions), so no view loses its accent when 70-01 turns primary into the neutral fill.**

## Performance

- **Duration:** 16 min
- **Started:** 2026-11-26T01:25:22Z
- **Completed:** 2026-11-26T01:41:41Z
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files modified:** 19 (15 source, 4 tests)

## Accomplishments

- Tracer: fleet header Create server link, the unavailable Create server button and the overview header Create server link are gold. The empty fleet is asserted to render exactly one `.button--gold`. Hosting-fleet e2e (chromium, 20 tests, includes axe) passes.
- Hosting: `JobFailurePanel` Retry is `costStop ? "ghost" : "gold"`; `confirmVariantFor` falls back to gold; `FinanceDialog` defaults to gold; the ledger dialog non-danger branch is gold; Add account and Save policy are gold. The four sibling ledger actions stay primary; reversal and freeze stay danger.
- Admin: Open editor, Save revision (bottom bar), Edit GDPR page, Save role, Create role submit, terrain wizard Next and Approve request are gold. The former-primary literal count under `src` dropped from 38 to 26, as planned.

## Task Commits

1. **Task 1: Tracer, Create server gold** - `8af22e4f` (fix)
2. **Task 2: Hosting surfaces** - `f791da8a` (fix)
3. **Task 3: Admin page main actions** - `26c9a1ab` (fix)

## Deviations from Plan

None - plan executed exactly as written. Tracer gate: `<verify>` re-run end-to-end (vitest and Playwright) passed before expansion.

## Cross-plan Findings

- **XPF-70-04-1:** `confirmVariantFor` is shared. Besides confirm dialogs it also styles the trigger buttons of `WorkspaceHeader` (primary server action), `CostAlertBanner` (stop control) and the `ServerActions` "More actions" grid in `OverviewSection` (`features/hosting/components.tsx:63`). With the gold fallback, every unmapped action in that grid (for example restart, install, update) now renders gold next to the header action, which is more than one gold in that view. The plan mandates the fallback and its tests (`Rotate keys` asserts `button--gold`), so it was applied as written. If the owner wants those grid siblings neutral, give `ServerActions` its own variant (primary except start and danger) instead of the shared helper. Owner: whoever plans the next hosting UI pass or 70-14.
- **XPF-70-04-2:** `PublicPageDetailPage.tsx:124` (empty-state link) stays primary per the table although it is mutually exclusive with the gold header link; the empty-state action is then the only call-to-action in that view and is neutral. Consider gold there if the owner wants the empty state accented.

## Known Stubs

None.

## Threat Flags

None. T-70-05 mitigation held: every danger confirmation (`stop`, `terminate`, reversal, freeze, reject) stays danger; only forward actions turned gold.

## Verification

- `npm --prefix sources/ReactApp test -- tests/hosting/fleet-page.test.tsx tests/hosting/fleet-card.test.tsx tests/hosting/job-failure.test.tsx`: 79 passed.
- Hosting e2e (`tests/e2e/hosting-fleet.spec.ts --project=chromium`, via the serial lock helper): 20 passed.
- Hosting vitest set for Task 2 (action-vocabulary, actions, finance-pages, cost-confirm-dialog, job-failure): 135 passed; `typecheck` clean.
- Full `tests/hosting`: one run showed load-induced 5 s timeouts in unrelated suites (parallel worktrees were running); the affected files (create-server-page, workspace-page, activity-section, automation-consent, automation-schedules) all pass in isolation (123 passed).
- `tests/admin-` and `tests/command-overview`: 31 files, 276 tests passed; `npm run lint` (max-warnings 0) and `npm run typecheck` exit 0.
- Acceptance criteria: gold counts FleetPages 3 and CommandOverviewPage 1; `?? "gold"` 1, `confirmVariant = "gold"` 1, `costStop ? "ghost" : "gold"` 1; remaining `variant="primary"` literals 26; RolesPage gold count 1.

## Issues Encountered

- A tracked-but-unchanged `src/generated/api.ts` shows as modified after the e2e build (line-ending normalization only, empty diff); it was left unstaged and uncommitted.
- A `node_modules` directory junction to the main checkout was created (untracked, gitignored) for running vitest, lint and Playwright.

## Self-Check: PASSED

- Commits `8af22e4f`, `f791da8a`, `26c9a1ab` exist on this branch; `git rev-list --count` from the plan base is 3.
- All 15 modified source files and 4 modified test files exist and carry the described variant changes.
