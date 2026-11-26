---
phase: 69-consistency-accessibility-verification
plan: 38
subsystem: webapp-accessibility
tags: [react, a11y, aria-live, statepanel, static-scan, gap-closure]
requires: ["69-34"]
provides:
  - "Error StatePanel as an assertive alert without an overriding aria-live"
  - "alert-made-polite static accessibility rule over src"
affects: [sources/ReactApp]
tech-stack:
  added: []
  patterns: ["TypeScript-AST static a11y scan rule with inline bad/good samples"]
key-files:
  created: []
  modified:
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/tests/primitives.test.tsx
    - sources/ReactApp/tests/a11y-static.test.ts
decisions:
  - "A conditional aria-live expression is not flagged by alert-made-polite; only constant non-assertive values are"
metrics:
  tasks: 2
  files: 3
status: complete
commits: 2
plan_head_before: b5a7800819842c3cc0488e9b06df7435121a0236
plan_head_after: bb9612ce85cfe7b036ee8e9df761603b862002e0
actuals:
  tokens: 6000
  tasks: 2
  commits: 2
completed: 2026-11-25
---

# Phase 69 Plan 38: Assertive error panels (WR-05) Summary

The error `StatePanel` (and so every `ErrorState`) now keeps the implicit assertive politeness of `role="alert"`, and a fourth static rule `alert-made-polite` prevents the pattern from returning anywhere in `src`.

## Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Tracer: error StatePanel is an assertive alert, other kinds stay polite | 1339f397 | primitives.tsx, primitives.test.tsx |
| 2 | Static rule rejects alerts made polite by a constant live attribute | bb9612ce | a11y-static.test.ts |

- Task 1: `aria-live` is `undefined` for kind error and `"polite"` for every other kind; the doc comment states this. The per-kind rendering test now expects the attribute to be absent for error, and "keeps the error panel assertive" covers `StatePanel kind="error"` and `ErrorState`. RED was confirmed (2 failures) before the fix.
- Task 2: `alert-made-polite` flags a role that can be `alert` (literal or any conditional containing the literal) together with a constant `aria-live` other than `assertive`. Samples cover literal alert + polite, conditional role + polite, alert + off, and accepted shapes (no live, assertive, status + polite, the StatePanel conditional shape). The source-tree test holds the rule for all handwritten `.tsx` files.

## Verification

- Vitest: primitives, state-views, hosting-states, a11y-static: 4 files, 112 tests passed.
- `npm run typecheck` and `npm run lint` passed.
- `grep -cF 'aria-live="polite"' primitives.tsx` = 0; `keeps the error panel assertive` count = 1; `alert-made-polite` count in the static test = 7.
- Deferred follow-up: the milestone-end screen-reader pass confirms the spoken result (backstop).

## Deviations from Plan

None - plan executed as written. Environment note: the worktree had no `node_modules`, so a Windows junction to the main checkout's `sources/ReactApp/node_modules` was created (git-ignored, nothing installed).

## Known Stubs

None.

## Self-Check: PASSED

- Commits 1339f397 and bb9612ce exist on the branch; modified files exist.
