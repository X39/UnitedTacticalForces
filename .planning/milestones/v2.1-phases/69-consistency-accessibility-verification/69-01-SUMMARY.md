---
phase: 69-consistency-accessibility-verification
plan: 01
subsystem: ui
tags: [react, css, design-tokens, accessibility, style-guard, ratchet]

requires:
  - phase: 61-design-foundation
    provides: "--utf-* tokens, state tone tokens, contrast guard, server style guard"
provides:
  - "src/components/shared-primitives.css: token-only page header, section heading, page stack, state panel, status badge and dialog shell"
  - "PageStack wrapper component"
  - "--utf-measure and --utf-scrim tokens"
  - "--quiet corrected to 4.5:1 on every surface, guarded by tokens-contrast.test.ts"
  - "findStyleViolations outline-removal and viewport-relative-type rules, plus collectStyleRules, findOutlineRemovals, measureLegacyCss"
  - "legacy-ratchet.json and legacy-ratchet.test.ts holding raw hex, off-grid px and outline-removal counts per legacy stylesheet"
affects: [69-02, 69-08, 69-24, 69-33, every Wave 2 page conversion]

actuals:
  tokens: 12000
  tasks: 3
  commits: 4
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: 7e9325765085bd3a085116a08d8b7402e05fbf7d

tech-stack:
  added: []
  patterns:
    - "Shared primitive CSS lives in src/components/shared-primitives.css, tokens only, scanned by the style guard"
    - "Legacy CSS is held by a per-file ceiling ratchet that recomputes counts in the test"
    - "State panel tone is one local custom property (--state-panel-tone) fed from --utf-state-{tone}-fg"

key-files:
  created:
    - sources/ReactApp/src/components/shared-primitives.css
    - sources/ReactApp/src/components/PageStack.tsx
    - sources/ReactApp/tests/page-stack.test.tsx
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/styles/legacy-ratchet.test.ts
  modified:
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/src/styles.css
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/helpers/cssGuard.ts
    - sources/ReactApp/tests/primitives.test.tsx
    - sources/ReactApp/tests/styles/server-style-guard.test.ts
    - sources/ReactApp/tests/styles/tokens-contrast.test.ts

key-decisions:
  - "PageHeader h1 is Display 28/700; h2 and h3 header levels use the Heading step (20/700), so only the h1 reaches Display"
  - "Section heading h2 uses the Heading step with no clamp, per the no-viewport-type rule"
  - "The dialog width rule is wrapped in :where() so page dialogs that set their own width (finance, file dialogs) keep it in any load order"
  - "Badge keeps uppercase and 0.08em tracking so the approved server badge look does not change"
  - "tokens.css is excluded from the legacy ratchet because raw values are the point of that file"

patterns-established:
  - "Style guard gate: raw hex, colour functions, px literals, viewport-relative type and unreplaced outline removals fail in shared-primitives.css and components/server/*.css"
  - "Ratchet: ceilings may only go down; a stylesheet missing from the JSON fails the test"

requirements-completed: [CON-01, CON-02, CON-03, FND-06]

coverage:
  - id: D1
    description: "PageHeader h1 renders at the Display token (1.75rem, 700, 1.2, not uppercase) from a guarded token-only stylesheet"
    requirement: "CON-01"
    verification:
      - kind: unit
        ref: "tests/primitives.test.tsx#PageHeader display typography (CON-01)"
        status: pass
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps shared-primitives.css free of raw colours, pixel literals and viewport type"
        status: pass
    human_judgment: false
  - id: D2
    description: "StatePanel and StatusBadge read the --utf-state-* tokens; PageStack wraps page bodies; --quiet passes 4.5:1 on all four surfaces"
    requirement: "CON-02"
    verification:
      - kind: unit
        ref: "tests/primitives.test.tsx#StatusBadge and StatePanel on state tokens (D-05, CON-02)"
        status: pass
      - kind: unit
        ref: "tests/page-stack.test.tsx"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#keeps the legacy --quiet text colour at 4.5:1 on the canvas and all three surfaces"
        status: pass
    human_judgment: false
  - id: D3
    description: "ConfirmDialog shell geometry (one content column, right-aligned actions above 520px, stacked below) and the TextField challenge"
    requirement: "CON-03"
    verification:
      - kind: unit
        ref: "tests/primitives.test.tsx#dialog shell geometry (D-10)"
        status: pass
      - kind: unit
        ref: "tests/primitives.test.tsx#ConfirmDialog additive capabilities"
        status: pass
    human_judgment: true
    rationale: "jsdom has no layout; the rendered left edge, stacked order and 44px targets are asserted in a real browser by plan 69-24 and judged visually in the milestone verification."
  - id: D4
    description: "No outline removal remains in styles.css and legacy stylesheets are held by a ratchet"
    requirement: "FND-06"
    verification:
      - kind: unit
        ref: "tests/styles/legacy-ratchet.test.ts"
        status: pass
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#flags an outline removal that keeps no replacement ring in the same rule"
        status: pass
    human_judgment: false

duration: 31min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 01: Shared Primitive Normalization Summary

**Page header at the Display 28/700 token, state panel and badge on `--utf-state-*` tokens, one dialog shell geometry and a PageStack wrapper, all in a token-only `shared-primitives.css` that the style guard scans, plus a corrected `--quiet` and a ratchet over every legacy stylesheet.**

## Performance

- **Duration:** about 31 min
- **Started:** 2026-11-25T10:38Z (approximate; the start time was not captured at spawn)
- **Completed:** 2026-11-25T11:09Z
- **Tasks:** 3 (1 tracer, 2 auto TDD)
- **Files modified:** 12 (5 created, 7 modified)

## Accomplishments

- Moved the page header, section heading, state panel, status badge and dialog rules out of `styles.css` into `src/components/shared-primitives.css`, expressed only in `--utf-*` tokens. `styles.css` has 0 top-level `.page-header`, `.section-heading`, `.state-panel`, `.status-badge` and `.dialog` selectors left.
- PageHeader h1 now renders at 1.75rem, weight 700, line height 1.2, not uppercase, `overflow-wrap: anywhere`, with the description capped at the new `--utf-measure` (45rem) token.
- StatePanel is a raised surface with a 1px divider border and a 4px tone stripe from one local `--state-panel-tone` property; StatusBadge colors come from `--utf-state-{tone}-fg|bg|border`; DOM, props and roles are unchanged.
- `--quiet` changed from `#778190` to `#8b97a7`; `tokens-contrast.test.ts` measures it on canvas and all three surfaces and proves the old value fails on raised and overlay.
- ConfirmDialog: one content column for kicker, title, description, body, challenge field, error slot and actions; the challenge is a `TextField` whose hint is the input description; initial focus on dismiss, busy spinner and the `role="alert"` error slot are unchanged and re-tested.
- Style guard now also fails on viewport-relative font sizes and on an outline removal without a `box-shadow` or `outline-offset` ring in the same rule. The three `styles.css` outline removals (search field input, media gallery grid button, event role symbol picker summary) now use `--utf-focus-ring` with `--utf-focus-offset`.
- `legacy-ratchet.json` records measured ceilings for 12 legacy stylesheets (for example `styles.css` hex 53, off-grid px 147, outline removals 0); `legacy-ratchet.test.ts` recomputes and fails on any increase or on a stylesheet missing from the JSON.

## Task Commits

1. **Task 1: Tracer - PageHeader at Display 28/700 from a guarded token-only stylesheet** - `3d0e157b` (feat)
2. **Task 2: State panel, status badge and PageStack on state tokens, quiet contrast fix** - `8a3233b2` (feat)
3. **Task 3: One dialog shell geometry, outline audit and the legacy CSS ratchet** - `9877fcf0` (feat, dialog shell, outline fixes, guard rule) and `7e932576` (test, ratchet)

**Plan metadata:** the SUMMARY commit that follows these four (docs).

## Files Created/Modified

- `sources/ReactApp/src/components/shared-primitives.css` - token-only page header, section heading, page stack, state panel, status badge and dialog shell
- `sources/ReactApp/src/components/PageStack.tsx` - `div.page-stack` wrapper that also loads the shared sheet
- `sources/ReactApp/src/components/primitives.tsx` - side-effect stylesheet import; challenge input replaced by `TextField`
- `sources/ReactApp/src/styles.css` - moved blocks removed, `--quiet` value, three outline fixes
- `sources/ReactApp/src/styles/tokens.css` - `--utf-measure` and `--utf-scrim`
- `sources/ReactApp/tests/helpers/cssGuard.ts` - outline rule, viewport-type rule, rule collector, legacy measurement
- `sources/ReactApp/tests/styles/*` - guard cases, contrast cases, legacy ratchet
- `sources/ReactApp/tests/primitives.test.tsx`, `tests/page-stack.test.tsx` - header, state, badge, dialog and PageStack assertions

## Decisions Made

- h2 and h3 `PageHeader` levels use the Heading step (20/700); only the h1 is Display. The UI-SPEC fixes the h1 only; four sizes exist and a Display h2 would out-weigh the page title.
- `.section-heading h2` uses the Heading step; the legacy clamp is not allowed in shared primitives.
- The dialog width sits in `:where(.dialog)` (zero specificity) so `.finance-dialog` and `.next-hosting-file-dialog` keep their own widths regardless of stylesheet order.
- The badge keeps uppercase and 0.08em tracking because `ServerStateBadge` inherited both from the legacy badge and was approved in Phases 61 to 68.
- `styles/tokens.css` is excluded from the ratchet (its raw values are the token source).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Dialog backdrop colour needed a token**
- **Found during:** Task 3
- **Issue:** The moved `.dialog::backdrop` used `rgb(2 4 7 / 76%)` and `blur(5px)`; the style guard rejects colour functions and px literals in `shared-primitives.css`, and `^\.dialog` had to reach 0 in `styles.css`.
- **Fix:** Added `--utf-scrim: rgb(2 4 7 / 76%)` to `tokens.css` (the same value, relocated, not a new colour) and used `blur(var(--utf-space-xs))` (4px instead of 5px).
- **Files modified:** `src/styles/tokens.css`, `src/components/shared-primitives.css`
- **Commit:** `9877fcf0`

**2. [Rule 3 - Blocking] Dependencies for verification in the worktree**
- **Found during:** Start of Task 1
- **Issue:** The worktree had no `node_modules`; no package may be installed (D-18).
- **Fix:** Created an untracked directory junction `sources/ReactApp/node_modules` to the main checkout's installed modules (ignored by git, no install, no lockfile change).
- **Commit:** none

**3. [Rule 2 - Missing critical] PageStack loads its own stylesheet**
- **Found during:** Task 2
- **Issue:** A page using `PageStack` without any `primitives.tsx` export would not load `shared-primitives.css`.
- **Fix:** `PageStack.tsx` also side-effect imports the sheet.
- **Commit:** `8a3233b2`

**4. [Rule 1 - Bug] Unused `helper` id after the TextField swap**
- **Found during:** Task 3
- **Issue:** `ids.helper` no longer had a consumer once the hint moved into `TextField`.
- **Fix:** Removed it from `ConfirmDialogForm` props and `ConfirmDialog`.
- **Commit:** `9877fcf0`

**Total deviations:** 4 (2 blocking, 1 missing critical, 1 cleanup). **Impact:** none on scope; the plan's artifacts and acceptance criteria are met.

### Other notes

- GPG signing timed out, so all four commits are unsigned (standing project rule).
- The plan's single Task 3 commit pair was kept as two commits as written; the guard changes ride with the dialog commit because the new `server-style-guard.test.ts` cases and `cssGuard.ts` are shared by both.
- Full-suite run: 2658 of 2660 tests passed on a machine shared with other agents; the 2 failures were wall-clock budgets in `tests/hosting/metrics-performance.test.tsx` and earlier 5000 ms timeouts in `workspace-header` and `workspace-page` tests, all of which pass in isolation (`workspace-*` with `--testTimeout=30000`, `metrics-performance` unchanged). No CSS change touches them.

## Cross-plan findings

- **XPF-69-01-1** - `sources/ReactApp/src/components/commandCenter.css`, lines 28-30: `.command-shell .page-header h1 { font-size: clamp(2rem, 3vw, 2.75rem); }` has higher specificity than `.page-header h1` in `shared-primitives.css`, so every command center page keeps a viewport-relative title size and the Display 28px rule does not hold there. Proposed fix (owner 69-08): delete the rule so the shared Display token applies.
- **XPF-69-01-2** - `sources/ReactApp/src/components/commandCenter.css`, lines 22-26 and 267-271: `.command-shell .page-header` sets `align-items: flex-start`, a bottom border and `padding-bottom: 1rem`, and a `max-width: 780px` stack rule that duplicates the shared 800px stack. Proposed fix (owner 69-08): keep only the border and padding in tokens, drop the 780px duplicate.
- **XPF-69-01-3** - `sources/ReactApp/src/components/server/server.css` (`.dialog.dialog--cost .field`, `.dialog__helper`) and `change.css`: after the TextField swap the `.field` and `.dialog__helper` rules for the cost dialog challenge no longer match the challenge markup (`.form-field`); the doubled `.dialog.dialog--*` shell duplicates (width, form padding, actions stacking) can be reduced to the genuinely variant-specific rules. Proposed fix (owner of the server CSS in 69-33 or a server family plan): delete the dead selectors and duplicates.
- **XPF-69-01-4** - `sources/ReactApp/src/features/hosting/finance.css` line 51, `features/hosting/hosting.css` around line 1836 and `features/wiki/wiki.css` (6 declarations): outline removals recorded in `legacy-ratchet.json` with `outlineRemoval` above 0 (finance 1, hosting 1, wiki 6). Proposed fix (owner of each family plan): replace with the `--utf-focus-ring` token and 2px offset and lower the ceiling.

## Known Stubs

None.

## Threat Flags

None. No new network surface, auth path, file access or schema change. T-69-01 (accidental confirm) is mitigated: initial focus stays on the dismiss control and the unit tests `can place initial focus on the dismiss button` and `keeps the dialog open, announces the error and leaves focus on the dismiss control` pass after the shell change.

## Verification

- `npm --prefix sources/ReactApp test -- tests/styles tests/primitives.test.tsx tests/page-stack.test.tsx`: 175 passed.
- `npm --prefix sources/ReactApp run lint`: clean (max-warnings 0).
- `npm --prefix sources/ReactApp run typecheck`: clean.
- Acceptance criteria re-run: `shared-primitives.css` present; `^\.page-header`, `^\.state-panel`, `^\.dialog` and `outline: none|0` counts in `styles.css` are 0; `--quiet: #778190` count 0; `--utf-measure` in `tokens.css` 1; `export function PageStack` 1; `legacy-ratchet.json` present; `outline` in `cssGuard.ts` 15; `shared-primitives` in the guard test 3.
- Tracer gate: tracer `<verify>` re-run end to end in auto mode; passed, expansion continued.

## Self-Check: PASSED

All created files exist (`shared-primitives.css`, `PageStack.tsx`, `page-stack.test.tsx`, `legacy-ratchet.json`, `legacy-ratchet.test.ts`) and commits `3d0e157b`, `8a3233b2`, `9877fcf0`, `7e932576` are ancestors of HEAD.
