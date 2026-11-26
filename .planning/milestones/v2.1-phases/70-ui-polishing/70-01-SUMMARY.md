---
phase: 70-ui-polishing
plan: 01
subsystem: ui
tags: [react, css, design-tokens, buttons, wcag, vitest, playwright]

requires:
  - phase: 69-ui-consistency
    provides: guarded token-only stylesheets, the style guard and the legacy ratchet
provides:
  - one guarded token-only stylesheet for the five button variants and the compact size
  - the --utf-hover layer and every Phase 70 control dimension token
  - contrast assertions for the button pairs and the control state table
  - a phone-width touch-target spec for the workspace details Refresh button
affects: [70-02, 70-03, 70-04, 70-05, 70-06, 70-07, 70-08, 70-09, 70-15]

actuals:
  tokens: 6200
  tasks: 3
  commits: 3
plan_head_before: b803c3e9fba5523e4c80c759050f6b8f460d90d0
plan_head_after: 530f04633b097bf3b99d85b4e95d5153859ae7ee

tech-stack:
  added: []
  patterns:
    - "Hover fills go through one --utf-hover layer painted as background-image linear-gradient so it composes with each variant fill"
    - "Component stylesheets imported by their component and registered in the style guard and the ratchet isGuarded list"

key-files:
  created:
    - sources/ReactApp/src/components/action-control.css
    - sources/ReactApp/tests/e2e/audit/control-target-size.spec.ts
  modified:
    - sources/ReactApp/src/components/ActionControl.tsx
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/src/styles.css
    - sources/ReactApp/src/components/commandCenter.css
    - sources/ReactApp/src/components/server/automation.css
    - sources/ReactApp/tests/styles/server-style-guard.test.ts
    - sources/ReactApp/tests/styles/legacy-ratchet.test.ts
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/styles/button-wrap.test.ts
    - sources/ReactApp/tests/styles/tokens-contrast.test.ts

key-decisions:
  - "Danger hover changes only the border colour and keeps the 12% tint; a 20% tint measures below 4.5:1 on the overlay surface (C-04, kept live as a negative sample in tokens-contrast.test.ts)"
  - "The hover layer is a rule list naming primary and ghost explicitly plus the default button, so the ghost-hover-has-no-gold guard sees a real rule instead of passing vacuously"
  - "Button label is Label 12/700 (--utf-text-label) instead of the legacy 0.84rem, so every button label shrinks slightly"
  - "The weekday chip token was renamed to --utf-day-chip-min at 3rem, which narrows the automation day grid cells until 70-08 rewrites that grid"

patterns-established:
  - "Guarded button sheet: action-control.css is imported by ActionControl.tsx before styles.css loads, so page sheets with equal specificity still win"

requirements-completed: [D-01, D-08, D-16, D-17, D-18]

coverage:
  - id: D1
    description: "Five button variants and the compact size defined once in the guarded token-only action-control.css; the legacy button rules and the command-shell gold primary override are gone"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps action-control.css free of raw colours, pixel literals and viewport type"
        status: pass
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps gold out of every ghost hover rule"
        status: pass
      - kind: unit
        ref: "tests/styles/button-wrap.test.ts#lets the label wrap like the surrounding text and breaks an overlong word"
        status: pass
      - kind: e2e
        ref: "tests/e2e/audit/dialog-geometry.spec.ts --project=chromium"
        status: pass
    human_judgment: true
    rationale: "Whether the finished hierarchy (visible neutral primary, 3:1 ghost border, tinted danger outline) reads well is a visual judgment; real-device and visual-baseline checks are deferred to milestone end"
  - id: D2
    description: "Button text pairs and the control state table pairs meet 4.5:1 text and 3:1 non-text contrast, with the Disabled row excluded for the stated reason"
    requirement: "D-18"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#button variant contrast"
        status: pass
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#control state contrast"
        status: pass
    human_judgment: false
  - id: D3
    description: "The workspace details Refresh button measures at least 44px high at 390px and 320px"
    requirement: "D-16"
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/control-target-size.spec.ts --project=mobile-390 --project=mobile-320"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every Phase 70 control dimension token exists in tokens.css and the old automation day token no longer exists in src"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "tests/styles/tokens-contrast.test.ts#defines every token the UI contract lists"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-26
status: complete
---

# Phase 70 Plan 01: Button hierarchy and Phase 70 control tokens Summary

**Five button variants in one guarded token-only stylesheet with a neutral --utf-hover layer, all Phase 70 control dimension tokens, and contrast plus touch-target assertions**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-26T01:47:46Z
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments

- `action-control.css` now owns `.button` and the variants default, primary, ghost, gold, glass and danger plus the small, full-width and icon-only sizes. It uses `--utf-*` tokens only and is imported by `ActionControl.tsx`. The style guard scans it, the ratchet treats it as guarded and `button-wrap.test.ts` reads its `.button` rule.
- The 14 legacy button rules left `styles.css`, the gold footer hover was dropped for `.site-footer button`, and the `.command-shell .button--primary` gold gradient, its hover, its focus-visible entry and its reduced-motion override left `commandCenter.css`. The ratchet ceilings were lowered by the measured amounts (`styles.css` hex 52 to 50 and off-grid 128 to 126; `commandCenter.css` hex 14 to 7 and off-grid 7 to 6).
- Primary is a visible neutral fill with a control border. Ghost has a 3:1 control border and a neutral hover. Danger is a tinted outline from the state tokens whose hover changes only the border. Gold and glass keep their look through new tokens moved out of `styles.css`.
- Buttons are at least 40px high, and 44px at or below 800px. The workspace details Refresh button measures at least 44px at 390px and 320px in a real browser.
- `tokens.css` gained `--utf-hover`, `--utf-control-height-compact`, the gold and glass tokens, `--utf-check-size`, the three switch tokens, `--utf-day-chip-min`, `--utf-dropzone-min-height`, `--utf-review-name-min` and `--utf-choice-card-min`. `--utf-automation-day-min` no longer exists in `src`.

## Task Commits

1. **Task 1: Tracer, five button variants from one guarded token-only stylesheet** - `85515949` (refactor)
2. **Task 2: Refresh touch target and the button contrast pairs** - `9a647afd` (test)
3. **Task 3: Control dimension tokens, control state contrast table and day-chip rename** - `530f0463` (feat)

**Plan metadata:** the SUMMARY commit that follows this file.

## Files Created/Modified

- `sources/ReactApp/src/components/action-control.css` - guarded token-only button variants, sizes and the 800px touch height
- `sources/ReactApp/src/components/ActionControl.tsx` - side-effect import of the stylesheet
- `sources/ReactApp/src/styles/tokens.css` - hover layer, gold and glass tokens, Phase 70 control dimension tokens
- `sources/ReactApp/src/styles.css`, `sources/ReactApp/src/components/commandCenter.css` - legacy button rules removed
- `sources/ReactApp/src/components/server/automation.css` - day token reference renamed
- `sources/ReactApp/tests/styles/*` - guard registration, ratchet list and ceilings, wrap test source, button and control contrast pairs
- `sources/ReactApp/tests/e2e/audit/control-target-size.spec.ts` - Refresh button touch target at 390px and 320px

## Decisions Made

- **Button label size.** Button text is now Label 12/700 (`--utf-text-label`, `--utf-leading-label`) instead of the legacy 0.84rem. This follows the typography contract since Phase 61, and every button label shrinks slightly. It also lets the three German leave-dialog actions share one row at 521px (70-09).
- **Hover rule list.** The hover layer rule names `.button--primary:hover` and `.button--ghost:hover` explicitly next to a `:not()` rule for the plain default button. A single generic selector would have left the ghost-hover guard test with no rule to inspect.
- **Day chip token.** `--utf-day-chip-min` is 3rem (the former token was 5rem), so the automation day grid packs more cells per row until 70-08 rewrites it, as the plan states.
- **Danger hover.** Danger hover keeps its fill and changes only the border, as the plan's orchestrator decision 2 states; the live negative sample (a 20% danger tint over the overlay surface) documents C-04.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Spec navigated to the app root instead of the workspace**
- **Found during:** Task 2
- **Issue:** `workspaceRoute()` returns a leading-slash path, and Playwright resolves that against the host instead of the `/next/` base URL. The page rendered the classic site and the Refresh button was never found.
- **Fix:** the spec strips the leading slash, as `a11y-audit.spec.ts` and `german-walk.spec.ts` do.
- **Files modified:** `sources/ReactApp/tests/e2e/audit/control-target-size.spec.ts`
- **Verification:** both phone projects pass.
- **Committed in:** `9a647afd`

---

**Total deviations:** 1 auto-fixed (1 bug in the new spec). **Impact on plan:** none; no scope change.

## Issues Encountered

- The full vitest suite (`npm test`) shows a different set of 10 to 12 failing files on each run, all `Test timed out in 5000ms`. Every one passes when run alone, and the 122 files of the previously failing directories pass together. The machine is loaded by parallel worktrees; the failures are load timeouts, not regressions from this plan. `tests/styles`, `tests/primitives.test.tsx`, lint and typecheck are green.
- Playwright rewrites `src/generated/api.ts` line endings during its web server build; the file was restored with `git checkout -- <file>` and never committed.

## Known Stubs

None.

## Cross-plan findings

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Every token later plans consume exists. 70-04 can reclassify the former `variant="primary"` sites. 70-08 rewrites the automation day grid against `--utf-day-chip-min`.
- 70-07 and 70-14 own the next `legacy-ratchet.json` edits and must run the ratchet writer after deleting legacy CSS.

## Self-Check: PASSED

- Created files exist: `action-control.css`, `control-target-size.spec.ts`.
- Commits `85515949`, `9a647afd` and `530f0463` are ancestors of HEAD.
- All task acceptance criteria re-ran green; `tests/styles`, `tests/primitives.test.tsx`, lint, typecheck, `dialog-geometry.spec.ts` (chromium) and `control-target-size.spec.ts` (mobile-390, mobile-320) pass.

---
*Phase: 70-ui-polishing*
*Completed: 2026-11-26*
