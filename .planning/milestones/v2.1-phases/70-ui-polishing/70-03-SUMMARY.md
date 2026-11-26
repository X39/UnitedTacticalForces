---
phase: 70-ui-polishing
plan: 03
subsystem: ui
tags: [react, disclosure, confirm-dialog, accessibility, css]
requires: []
provides:
  - "Disclosure with headingLevel, id, badge, variant and buttonRef (inline and panel shapes)"
  - "ConfirmDialog closeOnBackdrop, confirmPlacement and dismissVariant"
affects: [70-06, 70-09, 70-13]
tech-stack:
  added: []
  patterns:
    - "single-class base selectors so two-class page-local overrides keep winning"
    - "pointerdown plus click on the backdrop outside the dialog rect, guarded by busy"
key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/Disclosure.tsx
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/src/components/server/form-fields.css
    - sources/ReactApp/tests/disclosure.test.tsx
    - sources/ReactApp/tests/primitives.test.tsx
key-decisions:
  - "The disclosure content stays mounted behind the hidden attribute (RESEARCH Pattern 1)"
  - "closeOnBackdrop is off by default and only ever maps to onCancel (T-70-03)"
requirements-completed: [D-07, D-13]
duration: 40min
completed: 2026-11-26
status: complete
commits: 3
plan_head_before: b803c3e9fba5523e4c80c759050f6b8f460d90d0
plan_head_after: 5b7b890b4be2579c4b991c717376b7eefab80e22
actuals:
  tokens: 14000
  tasks: 2
  commits: 3
---

# Phase 70 Plan 03: Disclosure shapes and ConfirmDialog additions Summary

The shared Disclosure now renders as a designed bordered row (inline) or a raised panel header (panel) with an optional accordion heading wrapper, anchor id, badge and button ref, and ConfirmDialog gains backdrop close, confirm placement and a dismiss variant, all additive.

## Accomplishments

- Disclosure: `headingLevel` (h2 or h3 wrapping the button, named by the title only), `id` on the outer element, `badge` rendered before the chevron and linked through `aria-describedby` after the summary id, `variant` (`inline` default, `panel` adds `disclosure--panel`), `buttonRef` forwarded to the toggle. Children stay mounted behind `hidden`.
- Disclosure CSS (disclosure block of `form-fields.css`): inline header is a full-width grid row (title, badge, chevron, summary line spanning below) with 1px divider border, overlay fill and small radius; panel header is transparent on the raised surface with heading-size title, touch-height minimum and a divider above the padded content; chevron rotation covers the heading-wrapped button; 800px touch entry kept.
- ConfirmDialog: `closeOnBackdrop` (default false) calls `onCancel` only when pointerdown and click both hit the backdrop outside the dialog rect and the dialog is not busy; `confirmPlacement` `start` renders confirm, dismiss, extra action; `dismissVariant` (default ghost) styles the dismiss button; `data-dialog-dismiss` and initial focus unchanged.
- Tests: 8 new disclosure cases and 11 new ConfirmDialog cases (backdrop, drag from inside, busy, default off, placement, variant, initial focus in both placements).

## Task Commits

1. Task 1 (tracer): `bc47d2a8` feat(webapp): add heading, badge and panel shapes to the disclosure (plus `aaa173e6` prettier formatting follow-up)
2. Task 2: `5b7b890b` feat(webapp): let a confirm dialog close on its backdrop and order its actions

Tracer gate: the create page Advanced options render through the inline disclosure; `hosting-create.spec.ts` passed 20/20 on chromium and the create page unit tests passed before expansion.

## Verification

- `vitest tests/disclosure.test.tsx tests/primitives.test.tsx tests/hosting`: 2256 passed; one run showed timing-budget failures (`metrics-performance`, `activity-section`, `pending-start-notice`) under parallel load, which pass when run alone (unrelated to this plan).
- `npm run lint` (eslint, max-warnings 0) and `npm run typecheck` pass.
- Acceptance greps: headingLevel 4, buttonRef 3, disclosure--panel 8, `hidden={!open}` 1, closeOnBackdrop 5 and 8 (source, tests), confirmPlacement 5, dismissVariant 4.

## Deviations from Plan

None - plan executed as written. `activity.css` and `server.css` needed no change: `activity-entry__disclosure` wraps a button, not the Disclosure, and the cost-note override only sets font size, which still wins over the single-class base rules. A prettier pass on the new files was committed separately.

## Cross-plan findings

- XPF-70-03-1: `hosting.css:521-524` `.next-hosting-audit-evidence` keeps a `border-top` and `min-height` override; with the new bordered inline header the evidence disclosure shows a border above and around the header. Owner of `hosting.css` should reset the header border or drop the container border-top.
- XPF-70-03-2: `hosting.css:2006` `.next-hosting-protocol-list .disclosure` has its own padding, border and background; the inline header now draws a second border and fill inside it. Owner of `hosting.css` should neutralize the header border and background there or drop the container chrome.
- XPF-70-03-3: `eventAuthoring.css:31-39` role symbol picker overrides border, background and padding already (two-class), so it is unaffected; noted only as checked.

Legacy stylesheets were not edited (ratchet ceilings belong to 70-01).

## Known Stubs

None.

## Threat Flags

None. T-70-03 and T-70-04 mitigations are implemented and unit tested.

## Self-Check: PASSED

- Files exist: Disclosure.tsx, primitives.tsx, form-fields.css, both test files.
- Commits `bc47d2a8`, `aaa173e6`, `5b7b890b` are ancestors of HEAD.
